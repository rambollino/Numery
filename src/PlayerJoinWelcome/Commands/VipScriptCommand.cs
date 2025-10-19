using System;
using System.Globalization;
using System.Linq;
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
        public string Description => "Timed VIP rank assignment. Usage: vipscript set <player> <rank> <days>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 4)
            {
                response = "Usage: vipscript set <player> <rank> <days>";
                return false;
            }

            var action = arguments.ElementAt(0);
            if (!string.Equals(action, "set", StringComparison.OrdinalIgnoreCase))
            {
                response = "Unknown action. Usage: vipscript set <player> <rank> <days>";
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

            var target = Player.GetPlayers().FirstOrDefault(p => p.Nickname.Equals(playerQuery, StringComparison.OrdinalIgnoreCase) || p.UserId.StartsWith(playerQuery, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                response = "Player not found. Use UserId or exact nickname.";
                return false;
            }

            string error;
            var ok = VipService.Instance.AssignForDays(target, rank, days, out error);
            if (!ok)
            {
                response = error ?? "Failed to assign VIP.";
                return false;
            }

            response = $"Assigned rank '{rank}' to {target.Nickname} for {days} day(s).";
            return true;
        }
    }
}
