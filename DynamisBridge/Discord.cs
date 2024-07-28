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

namespace DynamisBridge
{
    internal class Discord
    {
        private static readonly Lazy<Discord> Instance = new(() => new Discord());
        public static Discord instance => Instance.Value;
        private static readonly DiscordSocketClient Client = new(new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds
        });
        private static IAudioClient? AudioClient;
        private static AudioOutStream? AudioStream;
        private static Process? FFMpeg;
        private static Stream? Output;
        private readonly ConcurrentQueue<string> audioQueue = new();
        private bool isPlaying = false;

        public static async Task Main()
        {
            Client.Log += Log;
            Client.Ready += OnReady;
            Client.Disconnected += OnDisconnect;
            Client.UserVoiceStateUpdated += OnVoiceStateUpdated;

            var token = Plugin.Config.Token;

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            await Task.Delay(-1);
        }

        private static async Task OnReady()
        {
            Plugin.Logger.Info($"Ready! Logged in as {Client.CurrentUser.GlobalName}");

            if (Plugin.Config.JoinType == JoinType.Specify)
            {
                Plugin.Logger.Info("Specify mode is enabled, attempting to join voice channel...");
                await JoinVoiceChannel();
            }
            else if (Plugin.Config.JoinType == JoinType.Follow)
            {
                var found = false;
                Plugin.Logger.Info("Follow mode is enabled, attempting to join user's voice channel...");
                foreach (var guild in Client.Guilds)
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
                            await JoinVoiceChannel(user.VoiceChannel);
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

        private static async Task OnDisconnect(Exception ex)
        {
            Plugin.Logger.Info($"Bot disconnected: {ex.Message}");
            await LeaveVoiceChannel();
        }

        public static async Task DisposeAsync()
        {
            await LeaveVoiceChannel();
            Client.Log -= Log;
            Client.Ready -= OnReady;
            Client.Disconnected -= OnDisconnect;
            Client.UserVoiceStateUpdated -= OnVoiceStateUpdated;
            Client?.Dispose();
        }

        private static async Task JoinVoiceChannel(SocketVoiceChannel? channel = null)
        {
            if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected)
                return;

            if (channel == null)
            {
                var guild = Client.GetGuild(ulong.Parse(Plugin.Config.GuildId));
                if (guild == null)
                {
                    Plugin.Logger.Error("Guild not found!");
                    return;
                }

                channel = guild.GetVoiceChannel(ulong.Parse(Plugin.Config.VoiceChannelId));
                if (channel == null)
                {
                    Plugin.Logger.Error("Voice channel not found!");
                    return;
                }
            }

            AudioClient = await channel.ConnectAsync();
            Plugin.Logger.Info($"Joined {channel.Name}!");
        }

        public static async Task LeaveVoiceChannel()
        {
            if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected)
            {
                await AudioClient.StopAsync();
                AudioClient = null;
                Plugin.Logger.Info("Disconnected from voice channel!");
            }
            else
            {
                Plugin.Logger.Debug("Bot is not connected to any voice channel.");
            }
        }

        private static async Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            if (Plugin.Config.JoinType == JoinType.Follow && user.Id == ulong.Parse(Plugin.Config.UserId))
            {
                // User joined a voice channel
                if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
                {
                    var channel = newState.VoiceChannel;
                    Plugin.Logger.Info($"{user.GlobalName} joined {channel.Name}, attempting to follow...");
                    await JoinVoiceChannel(channel);
                }
                else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                {
                    var channel = oldState.VoiceChannel;
                    Plugin.Logger.Info($"{user.GlobalName} left {channel.Name}, attempting to leave...");
                    await LeaveVoiceChannel();
                }
            }
        }

        public void EnqueueAudio(string filePath)
        {
            audioQueue.Enqueue(filePath);
            Plugin.Logger.Debug($"!isPlaying: {!isPlaying}");
            if (!isPlaying)
            {
                _ = Task.Run(() => PlayNextInQueue());
            }
        }

        private async Task PlayNextInQueue()
        {
            while (audioQueue.TryDequeue(out var filePath)) {
                Plugin.Logger.Debug($"PlayNextInQueue filePath: {filePath}");
                isPlaying = true;
                await PlayAudio(filePath);
                await Task.Delay(1000);
            }
            isPlaying = false;
        }

        private static async Task PlayAudio(string path)
        {
            if (AudioClient == null)
            {
                Plugin.Logger.Error("AudioClient has not been instantiated yet!");
                return;
            }

            Plugin.Logger.Debug($"PlayAudio playing audio: {path}");

            FFMpeg ??= CreateStream(path);
            Output ??= FFMpeg.StandardOutput.BaseStream;
            AudioStream ??= AudioClient.CreatePCMStream(AudioApplication.Voice);
            try
            {
                Plugin.Logger.Debug($"Playing voice message from {path}!");
                await Output.CopyToAsync(AudioStream);
            }
            finally
            {
                await AudioStream.FlushAsync();
            }
        }

        private static Process CreateStream(string path)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            return process ?? throw new InvalidOperationException("Failed to start process.");
        }

        private static Task Log(LogMessage msg)
        {
            Plugin.Logger.Debug(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
