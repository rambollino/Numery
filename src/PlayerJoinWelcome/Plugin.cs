using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using PluginAPI.Events;

namespace PlayerJoinWelcome
{
    public sealed class WelcomePlugin
    {
        [PluginEntryPoint("PlayerJoinWelcome", "1.0.0", "Logs join and welcomes player", "AutoDev")]
        private void OnLoad()
        {
            EventManager.RegisterEvents(this);
        }

        [PluginEvent(ServerEventType.PlayerJoined)]
        private void OnPlayerJoined(PlayerJoinedEvent ev)
        {
            Log.Info($"New Player Joined: {ev.Player.Nickname}");
            ev.Player.ReceiveHint("Witaj na serwerze! Zapoznaj się z zasadami.", 7f);
        }
    }
}
