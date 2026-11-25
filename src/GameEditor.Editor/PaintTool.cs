using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using Raylib_cs;

namespace GameEditor.Editor;

public enum PaintMode { Brush, Fill, Rect, Height }

/// <summary>Left-drag: on an object moves it, elsewhere paints tiles (Fill: one click floods the region; Rect: press = first corner, release fills the box;
/// Height: each tile dragged over goes up one step, Shift = down). Right-click picks the tile under the mouse. Called by ScenePanel inside its texture mode
/// with the mouse ray. Works in data cells throughout: only picking and highlights go through the camera.</summary>
public sealed class PaintTool(MapEditingService service, ObjectPanels objectPanels)
{
    private bool _painting;
    private TileMap? _strokeGround; // heights as the stroke began: picking against them keeps a raised/lowered tile from handing the cursor to its neighbour
    private TileCoord? _rectStart;
    // Grab = mouse minus object position, tile space, on the plane at world height Z (the object's ground when grabbed): a fixed plane,
    // because a ray over uneven ground can hit both the wall of a tile in front and a tile behind
    private (int Id, Vector2 Grab, float Z, int Token)? _drag;
    private TileMap _ground = null!; // read by _top: one delegate for the tool's life instead of a capturing lambda per frame
    private Func<TileCoord, float>? _top;

    public int TileId { get; set; } = 1;
    public PaintMode Mode { get; set; }

    /// <summary>In-bounds tile under the mouse in Scene, else null. Set by Update; ScenePanel clears it while hidden.</summary>
    public TileCoord? Hover { get; set; }

    public void Update(Camera3D camera, Ray ray, bool sceneHovered)
    {
        var map = service.Map;
        _ground = _strokeGround ?? map; // during a stroke: the tile the stroke will change
        _top ??= c => Renderer3D.TopY(_ground, c);
        var hit = World3D.Pick(ray.Position, ray.Direction, _ground, _top);
        // off the map: the ground-plane cell, like the old flat pick, so a rect dragged past the edge still clamps to it
        var hover = hit?.Cell ?? (World3D.PlaneHit(ray.Position, ray.Direction, 0) is { } g0 && float.IsFinite(g0.X) && MathF.Abs(g0.X) < 1e6f && MathF.Abs(g0.Y) < 1e6f
            ? new TileCoord((int)MathF.Floor(g0.X), (int)MathF.Floor(g0.Y)) : new TileCoord(int.MinValue, int.MinValue));
        Hover = sceneHovered && hit is not null ? hover : null;

        if (sceneHovered && hit is not null)
        {
            Raylib.BeginMode3D(camera);
            Renderer3D.DrawHighlight(map, hover, Color.Yellow);
            Raylib.EndMode3D();
        }

        // eyedropper: only swaps TileId, so an open stroke/drag keeps going and Mode stays
        if (sceneHovered && map.InBounds(hover) && Raylib.IsMouseButtonPressed(MouseButton.Right)) TileId = map.Get(hover);

        if (!_painting && _drag is null && _rectStart is null && sceneHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Renderer3D.ObjectAt(map, camera, ray, hit?.T) is { } obj)
            {
                objectPanels.SelectedId = obj.Id;
                float z = Renderer3D.TopY(map, new TileCoord((int)MathF.Floor(obj.X), (int)MathF.Floor(obj.Y)));
                var grab = World3D.PlaneHit(ray.Position, ray.Direction, z) is { } g ? g - new Vector2(obj.X, obj.Y) : Vector2.Zero;
                _drag = (obj.Id, grab, z, service.BeginObjectEdit(obj.Id));
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
                // dropped onto another height, it's drawn at that tile's height
                if (World3D.PlaneHit(ray.Position, ray.Direction, d.Z) is { } p)
                    service.UpdateObject(map.Objects[i] with { X = p.X - d.Grab.X, Y = p.Y - d.Grab.Y });
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
                Raylib.BeginMode3D(camera);
                for (int y = Math.Max(Math.Min(r.Y, hover.Y), 0); y <= Math.Min(Math.Max(r.Y, hover.Y), map.Height - 1); y++)
                    for (int x = Math.Max(Math.Min(r.X, hover.X), 0); x <= Math.Min(Math.Max(r.X, hover.X), map.Width - 1); x++)
                        Renderer3D.DrawHighlight(map, new TileCoord(x, y), Color.Yellow);
                Raylib.EndMode3D();
            }
            else
            {
                service.FillRect(r, hover, TileId);
                _rectStart = null;
            }
        }
    }
}
