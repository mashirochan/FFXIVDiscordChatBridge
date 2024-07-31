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

public sealed class AudioService
{
    private readonly LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
    public static DiscordSocketClient _socketClient;
    private readonly ILogger _logger;
    public readonly HashSet<ulong> VoteQueue;
    private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
    public readonly ConcurrentDictionary<ulong, ulong> TextChannels;

    public AudioService(
        LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode,
        DiscordSocketClient socketClient,
        ILogger<AudioService> logger)
    {
        _lavaNode = lavaNode;
        _socketClient = socketClient;
        _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        _logger = logger;
        TextChannels = new ConcurrentDictionary<ulong, ulong>();
        VoteQueue = [];
        _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
        _lavaNode.OnStats += OnStatsAsync;
        _lavaNode.OnPlayerUpdate += OnPlayerUpdateAsync;
        _lavaNode.OnTrackEnd += OnTrackEndAsync;
        _lavaNode.OnTrackStart += OnTrackStartAsync;
    }
}
