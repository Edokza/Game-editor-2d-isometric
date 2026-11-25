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
    private RenderTexture2D _rt;
    private Renderer3D? _renderer; // created on first draw: needs the window and Sprites loaded
    private readonly OrbitCamera _cam = new();
    private Vector2 _pan; // world x/z, relative to map center: New/Open/Resize keep the view centered
    private float? _distanceBeforePlay; // Stop restores it, play-mode zoom doesn't stick
    private bool _following; // play: false = snap onto the hero this frame instead of sliding across the map

    /// <summary>Non-null = play mode: shows the session's map, editing tools off.</summary>
    public PlaySession? Play { get; set; }

    /// <summary>Outline every tile over the map; editor only.</summary>
    public bool Grid { get; set; }

    /// <summary>Orthographic instead of perspective camera.</summary>
    public bool Ortho { get => _cam.Ortho; set => _cam.Ortho = value; }

    /// <summary>Current camera turn in degrees (eases toward 90° steps); view only, also kept in play mode.</summary>
    public float Yaw => _cam.Yaw;

    /// <summary>Settled view angle, 0–3 × 90°.</summary>
    public int Rotation => _cam.Steps;

    /// <summary>+1 turns the map clockwise around the look point.</summary>
    public void Rotate(int steps) => _cam.Rotate(steps);

    public void Draw()
    {
        if (Play is not null) _distanceBeforePlay ??= _cam.Distance;
        else if (_distanceBeforePlay is { } d0) (_cam.Distance, _distanceBeforePlay) = (d0, null);

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
        _renderer ??= new Renderer3D();

        var origin = ImGui.GetCursorScreenPos();
        rlImGui.ImageRenderTexture(_rt); // texture is sampled at rlImGui.End, so drawing into it below still shows this frame
        bool hovered = ImGui.IsItemHovered();
        var mouse = Raylib.GetMousePosition() - origin;
        var map = Play?.Map ?? service.Map;
        float dt = Raylib.GetFrameTime();
        _cam.Update(dt);

        // play: follow the hero like the Player does; _pan stays untouched so Stop returns to it
        if (Play is null)
        {
            _following = false;
            _cam.Target = new(map.Width / 2f + _pan.X, 0, map.Height / 2f + _pan.Y);
            if (hovered && Raylib.IsMouseButtonDown(MouseButton.Middle) && Ground(mouse - Raylib.GetMouseDelta(), size) is { } a && Ground(mouse, size) is { } b)
                _pan -= b - a; // the ground point under the cursor stays under it
        }
        else
        {
            var heroAt = World3D.At(Play.Hero.X, Play.Hero.Y, Play.HeroZ);
            _cam.Target = _following ? Vector3.Lerp(_cam.Target, heroAt, PlaySession.Ease(8, dt)) : heroAt;
            _following = true;
        }

        // zoom toward the mouse: keep the ground point under the cursor fixed (play: zoom around the hero at center)
        float wheel = Raylib.GetMouseWheelMove();
        if (hovered && wheel != 0 && float.IsFinite(wheel))
        {
            var before = Ground(mouse, size);
            _cam.Zoom(wheel);
            if (Play is null && before is { } p0 && Ground(mouse, size) is { } p1)
            {
                _pan += p0 - p1;
                _cam.Target += new Vector3(p0.X - p1.X, 0, p0.Y - p1.Y);
            }
        }

        _cam.ApplyClipPlanes();
        var camera = _cam.Camera;
        Raylib.BeginTextureMode(_rt);
        MapRenderer.DrawBackground(_rt.Texture.Width, _rt.Texture.Height);
        Raylib.BeginMode3D(camera);
        _renderer.DrawTerrain(map, Grid && Play is null);
        _renderer.DrawObjects(map, camera, Play is null ? objectPanels.SelectedId : null, Play is null ? null : (Play.Hero.Id, Play.HeroZ));
        Raylib.EndMode3D();
        if (Play is null) paintTool.Update(camera, Raylib.GetScreenToWorldRayEx(mouse, camera, (int)size.X, (int)size.Y), hovered);
        Raylib.EndTextureMode();

        ImGui.End();
    }

    /// <summary>Ground plane (y = 0) point under a panel-local screen point, as data x/y.</summary>
    private Vector2? Ground(Vector2 p, Vector2 size)
    {
        var ray = Raylib.GetScreenToWorldRayEx(p, _cam.Camera, (int)size.X, (int)size.Y);
        return World3D.PlaneHit(ray.Position, ray.Direction, 0);
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        if (_rt.Id != 0) Raylib.UnloadRenderTexture(_rt);
    }
}
