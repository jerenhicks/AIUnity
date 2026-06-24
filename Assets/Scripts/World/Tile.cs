using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// One grid location. Knows its coordinate. Later it will track terrain,
    /// occupants, and anything agents place on it.
    /// </summary>
    public class Tile : MonoBehaviour
    {
        [field: SerializeField] public GridCoord Coord { get; private set; }

        /// <summary>Which occupant (if any) currently stands on this tile. Null when empty.</summary>
        public ITileOccupant Occupant { get; set; }

        public bool IsOccupied => Occupant != null;

        /// <summary>This tile's terrain type (null if the world isn't biome-generated).</summary>
        public Biome Biome { get; private set; }

        public void Init(GridCoord coord)
        {
            Coord = coord;
            name = $"Tile {coord}";
        }

        public void SetBiome(Biome biome)
        {
            Biome = biome;
            if (biome != null) name = $"Tile {Coord} [{biome.name}]";
        }
    }
}
