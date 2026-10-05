using System.Numerics;
using GameEditor.Rendering;
using ImGuiNET;

namespace GameEditor.Editor;

public static class PalettePanel
{
    public static void Draw(PaintTool tool)
    {
        ImGui.SetNextWindowSize(new(220, 300), ImGuiCond.FirstUseEver);
        ImGui.Begin("Palette");
        int selected = tool.TileId;
        for (int i = 0; i < MapRenderer.Palette.Length; i++)
        {
            var c = MapRenderer.Palette[i];
            if (ImGui.ColorButton($"##tile{i}", new Vector4(c.R, c.G, c.B, c.A) / 255f, ImGuiColorEditFlags.NoTooltip, new(24, 24)))
                selected = i;
            ImGui.SameLine();
            ImGui.RadioButton($"Tile {i}", ref selected, i);
        }
        tool.TileId = selected;
        ImGui.End();
    }
}
