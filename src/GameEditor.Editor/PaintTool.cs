using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using Raylib_cs;

namespace GameEditor.Editor;

public enum PaintMode { Brush, Fill, Rect, Height }

/// <summary>Left-drag: on an object moves it, elsewhere paints tiles (Fill: one click floods the region; Rect: press = first corner, release fills the box;
/// Height: each tile dragged over goes up one step, Shift = down). Right-click picks the tile under the mouse. Called by ScenePanel inside its texture mode; mouse is panel-local.
/// Works in data cells throughout: only picking and highlights go through the view.</summary>
public sealed class PaintTool(MapEditingService service, ObjectPanels objectPanels)
{
    private bool _painting;
    private TileMap? _strokeGround; // heights as the stroke began: picking against them keeps a raised/lowered tile from handing the cursor to its neighbour
    private TileCoord? _rectStart;
    // Grab = mouse minus object position, tile space, on the plane at height Z (the object's tile height when grabbed): a fixed plane,
    // because a screen point over uneven ground can be both a tile behind and the wall of a tile in front
    private (int Id, Vector2 Grab, float Z, int Token)? _drag;

    public int TileId { get; set; } = 1;
    public PaintMode Mode { get; set; }

    /// <summary>In-bounds tile under the mouse in Scene, else null. Set by Update; ScenePanel clears it while hidden.</summary>
    public TileCoord? Hover { get; set; }

    public void Update(Camera2D camera, Vector2 mouse, bool sceneHovered, IsoView view)
    {
        var world = Raylib.GetScreenToWorld2D(mouse, camera);
        var map = service.Map;
        var hover = view.ScreenToTile(world, _strokeGround ?? map); // during a stroke: the tile the stroke will change
        Hover = sceneHovered && map.InBounds(hover) ? hover : null;

        if (sceneHovered && service.Map.InBounds(hover))
        {
            Raylib.BeginMode2D(camera);
            MapRenderer.DrawHighlight(hover, Vector2.Zero, Color.Yellow, view, map.GetHeight(hover));
            Raylib.EndMode2D();
        }

        // eyedropper: only swaps TileId, so an open stroke/drag keeps going and Mode stays
        if (sceneHovered && map.InBounds(hover) && Raylib.IsMouseButtonPressed(MouseButton.Right)) TileId = map.Get(hover);

        if (!_painting && _drag is null && _rectStart is null && sceneHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (MapRenderer.ObjectAt(map, Vector2.Zero, world, view) is { } hit)
            {
                objectPanels.SelectedId = hit.Id;
                float z = map.GetHeight(new TileCoord((int)MathF.Floor(hit.X), (int)MathF.Floor(hit.Y)));
                _drag = (hit.Id, view.ScreenToTileF(world - IsoMath.Lift(z)) - new Vector2(hit.X, hit.Y), z, service.BeginObjectEdit(hit.Id));
            }
            else if (Mode == PaintMode.Fill) service.Fill(hover, TileId);
            else if (Mode == PaintMode.Rect) _rectStart = hover;
            else
            {
                service.BeginStroke();
                _strokeGround = new TileMap(map.Width, map.Height, map.Tiles.ToArray(), heights: map.Heights.ToArray());
                _painting = true;
            }
        }

        if (_drag is { } d)
        {
            int i = map.IndexOf(d.Id);
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && i >= 0)
            {
                var p = view.ScreenToTileF(world - IsoMath.Lift(d.Z)) - d.Grab; // dropped onto another height, it's drawn at that tile's height
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
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                if (Mode != PaintMode.Height) service.Paint(hover, TileId);
                else service.Raise(hover, Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift) ? -1 : 1);
            }
            else
            {
                service.EndStroke();
                _painting = false;
                _strokeGround = null;
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
                        MapRenderer.DrawHighlight(new TileCoord(x, y), Vector2.Zero, Color.Yellow, view, map.GetHeight(new TileCoord(x, y)));
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
