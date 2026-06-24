using UnityEngine;

namespace AISandbox.World.Generation
{
    /// <summary>
    /// Carves meandering rivers over the base terrain. Each river is a single
    /// continuous walk (so it always "follows itself") from one edge toward a target,
    /// biased toward the target but wandering by a tunable amount. Overwrites whatever
    /// biome it crosses with water. Runs after the base terrain pass.
    /// </summary>
    public class RiverPass : IWorldGenPass
    {
        private readonly Biome _water;
        private readonly int _riverCount;
        private readonly float _meander;     // 0 = straight to target, 1 = very wandering
        private readonly bool _edgeToEdge;
        private readonly int _maxLength;      // 0 = auto (width + height) * 2

        private static readonly int[] DirX = { 1, -1, 0, 0 };
        private static readonly int[] DirY = { 0, 0, 1, -1 };

        public RiverPass(Biome water, int riverCount, float meander, bool edgeToEdge, int maxLength)
        {
            _water = water;
            _riverCount = riverCount;
            _meander = Mathf.Clamp01(meander);
            _edgeToEdge = edgeToEdge;
            _maxLength = maxLength;
        }

        public void Apply(BiomeMap map, System.Random rng)
        {
            if (_water == null || _riverCount <= 0) return;
            for (int i = 0; i < _riverCount; i++) CarveRiver(map, rng);
        }

        private void CarveRiver(BiomeMap map, System.Random rng)
        {
            int side = rng.Next(4);
            var (x, y) = RandomEdgeCell(map, rng, side);
            var (tx, ty) = _edgeToEdge
                ? RandomEdgeCell(map, rng, Opposite(side))
                : (rng.Next(map.Width), rng.Next(map.Height));

            int maxSteps = _maxLength > 0 ? _maxLength : (map.Width + map.Height) * 2;

            for (int step = 0; step < maxSteps; step++)
            {
                map.Set(x, y, _water);
                if (x == tx && y == ty) break;

                int ddx = 0, ddy = 0;
                if (rng.NextDouble() < _meander)
                {
                    int d = rng.Next(4); // wander in a random cardinal direction
                    ddx = DirX[d];
                    ddy = DirY[d];
                }
                else
                {
                    StepToward(x, y, tx, ty, ref ddx, ref ddy);
                }

                int nx = Mathf.Clamp(x + ddx, 0, map.Width - 1);
                int ny = Mathf.Clamp(y + ddy, 0, map.Height - 1);

                // If wandering pushed us into a wall, nudge toward the target instead.
                if (nx == x && ny == y)
                {
                    ddx = 0; ddy = 0;
                    StepToward(x, y, tx, ty, ref ddx, ref ddy);
                    nx = Mathf.Clamp(x + ddx, 0, map.Width - 1);
                    ny = Mathf.Clamp(y + ddy, 0, map.Height - 1);
                    if (nx == x && ny == y) break; // genuinely stuck
                }

                x = nx;
                y = ny;
            }
        }

        private static void StepToward(int x, int y, int tx, int ty, ref int ddx, ref int ddy)
        {
            int rx = tx - x, ry = ty - y;
            if (Mathf.Abs(rx) >= Mathf.Abs(ry) && rx != 0) ddx = (int)Mathf.Sign(rx);
            else if (ry != 0) ddy = (int)Mathf.Sign(ry);
            else if (rx != 0) ddx = (int)Mathf.Sign(rx);
        }

        private static (int x, int y) RandomEdgeCell(BiomeMap map, System.Random rng, int side)
        {
            switch (side)
            {
                case 0: return (0, rng.Next(map.Height));               // left
                case 1: return (map.Width - 1, rng.Next(map.Height));   // right
                case 2: return (rng.Next(map.Width), 0);                // bottom
                default: return (rng.Next(map.Width), map.Height - 1);  // top
            }
        }

        private static int Opposite(int side) => side switch
        {
            0 => 1,
            1 => 0,
            2 => 3,
            _ => 2,
        };
    }
}
