using System.Collections.Generic;
using AISandbox.World.Generation;
using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Builds a BiomeMap for the world by running an ordered pipeline of generation
    /// passes. Tune everything here in the Inspector; add new rules later by writing
    /// another IWorldGenPass and inserting it into <see cref="Generate"/>.
    /// Put this component on the same GameObject as WorldGrid (or anywhere in the scene).
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [Tooltip("0 = a new random world each run. Any other value = reproducible world.")]
        public int seed = 0;

        [Header("Base biomes (region growth)")]
        public List<Biome> baseBiomes = new List<Biome>
        {
            new Biome { name = "Plains",    color = new Color(0.45f, 0.70f, 0.35f), description = "open grassy plains",  weight = 3f,   minRegionSize = 10, maxRegionSize = 30 },
            new Biome { name = "Forest",    color = new Color(0.20f, 0.45f, 0.25f), description = "dense forest",        weight = 1.5f, minRegionSize = 6,  maxRegionSize = 18 },
            new Biome { name = "Desert",    color = new Color(0.85f, 0.78f, 0.45f), description = "arid sandy desert",   weight = 1.5f, minRegionSize = 6,  maxRegionSize = 20 },
            new Biome { name = "Mountains", color = new Color(0.55f, 0.52f, 0.50f), description = "rocky mountains",     weight = 1f,   minRegionSize = 4,  maxRegionSize = 14 },
        };

        [Header("Rivers")]
        public Biome waterBiome = new Biome { name = "Water", color = new Color(0.25f, 0.50f, 0.85f), description = "flowing river water" };

        [Min(0)] public int riverCount = 1;
        [Range(0f, 1f)] public float riverMeander = 0.35f;
        [Tooltip("Rivers run from one edge to the opposite edge when on.")]
        public bool riversEdgeToEdge = true;
        [Tooltip("0 = auto length based on map size.")]
        [Min(0)] public int riverMaxLength = 0;

        /// <summary>The seed actually used on the last Generate (useful when seed = 0).</summary>
        public int LastSeedUsed { get; private set; }

        public BiomeMap Generate(int width, int height)
        {
            int s = seed != 0 ? seed : System.Environment.TickCount;
            LastSeedUsed = s;
            var rng = new System.Random(s);

            var map = new BiomeMap(width, height);

            // ---- Pipeline (order matters) ----
            new RegionGrowthPass(baseBiomes).Apply(map, rng);
            new RiverPass(waterBiome, riverCount, riverMeander, riversEdgeToEdge, riverMaxLength).Apply(map, rng);
            // Add future passes here (forests-near-water, biome smoothing, doodads, …).

            Debug.Log($"WorldGenerator: generated {width}x{height} world (seed {LastSeedUsed}).");
            return map;
        }
    }
}
