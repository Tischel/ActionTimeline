using ActionTimeline.Helpers;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ActionTimeline.Windows
{
    internal class TimelineWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        private ImGuiWindowFlags _baseFlags = ImGuiWindowFlags.NoScrollbar
                                            | ImGuiWindowFlags.NoCollapse
                                            | ImGuiWindowFlags.NoTitleBar
                                            | ImGuiWindowFlags.NoNav
                                            | ImGuiWindowFlags.NoScrollWithMouse;

        public TimelineWindow(string name) : base(name)
        {
            Flags = _baseFlags;

            Size = new Vector2(560, 90);
            SizeCondition = ImGuiCond.FirstUseEver;

            Position = new Vector2(200, 200);
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        public override void PreDraw()
        {
            Vector4 bgColor = Settings.TimelineLocked ? Settings.TimelineLockedBackgroundColor : Settings.TimelineUnlockedBackgroundColor;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);

            Flags = _baseFlags;

            if (Settings.TimelineLocked)
            {
                Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
            }
        }

        public override void PostDraw()
        {
            ImGui.PopStyleColor();
        }

        public override void Draw()
        {
            if (ImGui.IsWindowHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    Plugin.ShowTimelineSettingsWindow();
                }
            }

            DrawGrid();

            IReadOnlyCollection<TimelineItem>? list = TimelineManager.Instance?.Items;
            if (list == null) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            float width = ImGui.GetWindowWidth();
            float height = ImGui.GetWindowHeight();
            double now = ImGui.GetTime();
            int maxTime = Settings.TimelineTime;

            Vector2 regularSize = new Vector2(Settings.TimelineIconSize);
            Vector2 offGCDSize = new Vector2(Settings.TimelineOffGCDIconSize);
            Vector2 autoAttackSize = new Vector2(Settings.TimelineAutoAttackSize);

            uint castInProgressColor = ImGui.ColorConvertFloat4ToU32(Settings.CastInProgressColor);
            uint castFinishedColor = ImGui.ColorConvertFloat4ToU32(Settings.CastFinishedColor);
            uint castCanceledColor = ImGui.ColorConvertFloat4ToU32(Settings.CastCanceledColor);

            for (int i = 0; i < list.Count; i++)
            {
                TimelineItem item = list.ElementAt(i);
                if (!Settings.TimelineShowAutoAttacks && item.Type == TimelineItemType.AutoAttack) { continue; }

                // position
                double timeDiff = Math.Abs(now - item.Time);
                float posX = width - ((float)timeDiff * width / maxTime);

                float posY = height / 2f;
                if (item.Type == TimelineItemType.OffGCD) { posY += Settings.TimelineOffGCDOffset; }
                else if (item.Type == TimelineItemType.AutoAttack) { posY += Settings.TimelineAutoAttackOffset; }

                // size
                Vector2 size = regularSize;
                if (item.Type == TimelineItemType.OffGCD) { size = offGCDSize; }
                else if (item.Type == TimelineItemType.AutoAttack) { size = autoAttackSize; }

                Vector2 position = new Vector2(pos.X + posX, pos.Y + posY - size.Y / 2f);

                // cast bar
                if (item.Type == TimelineItemType.CastStart)
                {
                    float endX = width;
                    uint color = castInProgressColor;

                    if (i < list.Count - 1)
                    {
                        TimelineItem nextItem = list.ElementAt(i + 1);
                        timeDiff = Math.Abs(now - nextItem.Time);
                        endX = width - ((float)timeDiff * width / maxTime);

                        if (nextItem.Type == TimelineItemType.CastCancel || nextItem.ActionID != item.ActionID)
                        {
                            color = castCanceledColor;
                        }
                        else if (nextItem.Type == TimelineItemType.Action)
                        {
                            color = castFinishedColor;
                        }
                    }

                    Vector2 startPosition = new Vector2(position.X + size.X / 2, position.Y);
                    Vector2 endPosition = new Vector2(pos.X + endX + 5, position.Y + size.Y);
                    if (endPosition.X > 0)
                    {
                        drawList.AddRectFilled(startPosition, endPosition, color);
                    }
                }

                if (position.X >= -size.X && item.Type != TimelineItemType.CastCancel)
                {
                    DrawHelper.DrawIcon(item.IconID, position, size, 1, drawList);
                }
            }
        }

        private void DrawGrid()
        {
            if (!Settings.ShowGrid) { return; }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            float width = ImGui.GetWindowWidth();
            float height = ImGui.GetWindowHeight();

            double now = ImGui.GetTime();
            int maxTime = Settings.TimelineTime;

            uint lineColor = ImGui.ColorConvertFloat4ToU32(Settings.GridLineColor);
            uint subdivisionLineColor = ImGui.ColorConvertFloat4ToU32(Settings.GridSubdivisionLineColor);

            if (Settings.ShowGridCenterLine)
            {
                drawList.AddLine(new Vector2(pos.X, pos.Y + height / 2f), new Vector2(pos.X + width, pos.Y + height / 2f), lineColor, Settings.GridLineWidth);
            }

            if (!Settings.GridDivideBySeconds) { return; }

            for (int i = 0; i < maxTime; i++)
            {
                float step = width / maxTime;
                float x = step * i;

                if (Settings.GridSubdivideSeconds && Settings.GridSubdivisionCount > 1)
                {
                    float subStep = step * 1f / (float)Settings.GridSubdivisionCount;
                    for (int j = 1; j < Settings.GridSubdivisionCount; j++)
                    {
                        drawList.AddLine(new Vector2(pos.X + x + subStep * j, pos.Y), new Vector2(pos.X + x + subStep * j, pos.Y + height), subdivisionLineColor, Settings.GridSubdivisionLineWidth);
                    }
                }

                drawList.AddLine(new Vector2(pos.X + x, pos.Y), new Vector2(pos.X + x, pos.Y + height), lineColor, Settings.GridLineWidth);

                if (Settings.GridShowSecondsText)
                {
                    drawList.AddText(new Vector2(pos.X + x + 2, pos.Y), lineColor, $"-{maxTime - i}s");
                }
            }
        }
    }
}
