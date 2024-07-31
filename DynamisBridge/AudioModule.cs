using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;
using Discord;
using Discord.Commands;
using DynamisBridge;
using Discord.Audio;
using Discord.WebSocket;
using System.Collections.Concurrent;
using Google.Api;
using Victoria.Rest.Search;

public sealed class AudioModule(LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode, AudioService audioService) : ModuleBase<SocketCommandContext>
{
    private static readonly IEnumerable<int> Range = Enumerable.Range(1900, 2000);
    private readonly ConcurrentQueue<string> audioQueue = new();
    private bool isPlaying = false;
    public static string CurrentGuildName = "Disconnected";
    private static SocketGuild? CurrentGuild;
    public static string CurrentChannelName = "Disconnected";
    private static SocketVoiceChannel? CurrentChannel;

    public async Task JoinAsync()
    {
        if (Plugin.Config.JoinType == JoinType.Specify)
        {
            Plugin.Logger.Info("Specify mode is enabled, attempting to join voice channel...");
            await ConnectToChannel();
        }
        else if (Plugin.Config.JoinType == JoinType.Follow)
        {
            var found = false;
            Plugin.Logger.Info("Follow mode is enabled, attempting to join user's voice channel...");
            foreach (var guild in AudioService._socketClient.Guilds)
            {
                Plugin.Logger.Debug($"Searcing for user in {guild.Name}...");
                var user = guild.GetUser(ulong.Parse(Plugin.Config.UserId));
                if (user == null)
                {
                    Plugin.Logger.Debug($"Could not find user in {guild.Name}!");
                }
                else
                {
                    if (user.VoiceChannel == null)
                    {
                        Plugin.Logger.Debug($"Found {user.DisplayName} in {guild.Name}, but they aren't in a voice channel!");
                    }
                    else
                    {
                        Plugin.Logger.Info($"Found {user.GlobalName} in {user.VoiceChannel.Name}, joining!");
                        found = true;
                        await ConnectToChannel(user.VoiceChannel);
                        break;
                    }
                }
            }
            if (!found)
            {
                Plugin.Logger.Info("User not found in any visible voice channels!");
            }
        }
    }

    private async Task ConnectToChannel(SocketVoiceChannel? channel = null)
    {
        if (lavaNode != null && lavaNode.IsConnected)
            return;

        Plugin.VoiceState = VoiceStates.Connecting;
        CurrentGuildName = "Connecting...";
        CurrentChannelName = "Connecting...";
        if (channel == null)
        {
            var guild = AudioService._socketClient.GetGuild(ulong.Parse(Plugin.Config.GuildId));
            if (guild == null)
            {
                Plugin.Logger.Error("Guild not found!");
                Plugin.VoiceState = VoiceStates.Disconnected;
                return;
            }

            channel = guild.GetVoiceChannel(ulong.Parse(Plugin.Config.VoiceChannelId));
            if (channel == null)
            {
                Plugin.Logger.Error("Voice channel not found!");
                Plugin.VoiceState = VoiceStates.Disconnected;
                return;
            }
        }

        try
        {
            await lavaNode.JoinAsync(channel);
            Plugin.Logger.Info($"Joined {channel.Name}!");
            Plugin.VoiceState = VoiceStates.Connected;
            CurrentGuild = channel.Guild;
            CurrentGuildName = channel.Guild.Name;
            CurrentChannel = channel;
            CurrentChannelName = channel.Name;
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error(ex.Message);
        }
    }

    public async Task LeaveAsync()
    {
        if (lavaNode != null && lavaNode.IsConnected && CurrentGuild != null && CurrentChannel != null)
        {
            try
            {
                await lavaNode.LeaveAsync(CurrentChannel);
                Plugin.Logger.Info("Disconnected from voice channel!");
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error(ex.Message);
            }
        }
        else
        {
            Plugin.Logger.Debug("Bot is not connected to any voice channel.");
        }
        Plugin.VoiceState = VoiceStates.Disconnected;
        CurrentGuildName = "Disconnected";
        CurrentChannelName = "Disconnected";
    }

    public async Task PlayAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Plugin.Logger.Error("Invalid filePath specified!");
            return;
        }

        if (CurrentGuild == null)
        {
            Plugin.Logger.Error("Could not get current guild!");
            return;
        }

        var player = await lavaNode.TryGetPlayerAsync(CurrentGuild.Id);
        if (player == null)
        {
            try
            {
                player = await lavaNode.JoinAsync(CurrentChannel);
                Plugin.Logger.Info($"Joined {CurrentChannelName}!");
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error(ex.Message);
            }
        }

        var searchResponse = await lavaNode.LoadTrackAsync(filePath);
        if (searchResponse.Type is SearchType.Empty or SearchType.Error)
        {
            Plugin.Logger.Error($"Could not find filepath: {filePath}");
            return;
        }

        var track = searchResponse.Tracks.FirstOrDefault();
        if (player.GetQueue().Count == 0)
        {
            await player.PlayAsync(lavaNode, track);
            Plugin.Logger.Info($"Now playing: {filePath}");
            return;
        }

        player.GetQueue().Enqueue(track);
        Plugin.Logger.Info($"Added file to queue: {filePath}");
    }
}
