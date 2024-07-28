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

namespace DynamisBridge.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin plugin;

    public MainWindow(Plugin _plugin)
        : base("Dynamis Bridge##dynamisbridge_main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        plugin = _plugin;
    }

    public void Dispose() { }

    public override void Draw()
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
            plugin.ToggleConfigUI();
        }

        ImGui.Spacing();

        var selectedChannelDetails = Plugin.Config.ChatChannel.GetDetails();
        var selectedChannel = selectedChannelDetails?.FancyName ?? Plugin.Config.ChatChannel.ToString();
        ImGui.Text($"Selected channel: {selectedChannel}");

        ImGui.Spacing();

        var selectedCharacter = Plugin.Config.Character ?? "None";
        ImGui.Text($"Selected character: {selectedCharacter}");
    }
}
