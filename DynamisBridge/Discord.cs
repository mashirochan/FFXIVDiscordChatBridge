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
        private readonly ConcurrentQueue<string> audioQueue = new();
        private bool isPlaying = false;
        public static string CurrentGuildName = "Disconnected";
        public static string CurrentChannelName = "Disconnected";

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

            if (Plugin.Config.AutoConnect == true)
                await JoinVoiceChannel();

            await 
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

        public static async Task JoinVoiceChannel()
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

        private static async Task ConnectToChannel(SocketVoiceChannel? channel = null)
        {
            if (AudioClient != null && AudioClient.ConnectionState == ConnectionState.Connected)
                return;

            Plugin.VoiceState = VoiceStates.Connecting;
            CurrentGuildName = "Connecting...";
            CurrentChannelName = "Connecting...";

            if (channel == null)
            {
                var guild = Client.GetGuild(ulong.Parse(Plugin.Config.GuildId));
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

            AudioClient = await channel.ConnectAsync();
            Plugin.Logger.Info($"Joined {channel.Name}!");
            Plugin.VoiceState = VoiceStates.Connected;
            CurrentGuildName = channel.Guild.Name;
            CurrentChannelName = channel.Name;
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
            Plugin.VoiceState = VoiceStates.Disconnected;
            CurrentGuildName = "Disconnected";
            CurrentChannelName = "Disconnected";
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
                    await ConnectToChannel(channel);
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
                Task.Run(PlayNextInQueue);
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

        private CancellationTokenSource _disposeToken;

        private async Task PlayAudio(string path)
        {
            try
            {
                _disposeToken = new CancellationTokenSource();
                bool exit = false;
                // Create FFmpeg using the previous example
                using (var ffmpeg = CreateStream(path))
                {
                    if (ffmpeg == null)
                    {
                        Plugin.Logger.Debug("FFMPEG null");
                        return;
                    }
                    if (AudioClient == null)
                    {
                        Plugin.Logger.Debug("AudioClient null");
                        return;
                    }
                    using (var output = ffmpeg.StandardOutput.BaseStream)
                    using (var discord = AudioClient.CreatePCMStream(AudioApplication.Mixed, 1920))
                    {
                        //try 
                        //{
                        //    await output.CopyToAsync(discord); 
                        //}
                        //finally 
                        //{ 
                        //    await discord.FlushAsync(); 
                        //}
                        int bufferSize = 1024;
                        int total = 0;

                        byte[] buffer = new byte[bufferSize];
                        while (!_disposeToken.IsCancellationRequested && !exit)
                        {
                            Plugin.Logger.Debug("Loop");
                            try
                            {
                                int read = await output.ReadAsync(buffer, 0, bufferSize, _disposeToken.Token);
                                total += read;
                                Plugin.Logger.Debug($"Read {read} - Total {total}");
                                if (read == 0)
                                {
                                    //No more data available
                                    exit = true;
                                    break;
                                }
                                await discord.WriteAsync(buffer, 0, read, _disposeToken.Token);
                            }
                            catch (Exception x)
                            {
                                exit = true;
                                Plugin.Logger.Debug(x.Message);
                            }

                        }
                        Plugin.Logger.Debug("Flushing");
                        await discord.FlushAsync();
                        Plugin.Logger.Debug("Done!");
                    }
                }
            }
            catch (Exception x)
            {
                Plugin.Logger.Debug(x.Message);
            }
        }

        private Process? CreateStream(string path)
        {
            var logPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "ffmpeg_log.txt");
            try
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-xerror -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                });
                Task.Run(async () =>
                {
                    Plugin.Logger.Debug($"Got error {await proc.StandardError.ReadToEndAsync()}");
                });
                proc.Exited += (object s, EventArgs a) =>
                {
                    Plugin.Logger.Debug("Process exited");
                };
                return proc;
            }
            catch (Exception x)
            {
                Plugin.Logger.Debug(x.Message);
                return null;
            }
        }

        private static Task Log(LogMessage msg)
        {
            Plugin.Logger.Debug(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
