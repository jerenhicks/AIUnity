using System.Collections.Generic;
using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Builds and owns the tile grid. Spawns a visible N x M board of tile slabs
    /// at Play (no prefabs or art assets required) and provides coordinate lookups.
    /// If a <see cref="WorldGenerator"/> is present, tiles are colored by biome;
    /// otherwise they fall back to a plain checkerboard.
    /// Attach this to an empty GameObject named "World".
    /// </summary>
    public class WorldGrid : MonoBehaviour
    {
        [SerializeField] private GridConfig config = new GridConfig();

        [Tooltip("Optional. Leave empty to auto-find one on this object or in the scene.")]
        [SerializeField] private WorldGenerator generator;

        public GridConfig Config => config;
        public int Width => config.width;
        public int Height => config.height;

        private Tile[,] _tiles;
        private Material _matLight;
        private Material _matDark;
        private readonly Dictionary<Biome, Material> _biomeMaterials = new Dictionary<Biome, Material>();

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            Clear();

            _matLight = MakeMaterial(config.colorLight);
            _matDark = MakeMaterial(config.colorDark);
            _biomeMaterials.Clear();

            // Generate a biome map if a generator is available; otherwise checkerboard.
            if (generator == null) generator = GetComponent<WorldGenerator>();
            if (generator == null) generator = FindFirstObjectByType<WorldGenerator>();
            BiomeMap biomeMap = generator != null ? generator.Generate(config.width, config.height) : null;

            _tiles = new Tile[config.width, config.height];

            for (int x = 0; x < config.width; x++)
            {
                for (int y = 0; y < config.height; y++)
                {
                    Biome biome = biomeMap != null ? biomeMap.Get(x, y) : null;
                    _tiles[x, y] = CreateTile(new GridCoord(x, y), biome);
                }
            }
        }

        private Tile CreateTile(GridCoord coord, Biome biome)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, false);

            float s = config.tileSize;
            float footprint = s * (1f - config.gap);
            go.transform.localScale = new Vector3(footprint, config.tileThickness, footprint);
            go.transform.localPosition = CoordToLocalPosition(coord);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = biome != null
                ? GetBiomeMaterial(biome)
                : ((coord.x + coord.y) % 2 == 0 ? _matLight : _matDark);

            var tile = go.AddComponent<Tile>();
            tile.Init(coord);
            tile.SetBiome(biome);
            return tile;
        }

        // One shared material per biome, built on demand.
        private Material GetBiomeMaterial(Biome biome)
        {
            if (!_biomeMaterials.TryGetValue(biome, out var mat))
            {
                mat = MaterialUtil.CreateColored(biome.color);
                _biomeMaterials[biome] = mat;
            }
            return mat;
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
