# Tile Biome Editor

A RimWorld **1.6 / Odyssey** mod that adds **Change biome...** and **Change features...**
buttons to any selected world tile. Pick a tile (on the landing-site screen or the in-game
world map), change its biome, and toggle features like windy, foggy, fertile or
mineral-rich.

Your driving example works: a sandy-desert peninsula re-biomed to forest stays a peninsula
— landforms and tile names are independent of the biome — so landing there generates a
forest map on a peninsula.

## How it works

A single Harmony postfix on `Tile.GetGizmos()` appends two `Command_Action`s. Because
`WorldGizmoUtility` draws the selected tile's gizmos, the buttons appear exactly when a
tile is selected.

- **Change biome...** lists every `BiomeDef`; picking one sets `tile.PrimaryBiome`.
- **Change features...** lists a curated, safe subset of `TileMutatorDef`s (weather,
  wildlife and resource/ground modifiers — Odyssey's `TileMutators_Modifiers.xml`). Each is
  toggled with `tile.AddMutator(def)` / `tile.RemoveMutator(def)`; `AddMutator` resolves
  same-category conflicts on its own. Structural/landmark mutators (caves, lakes, ancient
  ruins) are intentionally excluded — they're built for map generation.

Either way the terrain draw layer is marked dirty so the globe redraws.

See [Source/Patch_TileGizmos.cs](Source/Patch_TileGizmos.cs).

### 1.6 API notes (post planet-rework)

- Selected tile: `Find.WorldSelector.SelectedTile` — a `PlanetTile` struct, **not** an `int`.
- The biome field is private; set via `tile.PrimaryBiome`.
- Tile mutators live on the base `Tile`: read `tile.Mutators` (`IList<TileMutatorDef>`),
  edit with `tile.AddMutator` / `tile.RemoveMutator`. They're saved with the world
  (`Scribe_Collections … "mutatorDefs"`).
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
