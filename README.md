# Isometric Game Editor

A small isometric tile map editor built with .NET 10, Raylib and Dear ImGui. Paint maps, place objects, test them in Play mode, and export a standalone game as a single `.exe`.

## Features

- Isometric 64×32 tiles: click/drag to paint, one stroke = one undo step
- Pan and zoom (zoom snaps to whole numbers at 1× and above for crisp pixels)
- Dockable Scene, Assets, Hierarchy and Inspector panels
- Map objects: add, delete, rename, drag in the scene, depth-sorted drawing
- Assets panel built from PNG sprites (tiles, objects, characters)
- Undo/redo for every edit
- Multiple maps: New / Open / Save / Save As, with an unsaved-changes prompt
- Play mode: walk a hero around the map, blocked by solid tiles
- Standalone Player, published as one self-contained `.exe`

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
| Middle drag | Pan |
| Mouse wheel | Zoom toward the cursor |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Play / Stop (menu bar) | Enter / leave Play mode |
| WASD / arrow keys | Walk the hero (Play mode) |

Editing is disabled during Play mode. Stop restores the map exactly as it was.

## Assets

Sprites live in `assets/` and are copied next to the executable on build.

```
assets/
  tiles/<id>_<name>.png     e.g. 0_grass.png, 6_dirt_block.png
  objects/<name>.png        e.g. tree.png
  characters/<name>.png     hero_se.png, hero_ne.png
```

- Tile base is 64×32. Taller images (blocks) are allowed.
- Anchor is bottom center: a tile's bottom vertex, or an object's position.
- A tile id without an image is drawn as a flat color.
- Solid tiles (block the hero): `1` water, `6` dirt block, `7` stone block.
- Regenerate placeholders: `python tools/gen_assets.py`

## Map files

The editor saves to `Documents/GameEditor/maps/<name>.json`:

```json
{
  "Width": 20,
  "Height": 20,
  "Tiles": [0, 0, 1, ...],
  "Objects": [{ "Id": 1, "Name": "Tree", "X": 3.5, "Y": 4.5, "Sprite": "tree" }]
}
```

`Tiles` is row-major (`y * Width + x`). `Objects` and `Sprite` are optional. Names may not contain `..`, `/`, `\` or other invalid file name characters.

## Player / Export

Run a saved map:

```bash
dotnet run --project src/GameEditor.Player -- <map name>
```

With no name, the first map is loaded. The Player reads maps from `<exe>/maps` if it contains any `.json`, otherwise from `Documents/GameEditor/maps`.

To export a game:

1. Copy the map `.json` into `src/GameEditor.Player/maps/`.
2. Publish:

```bash
dotnet publish src/GameEditor.Player -c Release -r win-x64
```

The result is one self-contained `GameEditor.Player.exe` (runtime, raylib, assets and maps included). It runs on any Windows x64 machine without .NET or the editor.

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
| Infrastructure | `JsonMapRepository` (System.Text.Json source generation) |
| Rendering | Raylib drawing, sprite loading, shared input |
| Editor | ImGui editor app and composition root |
| Player | Standalone game |
| Tests | xUnit v3 tests for Domain, Application, Infrastructure |

## Known limitations

- New maps are a fixed 20×20 (loaded maps may be any size).
- Solid tile ids are hardcoded.
- One tile layer only.
- Windows x64 only for the published Player.