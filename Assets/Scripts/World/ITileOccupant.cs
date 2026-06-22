namespace AISandbox.World
{
    /// <summary>
    /// Anything that can stand on a tile (currently agents). Lets the World layer
    /// track occupancy without referencing the Agents layer directly.
    /// </summary>
    public interface ITileOccupant
    {
        GridCoord Coord { get; }
        string DisplayName { get; }
    }
}
