using System.Collections.Generic;
using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Services.Generation
{
    /// <summary>
    /// Role of a zone for the purpose of choosing its guard reaction weights.
    /// </summary>
    public enum GuardRole
    {
        /// <summary>Player start zone — kept safe so early exploration is not punished.</summary>
        StartZone,

        /// <summary>Ordinary neutral zone (low / medium quality).</summary>
        NeutralStandard,

        /// <summary>High-value neutral or hub zone, guarded more fiercely.</summary>
        NeutralDangerous
    }

    /// <summary>
    /// Produces the engine's six-bucket <c>guardReactionDistribution</c> weights.
    /// The engine order is Aggressive, Negative, Common, Friendly, Peaceful, Docile.
    /// Distribution preserves historical raw values for legacy seeds. NativeDistribution corrects
    /// the historical reversed interpretation for current-profile player-facing aggression.
    /// </summary>
    public static class GuardReaction
    {
        public static List<double> NativeDistribution(GuardRole role, MonsterAggression aggression)
            => Distribution(role, aggression).AsEnumerable().Reverse().ToList();
        public static List<double> Distribution(GuardRole role, MonsterAggression aggression) => role switch
        {
            GuardRole.StartZone => aggression switch
            {
                MonsterAggression.Passive    => [100, 20, 5, 3, 1, 0],
                MonsterAggression.Aggressive => [20, 20, 20, 20, 10, 5],
                _                            => [60, 20, 10, 10, 2, 0],
            },
            GuardRole.NeutralDangerous => aggression switch
            {
                MonsterAggression.Passive    => [10, 20, 20, 15, 10, 0],
                MonsterAggression.Aggressive => [0, 5, 10, 25, 20, 10],
                _                            => [0, 10, 10, 20, 10, 0],
            },
            // NeutralStandard
            _ => aggression switch
            {
                MonsterAggression.Passive    => [20, 30, 20, 10, 5, 0],
                MonsterAggression.Aggressive => [0, 5, 10, 20, 15, 5],
                _                            => [0, 10, 10, 10, 10, 0],
            },
        };
    }
}
