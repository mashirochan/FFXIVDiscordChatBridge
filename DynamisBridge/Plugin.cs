using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DynamisBridge.Windows;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Victoria;
using Discord.WebSocket;
using Discord;

namespace DynamisBridge;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState State { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string SettingsCommand1 = "/dynamisbridge";
    private const string SettingsCommand2 = "/dbridge";

    public static Configuration Config { get; set; } = new Configuration();
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public IServiceProvider ServiceProvider { get; private set; }

    public readonly WindowSystem WindowSystem = new("DynamisBridge");
    private bool isDisposed = false;
    public static VoiceStates VoiceState = VoiceStates.Disconnected;
    private readonly Discord _discord;
    public Discord DiscordService => _discord;
    public LavalinkManager LavalinkManager;
    private readonly ServiceProvider _services;

    public Plugin()
    {
        //if (PluginInterface?.AssemblyLocation?.Directory?.FullName is string pluginDir)
        //{
        //    LoadDll(Path.Combine(pluginDir, "runtimes\\win-x64\\native", "opus.dll"));
        //    LoadDll(Path.Combine(pluginDir, "runtimes\\win-x64\\native", "libsodium.dll"));
        //}
        //else
        //{
        //    Logger.Error("PluginInterface or AssemblyLocation is null.");
        //}

        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(SettingsCommand1, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the configuration window for Dynamis Bridge"
        });

        CommandManager.AddHandler(SettingsCommand2, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the configuration window for Dynamis Bridge"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Chat.ChatMessage += OnChatMessage;

        // Lavalink Setup
        LavalinkManager.StartLavalink();

        _services = ConfigureServices();

        _discord = new Discord(_services);
        Task.Run(() => _discord.Start());
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds
        }));

        services.AddLogging();

        services.AddLavaNode(config =>
        {
            config.Hostname = "127.0.0.1";
            config.Port = 33948;
            config.Authorization = "peekoiscute";
            config.SelfDeaf = true;
        });
        services.AddSingleton<AudioModule>();
        services.AddSingleton<AudioService>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        WindowSystem.RemoveAllWindows();

        Chat.ChatMessage -= OnChatMessage;

        ConfigWindow.Dispose();
        ((IDisposable)MainWindow).Dispose();

        CommandManager.RemoveHandler(SettingsCommand1);
        CommandManager.RemoveHandler(SettingsCommand2);

        DisposeAsync().GetAwaiter().GetResult(); // Await disposal properly
    }

    private async Task DisposeAsync()
    {
        try
        {
            await _discord.DisposeAsync();
            Logger.Info("Plugin disposed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during plugin disposal: {ex.Message}");
        }
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (Config.PluginEnabled && type == Config.ChatChannel && IsCharacterWatched(sender.TextValue))
        {
            Logger.Debug($"Message received in {type} from {sender}: {message}");
            var trimmedMessage = message.ToString().Trim();
            var prefix = Config.PrefixCommand?.TextValue ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(prefix)) {
                if (!trimmedMessage.StartsWith(prefix))
                {
                    Logger.Debug($"Message does not start with \"{prefix}\", not sending!");
                    return;
                }
                else
                {
                    trimmedMessage = trimmedMessage.Replace(prefix, string.Empty);
                }
            }

            var textToSend = trimmedMessage.Trim();

            AddMessageToQueue(textToSend);
        }
    }

    public async void AddMessageToQueue(string message)
    {
        var filePath = await Google.CreateAudioFile(message);
        Logger.Debug($"filePath: {filePath}");
        await _discord.PlayAudioFile(filePath);
    }

    private static bool IsCharacterWatched(SeString player)
    {
        if (Config.WatchingSelf)
            return State.LocalPlayer != null && State.LocalPlayer.Name.TextValue == player.TextValue;

        if (Config.TextboxCharacter == null)
            return false;

        var textboxName = Regex.Replace(Config.TextboxCharacter.ToString(), "[^a-zA-Z]", "").ToLower();
        var playerName = Regex.Replace(player.ToString(), "[^a-zA-Z]", "").ToLower();

        return textboxName == playerName;
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    //[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    //private static extern IntPtr LoadLibrary(string lpFileName);

    //private static void LoadDll(string libraryPath)
    //{
    //    if (LoadLibrary(libraryPath) == IntPtr.Zero)
    //    {
    //        throw new Exception($"Failed to load library: {libraryPath}");
    //    }
    //}
}
