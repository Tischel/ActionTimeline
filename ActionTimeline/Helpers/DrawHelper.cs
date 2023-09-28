using Dalamud.Interface.Internal;
using DelvUI.Helpers;
using ImGuiNET;
using ImGuiScene;
using System.Numerics;

namespace ActionTimeline.Helpers
{
    internal static class DrawHelper
    {
        public static void DrawIcon(uint iconId, Vector2 position, Vector2 size, float alpha, ImDrawListPtr drawList)
        {
            IDalamudTextureWrap? texture = TexturesHelper.GetTextureFromIconId(iconId);
            if (texture == null) return;

            uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
            drawList.AddImage(texture.ImGuiHandle, position, position + size, Vector2.Zero, Vector2.One, color);
        }

        public static void SetTooltip(string message)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(message);
            }
        }
    }
}
