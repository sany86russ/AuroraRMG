namespace Olden_Era___Template_Editor.Models
{
    /// <summary>High-level game shape the player picks in Simple Mode.</summary>
    public enum QuickGameType
    {
        /// <summary>1v1 — exactly two players, no shared borders.</summary>
        Duel,
        /// <summary>Free-for-all — every player for themselves.</summary>
        FreeForAll,
        /// <summary>Player(s) versus strong, aggressive neutrals.</summary>
        Pve,
        /// <summary>Even teams on a balanced layout.</summary>
        Team
    }

    /// <summary>Rough map size band; resolved to a concrete <see cref="GeneratorSettings.MapSize"/> within the band.</summary>
    public enum QuickMapScale { Small, Medium, Large }

    /// <summary>Desired game length — drives size, neutral count, guard strength and content density.</summary>
    public enum QuickGameLength { Short, Medium, Long }

    /// <summary>How much variance / how exotic the randomisation is allowed to get.</summary>
    public enum QuickChaos { Tame, Normal, Wild }

    /// <summary>
    /// The handful of player-facing options for Simple Mode / Quick Generate. The
    /// <see cref="Services.Generation.RandomTemplateBuilder"/> turns these (plus a <see cref="Seed"/>)
    /// into a full <see cref="GeneratorSettings"/> within safe, curated ranges.
    /// </summary>
    public class QuickGenerateOptions
    {
        public int PlayerCount { get; set; } = 2;
        public QuickGameType GameType { get; set; } = QuickGameType.FreeForAll;
        public QuickMapScale Scale { get; set; } = QuickMapScale.Medium;
        public QuickGameLength Length { get; set; } = QuickGameLength.Medium;
        public QuickChaos Chaos { get; set; } = QuickChaos.Normal;

        /// <summary>Add water borders between zones.</summary>
        public bool Water { get; set; }
        /// <summary>Add random teleport portals.</summary>
        public bool Portals { get; set; }
        /// <summary>Crank neutral guard strength + aggression (PvE-ish).</summary>
        public bool StrongNeutrals { get; set; }

        /// <summary>
        /// Win condition / game mode, as a <c>win_condition_N</c> id (the same set the Advanced tab
        /// exposes). The builder configures the matching rules and reconciles constraints
        /// (e.g. Tournament forces 2 players; City Hold guarantees a castle neutral). Default = classic.
        /// </summary>
        public string VictoryCondition { get; set; } = "win_condition_1";

        /// <summary>Deterministic seed driving every random choice. Assigned by the caller.</summary>
        public int Seed { get; set; }

        /// <summary>Optional explicit template name; when null/blank the builder generates a Latin one.</summary>
        public string? TemplateName { get; set; }
    }
}
