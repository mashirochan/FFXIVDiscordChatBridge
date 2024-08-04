using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using ImGuiNET;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace DynamisBridge.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    public MainWindow(Plugin plugin)
        : base($"Dynamis Bridge v{Assembly.GetExecutingAssembly().GetName().Version}##dynamisbridge_main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 210),
            MaximumSize = new Vector2(400, 210)
        };

        _plugin = plugin;
    }

    public void Dispose() { }

    public override async void Draw()
    {
        // can't ref a property, so use a local copy
        var pluginEnabled = Plugin.Config.PluginEnabled;

        using (var textColor = pluginEnabled ? ImRaii.PushColor(ImGuiCol.Text, KnownColor.LimeGreen.Vector()) : ImRaii.PushColor(ImGuiCol.Text, KnownColor.Red.Vector()))
        {
            if (ImGui.Checkbox(pluginEnabled ? "Bridge Enabled" : "Bridge Disabled", ref pluginEnabled))
            {
                Plugin.Config.PluginEnabled = pluginEnabled;
                // can save immediately on change, if you don't want to provide a "Save and Close" button
                Plugin.Config.Save();
            }
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - (ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString() + "Settings").X + (ImGui.GetStyle().FramePadding.X * 2)));

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Cog, "Settings"))
        {
            _plugin.ToggleConfigUI();
        }

        ImGui.Spacing();

        var selectedChannelDetails = Plugin.Config.ChatChannel.GetDetails();
        var selectedChannel = selectedChannelDetails?.FancyName ?? Plugin.Config.ChatChannel.ToString();
        ImGui.Text($"Selected channel: {selectedChannel}");

        ImGui.Spacing();

        var selectedCharacter = Plugin.Config.Character ?? "None";
        ImGui.Text($"Selected character: {selectedCharacter}");

        CenteredText("Discord Status");
        ImGui.Separator();
        ImGui.Spacing();

        // Discord Voice Status
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Bot Voice Connection");
        ImGui.SameLine();
        var drawList = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        p.X += 4.0f;
        p.Y += (ImGui.GetTextLineHeight() / 2) + ImGui.GetStyle().FramePadding.Y; // Adjust circle Y position
        var green = ImGui.GetColorU32(new Vector4(71f / 255f, 161f / 255f, 97f / 255f, 1f));
        var yellow = ImGui.GetColorU32(new Vector4(229f / 255f, 179f / 255f, 76f / 255f, 1f));
        var red = ImGui.GetColorU32(new Vector4(225f / 255f, 78f / 255f, 73f / 255f, 1f));
        uint colorToUse;
        if (Plugin.VoiceState == VoiceStates.Connected)
            colorToUse = green;
        else if (Plugin.VoiceState == VoiceStates.Connecting)
            colorToUse = yellow;
        else
            colorToUse = red;
        drawList.AddCircleFilled(p, 7.5f, colorToUse);
        ImGui.Dummy(new Vector2(0, 10));

        // Disconnect / Reconnect Button
        if (Plugin.VoiceState == VoiceStates.Connected)
        {
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - (ImGui.CalcTextSize(FontAwesomeIcon.Stop.ToIconString() + "Disconnect").X + (ImGui.GetStyle().FramePadding.X * 2)));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, "Disconnect"))
            {
                Plugin.Logger.Info("Leaving voice channel...");
                await _plugin.DiscordService.LeaveVoiceChannel();
            }
        }
        else if (Plugin.VoiceState == VoiceStates.Disconnected)
        {
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - (ImGui.CalcTextSize(FontAwesomeIcon.Play.ToIconString() + "Connect").X + (ImGui.GetStyle().FramePadding.X * 2)));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Connect"))
            {
                Plugin.Logger.Info("Joining voice channel...");
                await _plugin.DiscordService.JoinVoiceChannel();
            }
        }

        ImGui.Spacing();

        ImGui.Text($"Current Guild: {_plugin.DiscordService.GetGuildName()}");

        ImGui.Spacing();

        ImGui.Text($"Current Voice: {_plugin.DiscordService.GetChannelName()}");
    }

    public static void CenteredText(string text)
    {
        // Get the width of the text
        var textWidth = ImGui.CalcTextSize(text).X;
        // Get the width of the available space for drawing
        var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

        // Calculate the position to start drawing the text so it's centered
        var textPosX = (windowWidth - textWidth) / 2.0f;

        // Add padding from the left edge
        if (textPosX > 0.0f)
        {
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMin().X + textPosX);
        }
        else
        {
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMin().X);
        }

        // Draw the text
        ImGui.Text(text);
    }
}
