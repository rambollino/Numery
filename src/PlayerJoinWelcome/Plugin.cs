using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using PluginAPI.Events;
using PlayerJoinWelcome.Services;

namespace PlayerJoinWelcome
{
    public sealed class WelcomePlugin
    {
        [PluginEntryPoint("PlayerJoinWelcome", "1.0.0", "Logs join and welcomes player", "AutoDev")]
        private void OnLoad()
        {
            EventManager.RegisterEvents(this);
            VipService.Instance.Initialize();
            VipService.Instance.ReapplyForOnlinePlayers();
        }
        
        [PluginUnload]
        private void OnUnload()
        {
            VipService.Instance.Shutdown();
        }

        [PluginEvent(ServerEventType.PlayerJoined)]
        private void OnPlayerJoined(PlayerJoinedEvent ev)
        {
            Log.Info($"New Player Joined: {ev.Player.Nickname}");
            ev.Player.ReceiveHint("Witaj na serwerze! Zapoznaj się z zasadami.", 7f);
            VipService.Instance.TryApplyOnJoin(ev.Player);
        }
    }
}
