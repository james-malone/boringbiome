# Tile Biome Editor

A RimWorld **1.6 / Odyssey** mod that adds a **Change biome...** button to any selected
world tile. Pick a tile (on the landing-site screen or the in-game world map), choose a
biome, and the tile is repainted to match.

Your driving example works: a sandy-desert peninsula re-biomed to forest stays a peninsula
— landforms and tile names are independent of the biome — so landing there generates a
forest map on a peninsula.

## How it works

A single Harmony postfix on `Tile.GetGizmos()` appends one `Command_Action`. Because
`WorldGizmoUtility` draws the selected tile's gizmos, the button appears exactly when a
tile is selected. Clicking it lists every `BiomeDef`; picking one sets `tile.PrimaryBiome`
and marks the terrain draw layer dirty so the globe recolors.

See [Source/Patch_TileGizmos.cs](Source/Patch_TileGizmos.cs).

### 1.6 API notes (post planet-rework)

- Selected tile: `Find.WorldSelector.SelectedTile` — a `PlanetTile` struct, **not** an `int`.
- The biome field is private; set via `tile.PrimaryBiome`.
- Redraw: `Find.World.renderer.SetDirty<WorldDrawLayer_Terrain>(tile.Layer)` (layers were
  renamed `WorldDrawLayer_*` and take a `PlanetLayer`).

Older 1.4/1.5 snippets use the pre-rework names (`tile.biome`, `WorldLayer_*`, `int`
tile ids) and will not compile against 1.6.

## Building

Requires the .NET SDK (`brew install dotnet`). From the repo root:

```sh
dotnet build Source/TileBiomeEditor.csproj -c Release
```

The DLL is written to `Assemblies/TileBiomeEditor.dll`. First build restores
`Krafs.Rimworld.Ref` (the 1.6 game references) and `Lib.Harmony` from NuGet, so it needs
internet once. Harmony is referenced for compilation only — `0Harmony.dll` is **not**
bundled, because Harmony ships as its own mod.

## Installing

1. Install the [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
   mod and load it **above** this one.
2. Copy (or symlink) this folder into your RimWorld `Mods/` directory.
3. Enable **Tile Biome Editor** in the mod list.

## Usage

On the world map, select a tile → **Change biome...** → pick a biome → then land or settle.

**Timing matters:** biome drives map generation. Change a tile *before* its map is
generated. Editing a tile whose map already exists only affects maps generated afterward.

## Optional extensions

`SetBiome` in the source has commented-out lines that nudge `temperature`/`rainfall` to keep
a re-biomed tile climatically coherent. Uncomment if you want more than a biome-only edit.
