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
        private static readonly Texture2D Icon =
            ContentFinder<Texture2D>.Get("TileBiomeEditor/ChangeBiome", reportFailure: false)
            ?? BaseContent.BadTex;

        public static void Postfix(Tile __instance, ref IEnumerable<Gizmo> __result)
        {
            // GetGizmos() is an iterator that normally yields nothing (outside dev
            // mode), so we simply append our button to whatever it returned.
            __result = __result.Concat(new[] { BuildGizmo(__instance) });
        }

        private static Gizmo BuildGizmo(Tile tile)
        {
            return new Command_Action
            {
                defaultLabel = "Change biome...",
                defaultDesc = "Set this tile's biome.\n\nDo this before the tile's map is generated; "
                            + "the new biome only drives map generation for maps made afterward, it "
                            + "does not rewrite a map that already exists.",
                icon = Icon,
                action = () => Find.WindowStack.Add(new FloatMenu(BiomeOptions(tile)))
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
    }
}
