using System;
using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Tunable settings for the grid world. Edited in the Inspector on WorldGrid.
    /// </summary>
    [Serializable]
    public class GridConfig
    {
        [Min(1)] public int width = 10;
        [Min(1)] public int height = 10;

        [Tooltip("World units per tile.")]
        [Min(0.1f)] public float tileSize = 1f;

        [Tooltip("Visible gap between tiles, as a fraction of tileSize (0 = touching).")]
        [Range(0f, 0.5f)] public float gap = 0.05f;

        [Tooltip("Thickness (Y height) of each tile slab.")]
        [Min(0.01f)] public float tileThickness = 0.1f;

        public Color colorLight = new Color(0.80f, 0.80f, 0.85f);
        public Color colorDark = new Color(0.62f, 0.62f, 0.70f);

        public bool InBounds(GridCoord c) =>
            c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;
    }
}
