using System.Collections.Generic;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation
{
    /// <summary>
    /// Resolves a user-chosen <see cref="TerrainTheme"/> into the biome selectors the engine
    /// understands, and maps biomes to their matching water tileset.
    /// </summary>
    /// <remarks>
    /// The seven biome names are confirmed against the shipped templates and
    /// <c>GameData/GeneratorData/generator_environment_assets.json</c>.
    /// </remarks>
    public static class Biomes
    {
        /// <summary>All seven terrain biomes the engine supports.</summary>
        public static readonly string[] All =
        [
            "Grass", "Snow", "Lava", "Sand", "Dirt", "Deathland", "Autumn"
        ];

        /// <summary>
        /// Returns the biome name list that a <c>FromList</c> selector should carry for the given
        /// theme, or <c>null</c> when the theme should fall back to the engine's faction-derived
        /// defaults (<see cref="TerrainTheme.FactionBased"/>).
        /// </summary>
        public static IReadOnlyList<string>? Resolve(TerrainTheme theme) => theme switch
        {
            TerrainTheme.FactionBased => null,
            TerrainTheme.Random       => All,
            _                         => [theme.ToString()],
        };

        /// <summary>
        /// Maps the chosen theme to the water tileset name used by <c>border.waterType</c>.
        /// Faction-based / random themes default to the grass water set.
        /// </summary>
        public static string WaterType(TerrainTheme theme) => theme switch
        {
            TerrainTheme.Snow      => "water snow",
            TerrainTheme.Lava      => "lava",
            TerrainTheme.Sand      => "water sand",
            TerrainTheme.Dirt      => "water dirt",
            TerrainTheme.Deathland => "water death",
            TerrainTheme.Autumn    => "water fallen",
            _                      => "water grass",
        };

        /// <summary>
        /// Builds the <c>zoneBiome</c> / <c>contentBiome</c> / <c>metaObjectsBiome</c> trio for a zone.
        /// When <paramref name="forcedBiomes"/> is <c>null</c> the engine defaults are kept
        /// (match the town's faction when the zone has a castle, otherwise match the zone).
        /// When a theme is forced, the zone biome is locked to that list and its content / meta
        /// objects follow the zone so the whole zone is visually consistent.
        /// </summary>
        public static (BiomeSelector Zone, BiomeSelector Content, BiomeSelector Meta) ForZone(
            IReadOnlyList<string>? forcedBiomes, bool hasCastle)
        {
            if (forcedBiomes is { Count: > 0 })
            {
                var zone = new BiomeSelector { Type = "FromList", Args = [.. forcedBiomes] };
                var matchZone = new BiomeSelector { Type = "MatchZone", Args = [] };
                return (zone, matchZone, matchZone);
            }

            BiomeSelector Default() => hasCastle
                ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                : new BiomeSelector { Type = "MatchZone", Args = [] };

            return (Default(), Default(), Default());
        }
    }
}
