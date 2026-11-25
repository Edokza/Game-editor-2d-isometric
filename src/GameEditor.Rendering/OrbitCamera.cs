using System.Numerics;
using GameEditor.Domain;
using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>Camera looking down at <see cref="Target"/> from <see cref="Yaw"/> (same meaning as <see cref="World3D.DirFromScreen"/>);
/// Q/E set <see cref="YawGoal"/> and <see cref="Update"/> turns toward it smoothly.</summary>
public sealed class OrbitCamera
{
    private const float Pitch = 45 * MathF.PI / 180;
    /// <summary>Perspective field of view: narrow = little distortion, reads like the old iso view.</summary>
    private const float Fov = 30;

    public Vector3 Target;
    public float Yaw, YawGoal;
    /// <summary>World units from target to eye; the zoom.</summary>
    public float Distance = 24;
    public bool Ortho;

    /// <summary>+1 = turn the map 90° clockwise on screen.</summary>
    public void Rotate(int steps) => YawGoal += steps * 90;

    /// <summary>Settled 90° step 0–3 the camera is turning to.</summary>
    public int Steps => ((int)MathF.Round(YawGoal / 90) % 4 + 4) % 4;

    public void Update(float dt)
    {
        Yaw += (YawGoal - Yaw) * (1 - MathF.Exp(-14 * MathF.Max(dt, 0))); // ~0.25 s
        if (MathF.Abs(YawGoal - Yaw) < 0.01f) Yaw = YawGoal;
    }

    public Camera3D Camera
    {
        get
        {
            var v = World3D.ToViewer(Yaw);
            var eye = new Vector3(v.X * MathF.Cos(Pitch), MathF.Sin(Pitch), v.Y * MathF.Cos(Pitch)) * Distance;
            // ortho: fovy = visible height in world units, matched to what perspective shows at the target
            float fovy = Ortho ? 2 * Distance * MathF.Tan(Fov / 2 * MathF.PI / 180) : Fov;
            return new Camera3D(Target + eye, Target, Vector3.UnitY, fovy, Ortho ? CameraProjection.Orthographic : CameraProjection.Perspective);
        }
    }

    /// <summary>Near/far follow the zoom: raylib's fixed 0.01 near plane leaves too little depth precision far out, and the shadows
    /// and highlights 0.01 above the ground start to z-fight. Global raylib state: call before BeginMode3D and mouse rays.</summary>
    public void ApplyClipPlanes() => Rlgl.SetClipPlanes(Distance * 0.05, Distance + 400); // 400 > a 256² map's diagonal

    /// <summary>Wheel zoom: one notch = 10%, clamped. Min 8: the eye (8 · sin 45° ≈ 5.7 above the target) stays over the highest
    /// possible top, (MaxHeight + 1 block step) · Step = 4.5, so it never sinks into a column and picks the cell it's inside.</summary>
    public void Zoom(float wheel) => Distance = Math.Clamp(Distance * MathF.Pow(0.9f, wheel), 8, 200);
}
