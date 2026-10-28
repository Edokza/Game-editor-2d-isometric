using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace GameEditor.Editor;

/// <summary>Renders the map into a RenderTexture shown in the "Scene" panel. Call between rlImGui.Begin/End.</summary>
public sealed class ScenePanel(MapEditingService service, PaintTool paintTool, ObjectPanels objectPanels) : IDisposable
{
    private static readonly Color GridColor = new(255, 255, 255, 90);
    private RenderTexture2D _rt;
    private Vector2 _pan; // world units, relative to map center
    private Camera2D _camera = new() { Zoom = 1f };
    private float? _zoomBeforePlay; // Stop restores it, play-mode zoom doesn't stick

    /// <summary>Non-null = play mode: shows the session's map, editing tools off.</summary>
    public PlaySession? Play { get; set; }

    /// <summary>Outline every tile over the map; editor only.</summary>
    public bool Grid { get; set; }

    /// <summary>View rotation 0–3 (90° steps, clockwise); view only, also kept in play mode.</summary>
    public int Rotation { get; private set; }

    /// <summary>+1 turns the map clockwise around its own center: <c>_pan</c> is relative to the map center, so leaving it alone keeps the center in place on screen.</summary>
    public void Rotate(int steps) => Rotation = ((Rotation + steps) % 4 + 4) % 4;

    public void Draw()
    {
        if (Play is not null) _zoomBeforePlay ??= _camera.Zoom;
        else if (_zoomBeforePlay is { } z0) (_camera.Zoom, _zoomBeforePlay) = (z0, null);

        ImGui.SetNextWindowSize(new(800, 600), ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        // NoNavInputs: a focused Scene must not set WantCaptureKeyboard (Ctrl+Z/Y)
        bool visible = ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoNavInputs);
        ImGui.PopStyleVar();
        if (!visible)
        {
            paintTool.Hover = null; // Update won't run to clear it
            ImGui.End();
            return;
        }

        var size = Vector2.Max(ImGui.GetContentRegionAvail(), Vector2.One);
        if (_rt.Texture.Width != (int)size.X || _rt.Texture.Height != (int)size.Y)
        {
            if (_rt.Id != 0) Raylib.UnloadRenderTexture(_rt);
            _rt = Raylib.LoadRenderTexture((int)size.X, (int)size.Y);
        }

        var origin = ImGui.GetCursorScreenPos();
        rlImGui.ImageRenderTexture(_rt); // texture is sampled at rlImGui.End, so drawing into it below still shows this frame
        bool hovered = ImGui.IsItemHovered();
        var mouse = Raylib.GetMousePosition() - origin;

        if (Play is null && hovered && Raylib.IsMouseButtonDown(MouseButton.Middle))
            _pan += Raylib.GetMouseDelta() / _camera.Zoom;

        var map = Play?.Map ?? service.Map;
        var view = new IsoView(Rotation, map.Width, map.Height); // per frame: New/Open/Resize change the size
        _camera.Offset = size / 2f;
        // play: follow the hero like the Player does; _pan stays untouched so Stop returns to it
        _camera.Target = Play is not null ? view.TileToScreen(Play.Hero.X, Play.Hero.Y) : MapCenter(view) - _pan;

        // zoom toward mouse: keep the world point under the cursor fixed (play: zoom around the hero at center)
        float wheel = Raylib.GetMouseWheelMove();
        if (hovered && wheel != 0 && float.IsFinite(wheel))
        {
            var before = Raylib.GetScreenToWorld2D(mouse, _camera);
            // ≥1: whole steps so every sprite pixel is the same size on screen; <1: smooth overview
            // ponytail: one step per wheel event, touchpads with tiny fractional deltas zoom fast; accumulate if that bothers
            float z = _camera.Zoom;
            if (z > 1 || (z == 1 && wheel > 0)) z = MathF.Round(z) + MathF.Sign(wheel);
            else
            {
                z *= MathF.Pow(1.1f, wheel);
                if (z > 0.999f) z = 1; // float drift: 9 steps down then up gives 0.9999998, must still land on exactly 1
            }
            _camera.Zoom = Math.Clamp(z, 0.25f, 4f);
            if (Play is null)
            {
                var shift = before - Raylib.GetScreenToWorld2D(mouse, _camera);
                _pan -= shift;
                _camera.Target += shift;
            }
        }

        Raylib.BeginTextureMode(_rt);
        MapRenderer.DrawBackground(_rt.Texture.Width, _rt.Texture.Height);
        Raylib.BeginMode2D(_camera);
        MapRenderer.Draw(map, Vector2.Zero, view);
        MapRenderer.DrawObjects(map, Vector2.Zero, Play is null ? objectPanels.SelectedId : null, view);
        // ponytail: 4 lines per tile every frame, 256x256 = 262k lines; cull to the view if big maps lag
        if (Grid && Play is null)
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                MapRenderer.DrawHighlight(new TileCoord(x, y), Vector2.Zero, GridColor, view);
        Raylib.EndMode2D();
        if (Play is null) paintTool.Update(_camera, mouse, hovered, view);
        Raylib.EndTextureMode();

        ImGui.End();
    }

    /// <summary>Center of the map's iso bounding box as drawn (view tile (0,0) top vertex = world origin).</summary>
    private static Vector2 MapCenter(IsoView view) => new(
        (view.ViewWidth - view.ViewHeight) * IsoMath.TileWidth / 4f,
        (view.ViewWidth + view.ViewHeight) * IsoMath.TileHeight / 4f);

    public void Dispose()
    {
        if (_rt.Id != 0) Raylib.UnloadRenderTexture(_rt);
    }
}
