"""16-bit style pixel-art sprites -> assets/. Run: python tools/gen_assets.py  (needs Pillow)
Anchor for every sprite = bottom center (tile: its bottom vertex, object: its X,Y). Light comes from the top-left."""
import math, os
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets")
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))

def rnd(x, y, s):
    m = 0xFFFFFFFF
    n = (x * 374761393 + y * 668265263 + s * 1442695041) & m
    n = ((n ^ (n >> 13)) * 1274126177) & m
    return (n ^ (n >> 16)) / 4294967296

def hx(h): return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))

def spr(w, h):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    def p(x, y, c):
        if 0 <= x < w and 0 <= y < h: px[x, y] = hx(c) + (255,)
    return im, p

def outline(im, c):
    """Transparent pixels touching opaque ones -> c (a dark shade of the material, not black)."""
    a, px, (w, h) = im.getchannel("A").load(), im.load(), im.size
    for x, y in [(x, y) for y in range(h) for x in range(w) if a[x, y] == 0 and
                 any(0 <= x + dx < w and 0 <= y + dy < h and a[x + dx, y + dy] for dx, dy in N4)]:
        px[x, y] = hx(c) + (255,)
    return im

def shade(t, c):
    """t ~ -1 (lit, top-left) .. 1 (shadow) -> one of the 5 shades in c (light -> dark)."""
    return c[0 if t < -.6 else 1 if t < -.2 else 2 if t < .25 else 3 if t < .65 else 4]

# 5 shades per material, light -> dark; A = accent colors the tile patterns use
P = {
    "grass": ["#a6e070", "#74c450", "#55a43e", "#3f8034", "#2c5c28"],
    "water": ["#d4efff", "#7cb8f0", "#4c88d4", "#3668b4", "#244a8a"],
    "sand":  ["#fbf0c8", "#efdca4", "#dfc688", "#c8aa6a", "#a08250"],
    "stone": ["#c8c8d0", "#a6a6b0", "#888892", "#6a6a76", "#4c4c58"],
    "dirt":  ["#b48e66", "#98734e", "#7c583c", "#60422c", "#442c1c"],
    "snow":  ["#ffffff", "#f0f5fa", "#dde6f0", "#c4d0de", "#a2b2c6"],
    "wood":  ["#e0b878", "#c89c5c", "#ac8048", "#8a6236", "#634426"],
    "bark":  ["#a87850", "#8a5e3a", "#6c462a", "#50321e", "#362012"],
    "leaf":  ["#9ada6a", "#6cbc4c", "#4c9a3c", "#367a30", "#245424"],
    "skin":  ["#ffe4c8", "#f6cca4", "#e2aa7c", "#bc845c", "#8c5c40"],
    "hair":  ["#b88048", "#946232", "#744824", "#56321a", "#3a2010"],
    "shirt": ["#a4c8ff", "#6ea0f0", "#4a7ad0", "#3a5eaa", "#2a4280"],
    "pants": ["#7a7aa4", "#5c5c86", "#44446a", "#323252", "#22223a"],
}
A = {
    "grass": ["#ffe14a", "#ffffff", "#ff8ab0", "#b48cff"],  # flower center, petals
    "water": ["#ffffff", "#a8d4f8", "#3a72c0"],
    "sand":  ["#fff8e0", "#b89a6a", "#8a7048"],
    "stone": ["#e0e0e8", "#6e8c5a", "#56704a"],
    "dirt":  ["#c8c0b4", "#9a9288", "#6c665e"],
    "snow":  ["#d6ecff", "#a9c4e0", "#8aa4c4"],
}

def diamond(w):
    h, m = w // 2, set()
    for y in range(h):
        hw = (y + 1) * 2 if y < h // 2 else (h - y) * 2
        m.update((x, y) for x in range(w // 2 - hw, w // 2 + hw))
    return m

# tile patterns: q(x, y, color) writes only inside the face, never on its 1px rim
def pat_grass(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .02:  # tuft: dark base, lit blades
            for dx, dy, i in ((-1, 0, 4), (0, 0, 4), (1, 0, 4), (-2, -1, 1), (0, -1, 2), (2, -1, 1), (-1, -2, 1), (1, -2, 0), (0, -3, 0)):
                q(x + dx, y + dy, c[i])
        elif r < .026:  # flower
            for dx, dy in N4: q(x + dx, y + dy, a[1 + int(rnd(x, y, s) * 3)])
            q(x, y, a[0])

def pat_water(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .01:  # wave crest: arc, dark trough under it
            for k in range(7):
                dy = 0 if 1 <= k <= 5 else 1
                q(x + k, y + dy, c[0] if 2 <= k <= 4 else a[1] if k in (1, 5) else c[1])
                if 2 <= k <= 4: q(x + k, y + 1, a[2])
        elif r < .014: q(x, y, a[0])

def pat_sand(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .05: q(x, y, c[0] if r < .025 else c[3])
        elif r < .056: q(x, y, a[0])
        elif r < .06: q(x, y, a[1]); q(x + 1, y, a[1]); q(x, y + 1, a[2]); q(x + 1, y + 1, a[2])

def pat_stone(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .004:  # crack: random walk, lit lip below-right
            for k in range(8 + int(rnd(x, y, s) * 8)):
                q(x, y, c[4]); q(x + 1, y + 1, a[0])
                x += 1 if rnd(x, y, k) < .6 else -1
                y += 1 if rnd(x, y, k + 9) < .4 else 0
        elif r < .012: q(x, y, a[1] if r < .009 else a[2])

def pat_dirt(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .012: q(x, y, a[0]); q(x + 1, y, a[1]); q(x, y + 1, a[1]); q(x + 1, y + 1, a[2])
        elif r < .05: q(x, y, c[4])

def pat_snow(q, pts, s, c, a):
    for x, y in pts:
        r = rnd(x, y, s + 50)
        if r < .006:
            for dx, dy in N4: q(x + dx, y + dy, a[0])
            q(x, y, c[0])
        elif r < .012: q(x, y, a[1] if r < .01 else a[2])

def pat_wood(q, pts, s, c, a):  # planks along the top-right edge
    for x, y in pts:
        k = int(y + x / 2) % 11
        if k < 2: q(x, y, c[4] if k == 0 else c[0])

PAT = {"grass": pat_grass, "water": pat_water, "sand": pat_sand, "stone": pat_stone, "dirt": pat_dirt, "snow": pat_snow, "wood": pat_wood}

def top(p, w, mat, s):
    """Diamond face: dithered base, then the material pattern (kept off the 1px rim so stamps never get cut at the tile edge).
    No lit/dark rim: neighbouring tiles of one kind would show a grid seam."""
    m, c = diamond(w), P[mat]
    inner = []
    for x, y in sorted(m):
        r = rnd(x, y, s)
        i = 0 if r < .03 else 1 if r < .18 else 4 if r > .97 else 3 if r > .86 else 2
        if all((x + dx, y + dy) in m for dx, dy in N4): inner.append((x, y))
        p(x, y, c[i])
    ok = set(inner)
    PAT[mat](lambda x, y, col: (x, y) in ok and p(x, y, col), inner, s, c, A.get(mat))

def tile(mat, s):
    im, p = spr(128, 64); top(p, 128, mat, s); return im

def block(w, side, top_mat, wall, s, deco=None):
    """Top face + two walls shaded top -> bottom (left lit, right in shadow). deco(x, d, i, left) may override a wall pixel."""
    h, c = w // 2, P[wall]
    im, p = spr(w, h + side)
    for x in range(w):
        e = h - math.ceil(abs(x - (w / 2 - .5)) / 2)
        left = x < w / 2
        for d in range(side):
            t = d / side
            i = (1 if t < .4 else 2 if t < .75 else 3) + (0 if left else 1)
            if rnd(x, d, s) < .08: i = min(4, i + 1)
            p(x, e + d, (deco and deco(x, d, i, left)) or c[i])
    top(p, w, top_mat, s)
    return im

def dirt_wall(x, d, i, left):  # grass lip dripping over the edge, pebbles
    if d < 2 + 3 * (rnd(x, 0, 21) < .3): return P["grass"][3 if left else 4]
    if rnd(x, d, 22) < .015: return A["dirt"][1]

def brick_wall(x, d, i, left):  # rows follow the wall slope; mortar dark, brick top lit
    row = d // 8
    if d % 8 == 0 or (x + row % 2 * 8) % 16 == 0: return P["stone"][4]
    if d % 8 == 1: return P["stone"][i - 1]

def crate_wall(w):
    def f(x, d, i, left):
        c = P["wood"]
        if x in (0, w // 2 - 1, w // 2, w - 1): return c[1] if x == w // 2 - 1 else c[4]  # frame, lit corner
        if x in (1, w - 2): return c[3]
        if d % 12 == 0: return c[4]
        if d % 12 == 1: return c[0]
        if d % 12 in (5, 6) and x in (3, w // 2 - 4, w // 2 + 3, w - 4): return "#d8d8e0" if d % 12 == 5 else "#5a5a64"  # nail
    return f

mats = ["grass", "water", "sand", "stone", "dirt", "snow"]
tiles = {f"{i}_{k}": tile(k, i + 1) for i, k in enumerate(mats)}
tiles["6_dirt_block"] = block(128, 32, "grass", "dirt", 9, dirt_wall)
tiles["7_stone_block"] = block(128, 32, "stone", "stone", 10, brick_wall)

def ball(p, cx, cy, r, c, s, rim=True):
    """Lit-from-top-left shaded disc with leafy dither; dark rim bottom-right separates stacked clumps."""
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            dist = math.hypot(x - cx, y - cy)
            if dist < r:
                t = ((x - cx) + (y - cy)) / (r * 1.2) + (rnd(x, y, s) - .5) * .5
                p(x, y, c[4] if rim and dist > r - 1.5 and t > 0 else shade(t, c))

def tree():
    trunk, pt = spr(64, 128)
    for y in range(78, 126):
        flare = max(0, y - 120)  # roots
        for x in range(28 - flare, 36 + flare):
            t = (x - 31.5) / 5 + (.6 if y < 86 else 0)  # shade under the canopy
            pt(x, y, P["bark"][4] if rnd(x // 2, y // 5, 31) < .15 else shade(t, P["bark"]))
    outline(trunk, P["bark"][4])
    leaves, pl = spr(64, 128)
    for i, (cx, cy, r) in enumerate(((32, 22, 15), (20, 44, 16), (44, 44, 16), (32, 64, 20))):  # back to front
        ball(pl, cx, cy, r, P["leaf"], 40 + i)
    outline(leaves, "#1c401c")
    return Image.alpha_composite(trunk, leaves)

def rock():
    im, p = spr(64, 48)
    c, a = P["stone"], A["stone"]
    for y in range(48):
        for x in range(64):
            if (x - 32) ** 2 / 784 + (y - 31) ** 2 / 225 < 1 or (x - 26) ** 2 / 196 + (y - 22) ** 2 / 100 < 1:
                p(x, y, shade((x - 32) / 34 + (y - 26) / 16 + (rnd(x, y, 5) - .5) * .3, c))
    for x, y in ((18, 16), (19, 16), (20, 15), (17, 17), (18, 17), (21, 15)): p(x, y, a[0])  # highlight
    for x, y in ((38, 24), (39, 25), (39, 26), (40, 27), (41, 28), (40, 29), (24, 32), (25, 33), (26, 33), (27, 34)):
        p(x, y, c[4]); p(x + 1, y, c[1])  # cracks with lit lip
    for x, y in ((44, 40), (45, 40), (46, 41), (14, 38), (15, 39)): p(x, y, a[1])  # moss
    return outline(im, "#34343e")

def chr_(face, pack):
    """64x96, facing right (se) with a face, or back view (ne) with a backpack at x=pack."""
    im, p = spr(64, 96)
    pc, sc, kc, hc = P["pants"], P["shirt"], P["skin"], P["hair"]
    for x0 in (23, 34):  # legs + shoes
        for y in range(70, 93):
            for x in range(x0, x0 + 8):
                if y < 87: p(x, y, pc[1 if x - x0 < 2 else 4 if x - x0 > 5 else 2])
                else: p(x, y, "#5a3a24" if y < 90 and x - x0 < 4 else "#3a2416")
    for y in range(40, 72):  # torso, belt
        for x in range(20, 44):
            if 66 <= y <= 68: p(x, y, "#6a4428" if x != 34 else "#e8c048")
            else: p(x, y, sc[1 if x < 26 else 4 if y > 69 or x > 39 else 3 if x > 33 else 2])
    for x0, k in ((16, 1), (44, 3)):  # arms, hands
        for y in range(42, 66):
            for x in range(x0, x0 + 4):
                p(x, y, kc[k] if y > 61 else sc[k + (x - x0 > 1)])
    if pack is not None:
        for y in range(42, 66):
            for x in range(pack, pack + 10):
                p(x, y, "#3a2416" if x in (pack, pack + 9) or y in (42, 65) else "#a06a3a" if x < pack + 4 else "#7a4c26")
    for y in range(8, 40):  # head: skin face, hair cap + bangs (back view: all hair)
        for x in range(16, 48):
            if (x - 31.5) ** 2 + (y - 24) ** 2 < 225:
                t = ((x - 31.5) + (y - 24)) / 28
                hair = face is None or y < 17 + 2 * (x % 4 == 0) or (x < 24 and y < 30)
                p(x, y, shade(t - .3, hc) if hair else shade(t - .5, kc))
    for x, y in ((24, 12), (25, 12), (26, 11), (27, 11)): p(x, y, hc[0])  # hair shine
    if face is not None:
        for ex in (face, face + 6):
            for x, y in ((ex, 22), (ex + 1, 22), (ex, 23), (ex + 1, 23), (ex, 24), (ex + 1, 24)): p(x, y, "#222233")
            p(ex, 22, "#ffffff")
        p(face + 1, 27, "#f0907c"); p(face + 7, 27, "#f0907c")  # cheeks
    return outline(im, "#2a2238")

def spawn():  # editor-only marker: red flag on a pole, ring at the anchor; PlaySession hides it. Original 32x48 design, drawn x2
    im, p = spr(32, 48)
    for y in range(41, 48):
        for x in range(32):
            if 0.6 < (x - 15.5) ** 2 / 196 + (y - 44.5) ** 2 / 12 < 1: p(x, y, "#ffe040")
    for y in range(4, 45): p(15, y, "#e8e8e8"); p(16, y, "#a0a0a0")
    for y in range(4, 18):
        for x in range(17, 17 + (18 - y if y > 10 else y + 4)): p(x, y, "#e03030" if y < 11 else "#b02020")
    return im.resize((64, 96), Image.Resampling.NEAREST)

objects = {"tree": tree(), "rock": rock(), "spawn": spawn(), "crate": block(64, 48, "wood", "wood", 11, crate_wall(64))}
se, ne = chr_(34, None), chr_(None, 44)
flip = lambda im: im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
chars = {"hero_se": se, "hero_ne": ne, "hero_sw": flip(se), "hero_nw": flip(ne)}

for n, im in tiles.items():
    assert im.width == 128 and len(im.getcolors(1 << 16)) >= 8, n  # 128x64 grid, 16-bit look: >= 8 colors
for folder, d in (("tiles", tiles), ("objects", objects), ("characters", chars)):
    os.makedirs(f"{OUT}/{folder}", exist_ok=True)
    for n, im in d.items(): im.save(f"{OUT}/{folder}/{n}.png")
print("ok", os.path.normpath(OUT))
