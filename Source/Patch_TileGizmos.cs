using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace TileBiomeEditor
{
    // Attribute-based [HarmonyPatch] classes do NOTHING on their own — a Harmony
    // instance must call PatchAll() at startup to activate them. [StaticConstructorOnStartup]
    // guarantees this static constructor runs once, after defs/content have loaded.
    [StaticConstructorOnStartup]
    public static class TileBiomeEditorMod
    {
        static TileBiomeEditorMod()
        {
            new Harmony("jamalone.tilebiomeeditor").PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[TileBiomeEditor] Harmony patches applied.");
        }
    }

    // RimWorld.Planet.WorldGizmoUtility.WorldUIOnGUI() draws whatever
    // Find.WorldSelector.SelectedTile.Tile.GetGizmos() returns, both during
    // the "select landing site" step and on the in-game world map. So adding
    // one gizmo here is all we need: it shows up exactly when a tile is selected.
    //
    // SurfaceTile does not override GetGizmos(), so patching the base Tile is
    // enough to cover normal surface tiles.
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(Tile), nameof(Tile.GetGizmos))]
    public static class Patch_Tile_GetGizmos
    {
        private static readonly Texture2D BiomeIcon =
            ContentFinder<Texture2D>.Get("TileBiomeEditor/ChangeBiome", reportFailure: false)
            ?? BaseContent.BadTex;

        private static readonly Texture2D FeaturesIcon =
            ContentFinder<Texture2D>.Get("TileBiomeEditor/ChangeFeatures", reportFailure: false)
            ?? BiomeIcon;

        // The "safe" subset of Odyssey's tile mutators: weather, wildlife, and
        // resource/ground modifiers (the contents of TileMutators_Modifiers.xml).
        // Structural / landmark mutators (caves, lakes, ancient structures) are
        // intentionally excluded — they are built for map generation and can
        // behave oddly if hand-added to an arbitrary tile. Names are resolved at
        // runtime and silently skipped if absent (e.g. without the Odyssey DLC).
        private static readonly string[] CuratedMutatorNames =
        {
            "WindyMutator", "FoggyMutator", "SunnyMutator", "WetClimate",
            "AnimalLife_Increased", "AnimalLife_Decreased", "AnimalHabitat",
            "PlantLife_Increased", "PlantLife_Decreased", "PlantGrove",
            "WildPlants", "WildTropicalPlants", "ArcheanTrees",
            "MineralRich", "ObsidianDeposits", "SteamGeysers_Increased",
            "Fish_Increased", "Fish_Decreased",
            "DryGround", "Pollution_Increased", "Junkyard",
        };

        private static List<TileMutatorDef> CuratedMutators =>
            CuratedMutatorNames
                .Select(n => DefDatabase<TileMutatorDef>.GetNamedSilentFail(n))
                .Where(d => d != null)
                .OrderBy(d => d.label)
                .ToList();

        public static void Postfix(Tile __instance, ref IEnumerable<Gizmo> __result)
        {
            // GetGizmos() is an iterator that normally yields nothing (outside dev
            // mode), so we simply append our buttons to whatever it returned.
            __result = __result.Concat(new[]
            {
                BuildBiomeGizmo(__instance),
                BuildFeaturesGizmo(__instance),
            });
        }

        private static Gizmo BuildBiomeGizmo(Tile tile)
        {
            return new Command_Action
            {
                defaultLabel = "Change biome...",
                defaultDesc = "Set this tile's biome.\n\nDo this before the tile's map is generated; "
                            + "the new biome only drives map generation for maps made afterward, it "
                            + "does not rewrite a map that already exists.",
                icon = BiomeIcon,
                action = () => Find.WindowStack.Add(new FloatMenu(BiomeOptions(tile)))
            };
        }

        private static Gizmo BuildFeaturesGizmo(Tile tile)
        {
            return new Command_Action
            {
                defaultLabel = "Change features...",
                defaultDesc = "Toggle tile features (mutators) such as windy, foggy, fertile or "
                            + "mineral-rich. Features already on this tile are marked \"(active)\"; "
                            + "click one to toggle it.\n\nLike the biome, features drive map "
                            + "generation, so set them before the tile's map is made.",
                icon = FeaturesIcon,
                action = () => Find.WindowStack.Add(new FloatMenu(MutatorOptions(tile)))
            };
        }

        private static List<FloatMenuOption> BiomeOptions(Tile tile)
        {
            var options = new List<FloatMenuOption>();

            foreach (BiomeDef biome in DefDatabase<BiomeDef>.AllDefsListForReading
                         .OrderBy(b => b.label))
            {
                BiomeDef captured = biome; // avoid closure-over-loop-variable
                string label = captured.LabelCap;
                if (captured == tile.PrimaryBiome)
                    label += "  (current)";

                options.Add(new FloatMenuOption(label, () => SetBiome(tile, captured)));
            }

            return options;
        }

        private static List<FloatMenuOption> MutatorOptions(Tile tile)
        {
            var options = new List<FloatMenuOption>();

            foreach (TileMutatorDef mutator in CuratedMutators)
            {
                TileMutatorDef captured = mutator; // avoid closure-over-loop-variable
                string label = captured.LabelCap;
                if (tile.Mutators.Contains(captured))
                    label += "  (active)";

                options.Add(new FloatMenuOption(label, () => ToggleMutator(tile, captured)));
            }

            return options;
        }

        private static void SetBiome(Tile tile, BiomeDef biome)
        {
            if (tile.PrimaryBiome == biome)
                return;

            tile.PrimaryBiome = biome;

            // Recolor the planet. Marking the terrain layer dirty queues a short
            // "Generating planet" pass that redraws the tile in its new biome color.
            Find.World.renderer.SetDirty<WorldDrawLayer_Terrain>(tile.Layer);

            Messages.Message(
                "Tile biome set to " + biome.LabelCap + ".",
                MessageTypeDefOf.TaskCompletion,
                historical: false);

            // --- Optional, if you ever want more than "just the biome": ---
            // Bring climate roughly in line with the new biome so it feels coherent.
            // These are commented out because you asked for biome-only edits.
            //
            // tile.temperature = biome == BiomeDefOf.IceSheet ? -30f : 20f;
            // tile.rainfall    = biome.allowRivers ? 1200f : tile.rainfall;
        }

        private static void ToggleMutator(Tile tile, TileMutatorDef mutator)
        {
            bool active = tile.Mutators.Contains(mutator);
            if (active)
            {
                tile.RemoveMutator(mutator);
            }
            else
            {
                // AddMutator resolves same-category conflicts itself (e.g. adding
                // "increased animal life" drops "decreased animal life").
                tile.AddMutator(mutator);
            }

            // Some features (landforms) change how the tile is drawn; redraw to be safe.
            Find.World.renderer.SetDirty<WorldDrawLayer_Terrain>(tile.Layer);

            Messages.Message(
                (active ? "Removed feature: " : "Added feature: ") + mutator.LabelCap + ".",
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }
    }
}
