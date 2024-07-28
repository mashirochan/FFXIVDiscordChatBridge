using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamisBridge
{
    public enum JoinType
    {
        Follow,
        Specify
    };

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
