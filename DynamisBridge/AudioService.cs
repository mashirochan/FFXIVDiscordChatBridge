using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Victoria;
using Victoria.WebSocket.EventArgs;
using System.Text.Json;

namespace DynamisBridge
{
    public sealed class AudioService
    {
        private readonly LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
        public readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;

        public AudioService(
            LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode,
            DiscordSocketClient client)
        {
            _lavaNode = lavaNode;
            _client = client;
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            _lavaNode.OnStats += OnStatsAsync;
            _lavaNode.OnPlayerUpdate += OnPlayerUpdateAsync;
            _lavaNode.OnTrackEnd += OnTrackEndAsync;
            _lavaNode.OnTrackStart += OnTrackStartAsync;
        }

        private Task OnTrackStartAsync(TrackStartEventArg arg)
        {
            Plugin.Logger.Info($"Now playing: {arg.Track.Title}");
            return Task.CompletedTask;
        }

        private Task OnTrackEndAsync(TrackEndEventArg arg)
        {
            Plugin.Logger.Debug($"{arg.Track.Title} ended with reason: {arg.Reason}");
            return Task.CompletedTask;
        }

        private Task OnPlayerUpdateAsync(PlayerUpdateEventArg arg)
        {
            Plugin.Logger.Debug($"Guild latency: {arg.Ping}");
            return Task.CompletedTask;
        }

        private Task OnStatsAsync(StatsEventArg arg)
        {
            Plugin.Logger.Debug(JsonSerializer.Serialize(arg));
            return Task.CompletedTask;
        }

        private Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            Plugin.Logger.Error(JsonSerializer.Serialize(arg));
            return Task.CompletedTask;
        }
    }
}
