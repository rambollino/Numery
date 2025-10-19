using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using PluginAPI.Core;

namespace PlayerJoinWelcome.Services
{
    internal sealed class VipService
    {
        private static readonly object SyncRoot = new object();
        private static VipService _instance;
        public static VipService Instance => _instance ?? (_instance = new VipService());

        private readonly Dictionary<string, VipAssignment> userIdToAssignment = new Dictionary<string, VipAssignment>(StringComparer.OrdinalIgnoreCase);
        private Timer expirationTimer;
        private string dataDirectoryPath;
        private string dataFilePath;

        private VipService()
        {
        }

        public void Initialize()
        {
            lock (SyncRoot)
            {
                if (expirationTimer != null)
                    return;

                dataDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".plugin-data", "PlayerJoinWelcome");
                dataFilePath = Path.Combine(dataDirectoryPath, "vip_assignments.txt");

                Directory.CreateDirectory(dataDirectoryPath);
                LoadFromDisk();

                expirationTimer = new Timer(60_000);
                expirationTimer.AutoReset = true;
                expirationTimer.Elapsed += (_, __) => ExpireLoop();
                expirationTimer.Start();
            }
        }

        public void Shutdown()
        {
            lock (SyncRoot)
            {
                if (expirationTimer != null)
                {
                    expirationTimer.Stop();
                    expirationTimer.Dispose();
                    expirationTimer = null;
                }

                SaveToDisk();
            }
        }

        public bool AssignForDays(Player player, string rankText, int days, out string error)
        {
            error = null;
            if (player == null)
            {
                error = "Player not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rankText))
            {
                error = "Rank cannot be empty.";
                return false;
            }

            if (days <= 0 || days > 3650)
            {
                error = "Days must be between 1 and 3650.";
                return false;
            }

            var expiresAtUtc = DateTime.UtcNow.AddDays(days);

            lock (SyncRoot)
            {
                userIdToAssignment[player.UserId] = new VipAssignment
                {
                    UserId = player.UserId,
                    RankText = rankText,
                    ExpiresAtUtc = expiresAtUtc
                };
                SaveToDisk();
            }

            TryApplyBadge(player, rankText);
            return true;
        }

        public void TryApplyOnJoin(Player player)
        {
            if (player == null)
                return;

            VipAssignment assignment;
            lock (SyncRoot)
            {
                if (!userIdToAssignment.TryGetValue(player.UserId, out assignment))
                    return;
            }

            if (assignment.ExpiresAtUtc <= DateTime.UtcNow)
            {
                // Already expired, clean up on the next loop
                return;
            }

            TryApplyBadge(player, assignment.RankText);
        }

        private void ExpireLoop()
        {
            try
            {
                List<string> expiredUserIds = new List<string>();

                lock (SyncRoot)
                {
                    foreach (var kvp in userIdToAssignment)
                    {
                        if (kvp.Value.ExpiresAtUtc <= DateTime.UtcNow)
                        {
                            expiredUserIds.Add(kvp.Key);
                        }
                    }
                }

                if (expiredUserIds.Count == 0)
                    return;

                foreach (var userId in expiredUserIds)
                {
                    var online = Player.GetPlayers().FirstOrDefault(p => string.Equals(p.UserId, userId, StringComparison.OrdinalIgnoreCase));
                    if (online != null)
                    {
                        ClearBadge(online);
                    }

                    lock (SyncRoot)
                    {
                        userIdToAssignment.Remove(userId);
                        SaveToDisk();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"VipService expiration loop error: {ex}");
            }
        }

        private void TryApplyBadge(Player player, string rankText)
        {
            try
            {
                // Use PluginAPI wrapper properties if available
                try
                {
                    player.RankName = rankText;
                    if (string.IsNullOrEmpty(player.RankColor))
                        player.RankColor = "yellow";
                    player.RefreshPermissions();
                    return;
                }
                catch
                {
                    // Fallback to serverRoles if direct properties are unavailable
                    try
                    {
                        player.ReferenceHub.serverRoles.SetText(rankText, "yellow");
                    }
                    catch
                    {
                        // As a last resort just log
                        Log.Warning($"Could not set badge for {player.Nickname}. RankText={rankText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to apply VIP badge to {player.Nickname}: {ex}");
            }
        }

        private void ClearBadge(Player player)
        {
            try
            {
                try
                {
                    player.RankName = string.Empty;
                    player.RankColor = string.Empty;
                    player.RefreshPermissions();
                    return;
                }
                catch
                {
                    try
                    {
                        player.ReferenceHub.serverRoles.SetText(string.Empty, string.Empty);
                    }
                    catch
                    {
                        Log.Warning($"Could not clear badge for {player.Nickname}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to clear VIP badge from {player.Nickname}: {ex}");
            }
        }

        private void LoadFromDisk()
        {
            userIdToAssignment.Clear();
            if (!File.Exists(dataFilePath))
                return;

            foreach (var line in File.ReadAllLines(dataFilePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                if (trimmed.StartsWith("#"))
                    continue;

                // Format: userId|rankText|expiresAtUtcTicks
                var parts = trimmed.Split('|');
                if (parts.Length != 3)
                    continue;

                if (!long.TryParse(parts[2], out var ticks))
                    continue;

                var assignment = new VipAssignment
                {
                    UserId = parts[0],
                    RankText = parts[1],
                    ExpiresAtUtc = new DateTime(ticks, DateTimeKind.Utc)
                };
                userIdToAssignment[assignment.UserId] = assignment;
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var lines = new List<string>
                {
                    "# vip_assignments: userId|rankText|expiresAtUtcTicks"
                };

                foreach (var kvp in userIdToAssignment)
                {
                    var a = kvp.Value;
                    lines.Add($"{a.UserId}|{a.RankText}|{a.ExpiresAtUtc.Ticks}");
                }

                File.WriteAllLines(dataFilePath, lines);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save VIP assignments: {ex}");
            }
        }

        private sealed class VipAssignment
        {
            public string UserId { get; set; }
            public string RankText { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }
    }
}
