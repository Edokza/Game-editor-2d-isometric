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
var renderer = new Renderer3D();
var cam = new OrbitCamera { Distance = 14 };
double quitUntil = 0;
bool snap = true; // first frame: start on the hero instead of sliding in from the origin

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
        float dt = Raylib.GetFrameTime();
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) cam.Rotate(-1);
        if (Raylib.IsKeyPressed(KeyboardKey.E)) cam.Rotate(1);
        if (Raylib.GetMouseWheelMove() is var wheel and not 0 && float.IsFinite(wheel)) cam.Zoom(wheel);
        cam.Update(dt);
        play.Move(GameInput.Walk(), dt, cam.Yaw);
        var heroAt = World3D.At(play.Hero.X, play.Hero.Y, play.HeroZ);
        cam.Target = snap ? heroAt : Vector3.Lerp(cam.Target, heroAt, PlaySession.Ease(8, dt));
        snap = false;
        cam.ApplyClipPlanes();
        var camera = cam.Camera;
        Raylib.BeginMode3D(camera);
        renderer.DrawTerrain(play.Map, false);
        renderer.DrawObjects(play.Map, camera, null, (play.Hero.Id, play.HeroZ));
        Raylib.EndMode3D();
    }
    if (Raylib.GetTime() < quitUntil) Raylib.DrawText("Press Esc again to quit", 20, Raylib.GetScreenHeight() - 40, 20, Color.RayWhite);
    Raylib.EndDrawing();
}

renderer.Dispose();
Sprites.Unload();
Raylib.CloseWindow();
