using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using Victoria;
using Microsoft.Extensions.DependencyInjection;

namespace DynamisBridge
{
    public class Discord
    {
        private readonly ServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly AudioModule _audioModule;
        private readonly LavaNode _lavaNode;

        public Discord()
        {
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _lavaNode = _services.GetRequiredService<LavaNode>();
            _audioModule = _services.GetRequiredService<AudioModule>();

            _client.Log += Log;
            _client.Ready += OnReady;
            _client.Disconnected += OnDisconnect;
            _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
        }

        public async Task PlayAudioFile(string filePath)
        {
            await _audioModule.PlayAsync(filePath);
        }

        private ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds
            }));

            services.AddLavaNode();
            services.AddSingleton<AudioModule>();
            services.AddSingleton<AudioService>();

            return services.BuildServiceProvider();
        }

        public async Task Start()
        {
            var token = Plugin.Config.Token;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task OnReady()
        {
            Plugin.Logger.Info($"Ready! Logged in as {_client.CurrentUser.GlobalName}");

            try
            {
                if (Plugin.Config.AutoConnect == true)
                    await _audioModule.AutoJoinAsync();

                if (!_lavaNode.IsConnected)
                    await _services.UseLavaNodeAsync();
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
            await _audioModule.LeaveAsync();
            _client.Log -= Log;
            _client.Ready -= OnReady;
            _client.Disconnected -= OnDisconnect;
            _client.UserVoiceStateUpdated -= OnVoiceStateUpdated;
            _client?.Dispose();
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
