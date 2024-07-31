using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading.Channels;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace DynamisBridge.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Plugin plugin;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin _plugin) : base("Dynamis Bridge Settings###dynamisbridge_settings")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 327),
            MaximumSize = new Vector2(400, float.MaxValue)
        };

        plugin = _plugin;
    }

    public void Dispose() { }

    public override void PreDraw() { }

    public override void Draw()
    {
        CenteredText("FFXIV Settings");
        ImGui.Separator();
        ImGui.Spacing();

        // Chat Channel Setting

        var defaultChannelDetails = Plugin.Config.ChatChannel.GetDetails();
        var defaultChannel = defaultChannelDetails?.FancyName ?? Plugin.Config.ChatChannel.ToString();
        using (var combo = ImRaii.Combo("Chat Channel", defaultChannel))
        {
            if (combo)
            {
                var previousChannel = Plugin.Config.ChatChannel;

                if (ImGui.Selectable("None", Plugin.Config.ChatChannel == XivChatType.None))
                    Plugin.Config.ChatChannel = XivChatType.None;

                foreach (var channel in Enum.GetValues<XivChatType>())
                {
                    if (channel == XivChatType.None)
                        continue;

                    if (!ShouldIncludeChannel(channel))
                        continue;

                    var channelDetails = channel.GetDetails();
                    var fancyName = channelDetails?.FancyName ?? channel.ToString();

                    if (ImGui.Selectable(fancyName, Plugin.Config.ChatChannel == channel))
                        Plugin.Config.ChatChannel = channel;
                }

                if (previousChannel != Plugin.Config.ChatChannel)
                {
                    Plugin.Logger.Debug($"ChatChannel set to: {Plugin.Config.ChatChannel.GetDetails().FancyName}");
                    Plugin.Config.Save();
                }
            }
        }

        ImGui.Spacing();

        // Watching Self Setting

        var watchingSelf = Plugin.Config.WatchingSelf;
        if (ImGui.Checkbox("Watch Self", ref watchingSelf))
        {
            SeString? selectedCharacter = null;

            if (watchingSelf && Plugin.State.LocalPlayer != null)
                selectedCharacter = Plugin.State.LocalPlayer.Name;
            else if (!watchingSelf && Plugin.Config.TextboxCharacter != null)
                selectedCharacter = Plugin.Config.TextboxCharacter;

            Plugin.Config.Character = selectedCharacter;

            if (Plugin.Config.Character != null)
                Plugin.Logger.Debug($"Character set to: {Plugin.Config.Character.TextValue}");
            else
                Plugin.Logger.Debug($"Character set to: null");

            Plugin.Config.WatchingSelf = watchingSelf;
            Plugin.Logger.Debug($"WatchingSelf set to: {(watchingSelf ? "True" : "False")}");

            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Plugin.Config.Save();
        }

        ImGui.Spacing();

        // Character Name Setting

        ImGui.BeginDisabled(watchingSelf);
        var characterName = Plugin.Config.TextboxCharacter?.TextValue ?? string.Empty;
        if (ImGui.InputText("Character Name", ref characterName, 100))
        {
            if (!watchingSelf)
            {
                Plugin.Config.TextboxCharacter = string.IsNullOrWhiteSpace(characterName) ? SeString.Empty : new SeString(new TextPayload(characterName));
                Plugin.Config.Character = string.IsNullOrWhiteSpace(characterName) ? SeString.Empty : new SeString(new TextPayload(characterName));

                if (Plugin.Config.Character != null)
                    Plugin.Logger.Debug($"Character set to: {Plugin.Config.Character.TextValue}");
                else
                    Plugin.Logger.Debug($"Character set to: null");

                Plugin.Config.Save();
            }
        }
        ImGui.EndDisabled();

        ImGui.Spacing();

        // Prefix Command Setting

        var prefixCommand = Plugin.Config.PrefixCommand?.TextValue ?? string.Empty;
        if (ImGui.InputText("Prefix Command", ref prefixCommand, 100)) {
            Plugin.Config.PrefixCommand = string.IsNullOrWhiteSpace(prefixCommand) ? SeString.Empty : new SeString(new TextPayload(prefixCommand));

            if (Plugin.Config.PrefixCommand != null)
                Plugin.Logger.Debug($"PrefixCommand set to: {Plugin.Config.PrefixCommand.TextValue}");
            else
                Plugin.Logger.Debug($"PrefixCommand set to: null");

            Plugin.Config.Save();
        }

        ImGui.Spacing();

        CenteredText("Discord Settings");
        ImGui.Separator();
        ImGui.Spacing();

        // Bot Token Setting

        var token = Plugin.Config.Token ?? string.Empty;
        if (ImGui.InputText("Bot Token", ref token, 100, ImGuiInputTextFlags.Password))
        {
            Plugin.Config.Token = string.IsNullOrWhiteSpace(token) ? string.Empty : token;

            if (!string.IsNullOrWhiteSpace(token))
                Plugin.Logger.Debug($"Token set to: {token}");
            else
                Plugin.Logger.Debug($"Token set to: null");

            Plugin.Config.Save();
        }

        ImGui.Spacing();

        // Auto Connect Setting

        var autoConnect = Plugin.Config.AutoConnect;
        if (ImGui.Checkbox("Auto Connect", ref autoConnect))
        {
            Plugin.Config.AutoConnect = autoConnect;
            Plugin.Logger.Debug($"AutoConnect set to: {(autoConnect ? "True" : "False")}");
            Plugin.Config.Save();
        }

        ImGui.SameLine();

        // Join Type Setting

        var joinTypeText = Plugin.Config.JoinType.ToString();

        ImGui.SetNextItemWidth(142.0f);

        using (var joinTypeCombo = ImRaii.Combo("Join Type", joinTypeText))
        {
            if (joinTypeCombo)
            {
                var previousType = Plugin.Config.JoinType;

                foreach (var type in Enum.GetValues<JoinType>())
                {
                    if (ImGui.Selectable(type.ToString(), Plugin.Config.JoinType == type))
                        Plugin.Config.JoinType = type;
                }

                if (previousType != Plugin.Config.JoinType)
                {
                    Plugin.Logger.Debug($"JoinType set to: {Plugin.Config.JoinType}");
                    Plugin.Config.Save();
                }
            }
        }

        // Guild ID Setting

        if (Plugin.Config.JoinType == JoinType.Specify)
        {
            ImGui.Spacing();

            var guildId = Plugin.Config.GuildId;
            if (ImGui.InputText("Guild ID", ref guildId, 100, ImGuiInputTextFlags.Password))
            {
                Plugin.Config.GuildId = string.IsNullOrWhiteSpace(guildId) ? string.Empty : guildId;

                if (!string.IsNullOrWhiteSpace(guildId))
                    Plugin.Logger.Debug($"GuildId set to: {guildId}");
                else
                    Plugin.Logger.Debug($"GuildId set to: null");

                Plugin.Config.Save();
            }
        }

        // Voice Channel ID Setting

        if (Plugin.Config.JoinType == JoinType.Specify)
        {
            ImGui.Spacing();

            var voiceChannelId = Plugin.Config.VoiceChannelId;
            if (ImGui.InputText("Voice Channel ID", ref voiceChannelId, 100, ImGuiInputTextFlags.Password))
            {
                Plugin.Config.VoiceChannelId = string.IsNullOrWhiteSpace(voiceChannelId) ? string.Empty : voiceChannelId;

                if (!string.IsNullOrWhiteSpace(voiceChannelId))
                    Plugin.Logger.Debug($"VoiceChannelId set to: {voiceChannelId}");
                else
                    Plugin.Logger.Debug($"VoiceChannelId set to: null");

                Plugin.Config.Save();
            }
        }

        // User ID Setting

        if (Plugin.Config.JoinType == JoinType.Follow)
        {
            ImGui.Spacing();

            var userId = Plugin.Config.UserId;
            if (ImGui.InputText("User ID", ref userId, 100, ImGuiInputTextFlags.Password))
            {
                Plugin.Config.UserId = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId;

                if (!string.IsNullOrWhiteSpace(userId))
                    Plugin.Logger.Debug($"UserId set to: {userId}");
                else
                    Plugin.Logger.Debug($"UserId set to: null");

                Plugin.Config.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        var peekoMode = Plugin.Config.PeekoMode;
        if (ImGui.Checkbox("Peeko Mode", ref peekoMode))
        {
            Plugin.Config.PeekoMode = peekoMode;
            Plugin.Logger.Debug($"PeekoMode set to: {(peekoMode ? "True" : "False")}");

            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Plugin.Config.Save();
        }

    }

    private bool ShouldIncludeChannel(XivChatType channel)
    {
        var excludedTypes = new List<XivChatType>()
        {
            XivChatType.Urgent,
            XivChatType.Notice,
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
            XivChatType.Echo,
            XivChatType.SystemMessage,
            XivChatType.SystemError,
            XivChatType.ErrorMessage,
            XivChatType.GatheringSystemMessage,
            XivChatType.RetainerSale,
            XivChatType.NPCDialogue,
            XivChatType.NPCDialogueAnnouncements
        };
        return !excludedTypes.Contains(channel);
    }

    public static void CenteredText(string text)
    {
        // Get the width of the text
        float textWidth = ImGui.CalcTextSize(text).X;
        // Get the width of the available space for drawing
        float windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

        // Calculate the position to start drawing the text so it's centered
        float textPosX = (windowWidth - textWidth) / 2.0f;

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
