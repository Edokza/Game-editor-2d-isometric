using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Infrastructure;
using GameEditor.Rendering;
using Raylib_cs;

PlaySession? play = null;
string error = "";
string? name = args.Length > 0 ? args[0] : null;
try // folder listing can throw too (unreadable Documents): must end in the in-window error, not a crash
{
    // maps next to the exe (Export) win, then maps bundled in the single-file exe (extracted to BaseDirectory,
    // a temp folder, not the exe's), otherwise play what the editor saved
    var repo = new JsonMapRepository(new[] { Path.GetDirectoryName(Environment.ProcessPath), AppContext.BaseDirectory }
        .OfType<string>()
        .Select(d => Path.Combine(d, "maps"))
        .FirstOrDefault(d => Directory.Exists(d) && Directory.EnumerateFiles(d, "*.json").Any())
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameEditor", "maps"));
    name ??= repo.List().FirstOrDefault();
    if (name is null) error = "No maps found.";
    else play = new PlaySession(repo.Load(name));
}
catch (Exception e)
{
    error = $"Load '{name}' failed: {e.Message}";
}

RaylibLog.Install();
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 720, name ?? "Game");
Sprites.Load(Path.Combine(AppContext.BaseDirectory, "assets"));
Raylib.SetExitKey(KeyboardKey.Null); // Esc quits only when pressed twice; window X still closes at once
var camera = new Camera2D { Zoom = 1f };
double quitUntil = 0;
int rotation = 0; // view only: every run starts unrotated

while (!Raylib.WindowShouldClose())
{
    if (Raylib.IsKeyPressed(KeyboardKey.F11)) Raylib.ToggleBorderlessWindowed();
    if (Raylib.IsKeyPressed(KeyboardKey.Escape))
    {
        if (Raylib.GetTime() < quitUntil) break;
        quitUntil = Raylib.GetTime() + 2;
    }
    Raylib.BeginDrawing();
    MapRenderer.DrawBackground(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    if (play is null) Raylib.DrawText(error, 20, 20, 20, Color.RayWhite);
    else
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) rotation = (rotation + 3) % 4;
        if (Raylib.IsKeyPressed(KeyboardKey.E)) rotation = (rotation + 1) % 4;
        var view = new IsoView(rotation, play.Map.Width, play.Map.Height);
        play.Move(GameInput.Walk(), Raylib.GetFrameTime(), view);
        camera.Offset = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()) / 2f;
        camera.Target = view.TileToScreen(play.Hero.X, play.Hero.Y);
        Raylib.BeginMode2D(camera);
        MapRenderer.Draw(play.Map, Vector2.Zero, view);
        MapRenderer.DrawObjects(play.Map, Vector2.Zero, null, view);
        Raylib.EndMode2D();
    }
    if (Raylib.GetTime() < quitUntil) Raylib.DrawText("Press Esc again to quit", 20, Raylib.GetScreenHeight() - 40, 20, Color.RayWhite);
    Raylib.EndDrawing();
}

Sprites.Unload();
Raylib.CloseWindow();
