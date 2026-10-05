using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace GameEditor.Editor;

/// <summary>Renders the map into a RenderTexture shown in the "Scene" panel. Call between rlImGui.Begin/End.</summary>
public sealed class ScenePanel(MapEditingService service, PaintTool paintTool) : IDisposable
{
    private RenderTexture2D _rt;
    private Vector2 _pan; // world units, relative to map center
    private Camera2D _camera = new() { Zoom = 1f };

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

        var map = service.Map;
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
            _camera.Zoom = Math.Clamp(_camera.Zoom * MathF.Pow(1.1f, wheel), 0.25f, 4f);
            var shift = before - Raylib.GetScreenToWorld2D(mouse, _camera);
            _pan -= shift;
            _camera.Target += shift;
        }

        Raylib.BeginTextureMode(_rt);
        Raylib.ClearBackground(new Color(30, 30, 36, 255));
        Raylib.BeginMode2D(_camera);
        MapRenderer.Draw(map, Vector2.Zero);
        Raylib.EndMode2D();
        paintTool.Update(_camera, mouse, hovered);
        Raylib.EndTextureMode();

        ImGui.End();
    }

    public void Dispose()
    {
        if (_rt.Id != 0) Raylib.UnloadRenderTexture(_rt);
    }
}
