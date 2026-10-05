using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using Raylib_cs;

namespace GameEditor.Editor;

/// <summary>Called by ScenePanel inside its texture mode; <paramref name="mouse"/> is panel-local.</summary>
public sealed class PaintTool(MapEditingService service)
{
    private bool _painting;

    public int TileId { get; set; } = 1;

    public void Update(Camera2D camera, Vector2 mouse, bool sceneHovered)
    {
        var hover = IsoMath.ScreenToTile(Raylib.GetScreenToWorld2D(mouse, camera));

        if (sceneHovered && service.Map.InBounds(hover))
        {
            Raylib.BeginMode2D(camera);
            MapRenderer.DrawHighlight(hover, Vector2.Zero, Color.Yellow);
            Raylib.EndMode2D();
        }

        if (!_painting && sceneHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            service.BeginStroke();
            _painting = true;
        }

        if (_painting)
        {
            // button state, not the release edge: a release missed while Scene was hidden or unfocused must still end the stroke
            if (Raylib.IsMouseButtonDown(MouseButton.Left)) service.Paint(hover, TileId);
            else
            {
                service.EndStroke();
                _painting = false;
            }
        }
    }
}
