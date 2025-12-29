using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using PluginAPI.Core;
using Newtonsoft.Json;

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
                dataFilePath = Path.Combine(dataDirectoryPath, "vip_assignments.json");

                Directory.CreateDirectory(dataDirectoryPath);
                LoadFromDisk();

                expirationTimer = new Timer(30_000);
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
                if (userIdToAssignment.ContainsKey(player.UserId))
                {
                    error = "Player already has a VIP assignment. Remove it first.";
                    return false;
                }

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

        public bool TryGetAssignment(string userId, out VipAssignment assignment)
        {
            lock (SyncRoot)
            {
                return userIdToAssignment.TryGetValue(userId, out assignment);
            }
        }

        public bool RemoveForPlayer(Player player)
        {
            if (player == null) return false;
            bool removed;
            lock (SyncRoot)
            {
                removed = userIdToAssignment.Remove(player.UserId);
                if (removed)
                {
                    SaveToDisk();
                }
            }
            if (removed)
            {
                ClearBadge(player);
            }
            return removed;
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
                // Already expired, clean up immediately
                lock (SyncRoot)
                {
                    userIdToAssignment.Remove(player.UserId);
                    SaveToDisk();
                }
                ClearBadge(player);
                return;
            }

            TryApplyBadge(player, assignment.RankText);
        }

        public void ReapplyForOnlinePlayers()
        {
            foreach (var player in Player.GetPlayers())
            {
                TryApplyOnJoin(player);
            }
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
                if (player.ReferenceHub != null && player.ReferenceHub.serverRoles != null)
                {
                    player.ReferenceHub.serverRoles.SetText(rankText);
                    player.ReferenceHub.serverRoles.SetColor("yellow");
                    player.ReferenceHub.serverRoles.RefreshPermissions();
                    return;
                }
                Log.Warning($"serverRoles is null for {player.Nickname}; cannot apply badge.");
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
                if (player.ReferenceHub != null && player.ReferenceHub.serverRoles != null)
                {
                    player.ReferenceHub.serverRoles.SetText(string.Empty);
                    player.ReferenceHub.serverRoles.SetColor(string.Empty);
                    player.ReferenceHub.serverRoles.RefreshPermissions();
                    return;
                }
                Log.Warning($"serverRoles is null for {player.Nickname}; cannot clear badge.");
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

            try
            {
                var json = File.ReadAllText(dataFilePath);
                var list = JsonConvert.DeserializeObject<List<VipAssignment>>(json) ?? new List<VipAssignment>();
                foreach (var a in list)
                {
                    if (a == null || string.IsNullOrWhiteSpace(a.UserId))
                        continue;
                    userIdToAssignment[a.UserId] = a;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load VIP assignments: {ex}");
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var json = JsonConvert.SerializeObject(userIdToAssignment.Values.ToList(), Formatting.Indented);
                File.WriteAllText(dataFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save VIP assignments: {ex}");
            }
        }

        public sealed class VipAssignment
        {
            public string UserId { get; set; }
            public string RankText { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }
    }
}
