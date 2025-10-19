using System;
using System.Globalization;
using System.Linq;
using CommandSystem;
using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using PluginAPI.Events;
using PlayerJoinWelcome.Services;

namespace PlayerJoinWelcome.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class VipScriptCommand : ICommand
    {
        public string Command => "vipscript";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Timed VIP rank assignment. Usage: vipscript set <player> <rank> <days> | vipscript remove <player> | vipscript status <player>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count == 0)
            {
                response = "Usage: vipscript set <player> <rank> <days> | vipscript remove <player> | vipscript status <player>";
                return false;
            }

            var action = arguments.ElementAt(0);

            if (string.Equals(action, "set", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count < 4)
                {
                    response = "Usage: vipscript set <player> <rank> <days>";
                    return false;
                }

                var playerQuery = arguments.ElementAt(1);
                var rank = arguments.ElementAt(2);
                var daysStr = arguments.ElementAt(3);

                if (!int.TryParse(daysStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
                {
                    response = "Days must be an integer.";
                    return false;
                }

                var target = Player.Get(playerQuery);
                if (target == null)
                {
                    response = "Player not found.";
                    return false;
                }

                string error;
                var ok = VipService.Instance.AssignForDays(target, rank, days, out error);
                if (!ok)
                {
                    response = error ?? "Failed to assign VIP.";
                    return false;
                }

                target.SendBroadcast($"You received VIP rank {rank} for {days} days!", 5);
                Log.Info($"{sender.LogName} assigned VIP '{rank}' to {target.Nickname} for {days} day(s)");
                response = $"Assigned rank '{rank}' to {target.Nickname} for {days} day(s).";
                return true;
            }
            else if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count < 2)
                {
                    response = "Usage: vipscript remove <player>";
                    return false;
                }

                var playerQuery = arguments.ElementAt(1);
                var target = Player.Get(playerQuery);
                if (target == null)
                {
                    response = "Player not found.";
                    return false;
                }

                if (!VipService.Instance.RemoveForPlayer(target))
                {
                    response = "Player does not have an active VIP assignment.";
                    return false;
                }

                Log.Info($"{sender.LogName} removed VIP from {target.Nickname}");
                response = $"Removed VIP from {target.Nickname}.";
                return true;
            }
            else if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count < 2)
                {
                    response = "Usage: vipscript status <player>";
                    return false;
                }

                var playerQuery = arguments.ElementAt(1);
                var target = Player.Get(playerQuery);
                if (target == null)
                {
                    response = "Player not found.";
                    return false;
                }

                if (VipService.Instance.TryGetAssignment(target.UserId, out var a))
                {
                    var remaining = a.ExpiresAtUtc - DateTime.UtcNow;
                    if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                    response = $"VIP '{a.RankText}' active. Remaining: {remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
                    return true;
                }

                response = "No active VIP assignment.";
                return true;
            }
            else
            {
                response = "Unknown action. Use: set | remove | status";
                return false;
            }
        }
    }
}
