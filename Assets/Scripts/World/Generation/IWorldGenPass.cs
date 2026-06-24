namespace AISandbox.World.Generation
{
    /// <summary>
    /// One step in world generation. Passes run in order, each mutating the shared
    /// BiomeMap. Adding a new generation rule later = write a new pass and add it to
    /// the WorldGenerator's pipeline; nothing else needs to change.
    /// </summary>
    public interface IWorldGenPass
    {
        void Apply(BiomeMap map, System.Random rng);
    }
}
