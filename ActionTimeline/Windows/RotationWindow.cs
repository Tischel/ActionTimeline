using ActionTimeline.Helpers;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ActionTimeline.Windows
{
    internal class RotationWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        private ImGuiWindowFlags _baseFlags = ImGuiWindowFlags.NoScrollbar
                                            | ImGuiWindowFlags.NoCollapse
                                            | ImGuiWindowFlags.NoTitleBar
                                            | ImGuiWindowFlags.NoNav
                                            | ImGuiWindowFlags.NoScrollWithMouse;

        public RotationWindow(string name) : base(name)
        {
            Flags = _baseFlags;

            Size = new Vector2(560, 90);
            SizeCondition = ImGuiCond.FirstUseEver;

            Position = new Vector2(200, 295);
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        public override void PreDraw()
        {
            Vector4 bgColor = Settings.RotationLocked ? Settings.RotationLockedBackgroundColor : Settings.RotationUnlockedBackgroundColor;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);

            Flags = _baseFlags;

            if (Settings.RotationLocked)
            {
                Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        }

        public override void PostDraw()
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
        }

        public override void Draw()
        {
            if (ImGui.IsWindowHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    Plugin.ShowRotationSettingsWindow();
                }
            }

            IReadOnlyCollection<TimelineItem>? list = TimelineManager.Instance?.Items;
            if (list == null) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            float width = ImGui.GetWindowWidth();
            float height = ImGui.GetWindowHeight();
            float posX = width - 5;

            Vector2 regularSize = new Vector2(Settings.RotationIconSize);
            Vector2 offGCDSize = new Vector2(Settings.RotationOffGCDIconSize);

            uint separatorColor = ImGui.ColorConvertFloat4ToU32(Settings.RotationSeparatorColor);

            TimelineItem? lastValidItem = null;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (posX < -50) { break; }

                TimelineItem item = list.ElementAt(i);
                if (item.Type != TimelineItemType.Action && item.Type != TimelineItemType.OffGCD && item.Type != TimelineItemType.Item) { continue; }

                // spacing
                if (lastValidItem != null)
                {
                    int spacing = (lastValidItem.Type == TimelineItemType.Action ? Settings.RotationGCDSpacing : Settings.RotationOffGCDSpacing);
                    posX -= spacing;

                    double timeDiff = Math.Abs(item.Time - lastValidItem.Time);
                    if (Settings.RotationSeparatorEnabled && timeDiff > Settings.RotationSeparatorTime)
                    {
                        posX -= Math.Min(1, (int)Settings.RotationSeparatorWidth / 2);

                        Vector2 separatorPosition = new Vector2(pos.X + posX, pos.Y);
                        Vector2 separatorSize = new Vector2(Settings.RotationSeparatorWidth, height);
                        drawList.AddRectFilled(separatorPosition, separatorPosition + separatorSize, separatorColor);

                        posX -= spacing;
                    }
                }

                // size
                Vector2 size = (item.Type == TimelineItemType.Action ? regularSize : offGCDSize);

                // position
                posX -= size.X;
                float posY = height / 2f;
                if (item.Type == TimelineItemType.OffGCD) { posY += Settings.RotationOffGCDOffset; }

                Vector2 position = new Vector2(pos.X + posX, pos.Y + posY - size.Y / 2f);

                // icon
                DrawHelper.DrawIcon(item.IconID, position, size, 1, drawList);

                lastValidItem = item;
            }
        }
    }
}
