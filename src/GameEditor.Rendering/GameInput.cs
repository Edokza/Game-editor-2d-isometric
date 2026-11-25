using System.Numerics;
using Raylib_cs;

namespace GameEditor.Rendering;

public static class GameInput
{
    /// <summary>Screen-relative direction from WASD/arrows; W = up the screen = (−1, −1) at yaw 0.
    /// Pass PlaySession.Move the camera yaw, so walking stays screen-relative however the view is turned.</summary>
    public static Vector2 Walk()
    {
        static bool Down(KeyboardKey a, KeyboardKey b) => Raylib.IsKeyDown(a) || Raylib.IsKeyDown(b);
        Vector2 d = default;
        if (Down(KeyboardKey.W, KeyboardKey.Up)) d += new Vector2(-1, -1);
        if (Down(KeyboardKey.S, KeyboardKey.Down)) d += new Vector2(1, 1);
        if (Down(KeyboardKey.A, KeyboardKey.Left)) d += new Vector2(-1, 1);
        if (Down(KeyboardKey.D, KeyboardKey.Right)) d += new Vector2(1, -1);
        return d;
    }
}
