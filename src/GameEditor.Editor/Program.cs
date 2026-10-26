using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Editor;
using GameEditor.Infrastructure;
using GameEditor.Rendering;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

const int MapSize = 20;
const uint DockId = 0x6D61696E; // fixed, not 0: DockLayout must know the id before DockSpaceOverViewport makes it
var docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameEditor");
var service = new MapEditingService(new TileMap(MapSize, MapSize), new JsonMapRepository(Path.Combine(docs, "maps")));
var fileMenu = new FileMenu(service, docs);
var objectPanels = new ObjectPanels(service);
var paintTool = new PaintTool(service, objectPanels);
var scenePanel = new ScenePanel(service, paintTool, objectPanels);
var quit = false;
var layoutChecked = false;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
var version = typeof(Program).Assembly.GetName().Version!.ToString(3);
(string? Name, bool Dirty)? titled = null; // what the title shows: rebuilt only on change, not per frame
(TileCoord Tile, int Id)? hovered = null; // same for the menu bar's hover text
var hoverText = "";
Raylib.InitWindow(1280, 720, $"Game Editor v{version}");
Raylib.SetExitKey(KeyboardKey.Null); // Esc must not quit the editor
Sprites.Load(Path.Combine(AppContext.BaseDirectory, "assets"));
rlImGui.Setup(true, true);
ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true; // left-drag in Scene paints, must not move the window

while (!quit)
{
    if (Raylib.WindowShouldClose()) fileMenu.ConfirmDiscard(() => quit = true);
    if (titled != (service.CurrentName, service.IsDirty))
    {
        titled = (service.CurrentName, service.IsDirty);
        Raylib.SetWindowTitle($"{service.CurrentName ?? "(unsaved)"}{(service.IsDirty ? "*" : "")} - Game Editor v{version}");
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(30, 30, 36, 255));

    rlImGui.Begin();
    var play = scenePanel.Play;
    // here, not in ScenePanel: undo/redo must work while Scene is hidden or collapsed
    if (!ImGui.GetIO().WantCaptureKeyboard)
    {
        if (play is not null) play.Move(GameInput.Walk(), Raylib.GetFrameTime());
        else if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl))
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Z)) service.Undo();
            if (Raylib.IsKeyPressed(KeyboardKey.Y)) service.Redo();
            if (Raylib.IsKeyPressed(KeyboardKey.N)) fileMenu.New();
            if (Raylib.IsKeyPressed(KeyboardKey.O)) fileMenu.Open();
            if (Raylib.IsKeyPressed(KeyboardKey.S))
            {
                if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift)) fileMenu.SaveAs();
                else fileMenu.Save();
            }
            // spawn skipped: AddObject would move the one spawn, not copy it
            if (Raylib.IsKeyPressed(KeyboardKey.D) && objectPanels.SelectedId is { } sel
                && service.Map.Objects.FirstOrDefault(o => o.Id == sel) is { Sprite: not PlaySession.SpawnSprite } dup)
                objectPanels.SelectedId = service.AddObject(dup.Name, dup.X + 0.5f, dup.Y + 0.5f, dup.Sprite).Id;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.B)) paintTool.Mode = PaintMode.Brush;
        else if (Raylib.IsKeyPressed(KeyboardKey.G)) paintTool.Mode = PaintMode.Fill;
        else if (Raylib.IsKeyPressed(KeyboardKey.R)) paintTool.Mode = PaintMode.Rect;
        else if (Raylib.IsKeyPressed(KeyboardKey.Delete) && objectPanels.SelectedId is { } id) service.RemoveObject(id);
    }
    // first frame: build only when there's no saved layout, so the user's own arrangement is kept
    if (fileMenu.ResetLayout || (!layoutChecked && DockLayout.IsEmpty(DockId)))
        DockLayout.Build(DockId, ImGui.GetMainViewport().WorkSize);
    fileMenu.ResetLayout = false;
    layoutChecked = true;
    ImGui.DockSpaceOverViewport(DockId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

    if (ImGui.BeginMainMenuBar())
    {
        fileMenu.DrawMenu(scenePanel, play is not null);
        if (ImGui.MenuItem(play is null ? "Play" : "Stop")) scenePanel.Play = play is null ? new PlaySession(service.Map) : null;
        // Hover is last frame's: InBounds guards a resize/load since then; play: Update doesn't run, so it's stale
        if (play is null && paintTool.Hover is { } h && service.Map.InBounds(h))
        {
            int id = service.Map.Get(h);
            if (hovered != (h, id))
            {
                hovered = (h, id);
                hoverText = $"({h.X}, {h.Y}) id {id}";
            }
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - ImGui.CalcTextSize(hoverText).X - ImGui.GetStyle().ItemSpacing.X);
            ImGui.TextDisabled(hoverText);
        }
        ImGui.EndMainMenuBar();
    }
    fileMenu.DrawPopups(); // not disabled: closing the window during play must still be able to confirm

    scenePanel.Draw();
    ImGui.BeginDisabled(play is not null);
    AssetsPanel.Draw(paintTool, service, objectPanels);
    objectPanels.DrawHierarchy();
    objectPanels.DrawInspector();
    ImGui.EndDisabled();

    rlImGui.End();
    Raylib.EndDrawing();
}

scenePanel.Dispose(); // GPU resources: free before CloseWindow
Sprites.Unload();
rlImGui.Shutdown();
Raylib.CloseWindow();
