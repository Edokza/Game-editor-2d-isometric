using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;
using rlImGui_cs;

namespace GameEditor.Editor;

/// <summary>Tiles tab picks the paint tile, right-click toggles Solid; Objects tab (right-click: Solid / Shadow) adds an object with that sprite at the map center.</summary>
public static class AssetsPanel
{
    private static readonly Vector4 Selected = new(0.9f, 0.75f, 0.2f, 1f);
    private static readonly Vector4 SolidMark = new(0.85f, 0.2f, 0.2f, 1f);

    public static void Draw(PaintTool tool, MapEditingService service, ObjectPanels objectPanels)
    {
        ImGui.SetNextWindowSize(new(240, 320), ImGuiCond.FirstUseEver);
        ImGui.Begin("Assets");
        if (ImGui.RadioButton("Brush (B)", tool.Mode == PaintMode.Brush)) tool.Mode = PaintMode.Brush;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fill (G)", tool.Mode == PaintMode.Fill)) tool.Mode = PaintMode.Fill;
        ImGui.SameLine();
        if (ImGui.RadioButton("Rect (R)", tool.Mode == PaintMode.Rect)) tool.Mode = PaintMode.Rect;
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
                    Wrap(ref any, IsoMath.TileWidth / 2f); // half size: 128px tiles would crowd the panel
                    ImGui.PushID(i); // constant labels: no per-frame string allocation
                    bool clicked;
                    if (tex.Id != 0) clicked = rlImGui.ImageButtonSize("##t", tex, new(tex.Width / 2f, tex.Height / 2f));
                    else
                    {
                        var c = MapRenderer.Palette[i];
                        clicked = ImGui.ColorButton("##t", new Vector4(c.R, c.G, c.B, c.A) / 255f, ImGuiColorEditFlags.NoTooltip, new(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f));
                    }
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    var draw = ImGui.GetWindowDrawList();
                    // outline, not a button color: ColorButton ignores ImGuiCol.Button
                    if (i == tool.TileId) draw.AddRect(min, max, ImGui.GetColorU32(Selected), 0, ImDrawFlags.None, 2);
                    bool solid = service.Map.Solid.Contains(i);
                    if (solid) draw.AddTriangleFilled(new(max.X - 12, min.Y), new(max.X, min.Y), new(max.X, min.Y + 12), ImGui.GetColorU32(SolidMark));
                    if (ImGui.BeginItemTooltip())
                    {
                        ImGui.Value("Tile", i); // formats on the native side: no string per frame while hovered
                        if (solid) ImGui.TextUnformatted("Solid");
                        ImGui.EndTooltip();
                    }
                    if (ImGui.BeginPopupContextItem("solid"))
                    {
                        if (ImGui.MenuItem("Solid", "", solid)) service.SetSolid(i, !solid);
                        ImGui.EndPopup();
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
                    Wrap(ref any, tex.Width / 2f);
                    ImGui.PushID(k++);
                    if (rlImGui.ImageButtonSize("##o", tex, new(tex.Width / 2f, tex.Height / 2f)))
                        objectPanels.SelectedId = service.AddObject(name, map.Width / 2f, map.Height / 2f, name).Id;
                    bool solid = map.SolidSprites.Contains(name);
                    if (solid)
                    {
                        var min = ImGui.GetItemRectMin();
                        var max = ImGui.GetItemRectMax();
                        ImGui.GetWindowDrawList().AddTriangleFilled(new(max.X - 12, min.Y), new(max.X, min.Y), new(max.X, min.Y + 12), ImGui.GetColorU32(SolidMark));
                    }
                    if (ImGui.IsItemHovered()) Tooltip(solid ? name + " (Solid)" : name);
                    if (ImGui.BeginPopupContextItem("solid"))
                    {
                        if (ImGui.MenuItem("Solid", "", solid)) service.SetSolidSprite(name, !solid);
                        bool shadow = !map.ShadowlessSprites.Contains(name);
                        if (ImGui.MenuItem("Shadow", "", shadow)) service.SetShadow(name, !shadow);
                        ImGui.EndPopup();
                    }
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
