using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;
using rlImGui_cs;

namespace GameEditor.Editor;

/// <summary>Tiles tab picks the paint tile; Objects tab adds an object with that sprite at the map center.</summary>
public static class AssetsPanel
{
    private static readonly Vector4 Selected = new(0.9f, 0.75f, 0.2f, 1f);

    public static void Draw(PaintTool tool, MapEditingService service, ObjectPanels objectPanels)
    {
        ImGui.SetNextWindowSize(new(240, 320), ImGuiCond.FirstUseEver);
        ImGui.Begin("Assets");
        if (ImGui.BeginTabBar("assets"))
        {
            if (ImGui.BeginTabItem("Tiles"))
            {
                bool any = false;
                int count = Math.Max(MapRenderer.Palette.Length, Sprites.Tiles.Length);
                for (int i = 0; i < count; i++)
                {
                    var tex = Sprites.Tile(i);
                    if (tex.Id == 0 && i >= MapRenderer.Palette.Length) continue; // id gap in tiles/: nothing to show
                    Wrap(ref any, IsoMath.TileWidth);
                    ImGui.PushID(i); // constant labels: no per-frame string allocation
                    bool clicked;
                    if (tex.Id != 0) clicked = rlImGui.ImageButton("##t", tex);
                    else
                    {
                        var c = MapRenderer.Palette[i];
                        clicked = ImGui.ColorButton("##t", new Vector4(c.R, c.G, c.B, c.A) / 255f, ImGuiColorEditFlags.NoTooltip, new(IsoMath.TileWidth, IsoMath.TileHeight));
                    }
                    // outline, not a button color: ColorButton ignores ImGuiCol.Button
                    if (i == tool.TileId) ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(Selected), 0, ImDrawFlags.None, 2);
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.Value("Tile", i); // formats on the native side: no string per frame while hovered
                        ImGui.EndTooltip();
                    }
                    if (clicked) tool.TileId = i;
                    ImGui.PopID();
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Objects"))
            {
                var map = service.Map;
                bool any = false;
                int k = 0;
                foreach (var (name, tex) in Sprites.Objects)
                {
                    Wrap(ref any, tex.Width);
                    ImGui.PushID(k++);
                    if (rlImGui.ImageButton("##o", tex))
                        objectPanels.SelectedId = service.AddObject(name, map.Width / 2f, map.Height / 2f, name).Id;
                    if (ImGui.IsItemHovered()) Tooltip(name);
                    ImGui.PopID();
                }
                if (Sprites.Objects.Count == 0) ImGui.TextDisabled("(no assets/objects/*.png)");
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.End();
    }

    // TextUnformatted: SetTooltip is printf-style, a '%' in a file name would be read as a format spec
    private static void Tooltip(string text)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(text);
        ImGui.EndTooltip();
    }

    /// <summary>Same line as the previous button if one <paramref name="width"/> wide still fits.</summary>
    private static void Wrap(ref bool any, float width)
    {
        if (!any)
        {
            any = true;
            return;
        }
        var style = ImGui.GetStyle();
        float next = ImGui.GetItemRectMax().X + style.ItemSpacing.X + width + style.FramePadding.X * 2;
        // cursor is at the start of a fresh line here, so this is the right edge of the content area
        if (next < ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X) ImGui.SameLine();
    }
}
