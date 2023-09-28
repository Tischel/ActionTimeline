using ActionTimeline.Helpers;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;

namespace ActionTimeline.Windows
{
    public class RotationSettingsWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        public RotationSettingsWindow(string name) : base(name)
        {
            Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoScrollWithMouse;

            Size = new Vector2(300, 350);
        }

        public override void Draw()
        {
            if (!ImGui.BeginTabBar("##Rotation_Settings_TabBar"))
            {
                return;
            }

            ImGui.PushItemWidth(80 * _scale);

            // general
            if (ImGui.BeginTabItem("General##Rotation_General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            // icons
            if (ImGui.BeginTabItem("Icons##Rotation_Icons"))
            {
                DrawIconsTab();
                ImGui.EndTabItem();
            }

            // separator
            if (ImGui.BeginTabItem("Separator##Rotation_Separator"))
            {
                DrawSeparatorTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        public void DrawGeneralTab()
        {
            ImGui.Checkbox("Enabled", ref Settings.ShowRotation);

            if (!Settings.ShowRotation) { return; }

            ImGui.DragInt("GCD Spacing", ref Settings.RotationGCDSpacing);
            ImGui.DragInt("Off-GCD Spacing", ref Settings.RotationOffGCDSpacing);

            ImGui.NewLine();
            ImGui.Checkbox("Locked", ref Settings.RotationLocked);
            ImGui.ColorEdit4("Locked Color", ref Settings.RotationLockedBackgroundColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Unlocked Color", ref Settings.RotationUnlockedBackgroundColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();
            ImGui.DragInt("Out of Combat Clear Time (seconds)", ref Settings.OutOfCombatClearTime, 0.1f, 1, 30);
            DrawHelper.SetTooltip("The rotation will be cleared after being out of combat for this many seconds.");

            ImGui.Checkbox("Show Only In Duty", ref Settings.ShowRotationOnlyInDuty);
            ImGui.Checkbox("Show Only In Combat", ref Settings.ShowRotationOnlyInCombat);
        }

        public void DrawIconsTab()
        {
            ImGui.DragInt("Icon Size", ref Settings.RotationIconSize);

            ImGui.NewLine();
            ImGui.DragInt("Off GCD Icon Size", ref Settings.RotationOffGCDIconSize);
            ImGui.DragInt("Iff GCD Vertical Offset", ref Settings.RotationOffGCDOffset);
        }

        public void DrawSeparatorTab()
        {
            ImGui.Checkbox("Enabled", ref Settings.RotationSeparatorEnabled);
            DrawHelper.SetTooltip("Draws a separator between 2 abilities if enough time has passed in between.");

            if (!Settings.RotationSeparatorEnabled) { return; }

            ImGui.DragInt("Time (seconds)", ref Settings.RotationSeparatorTime, 0.5f, 5, 60);
            ImGui.DragInt("Width", ref Settings.RotationSeparatorWidth, 0.5f, 1, 10);
            ImGui.ColorEdit4("Color", ref Settings.RotationSeparatorColor, ImGuiColorEditFlags.NoInputs);
        }
    }
}
