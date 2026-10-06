using System.Numerics;
using Raylib_cs;

namespace GameEditor.Rendering;

public static class GameInput
{
    /// <summary>Tile-space direction from WASD/arrows; W = up the screen = toward smaller x and y.</summary>
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
