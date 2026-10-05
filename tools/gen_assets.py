"""Placeholder pixel-art sprites -> assets/. Run: python tools/gen_assets.py  (needs Pillow)
Anchor for every sprite = bottom center (tile: its bottom vertex, object: its X,Y)."""
import math, os
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets")

def rnd(x, y, s):
    m = 0xFFFFFFFF
    n = (x * 374761393 + y * 668265263 + s * 1442695041) & m
    n = ((n ^ (n >> 13)) * 1274126177) & m
    return (n ^ (n >> 16)) / 4294967296

def hx(h): return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))
def sh(c, k): return tuple(max(0, min(255, round(v * k))) for v in hx(c)) if isinstance(c, str) else tuple(max(0, min(255, round(v * k))) for v in c)

def spr(w, h):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = im.load()
    def p(x, y, c):
        if 0 <= x < w and 0 <= y < h: px[x, y] = (hx(c) if isinstance(c, str) else c) + (255,)
    return im, p

def pick(cols, x, y, s):
    r = rnd(x, y, s)
    return cols[1] if r < .12 else cols[2] if r < .2 else cols[0]

def top(p, w, oy, cols, s):
    h = w // 2
    for y in range(h):
        hw = (y + 1) * 2 if y < h // 2 else (h - y) * 2
        for x in range(w // 2 - hw, w // 2 + hw):
            p(x, y + oy, pick(cols, x, y, s))

def tile(cols, s):
    im, p = spr(64, 32); top(p, 64, 0, cols, s); return im

def block(w, side, cols, sc, s, lines=False):
    h = w // 2
    im, p = spr(w, h + side)
    for x in range(w):
        e = h - math.ceil(abs(x - (w / 2 - .5)) / 2)
        for y in range(e, e + side):
            k = .82 if x < w / 2 else .62
            if lines and (y - e) % 8 == 0: k *= .75
            p(x, y, sh(sc, k))
    top(p, w, 0, cols, s)
    return im

T = {
    "grass": ["#4c994c", "#3e863e", "#62b062"],
    "water": ["#4078c8", "#3567b0", "#78a8e8"],
    "sand":  ["#dcc882", "#ccb56c", "#eadaa0"],
    "stone": ["#84848e", "#70707a", "#9c9ca6"],
    "dirt":  ["#78553a", "#664630", "#8c6848"],
    "snow":  ["#ebf0f5", "#d4dce6", "#ffffff"],
}
tiles = {f"{i}_{k}": tile(c, i + 1) for i, (k, c) in enumerate(T.items())}
tiles["6_dirt_block"] = block(64, 16, T["grass"], "#78553a", 9)
tiles["7_stone_block"] = block(64, 16, T["stone"], "#84848e", 10)

def tree():
    im, p = spr(32, 64)
    for y in range(40, 64):
        for x in range(14, 18): p(x, y, "#7a5434" if x < 16 else "#5e3f26")
    for y in range(46):
        for x in range(32):
            if (x - 16) ** 2 / 225 + (y - 24) ** 2 / 400 < 1:
                r = rnd(x, y, 3)
                p(x, y, ("#6cc06c" if r < .3 else "#4ea24e") if x + y < 36 else ("#3a7e3a" if r < .3 else "#2f6b2f"))
    return im

def rock():
    im, p = spr(32, 24)
    for y in range(24):
        for x in range(32):
            if (x - 16) ** 2 / 196 + (y - 14) ** 2 / 90 < 1:
                p(x, y, "#a4a4ae" if y < 11 else "#6a6a74" if rnd(x, y, 5) < .2 else "#86868f")
    return im

def chr_(face, pack):
    im, p = spr(32, 48)
    for y in range(39, 48):
        for x in (11, 12, 13, 14, 17, 18, 19, 20): p(x, y, "#3a3a52")
    for y in range(19, 39):
        for x in range(10, 22): p(x, y, "#4a7ad0" if x < 16 else "#3a64b0")
    if pack is not None:
        for y in range(21, 33):
            for x in range(pack, pack + 5): p(x, y, "#8a5a30")
    for y in range(4, 20):
        for x in range(8, 24):
            if (x - 15.5) ** 2 + (y - 12) ** 2 < 56:
                p(x, y, "#5a3a22" if face is None or y < 8 else "#f0c8a0")
    if face is not None: p(face, 12, "#222222"); p(face + 3, 12, "#222222")
    return im

objects = {"tree": tree(), "rock": rock(),
           "crate": block(32, 24, ["#b08850", "#9a7442", "#c49c60"], "#a07a44", 11, True)}
se, ne = chr_(16, None), chr_(None, 22)
chars = {"hero_se": se, "hero_ne": ne}

for folder, d in (("tiles", tiles), ("objects", objects), ("characters", chars)):
    os.makedirs(f"{OUT}/{folder}", exist_ok=True)
    for n, im in d.items(): im.save(f"{OUT}/{folder}/{n}.png")
print("ok", os.path.normpath(OUT))
