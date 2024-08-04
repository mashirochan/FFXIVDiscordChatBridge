using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Victoria;

namespace DynamisBridge
{
    public class Discord
    {
        private readonly DiscordSocketClient _client;
        private readonly AudioModule _audioModule;
        private readonly LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
        private readonly IServiceProvider _serviceProvider;

        public Discord(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            try
            {
                _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
                _lavaNode = serviceProvider.GetRequiredService<LavaNode<LavaPlayer<LavaTrack>, LavaTrack>>();
                _audioModule = serviceProvider.GetRequiredService<AudioModule>();

                _client.Log += Log;
                _client.Ready += OnReady;
                _client.Disconnected += OnDisconnect;
                _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"Service resolution failed: {ex.Message}");
            }
        }

        public async Task Start()
        {
            var token = Plugin.Config.Token;
            Plugin.Logger.Info("Logging into Discord...");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task OnReady()
        {
            Plugin.Logger.Info($"Ready! Logged in as {_client.CurrentUser.GlobalName}");

            try
            {
                await _serviceProvider.UseLavaNodeAsync();
                Plugin.Logger.Info("Connected to Lavalink!");

                if (Plugin.Config.AutoConnect == true)
                    await _audioModule.AutoJoinAsync();
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error(ex.Message);
            }
        }

        private async Task OnDisconnect(Exception ex)
        {
            Plugin.Logger.Info($"Bot disconnected: {ex.Message}");
            await _audioModule.LeaveAsync();
        }

        public async Task DisposeAsync()
        {
            if (_audioModule != null)
            {
                await _audioModule.LeaveAsync();
            }
            else
            {
                Plugin.Logger.Warning("Audio module is null during disposal.");
            }

            if (_client != null)
            {
                _client.Log -= Log;
                _client.Ready -= OnReady;
                _client.Disconnected -= OnDisconnect;
                _client.UserVoiceStateUpdated -= OnVoiceStateUpdated;

                await _client.LogoutAsync();
                await _client.StopAsync();

                _client.Dispose();
            }
            else
            {
                Plugin.Logger.Warning("Discord client is null during disposal.");
            }
        }

        private async Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            if (Plugin.Config.JoinType == JoinType.Follow && user.Id == ulong.Parse(Plugin.Config.UserId))
            {
                // User joined a voice channel
                if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
                {
                    var channel = newState.VoiceChannel;
                    Plugin.Logger.Info($"{user.GlobalName} joined {channel.Name}, attempting to follow...");
                    await _audioModule.JoinAsync(channel);
                }
                else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                {
                    var channel = oldState.VoiceChannel;
                    Plugin.Logger.Info($"{user.GlobalName} left {channel.Name}, attempting to leave...");
                    await _audioModule.LeaveAsync();
                }
            }
        }

        public async Task PlayAudioFile(string filePath)
        {
            await _audioModule.PlayAsync(filePath);
        }

        public async Task JoinVoiceChannel()
        {
            await _audioModule.JoinAsync();
        }

        public async Task LeaveVoiceChannel()
        {
            await _audioModule.LeaveAsync();
        }

        public string GetGuildName()
        {
            return _audioModule.CurrentGuildName;
        }

        public string GetChannelName()
        {
            return _audioModule.CurrentChannelName;
        }

        private static Task Log(LogMessage msg)
        {
            Plugin.Logger.Debug(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
