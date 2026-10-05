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
var fileMenu = new FileMenu(service);
var objectPanels = new ObjectPanels(service);
var paintTool = new PaintTool(service, objectPanels);
var scenePanel = new ScenePanel(service, paintTool, objectPanels);
var quit = false;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 720, "Game Editor");
Raylib.SetExitKey(KeyboardKey.Null); // Esc must not quit the editor
Sprites.Load(Path.Combine(AppContext.BaseDirectory, "assets"));
rlImGui.Setup(true, true);
ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true; // left-drag in Scene paints, must not move the window

while (!quit)
{
    if (Raylib.WindowShouldClose()) fileMenu.ConfirmDiscard(() => quit = true);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(30, 30, 36, 255));

    rlImGui.Begin();
    // here, not in ScenePanel: undo/redo must work while Scene is hidden or collapsed
    if (!ImGui.GetIO().WantCaptureKeyboard && (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl)))
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Z)) service.Undo();
        if (Raylib.IsKeyPressed(KeyboardKey.Y)) service.Redo();
    }
    ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

    if (ImGui.BeginMainMenuBar())
    {
        fileMenu.DrawMenu();
        ImGui.EndMainMenuBar();
    }
    fileMenu.DrawPopups();

    scenePanel.Draw();
    AssetsPanel.Draw(paintTool, service, objectPanels);
    objectPanels.DrawHierarchy();
    objectPanels.DrawInspector();

    rlImGui.End();
    Raylib.EndDrawing();
}

scenePanel.Dispose(); // GPU resources: free before CloseWindow
Sprites.Unload();
rlImGui.Shutdown();
Raylib.CloseWindow();

