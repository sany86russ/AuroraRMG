namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Terrain (biome) theme applied to the generated map. Olden Era supports exactly seven
    /// terrain biomes; a template can either let each zone derive its biome from the faction of
    /// its town (<see cref="FactionBased"/>), randomise across all biomes (<see cref="Random"/>),
    /// or lock the whole map to a single biome.
    /// </summary>
    public enum TerrainTheme
    {
        /// <summary>Each zone's terrain follows the faction of its main town (engine default).</summary>
        FactionBased,

        /// <summary>Every zone independently picks from all seven biomes.</summary>
        Random,

        Grass,
        Snow,
        Lava,
        Sand,
        Dirt,
        Deathland,
        Autumn
    }

    /// <summary>
    /// How aggressively neutral guards react to the player, mapped to the engine's
    /// six-bucket <c>guardReactionDistribution</c> weights.
    /// </summary>
    public enum MonsterAggression
    {
        /// <summary>Guards are more likely to flee or let the player pass.</summary>
        Passive,

        /// <summary>Engine-default reaction weights.</summary>
        Normal,

        /// <summary>Guards strongly favour standing and fighting.</summary>
        Aggressive
    }

    /// <summary>
    /// Amount of water placed on the zone borders (the engine's per-variant <c>border.waterWidth</c>).
    /// </summary>
    public enum WaterLevel
    {
        /// <summary>No water borders — a fully land-based map (engine default).</summary>
        None,

        /// <summary>Thin water borders between zones (<c>waterWidth = 3</c>).</summary>
        Small,

        /// <summary>Medium water borders (<c>waterWidth = 4</c>).</summary>
        Medium,

        /// <summary>Wide water borders that strongly separate zones (<c>waterWidth = 6</c>).</summary>
        Large
    }
}
