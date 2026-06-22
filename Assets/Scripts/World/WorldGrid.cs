using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Builds and owns the tile grid. Spawns a visible N x M board of tile slabs
    /// at Play (no prefabs or art assets required) and provides coordinate lookups.
    /// Attach this to an empty GameObject named "World".
    /// </summary>
    public class WorldGrid : MonoBehaviour
    {
        [SerializeField] private GridConfig config = new GridConfig();

        public GridConfig Config => config;
        public int Width => config.width;
        public int Height => config.height;

        private Tile[,] _tiles;
        private Material _matLight;
        private Material _matDark;

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            Clear();

            _matLight = MakeMaterial(config.colorLight);
            _matDark = MakeMaterial(config.colorDark);

            _tiles = new Tile[config.width, config.height];

            for (int x = 0; x < config.width; x++)
            {
                for (int y = 0; y < config.height; y++)
                {
                    _tiles[x, y] = CreateTile(new GridCoord(x, y));
                }
            }
        }

        private Tile CreateTile(GridCoord coord)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, false);

            float s = config.tileSize;
            float footprint = s * (1f - config.gap);
            go.transform.localScale = new Vector3(footprint, config.tileThickness, footprint);
            go.transform.localPosition = CoordToLocalPosition(coord);

            var renderer = go.GetComponent<MeshRenderer>();
            bool isLight = (coord.x + coord.y) % 2 == 0;
            renderer.sharedMaterial = isLight ? _matLight : _matDark;

            var tile = go.AddComponent<Tile>();
            tile.Init(coord);
            return tile;
        }

        /// <summary>Local position of a tile's center on the ground plane (XZ).</summary>
        public Vector3 CoordToLocalPosition(GridCoord coord)
        {
            return new Vector3(coord.x * config.tileSize, 0f, coord.y * config.tileSize);
        }

        /// <summary>World position of a tile's center.</summary>
        public Vector3 CoordToWorldPosition(GridCoord coord)
        {
            return transform.TransformPoint(CoordToLocalPosition(coord));
        }

        public Tile GetTile(GridCoord coord)
        {
            if (_tiles == null || !config.InBounds(coord)) return null;
            return _tiles[coord.x, coord.y];
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            _tiles = null;
        }

        // Tiles share two materials (light/dark) for the checkerboard, built via
        // MaterialUtil so they render correctly under URP or the Built-in pipeline.
        private static Material MakeMaterial(Color color) => MaterialUtil.CreateColored(color);
    }
}
