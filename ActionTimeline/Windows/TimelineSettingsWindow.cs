using ActionTimeline.Helpers;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;

namespace ActionTimeline.Windows
{
    public class TimelineSettingsWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        public TimelineSettingsWindow(string name) : base(name)
        {
            Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoScrollWithMouse;

            Size = new Vector2(300, 350);
        }

        public override void Draw()
        {
            if (!ImGui.BeginTabBar("##Timeline_Settings_TabBar"))
            {
                return;
            }

            ImGui.PushItemWidth(120 * _scale);

            // general
            if (ImGui.BeginTabItem("General##Timeline_General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            // icons
            if (ImGui.BeginTabItem("Icons##Timeline_Icons"))
            {
                DrawIconsTab();
                ImGui.EndTabItem();
            }

            // casts
            if (ImGui.BeginTabItem("Casts##Timeline_Casts"))
            {
                DrawCastsTab();
                ImGui.EndTabItem();
            }

            // grid
            if (ImGui.BeginTabItem("Grid##Timeline_Grid"))
            {
                DrawGridTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        public void DrawGeneralTab()
        {
            ImGui.Checkbox("Enabled", ref Settings.ShowTimeline);

            if (!Settings.ShowTimeline) { return; }

            ImGui.DragInt("Time (seconds)", ref Settings.TimelineTime, 0.1f, 1, 30);
            DrawHelper.SetTooltip("This is how far in the past the timeline will go.");

            ImGui.NewLine();
            ImGui.Checkbox("Locked", ref Settings.TimelineLocked);
            ImGui.ColorEdit4("Locked Color", ref Settings.TimelineLockedBackgroundColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Unlocked Color", ref Settings.TimelineUnlockedBackgroundColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();
            ImGui.Checkbox("Show Only In Duty", ref Settings.ShowTimelineOnlyInDuty);
            ImGui.Checkbox("Show Only In Combat", ref Settings.ShowTimelineOnlyInCombat);
         }

        public void DrawIconsTab()
        {
            ImGui.DragInt("Icon Size", ref Settings.TimelineIconSize);

            ImGui.NewLine();
            ImGui.DragInt("Off GCD Icon Size", ref Settings.TimelineOffGCDIconSize);
            ImGui.DragInt("Iff GCD Vertical Offset", ref Settings.TimelineOffGCDOffset);

            ImGui.NewLine();
            ImGui.Checkbox("Show Auto Attacks", ref Settings.TimelineShowAutoAttacks);
            ImGui.DragInt("Auto Attack Icon Size", ref Settings.TimelineAutoAttackSize);
            ImGui.DragInt("Auto Attack Vertical Offset", ref Settings.TimelineAutoAttackOffset);
        }

        public void DrawCastsTab()
        {
            ImGui.ColorEdit4("Cast In Progress Color", ref Settings.CastInProgressColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Cast Finished Color", ref Settings.CastFinishedColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Cast Canceled Color", ref Settings.CastCanceledColor, ImGuiColorEditFlags.NoInputs);
        }

        public void DrawGridTab()
        {
            ImGui.Checkbox("Enabled", ref Settings.ShowGrid);

            if (!Settings.ShowGrid) { return; }

            ImGui.Checkbox("Show Center Line", ref Settings.ShowGridCenterLine);
            ImGui.DragInt("Line Width", ref Settings.GridLineWidth, 0.5f, 1, 5);
            ImGui.ColorEdit4("Line Color", ref Settings.GridLineColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();
            ImGui.Checkbox("Divide By Seconds", ref Settings.GridDivideBySeconds);

            if (!Settings.GridDivideBySeconds) { return; }

            ImGui.Checkbox("Show Text", ref Settings.GridShowSecondsText);

            ImGui.NewLine();
            ImGui.Checkbox("Sub-Divide By Seconds", ref Settings.GridSubdivideSeconds);

            if (!Settings.GridSubdivideSeconds) { return; }

            ImGui.DragInt("Sub-Division Count", ref Settings.GridSubdivisionCount, 0.5f, 2, 8);
            ImGui.DragInt("Sub-Division Line Width", ref Settings.GridSubdivisionLineWidth, 0.5f, 1, 5);
            ImGui.ColorEdit4("Sub-Division Line Color", ref Settings.GridSubdivisionLineColor, ImGuiColorEditFlags.NoInputs);
        }
    }
}
