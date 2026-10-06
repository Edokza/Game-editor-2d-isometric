using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using Raylib_cs;

namespace GameEditor.Editor;

/// <summary>Left-drag: on an object moves it, elsewhere paints tiles. Called by ScenePanel inside its texture mode; mouse is panel-local.</summary>
public sealed class PaintTool(MapEditingService service, ObjectPanels objectPanels)
{
    private bool _painting;
    private (int Id, Vector2 Grab, int Token)? _drag; // Grab = mouse minus object position, tile space

    public int TileId { get; set; } = 1;

    public void Update(Camera2D camera, Vector2 mouse, bool sceneHovered)
    {
        var world = Raylib.GetScreenToWorld2D(mouse, camera);
        var hover = IsoMath.ScreenToTile(world);
        var map = service.Map;

        if (sceneHovered && service.Map.InBounds(hover))
        {
            Raylib.BeginMode2D(camera);
            MapRenderer.DrawHighlight(hover, Vector2.Zero, Color.Yellow);
            Raylib.EndMode2D();
        }

        if (!_painting && _drag is null && sceneHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (MapRenderer.ObjectAt(map, Vector2.Zero, world) is { } hit)
            {
                objectPanels.SelectedId = hit.Id;
                _drag = (hit.Id, IsoMath.ScreenToTileF(world) - new Vector2(hit.X, hit.Y), service.BeginObjectEdit(hit.Id));
            }
            else
            {
                service.BeginStroke();
                _painting = true;
            }
        }

        if (_drag is { } d)
        {
            int i = map.IndexOf(d.Id);
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && i >= 0)
            {
                var p = IsoMath.ScreenToTileF(world) - d.Grab;
                service.UpdateObject(map.Objects[i] with { X = p.X, Y = p.Y });
            }
            else
            {
                service.EndObjectEdit(d.Token);
                _drag = null;
            }
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
