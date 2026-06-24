using System;
using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// A terrain type. Data only — describes how a biome looks and how the world
    /// generator should place it. Visuals are flat color for now; height, mesh
    /// variants, and doodads can be layered on later without touching this contract.
    /// </summary>
    [Serializable]
    public class Biome
    {
        [Tooltip("Display name, e.g. Plains, Desert. Also surfaced to agents later.")]
        public string name = "Biome";

        [Tooltip("Flat tile color (placeholder for richer visuals later).")]
        public Color color = Color.gray;

        [Tooltip("Short description an LLM agent can perceive later.")]
        [TextArea] public string description = "";

        [Header("Region growth")]
        [Tooltip("Relative chance this biome is chosen to seed a region.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Smallest number of tiles a region of this biome aims for.")]
        [Min(1)] public int minRegionSize = 4;

        [Tooltip("Largest number of tiles a region of this biome aims for.")]
        [Min(1)] public int maxRegionSize = 12;

        /// <summary>A target region size within [min, max], inclusive.</summary>
        public int PickRegionSize(System.Random rng)
        {
            int lo = Mathf.Max(1, Mathf.Min(minRegionSize, maxRegionSize));
            int hi = Mathf.Max(lo, maxRegionSize);
            return rng.Next(lo, hi + 1);
        }
    }
}
