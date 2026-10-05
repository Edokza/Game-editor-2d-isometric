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
var pan = Vector2.Zero;
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
        pan += Raylib.GetMouseDelta();

    var map = service.Map;
    // center the map's iso bounding box on screen, then apply pan
    var origin = pan + new Vector2(
        Raylib.GetScreenWidth() / 2f - (map.Width - map.Height) * IsoMath.TileWidth / 4f,
        (Raylib.GetScreenHeight() - (map.Width + map.Height) * IsoMath.TileHeight / 2f) / 2f);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(30, 30, 36, 255));
    MapRenderer.Draw(map, origin);

    rlImGui.Begin();
    paintTool.Update(origin);
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

