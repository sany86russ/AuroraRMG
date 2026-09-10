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
        Team,
        /// <summary>Parallel non-crossing lanes (bronze→silver→gold) that meet only at a shared central arena.</summary>
        Lanes,
        /// <summary>
        /// Experimental solo PvE — exactly ONE player (only your hero(es) roam; no AI opponent), the rest of
        /// the map is neutral to explore, and victory is capturing &amp; holding a specific neutral city. The
        /// closest an RMG template gets to an RPG-style single-player scenario. The RMG format has never
        /// shipped a 1-spawn template, so the game must be tested to confirm it launches such a map.
        /// </summary>
        Solo
    }

    /// <summary>Rough map size band; resolved to a concrete <see cref="GeneratorSettings.MapSize"/> within the band.</summary>
    /// <remarks><see cref="Huge"/> reaches into the experimental sizes above the official 240×240 cap
    /// (≈256–400) — the game engine handles them, but they aren't backed by official templates.</remarks>
    public enum QuickMapScale { Small, Medium, Large, Huge }

    /// <summary>Desired game length — drives size, neutral count, guard strength and content density.</summary>
    public enum QuickGameLength { Short, Medium, Long }

    /// <summary>How much variance / how exotic the randomisation is allowed to get.</summary>
    public enum QuickChaos { Tame, Normal, Wild }

    /// <summary>
    /// Strength of the monster guards on the passages between zones (the "borderland" gate). Higher
    /// levels keep aggressive opponents — human or AI — penned in their own zones longer, acting as a
    /// "face-control" against early rushes. <see cref="Normal"/> reproduces the historical band so a
    /// given seed's map is byte-identical to maps made before this control existed. <see cref="Impassable"/>
    /// uses the historical 300–500% band; Extreme sets exactly 800% regardless of chaos.
    /// </summary>
    public enum QuickGuardLevel { Weak, Normal, Strong, Fortress, Impassable, Extreme }

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
        /// Strength of the guards on zone borders (the gate between zones). Lets the player set how hard
        /// it is to cross into a neighbouring zone — a stronger gate keeps an aggressive opponent out
        /// longer. Default <see cref="QuickGuardLevel.Normal"/> keeps the historical border-guard band.
        /// </summary>
        public QuickGuardLevel BorderGuards { get; set; } = QuickGuardLevel.Normal;

        /// <summary>
        /// Win condition / game mode, as a <c>win_condition_N</c> id (the same set the Advanced tab
        /// exposes). The builder configures the matching rules and reconciles constraints
        /// (e.g. Tournament forces 2 players; City Hold guarantees a castle neutral). Default = classic.
        /// </summary>
        public string VictoryCondition { get; set; } = "win_condition_1";

        /// <summary>
        /// Optional fixed faction for the (unowned, capturable) castles placed in neutral zones. Empty
        /// or "Random" = the engine's random faction (historical behaviour → byte-identical seeds). One of
        /// Human/Undead/Dungeon/Nature/Demon/Unfrozen pins every neutral castle to that faction — a full
        /// town of that faction that stays neutral until captured. Ideal for co-op "lanes" maps where
        /// friends want the town they capture to be a chosen faction.
        /// </summary>
        public string NeutralCastleFaction { get; set; } = "";

        /// <summary>Deterministic seed driving every random choice. Assigned by the caller.</summary>
        public int Seed { get; set; }

        /// <summary>Optional explicit template name; when null/blank the builder generates a Latin one.</summary>
        public string? TemplateName { get; set; }
    }
}
