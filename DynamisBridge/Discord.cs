using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using Discord.Audio;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System.Threading;
using Victoria;

namespace DynamisBridge
{
    internal class Discord
    {
        private readonly DiscordSocketClient _client;
        private readonly AudioModule _audioModule;

        public Discord(DiscordSocketClient client, AudioModule audioModule)
        {
            _client = client;
            _audioModule = audioModule;

            _client.Log += Log;
            _client.Ready += OnReady;
            _client.Disconnected += OnDisconnect;
            _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
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

            if (Plugin.Config.AutoConnect == true)
                await _audioModule.JoinAsync();
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
                    await _audioModule.ConnectToChannel(channel);
                }
                else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                {
                    var channel = oldState.VoiceChannel;
                    Plugin.Logger.Info($"{user.GlobalName} left {channel.Name}, attempting to leave...");
                    await _audioModule.LeaveAsync();
                }
            }
        }

        private static Task Log(LogMessage msg)
        {
            Plugin.Logger.Debug(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
