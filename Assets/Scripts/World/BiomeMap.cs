using System.Collections.Generic;

namespace AISandbox.World
{
    /// <summary>
    /// The generator's working canvas: which Biome sits at each coordinate.
    /// Plain int coordinates keep generation independent of the rest of the world.
    /// Passes read and write this; WorldGrid consumes the finished map.
    /// </summary>
    public class BiomeMap
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Biome[,] _cells;

        // 4-connected neighbor offsets (cardinal), matching agent movement.
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public BiomeMap(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new Biome[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public Biome Get(int x, int y) => InBounds(x, y) ? _cells[x, y] : null;

        public void Set(int x, int y, Biome biome)
        {
            if (InBounds(x, y)) _cells[x, y] = biome;
        }

        public bool IsAssigned(int x, int y) => InBounds(x, y) && _cells[x, y] != null;

        /// <summary>Cardinal in-bounds neighbors of a cell, appended to <paramref name="into"/>.</summary>
        public void GetNeighbors(int x, int y, List<(int x, int y)> into)
        {
            into.Clear();
            for (int i = 0; i < 4; i++)
            {
                int nx = x + Dx[i], ny = y + Dy[i];
                if (InBounds(nx, ny)) into.Add((nx, ny));
            }
        }
    }
}
