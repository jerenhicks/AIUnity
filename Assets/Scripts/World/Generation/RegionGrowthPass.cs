using System.Collections.Generic;
using UnityEngine;

namespace AISandbox.World.Generation
{
    /// <summary>
    /// Base terrain. Fills the whole map with contiguous biome regions ("blobs").
    /// Repeatedly: pick an unassigned tile, choose a biome (weighted), then grow
    /// outward over unassigned cardinal neighbors until the region reaches a target
    /// size drawn from that biome's [min, max]. This is where "deserts cluster" and
    /// "a region is between N and M tiles" rules live.
    /// </summary>
    public class RegionGrowthPass : IWorldGenPass
    {
        private readonly List<Biome> _biomes;

        public RegionGrowthPass(List<Biome> biomes)
        {
            _biomes = biomes;
        }

        public void Apply(BiomeMap map, System.Random rng)
        {
            if (_biomes == null || _biomes.Count == 0) return;

            // Visit every cell in a random order; each still-empty cell seeds a region.
            var order = BuildShuffledOrder(map, rng);
            var neighborBuffer = new List<(int x, int y)>(4);
            var candidates = new List<(int x, int y)>(4);

            foreach (var (sx, sy) in order)
            {
                if (map.IsAssigned(sx, sy)) continue;

                Biome biome = PickBiome(rng);
                if (biome == null) continue;

                int target = biome.PickRegionSize(rng);
                GrowRegion(map, rng, sx, sy, biome, target, neighborBuffer, candidates);
            }
        }

        private static void GrowRegion(BiomeMap map, System.Random rng, int sx, int sy,
            Biome biome, int target, List<(int x, int y)> neighborBuffer, List<(int x, int y)> candidates)
        {
            map.Set(sx, sy, biome);
            var frontier = new List<(int x, int y)> { (sx, sy) };
            int count = 1;

            while (count < target && frontier.Count > 0)
            {
                int fi = rng.Next(frontier.Count);
                var (cx, cy) = frontier[fi];

                map.GetNeighbors(cx, cy, neighborBuffer);
                candidates.Clear();
                foreach (var n in neighborBuffer)
                    if (!map.IsAssigned(n.x, n.y)) candidates.Add(n);

                if (candidates.Count == 0)
                {
                    frontier.RemoveAt(fi); // this cell can't grow any further
                    continue;
                }

                var pick = candidates[rng.Next(candidates.Count)];
                map.Set(pick.x, pick.y, biome);
                frontier.Add(pick);
                count++;
            }
        }

        private Biome PickBiome(System.Random rng)
        {
            float total = 0f;
            foreach (var b in _biomes)
                if (b != null) total += Mathf.Max(0f, b.weight);

            if (total <= 0f) return _biomes[rng.Next(_biomes.Count)];

            double r = rng.NextDouble() * total;
            foreach (var b in _biomes)
            {
                if (b == null) continue;
                r -= Mathf.Max(0f, b.weight);
                if (r <= 0d) return b;
            }
            return _biomes[_biomes.Count - 1];
        }

        private static List<(int x, int y)> BuildShuffledOrder(BiomeMap map, System.Random rng)
        {
            var order = new List<(int x, int y)>(map.Width * map.Height);
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    order.Add((x, y));

            // Fisher-Yates.
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }
    }
}
