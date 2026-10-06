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
    // maps shipped next to the exe win; otherwise play what the editor saved
    var bundled = Path.Combine(AppContext.BaseDirectory, "maps");
    var repo = new JsonMapRepository(Directory.Exists(bundled) && Directory.EnumerateFiles(bundled, "*.json").Any()
        ? bundled
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameEditor", "maps"));
    name ??= repo.List().FirstOrDefault();
    if (name is null) error = "No maps found.";
    else play = new PlaySession(repo.Load(name));
}
catch (Exception e)
{
    error = $"Load '{name}' failed: {e.Message}";
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 720, name ?? "Game");
Sprites.Load(Path.Combine(AppContext.BaseDirectory, "assets"));
var camera = new Camera2D { Zoom = 2f };

while (!Raylib.WindowShouldClose())
{
    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(30, 30, 36, 255));
    if (play is null) Raylib.DrawText(error, 20, 20, 20, Color.RayWhite);
    else
    {
        play.Move(GameInput.Walk(), Raylib.GetFrameTime());
        camera.Offset = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()) / 2f;
        camera.Target = IsoMath.TileToScreen(play.Hero.X, play.Hero.Y);
        Raylib.BeginMode2D(camera);
        MapRenderer.Draw(play.Map, Vector2.Zero);
        MapRenderer.DrawObjects(play.Map, Vector2.Zero, null);
        Raylib.EndMode2D();
    }
    Raylib.EndDrawing();
}

Sprites.Unload();
Raylib.CloseWindow();
