"""Default map: 35x35 with mountain, forest, swamp, lake/beach/dunes, river and a camp in the middle.
Run: python tools/gen_map.py  -> src/GameEditor.Player/maps/Map_default_1.json (same output every run; tweak and re-run).
Data coords: x grows down-right, y down-left on screen at rotation 0, so small x+y is the top of the screen."""
import json, math, os
from collections import deque

W = H = 35
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "GameEditor.Player", "maps", "Map_default_1.json")
GRASS, WATER, SAND, STONE, DIRT, SNOW, DIRT_BLOCK, STONE_BLOCK = range(8)
SOLID_TILES = {WATER, DIRT_BLOCK, STONE_BLOCK}  # TileMap.DefaultSolid
MAX_H = 8

def rnd(x, y, s):  # same hash as gen_assets.py
    m = 0xFFFFFFFF
    n = (x * 374761393 + y * 668265263 + s * 1442695041) & m
    n = ((n ^ (n >> 13)) * 1274126177) & m
    return (n ^ (n >> 16)) / 4294967296

def noise(x, y, s, scale=4.0):
    """Smooth value noise 0..1."""
    fx, fy = x / scale, y / scale
    ix, iy = math.floor(fx), math.floor(fy)
    tx, ty = fx - ix, fy - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = rnd(ix, iy, s), rnd(ix + 1, iy, s)
    c, d = rnd(ix, iy + 1, s), rnd(ix + 1, iy + 1, s)
    return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty

def dist(x, y, cx, cy): return math.hypot(x - cx, y - cy)

tile = [[GRASS] * W for _ in range(H)]   # [y][x]
h = [[0] * W for _ in range(H)]
cells = [(x, y) for y in range(H) for x in range(W)]

# --- regions ---------------------------------------------------------------
MOUNT, FOREST, SWAMP, LAKE, DUNES, CAMP = (7, 7), (28, 7), (7, 28), (27, 27), (31, 18), (17, 17)
for x, y in cells:
    n = noise(x, y, 1)
    # forest: rolling hills, grass with dirt patches
    if (d := dist(x, y, *FOREST)) < 11 + 2 * n:
        h[y][x] = max(0, min(2, round(2.6 * noise(x, y, 2, 5) - 0.3 - d / 12)))
        tile[y][x] = DIRT if noise(x, y, 3, 3) > 0.72 else GRASS
    # dunes: sand hills between forest and lake
    if dist(x, y, *DUNES) < 4.5 + 1.5 * n:
        tile[y][x] = SAND
        h[y][x] = max(0, min(2, round(2.4 * noise(x, y, 4, 3) - 0.2)))
    # swamp: shallow pools, mud, little islands
    if dist(x, y, *SWAMP) < 10 + 2 * n:
        w = noise(x, y, 5, 2.5)
        tile[y][x] = WATER if w > 0.55 else DIRT if w > 0.42 else GRASS
        h[y][x] = 0
    # lake with a sand beach around it
    if (d := dist(x, y, *LAKE) + 1.5 * (n - 0.5)) < 9:
        tile[y][x] = WATER if d < 6 else SAND
        h[y][x] = 0
    # mountain: cone with noisy slopes, stone, snow cap, grassy foot
    if (d := dist(x, y, *MOUNT)) < 12:
        mh = round(MAX_H - d * 0.8 + 2.2 * (noise(x, y, 6, 3) - 0.5))
        if mh > 0:
            h[y][x] = max(h[y][x], min(MAX_H, mh))
            tile[y][x] = SNOW if h[y][x] >= 6 else STONE if h[y][x] >= 2 else GRASS
h[MOUNT[1]][MOUNT[0]] = MAX_H
tile[MOUNT[1]][MOUNT[0]] = SNOW

# swamp island (path target) and its tree ring
SWAMP_ISLAND = (5, 29)
for x, y in cells:
    if dist(x, y, *SWAMP_ISLAND) < 1.8:
        tile[y][x], h[y][x] = GRASS, 1 if dist(x, y, *SWAMP_ISLAND) < 1 else 0

# --- river: mountain foot -> lake, around the camp ----------------------------
river = []
bends = [(12, 11), (16, 11), (20, 12), (23, 15), (24, 19), (25, 23)]
for (ax, ay), (bx, by) in zip(bends, bends[1:]):
    for i in range(13):
        c = (round(ax + (bx - ax) * i / 12), round(ay + (by - ay) * i / 12))
        if river and river[-1] == c: continue
        if river and abs(c[0] - river[-1][0]) + abs(c[1] - river[-1][1]) == 2:  # keep it 4-connected
            river.append((c[0], river[-1][1]))
        river.append(c)
for x, y in river:
    tile[y][x], h[y][x] = WATER, 0

# --- paths: 4-connected L segments through waypoints; walkable heights (<= 1 step apart) -------
def segment(a, b):
    (ax, ay), (bx, by) = a, b
    out, x, y = [], ax, ay
    while x != bx: x += 1 if bx > x else -1; out.append((x, y))
    while y != by: y += 1 if by > y else -1; out.append((x, y))
    return out

def trail(points, top=None):
    """Dirt trail; each cell follows the terrain but stays within one step of the previous one.
    top: last cell keeps its own height (the peak), the trail climbs to it."""
    path = [points[0]]
    for a, b in zip(points, points[1:]):
        path += segment(a, b)
    prev = h[path[0][1]][path[0][0]]
    for i, (x, y) in enumerate(path):
        want = h[y][x]
        if top and i >= len(path) - (MAX_H + 1):  # last stretch: climb to the peak
            want = max(want, MAX_H - (len(path) - 1 - i))
        prev = max(prev - 1, min(prev + 1, want))
        h[y][x] = prev
        if tile[y][x] != SNOW: tile[y][x] = DIRT   # river crossings become dirt bridges
    return path

paths = [
    trail([CAMP, (14, 14), (14, 9), (12, 3), (5, 3), (3, 7), MOUNT], top=True),  # switchback up the mountain
    trail([CAMP, (21, 15), (24, 11), (28, 8), (31, 4)]),                         # through the forest
    trail([CAMP, (13, 21), (9, 24), (8, 28), SWAMP_ISLAND]),                     # swamp causeway
    trail([CAMP, (20, 20), (21, 25), (21, 29)]),                                 # down to the beach
    trail([(21, 15), (27, 16), (31, 18)]),                                       # out to the dunes
]
path_cells = {c for p in paths for c in p}

# boulders: stone blocks on steep mountain spots, dirt blocks in the forest (both solid, never on a trail)
for x, y in cells:
    if (x, y) in path_cells or tile[y][x] == WATER: continue
    if tile[y][x] == STONE and rnd(x, y, 7) < 0.10: tile[y][x] = STONE_BLOCK
    elif dist(x, y, *FOREST) < 10 and rnd(x, y, 8) < 0.04: tile[y][x] = DIRT_BLOCK

# camp: stone plaza around the spawn (after the boulders, which would turn its stone into blocks)
for x, y in cells:
    if abs(x - CAMP[0]) <= 2 and abs(y - CAMP[1]) <= 2:
        tile[y][x], h[y][x] = STONE, 0

# --- objects -----------------------------------------------------------------
objects, taken = [], set()
def put(name, x, y, jitter=True):
    if (x, y) in taken: return
    taken.add((x, y))
    ox = x + (0.3 + 0.4 * rnd(x, y, 20) if jitter else 0.5)
    oy = y + (0.3 + 0.4 * rnd(x, y, 21) if jitter else 0.5)
    objects.append({"Id": len(objects) + 1, "Name": name, "X": round(ox, 2), "Y": round(oy, 2), "Sprite": name})

put("spawn", *CAMP, jitter=False)
camp_ring = [(CAMP[0] + dx, CAMP[1] + dy) for dx, dy in ((-3, -3), (-3, -2), (3, 3), (2, 3), (3, -3), (-3, 3))]
for x, y in camp_ring:
    if (x, y) not in path_cells: put("crate", x, y)
for x, y in cells:
    t, c = tile[y][x], (x, y)
    if c in path_cells or t in SOLID_TILES or abs(x - CAMP[0]) <= 3 and abs(y - CAMP[1]) <= 3: continue
    r = rnd(x, y, 30)
    if dist(x, y, *FOREST) < 11 and t in (GRASS, DIRT) and r < 0.5: put("tree", x, y)
    elif dist(x, y, *SWAMP) < 11 and t == GRASS and r < 0.25: put("tree", x, y)
    elif dist(x, y, *MOUNT) < 12 and t == STONE and r < 0.12: put("rock", x, y)
    elif dist(x, y, *MOUNT) < 12 and t == GRASS and r < 0.3: put("tree", x, y)
    elif t == SAND and dist(x, y, *DUNES) < 6 and r < 0.06: put("rock", x, y)
    elif t == GRASS and r < 0.05: put("tree", x, y)

# --- check: every landmark reachable from the spawn with the game's rules -------
blocked = {(int(o["X"]), int(o["Y"])) for o in objects if o["Name"] != "spawn"} | {c for c in cells if tile[c[1]][c[0]] in SOLID_TILES}
seen, todo = {CAMP}, deque([CAMP])
while todo:
    x, y = todo.popleft()
    for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= nx < W and 0 <= ny < H and (nx, ny) not in seen and (nx, ny) not in blocked \
                and abs(h[ny][nx] - h[y][x]) <= 1:
            seen.add((nx, ny)); todo.append((nx, ny))
for name, c in (("peak", MOUNT), ("forest", (31, 4)), ("swamp island", SWAMP_ISLAND), ("beach", (21, 29)), ("dunes", (31, 18))):
    assert c in seen, f"{name} {c} not reachable from the spawn"
assert all(0 <= h[y][x] <= MAX_H for x, y in cells)
assert h[MOUNT[1]][MOUNT[0]] == MAX_H

flat = lambda g: [g[y][x] for y in range(H) for x in range(W)]
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    json.dump({"Width": W, "Height": H, "Tiles": flat(tile), "Objects": objects, "Heights": flat(h)}, f)
print(f"{OUT}: {W}x{H}, {len(objects)} objects, reachable {len(seen)}/{W * H} cells")
