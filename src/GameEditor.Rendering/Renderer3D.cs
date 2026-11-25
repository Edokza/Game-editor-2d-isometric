using System.Numerics;
using GameEditor.Domain;
using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>3D map drawing: terrain as one static mesh per 16×16 chunk (rebuilt only where tiles/heights changed), objects as
/// upright billboards facing the camera. Create after InitWindow and <see cref="Sprites.Load"/>, dispose before CloseWindow.
/// Call the Draw methods inside BeginMode3D.</summary>
public sealed unsafe class Renderer3D : IDisposable
{
    private const int Chunk = 16;
    /// <summary>Sprite pixels per world unit: same texel size as the 64 px tile tops.</summary>
    public const float PixelsPerUnit = 64;
    private const string Vs = """
        #version 330
        in vec3 vertexPosition; in vec2 vertexTexCoord; in vec4 vertexColor;
        uniform mat4 mvp; uniform mat4 matModel;
        out vec2 uv; out vec4 col; out vec3 pos;
        void main() { uv = vertexTexCoord; col = vertexColor; pos = (matModel * vec4(vertexPosition, 1.0)).xyz; gl_Position = mvp * vec4(vertexPosition, 1.0); }
        """;
    // alpha < 0.9 flags a water top: brightness 0.7–1.0 wave running diagonally (old 2D ripple)
    private const string TerrainFs = """
        #version 330
        in vec2 uv; in vec4 col; in vec3 pos;
        uniform sampler2D texture0; uniform vec4 colDiffuse; uniform float time; uniform int grid;
        out vec4 o;
        void main() {
            vec3 c = texture(texture0, uv).rgb * col.rgb * colDiffuse.rgb;
            if (col.a < 0.9) c *= 1.0 - 0.15 * (1.0 + sin(time * 3.0 + (pos.x + pos.z) * 1.5));
            // f = distance to the nearest cell edge, w = one pixel in world units: ~1px lines at any zoom; top faces only
            // (walls lie on cell edges and would be all line)
            vec2 f = abs(fract(pos.xz + 0.5) - 0.5), w = fwidth(pos.xz);
            if (grid == 1 && abs(normalize(cross(dFdx(pos), dFdy(pos))).y) > 0.5 && (f.x < w.x || f.y < w.y)) c = mix(c, vec3(1.0), 0.35);
            o = vec4(c, 1.0);
        }
        """;
    // sprites are 0/255 alpha only: discard instead of blending, so the depth buffer sorts them and clear corners never hide what's behind
    private const string BillboardFs = """
        #version 330
        in vec2 uv; in vec4 col;
        uniform sampler2D texture0;
        out vec4 o;
        void main() { vec4 c = texture(texture0, uv) * col; if (c.a < 0.5) discard; o = c; }
        """;

    private readonly Shader _terrainShader = Raylib.LoadShaderFromMemory(Vs, TerrainFs);
    private readonly Shader _billboard = Raylib.LoadShaderFromMemory(Vs, BillboardFs);
    private readonly int _timeLoc, _gridLoc;
    private Material _material;
    private int _w, _h, _cw;
    private int[] _tiles = [], _heights = []; // copy of what the meshes show
    private Mesh?[] _meshes = [];

    /// <summary>Chunks rebuilt since creation; for checking that an edit only touches its own chunks.</summary>
    public int Rebuilds { get; private set; }

    public Renderer3D()
    {
        _timeLoc = Raylib.GetShaderLocation(_terrainShader, "time");
        _gridLoc = Raylib.GetShaderLocation(_terrainShader, "grid");
        _material = Raylib.LoadMaterialDefault();
        _material.Shader = _terrainShader;
        _material.Maps[(int)MaterialMapIndex.Albedo].Texture = Sprites.TopAtlas;
    }

    /// <summary>World Y of a cell's top face. Tall tile sprites (blocks) stand one step above the cell's height: looks only.</summary>
    public static float TopY(TileMap map, TileCoord c) =>
        (map.GetHeight(c) + (Sprites.Tile(map.Get(c)) is { Id: not 0 } t && t.Height > 64 ? 1 : 0)) * World3D.Step;

    /// <summary>Opaque terrain; <paramref name="grid"/> = white cell outlines.</summary>
    public void DrawTerrain(TileMap map, bool grid)
    {
        Sync(map);
        Raylib.SetShaderValue(_terrainShader, _timeLoc, (float)Raylib.GetTime(), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(_terrainShader, _gridLoc, grid ? 1 : 0, ShaderUniformDataType.Int);
        foreach (var m in _meshes)
            if (m is { } mesh) Raylib.DrawMesh(mesh, _material, Matrix4x4.Identity);
    }

    private void Sync(TileMap map)
    {
        bool resized = map.Width != _w || map.Height != _h;
        if (!resized && map.Tiles.SequenceEqual(_tiles) && map.Heights.SequenceEqual(_heights)) return;
        if (resized)
        {
            foreach (var m in _meshes) if (m is { } mesh) Raylib.UnloadMesh(mesh);
            (_w, _h, _cw) = (map.Width, map.Height, (map.Width + Chunk - 1) / Chunk);
            _meshes = new Mesh?[_cw * ((map.Height + Chunk - 1) / Chunk)];
        }
        // walls look at the 4 neighbours: a changed cell on a chunk border dirties the chunk next door too
        var dirty = new bool[_meshes.Length];
        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
        {
            int i = y * _w + x;
            if (!resized && map.Tiles[i] == _tiles[i] && map.Heights[i] == _heights[i]) continue;
            for (int k = -1; k <= 1; k++)
            {
                if ((uint)(x + k) < (uint)_w) dirty[y / Chunk * _cw + (x + k) / Chunk] = true;
                if ((uint)(y + k) < (uint)_h) dirty[(y + k) / Chunk * _cw + x / Chunk] = true;
            }
        }
        if (resized) (_tiles, _heights) = (new int[map.Tiles.Length], new int[map.Heights.Length]);
        map.Tiles.CopyTo(_tiles); // copy, not ToArray: a stroke changes the map every frame and 256² arrays would land on the LOH
        map.Heights.CopyTo(_heights);
        for (int i = 0; i < _meshes.Length; i++)
        {
            if (!dirty[i]) continue;
            if (_meshes[i] is { } old) Raylib.UnloadMesh(old);
            _meshes[i] = Build(map, i % _cw * Chunk, i / _cw * Chunk);
            Rebuilds++;
        }
    }

    // side faces: direction to the neighbour, then the face's two edge corners as cell offsets (seen from outside: A right, B left)
    private static readonly (int Dx, int Dy, Vector2 A, Vector2 B, float Light)[] Sides =
    [
        (0, 1, new(1, 1), new(0, 1), 0.75f),  // +y (world +Z)
        (1, 0, new(1, 0), new(1, 1), 0.55f),  // +x
        (0, -1, new(0, 0), new(1, 0), 0.65f), // -y
        (-1, 0, new(0, 1), new(0, 0), 0.85f), // -x
    ];

    private static Mesh Build(TileMap map, int x0, int y0)
    {
        var pos = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color>();
        var white = Sprites.TileTopUv(-1);
        var whiteUv = new Vector2((white.X + white.Z) / 2, (white.Y + white.W) / 2);

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 r, bool textured, Color color)
        {
            // a b c d counter-clockwise seen from outside (raylib culls back faces)
            pos.AddRange([a, b, c, d]);
            if (textured) uv.AddRange([new(r.X, r.Y), new(r.X, r.W), new(r.Z, r.W), new(r.Z, r.Y)]);
            else uv.AddRange([whiteUv, whiteUv, whiteUv, whiteUv]);
            col.AddRange([color, color, color, color]);
        }

        for (int y = y0; y < Math.Min(y0 + Chunk, map.Height); y++)
        for (int x = x0; x < Math.Min(x0 + Chunk, map.Width); x++)
        {
            var c = new TileCoord(x, y);
            int id = map.Get(c);
            float top = TopY(map, c);
            bool hasTop = Sprites.HasTop(id);
            var tint = hasTop ? Color.White : MapRenderer.ColorOf(id);
            if (id == 1) tint.A = 128; // water flag for the shader
            Quad(new(x, top, y), new(x, top, y + 1), new(x + 1, top, y + 1), new(x + 1, top, y), Sprites.TileTopUv(id), true, tint);
            var wall = MapRenderer.ColorOf((uint)id < (uint)MapRenderer.Palette.Length ? id : 4);
            foreach (var (dx, dy, a, b, light) in Sides)
            {
                var n = new TileCoord(x + dx, y + dy);
                // past the map edge: one step below ground, the map reads as a floating island
                float bottom = map.InBounds(n) ? TopY(map, n) : -World3D.Step;
                if (bottom >= top) continue;
                var shade = Raylib.ColorBrightness(wall, light - 1);
                Quad(new(x + b.X, top, y + b.Y), new(x + b.X, bottom, y + b.Y), new(x + a.X, bottom, y + a.Y), new(x + a.X, top, y + a.Y),
                    default, false, shade);
            }
        }

        var mesh = new Mesh(pos.Count, pos.Count / 2);
        mesh.AllocVertices();
        mesh.AllocTexCoords();
        mesh.AllocColors();
        mesh.AllocIndices();
        var v = mesh.VerticesAs<Vector3>();
        var t = mesh.TexCoordsAs<Vector2>();
        var cl = mesh.ColorsAs<Color>();
        var ix = mesh.IndicesAs<ushort>();
        for (int i = 0; i < pos.Count; i++) (v[i], t[i], cl[i]) = (pos[i], uv[i], col[i]);
        for (int q = 0; q < pos.Count / 4; q++)
        {
            int b = q * 4, k = q * 6;
            (ix[k], ix[k + 1], ix[k + 2], ix[k + 3], ix[k + 4], ix[k + 5]) =
                ((ushort)b, (ushort)(b + 1), (ushort)(b + 2), (ushort)b, (ushort)(b + 2), (ushort)(b + 3));
        }
        Raylib.UploadMesh(ref mesh, false);
        return mesh;
    }

    /// <summary>Camera-facing corners of an object's sprite (bottom left, bottom right, top right, top left), standing upright
    /// with its bottom center on the object position raised <paramref name="z"/> steps (as seen on screen).</summary>
    public static (Vector3 Bl, Vector3 Br, Vector3 Tr, Vector3 Tl) Quad(MapObject o, Camera3D cam, float z)
    {
        var tex = Sprites.Object(o.Sprite);
        float w = (tex.Id != 0 ? tex.Width : 48) / PixelsPerUnit, h = (tex.Id != 0 ? tex.Height : 80) / PixelsPerUnit;
        var f = cam.Target - cam.Position;
        var right = Vector3.Normalize(new Vector3(-f.Z, 0, f.X)) * (w / 2); // horizontal: sprites stay upright when the camera tilts
        var foot = World3D.At(o.X, o.Y, z);
        var up = new Vector3(0, h, 0);
        Vector3 bl = foot - right, br = foot + right, tr = br + up, tl = bl + up;
        // pull the card w/2 toward the eye, so its side edges don't sink into a higher column right behind the object and get cut
        // by that wall. Perspective: scale toward the eye (same pixels on screen); ortho: shift along the view direction
        float pull = w / 2;
        if (cam.Projection == CameraProjection.Orthographic)
        {
            var back = Vector3.Normalize(f) * pull;
            return (bl - back, br - back, tr - back, tl - back);
        }
        float k = 1 - pull / Vector3.Distance(cam.Position, foot);
        Vector3 P(Vector3 v) => cam.Position + (v - cam.Position) * k;
        return (P(bl), P(br), P(tr), P(tl));
    }

    private static float GroundZ(TileMap map, MapObject o) =>
        TopY(map, new((int)MathF.Floor(o.X), (int)MathF.Floor(o.Y))) / World3D.Step;

    /// <summary>Shadows, then sprites, after <see cref="DrawTerrain"/>. <paramref name="lift"/> overrides one object's drawn height
    /// (the hero easing up a step), not its shadow.</summary>
    public void DrawObjects(TileMap map, Camera3D cam, int? selectedId, (int Id, float Z)? lift = null)
    {
        // shadows: soft = faint outer + darker inner disc, just above the ground, no depth write so overlapping ones don't cut each other
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableDepthMask();
        // for, not foreach: map.Objects is an IReadOnlyList, its enumerator would be boxed every frame
        for (int i = 0; i < map.Objects.Count; i++)
        {
            var o = map.Objects[i];
            if (o.Sprite == "spawn" || o.Sprite is { } s && map.ShadowlessSprites.Contains(s)) continue;
            var tex = Sprites.Object(o.Sprite);
            float w = (tex.Id != 0 ? tex.Width : 48) / PixelsPerUnit;
            var at = World3D.At(o.X, o.Y, GroundZ(map, o)) + new Vector3(0, 0.01f, 0);
            Disc(at, w * 0.4f, new Color(0, 0, 0, 40));
            Disc(at, w * 0.28f, new Color(0, 0, 0, 50));
        }
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();

        Raylib.BeginShaderMode(_billboard);
        for (int i = 0; i < map.Objects.Count; i++)
        {
            var o = map.Objects[i];
            float z = lift is { } l && l.Id == o.Id ? l.Z : GroundZ(map, o);
            var (bl, br, tr, tl) = Quad(o, cam, z);
            var tex = Sprites.Object(o.Sprite);
            Rlgl.SetTexture(tex.Id != 0 ? tex.Id : Rlgl.GetTextureIdDefault());
            Rlgl.Begin(DrawMode.Quads);
            var c = tex.Id != 0 ? Color.White : Raylib.ColorFromHSV(o.Id * 67 % 360, 0.6f, 0.9f);
            Rlgl.Color4ub(c.R, c.G, c.B, c.A);
            Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(bl.X, bl.Y, bl.Z);
            Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(br.X, br.Y, br.Z);
            Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(tr.X, tr.Y, tr.Z);
            Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(tl.X, tl.Y, tl.Z);
            Rlgl.End();
            Rlgl.SetTexture(0);
        }
        Raylib.EndShaderMode();

        if (selectedId is { } sel && map.IndexOf(sel) is >= 0 and var si)
        {
            var so = map.Objects[si];
            var (bl, br, tr, tl) = Quad(so, cam, GroundZ(map, so));
            Raylib.DrawLine3D(bl, br, Color.Yellow);
            Raylib.DrawLine3D(br, tr, Color.Yellow);
            Raylib.DrawLine3D(tr, tl, Color.Yellow);
            Raylib.DrawLine3D(tl, bl, Color.Yellow);
        }
    }

    /// <summary>Front-most object whose sprite quad the ray hits before <paramref name="terrainT"/> (where it hits the ground),
    /// so a click on a hill paints instead of grabbing the tree hidden behind it.</summary>
    public static MapObject? ObjectAt(TileMap map, Camera3D cam, Ray ray, float? terrainT)
    {
        MapObject? best = null;
        float bestT = terrainT + 0.01f ?? float.MaxValue; // objects standing on the hit tile touch the ground there
        foreach (var o in map.Objects)
        {
            var (bl, br, tr, tl) = Quad(o, cam, GroundZ(map, o));
            if (Raylib.GetRayCollisionQuad(ray, bl, br, tr, tl) is var h && h.Hit && h.Distance < bestT) (best, bestT) = (o, h.Distance);
        }
        return best;
    }

    private static void Disc(Vector3 c, float r, Color color)
    {
        const int n = 20;
        Rlgl.Begin(DrawMode.Triangles);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
        for (int i = 0; i < n; i++)
        {
            float a0 = i * MathF.Tau / n, a1 = (i + 1) * MathF.Tau / n;
            Rlgl.Vertex3f(c.X, c.Y, c.Z);
            Rlgl.Vertex3f(c.X + MathF.Cos(a1) * r, c.Y, c.Z + MathF.Sin(a1) * r);
            Rlgl.Vertex3f(c.X + MathF.Cos(a0) * r, c.Y, c.Z + MathF.Sin(a0) * r);
        }
        Rlgl.End();
    }

    /// <summary>Outline of data cell <paramref name="c"/> on its top face.</summary>
    public static void DrawHighlight(TileMap map, TileCoord c, Color color)
    {
        float y = TopY(map, c) + 0.01f;
        Vector3 a = new(c.X, y, c.Y), b = new(c.X + 1, y, c.Y), d = new(c.X + 1, y, c.Y + 1), e = new(c.X, y, c.Y + 1);
        Raylib.DrawLine3D(a, b, color);
        Raylib.DrawLine3D(b, d, color);
        Raylib.DrawLine3D(d, e, color);
        Raylib.DrawLine3D(e, a, color);
    }

    public void Dispose()
    {
        foreach (var m in _meshes) if (m is { } mesh) Raylib.UnloadMesh(mesh);
        _meshes = [];
        Raylib.MemFree(_material.Maps); // not UnloadMaterial: it would unload the atlas, which belongs to Sprites
        Raylib.UnloadShader(_terrainShader);
        Raylib.UnloadShader(_billboard);
    }
}
