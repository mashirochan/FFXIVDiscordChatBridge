using Dalamud.Configuration;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using Dalamud.Game.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;

namespace DynamisBridge;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool PluginEnabled { get; set; } = true;
    public XivChatType ChatChannel { get; set; } = XivChatType.None;
    public SeString? Character { get; set; }
    public SeString? TextboxCharacter { get; set; }
    public bool WatchingSelf { get; set; }
    public SeString? PrefixCommand { get; set; }
    public JoinType JoinType { get; set; } = JoinType.Specify;
    public string GuildId { get; set; } = "";
    public string VoiceChannelId { get; set; } = "";
    public string UserId { get; set; } = "";
    public bool PeekoMode { get; set; } = false;
    public string? Token { get; set; }
    public bool AutoConnect { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
