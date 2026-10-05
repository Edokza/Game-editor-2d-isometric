using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Editor;
using GameEditor.Infrastructure;
using GameEditor.Rendering;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

const int MapSize = 20;
var service = new MapEditingService(new TileMap(MapSize, MapSize), new JsonMapRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameEditor", "maps")));
var paintTool = new PaintTool(service);
var fileMenu = new FileMenu(service);
var pan = Vector2.Zero; // world units, relative to map center
var camera = new Camera2D { Zoom = 1f };
var quit = false;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 720, "Game Editor");
Raylib.SetExitKey(KeyboardKey.Null); // Esc must not quit the editor
rlImGui.Setup(true, true);

while (!quit)
{
    if (Raylib.WindowShouldClose()) fileMenu.ConfirmDiscard(() => quit = true);

    // WantCaptureMouse here is from last frame; fine for panning
    if (Raylib.IsMouseButtonDown(MouseButton.Middle) && !ImGui.GetIO().WantCaptureMouse)
        pan += Raylib.GetMouseDelta() / camera.Zoom;

    var map = service.Map;
    // screen center looks at the map's iso bounding box center (tile (0,0) top vertex = world origin), minus pan
    camera.Offset = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()) / 2f;
    camera.Target = new Vector2(
        (map.Width - map.Height) * IsoMath.TileWidth / 4f,
        (map.Width + map.Height) * IsoMath.TileHeight / 4f) - pan;

    // zoom toward mouse: keep the world point under the cursor fixed
    float wheel = Raylib.GetMouseWheelMove();
    if (wheel != 0 && float.IsFinite(wheel) && !ImGui.GetIO().WantCaptureMouse)
    {
        var mouse = Raylib.GetMousePosition();
        var before = Raylib.GetScreenToWorld2D(mouse, camera);
        camera.Zoom = Math.Clamp(camera.Zoom * MathF.Pow(1.1f, wheel), 0.25f, 4f);
        var shift = before - Raylib.GetScreenToWorld2D(mouse, camera);
        pan -= shift;
        camera.Target += shift;
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(30, 30, 36, 255));
    Raylib.BeginMode2D(camera);
    MapRenderer.Draw(map, Vector2.Zero);
    Raylib.EndMode2D();

    rlImGui.Begin();
    paintTool.Update(camera);
    ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

    if (ImGui.BeginMainMenuBar())
    {
        fileMenu.DrawMenu();
        ImGui.EndMainMenuBar();
    }
    fileMenu.DrawPopups();

    PalettePanel.Draw(paintTool);

    rlImGui.End();
    Raylib.EndDrawing();
}

rlImGui.Shutdown();
Raylib.CloseWindow();

