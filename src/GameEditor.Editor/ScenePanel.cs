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
    private Vector2 _pan; // world units, relative to map center
    private Camera2D _camera = new() { Zoom = 1f };

    /// <summary>Non-null = play mode: shows the session's map, editing tools off.</summary>
    public PlaySession? Play { get; set; }

    public void Draw()
    {
        ImGui.SetNextWindowSize(new(800, 600), ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        // NoNavInputs: a focused Scene must not set WantCaptureKeyboard (Ctrl+Z/Y)
        bool visible = ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoNavInputs);
        ImGui.PopStyleVar();
        if (!visible)
        {
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

        if (hovered && Raylib.IsMouseButtonDown(MouseButton.Middle))
            _pan += Raylib.GetMouseDelta() / _camera.Zoom;

        var map = Play?.Map ?? service.Map;
        // panel center looks at the map's iso bounding box center (tile (0,0) top vertex = world origin), minus pan
        _camera.Offset = size / 2f;
        _camera.Target = new Vector2(
            (map.Width - map.Height) * IsoMath.TileWidth / 4f,
            (map.Width + map.Height) * IsoMath.TileHeight / 4f) - _pan;

        // zoom toward mouse: keep the world point under the cursor fixed
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
            var shift = before - Raylib.GetScreenToWorld2D(mouse, _camera);
            _pan -= shift;
            _camera.Target += shift;
        }

        Raylib.BeginTextureMode(_rt);
        Raylib.ClearBackground(new Color(30, 30, 36, 255));
        Raylib.BeginMode2D(_camera);
        MapRenderer.Draw(map, Vector2.Zero);
        MapRenderer.DrawObjects(map, Vector2.Zero, Play is null ? objectPanels.SelectedId : null);
        Raylib.EndMode2D();
        if (Play is null) paintTool.Update(_camera, mouse, hovered);
        Raylib.EndTextureMode();

        ImGui.End();
    }

    public void Dispose()
    {
        if (_rt.Id != 0) Raylib.UnloadRenderTexture(_rt);
    }
}
