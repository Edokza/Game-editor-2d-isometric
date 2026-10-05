using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;
using Raylib_cs;

namespace GameEditor.Editor;

/// <summary>Call after rlImGui.Begin() so WantCapture* is current.</summary>
public sealed class PaintTool(MapEditingService service)
{
    private bool _painting;

    public int TileId { get; set; } = 1;

    public void Update(Vector2 origin)
    {
        var io = ImGui.GetIO();
        var hover = IsoMath.ScreenToTile(Raylib.GetMousePosition() - origin);

        if (!io.WantCaptureMouse && service.Map.InBounds(hover))
            MapRenderer.DrawHighlight(hover, origin, Color.Yellow);

        if (!_painting && !io.WantCaptureMouse && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            service.BeginStroke();
            _painting = true;
        }

        if (_painting)
        {
            service.Paint(hover, TileId);
            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                service.EndStroke();
                _painting = false;
            }
        }

        if (io.WantCaptureKeyboard) return;
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.Z)) service.Undo();
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.Y)) service.Redo();
    }
}
