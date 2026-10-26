using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using Raylib_cs;

namespace GameEditor.Editor;

public enum PaintMode { Brush, Fill, Rect }

/// <summary>Left-drag: on an object moves it, elsewhere paints tiles (Fill: one click floods the region; Rect: press = first corner, release fills the box). Right-click picks the tile under the mouse. Called by ScenePanel inside its texture mode; mouse is panel-local.</summary>
public sealed class PaintTool(MapEditingService service, ObjectPanels objectPanels)
{
    private bool _painting;
    private TileCoord? _rectStart;
    private (int Id, Vector2 Grab, int Token)? _drag; // Grab = mouse minus object position, tile space

    public int TileId { get; set; } = 1;
    public PaintMode Mode { get; set; }

    /// <summary>In-bounds tile under the mouse in Scene, else null. Set by Update; ScenePanel clears it while hidden.</summary>
    public TileCoord? Hover { get; set; }

    public void Update(Camera2D camera, Vector2 mouse, bool sceneHovered)
    {
        var world = Raylib.GetScreenToWorld2D(mouse, camera);
        var hover = IsoMath.ScreenToTile(world);
        var map = service.Map;
        Hover = sceneHovered && map.InBounds(hover) ? hover : null;

        if (sceneHovered && service.Map.InBounds(hover))
        {
            Raylib.BeginMode2D(camera);
            MapRenderer.DrawHighlight(hover, Vector2.Zero, Color.Yellow);
            Raylib.EndMode2D();
        }

        // eyedropper: only swaps TileId, so an open stroke/drag keeps going and Mode stays
        if (sceneHovered && map.InBounds(hover) && Raylib.IsMouseButtonPressed(MouseButton.Right)) TileId = map.Get(hover);

        if (!_painting && _drag is null && _rectStart is null && sceneHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (MapRenderer.ObjectAt(map, Vector2.Zero, world) is { } hit)
            {
                objectPanels.SelectedId = hit.Id;
                _drag = (hit.Id, IsoMath.ScreenToTileF(world) - new Vector2(hit.X, hit.Y), service.BeginObjectEdit(hit.Id));
            }
            else if (Mode == PaintMode.Fill) service.Fill(hover, TileId);
            else if (Mode == PaintMode.Rect) _rectStart = hover;
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

        if (_rectStart is { } r)
        {
            // button state like the stroke; highlights the same clamped box FillRect fills
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                Raylib.BeginMode2D(camera);
                for (int y = Math.Max(Math.Min(r.Y, hover.Y), 0); y <= Math.Min(Math.Max(r.Y, hover.Y), map.Height - 1); y++)
                    for (int x = Math.Max(Math.Min(r.X, hover.X), 0); x <= Math.Min(Math.Max(r.X, hover.X), map.Width - 1); x++)
                        MapRenderer.DrawHighlight(new TileCoord(x, y), Vector2.Zero, Color.Yellow);
                Raylib.EndMode2D();
            }
            else
            {
                service.FillRect(r, hover, TileId);
                _rectStart = null;
            }
        }
    }
}
