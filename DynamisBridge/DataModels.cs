using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace DynamisBridge
{
    public enum JoinType
    {
        Follow,
        Specify
    };

    public enum VoiceStates
    {
        Disconnected,
        Connecting,
        Connected
    }

    public class CustomColors
    {
        public uint GREEN = ImGui.GetColorU32(new Vector4(71f / 255f, 161f / 255f, 97f / 255f, 1f));
        public uint YELLOW = ImGui.GetColorU32(new Vector4(229f / 255f, 179f / 255f, 76f / 255f, 1f));
        public uint RED = ImGui.GetColorU32(new Vector4(225f / 255f, 78f / 255f, 73f / 255f, 1f));
    }

    public class DynamisMessage
    {
        public bool SyncSettings { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public bool IsFollowing { get; set; } = false;
        public string GuildId { get; set; } = string.Empty;
        public string VoiceChannelId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool PeekoMode { get; set; } = false;
    }
}
