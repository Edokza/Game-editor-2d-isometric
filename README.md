# Isometric Game Editor

A small isometric tile map editor built with .NET 10, Raylib and Dear ImGui. Paint maps, place objects, test them in Play mode, and export a standalone game with one click.

## Features

- Isometric 64×32 tiles: click/drag to paint, one stroke = one undo step
- Bucket fill: flood-fill connected tiles of the same id (4-way), one undo step
- Rectangle tool: drag a box, release to fill every tile in it, one undo step
- Height tool: click/drag raises each tile one step (Shift lowers), 0–8 steps, one undo step per stroke; raised tiles get walls and objects stand on top
- Eyedropper: right-click a tile in the Scene to pick it as the paint tile
- Map size: New asks for width × height, Edit → Resize Map changes it (1–256 each). Resize drops objects outside the new size and can be undone
- Solid tiles per map: right-click a tile in Assets to toggle Solid (red corner mark), saved in the map file
- Solid objects per map: right-click an object in Assets to toggle Solid; the hero can't enter a tile holding one (default: tree, rock, crate)
- Hero spawn marker: add the `spawn` object to set where Play starts (one per map; adding another moves it). No spawn = map center
- Pan and zoom (zoom snaps to whole numbers at 1× and above for crisp pixels)
- Dockable Scene, Assets, Hierarchy and Inspector panels with a default layout; View → Reset Layout restores it
- View → Grid: tile outlines over the map; the menu bar shows the tile `(x, y)` and id under the mouse
- Map objects: add, delete, duplicate, rename, drag in the scene, depth-sorted drawing
- Assets panel built from PNG sprites (tiles, objects, characters)
- Undo/redo for every edit
- Multiple maps: New / Open / Save / Save As, with an unsaved-changes prompt
- Play mode: walk a hero around the map, blocked by solid tiles and objects, facing the way it walks; the camera follows the hero smoothly
- Elevation: the hero climbs or drops one step at a time and eases up/down instead of jumping; a difference of two or more steps is a wall
- File → Export Game: a playable folder (exe + map) that runs without .NET or the editor

## Requirements

- .NET 10 SDK
- Windows x64
- Optional: Python 3 + Pillow, only to regenerate the placeholder sprites

## Quick start

```bash
dotnet build GameEditor.slnx
dotnet test GameEditor.slnx
dotnet run --project src/GameEditor.Editor
```

## Editor controls

| Input | Action |
|---|---|
| Left click / drag | Paint the selected tile, or drag an object |
| B / G / R / H | Brush / Fill / Rectangle / Height tool |
| Shift + click (Height tool) | Lower the tile one step |
| Right click (Scene) | Pick the tile under the cursor |
| Right click (Assets tile or object) | Toggle Solid |
| Middle drag | Pan (not in Play mode) |
| Mouse wheel | Zoom toward the cursor (screen center in Play mode) |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+N / Ctrl+O | New / Open |
| Ctrl+S / Ctrl+Shift+S | Save / Save As |
| Delete / Ctrl+D | Delete / duplicate the selected object |
| Play / Stop (menu bar) | Enter / leave Play mode |
| Q / E | Rotate the view 90° left / right around the map center (also in Play mode; view only, not saved) |
| WASD / arrow keys | Walk the hero (Play mode), relative to the screen |

Editing is disabled during Play mode. Stop restores the map and zoom exactly as they were.

## Assets

Sprites live in `assets/` and are copied next to the executable on build.

```
assets/
  tiles/<id>_<name>.png     e.g. 0_grass.png, 6_dirt_block.png
  objects/<name>.png        e.g. tree.png, spawn.png (hero spawn marker)
  characters/<name>.png     hero_se.png, hero_ne.png, hero_sw.png, hero_nw.png
```

- Tile base is 64×32. Taller images (blocks) are allowed.
- Anchor is bottom center: a tile's bottom vertex, or an object's position.
- A tile id without an image is drawn as a flat color.
- Default solid tiles for new maps: `1` water, `6` dirt block, `7` stone block.
- Regenerate placeholders: `python tools/gen_assets.py`

## Map files

The editor saves to `Documents/GameEditor/maps/<name>.json`:

```json
{
  "Width": 20,
  "Height": 20,
  "Tiles": [0, 0, 1, ...],
  "Objects": [
    { "Id": 1, "Name": "tree", "X": 3.5, "Y": 4.5, "Sprite": "tree" },
    { "Id": 2, "Name": "spawn", "X": 10.5, "Y": 10.5, "Sprite": "spawn" }
  ],
  "Solid": [1, 6, 7],
  "SolidSprites": ["crate", "rock", "tree"],
  "Heights": [0, 0, 2, ...]
}
```

`Tiles` and `Heights` are row-major (`y * Width + x`). `Objects`, `Sprite`, `Solid`, `SolidSprites` and `Heights` are optional; a missing `Heights` means every tile is at ground level (each value 0–8 steps). A missing `Solid` means the defaults `[1, 6, 7]`, a missing `SolidSprites` means `["crate", "rock", "tree"]`; `[]` means nothing is solid. A solid object blocks the tile it stands on. The first object with sprite `spawn` is the hero's start. Names may not contain `..`, `/`, `\` or other invalid file name characters.

## Export

Save the map, then **File → Export Game...** (an unsaved map asks for a name first; a modified map is saved). The game is written to `Documents/GameEditor/export/<name>/`:

```
<name>/
  <name>.exe          self-contained Player (runtime, raylib and assets inside)
  maps/<name>.json
```

Copy the whole folder. It runs on any Windows x64 machine without .NET or the editor. Exporting again overwrites it; close the game first.

The editor build publishes the Player into `<editor output>/player/` for this, so the first build (and builds after Player changes) take longer. If that publish fails (e.g. offline), the build still succeeds but Export reports "Player not found"; rebuild when online.

## Player

Run a saved map without exporting:

```bash
dotnet run --project src/GameEditor.Player -- <map name>
```

In the game: WASD / arrow keys walk (always relative to the screen), Q / E rotate the view 90°, F11 toggles fullscreen, Esc twice quits. Every run starts unrotated.

With no name, the first map is loaded. The Player reads maps from `maps/` next to its exe if it contains any `.json`, otherwise from `Documents/GameEditor/maps`.

Manual publish (one exe with the maps inside):

1. Copy the map `.json` into `src/GameEditor.Player/maps/`.
2. Publish:

```bash
dotnet publish src/GameEditor.Player -c Release -r win-x64
```

The result is `src/GameEditor.Player/bin/Release/net10.0/win-x64/publish/GameEditor.Player.exe`. `src/GameEditor.Player/maps/` already contains `Map_default_1.json`, which gets bundled too: delete it before publishing, or pass your map name when running.

## Architecture

```
Editor ─┬─> Infrastructure ─> Application ─> Domain
Player ─┤                                      ^
        └─> Rendering ─────────────────────────┘
```

| Project | Role |
|---|---|
| Domain | `TileMap`, `MapObject`, `IsoMath`, solid tiles. No dependencies. |
| Application | `MapEditingService`, commands + `UndoStack`, `PlaySession`, `IMapRepository` |
| Infrastructure | `JsonMapRepository` (System.Text.Json source generation), `GameExporter` |
| Rendering | Raylib drawing, sprite loading, shared input |
| Editor | ImGui editor app and composition root |
| Player | Standalone game |
| Tests | xUnit v3 tests for Domain, Application, Infrastructure |

## Known limitations

- New / Resize Map are limited to 256×256 (loaded files may be larger).
- One tile layer only.
- Windows x64 only for the published Player.
- Export is a folder (exe + `maps/`), not a single file, and holds one map.