using System.Collections.Generic;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Curated quick-start configurations, focused on 2-player (1v1) Classic and Single-Hero play.
    /// Each preset is a fully-formed <see cref="SettingsFile"/> that can be loaded straight into the UI,
    /// and can also be converted to a <see cref="GeneratorSettings"/> for headless map generation.
    /// </summary>
    public static class Presets
    {
        public sealed record Preset(string Name, string Description, SettingsFile Settings)
        {
            /// <summary>Mode group, derived from the name prefix — used to organise the preset menu.</summary>
            public string Group =>
                  Name.StartsWith("1v1 Классика",   System.StringComparison.Ordinal) ? "1v1 — Классика"
                : Name.StartsWith("1v1 Один герой", System.StringComparison.Ordinal) ? "1v1 — Один герой"
                : Name.StartsWith("ПвЕ",            System.StringComparison.Ordinal) ? "ПвЕ — 1 против ИИ"
                : "FFA / мультиплеер";

            /// <summary>Name with the group prefix stripped, for compact display inside its submenu.</summary>
            public string ShortName
            {
                get
                {
                    int dash = Name.IndexOf('—');
                    return dash >= 0 ? Name[(dash + 1)..].Trim() : Name;
                }
            }

            /// <summary>Localization key for the group submenu header.</summary>
            public string GroupKey => Group switch
            {
                "1v1 — Классика"     => "S.PG.Classic",
                "1v1 — Один герой"   => "S.PG.SingleHero",
                "ПвЕ — 1 против ИИ"  => "S.PG.PvE",
                _                     => "S.PG.FFA",
            };

            /// <summary>Submenu display name in the requested language (EN map → else the RU short name).</summary>
            public string ShortNameLocalized(bool english) =>
                english && EnShort.TryGetValue(Name, out var en) ? en : ShortName;

            /// <summary>Tooltip description in the requested language (EN map → else the RU description).</summary>
            public string DescriptionLocalized(bool english) =>
                english && EnDesc.TryGetValue(Name, out var en) ? en : Description;
        }

        /// <summary>English tooltip descriptions, keyed by the (stable) Russian preset name.</summary>
        public static readonly Dictionary<string, string> EnDesc = new()
        {
            ["1v1 Классика — Дуэль (быстрая)"]          = "Small map, ring, minimal neutrals — a fast 2-player game.",
            ["1v1 Классика — Стандарт"]                 = "Medium balanced map with neutral zones of varying quality.",
            ["1v1 Классика — Богатые земли"]            = "More resources and structures, random terrain, aggressive neutrals.",
            ["1v1 Классика — Удержание города"]         = "Win by holding the central neutral town (City Hold).",
            ["1v1 Классика — Турнир"]                   = "1v1 tournament mode: isolated mirrored clusters, a series of battles.",
            ["1v1 Классика — Острова (вода)"]           = "Zones separated by water, random terrain — focus on scouting and portals.",
            ["1v1 Один герой — Блиц"]                   = "One hero per player, small map — very fast games.",
            ["1v1 Один герой — Дуэль"]                  = "One hero, medium map, balanced neutrals.",
            ["1v1 Один герой — Эпопея"]                 = "One hero, large map, many neutrals and aggressive monsters.",
            ["1v1 Классика — Хаб"]                      = "All zones around a shared central hub; players don't border directly.",
            ["1v1 Классика — Цепь"]                     = "Linear map: zones stretched in a chain from one player to the other.",
            ["1v1 Классика — Изоляция"]                 = "Players connected only through neutral zones — they meet only via the center.",
            ["1v1 Классика — Снежная"]                  = "Snowy terrain across the whole map.",
            ["1v1 Классика — Лавовая"]                  = "Scorched lava lands with aggressive monsters.",
            ["1v1 Классика — Пустыня"]                  = "Sandy open spaces, few obstacles.",
            ["1v1 Классика — Хардкор"]                  = "Strong guards, aggressive monsters, rugged terrain.",
            ["1v1 Классика — Мирная (эконом)"]          = "Passive monsters, weak guards, a fast economic start.",
            ["1v1 Классика — Два замка"]                = "Players start with two towns of their faction in their zone.",
            ["1v1 Один герой — Хаб"]                    = "One hero per player, central hub.",
            ["1v1 Один герой — Снежный блиц"]           = "One hero, small snowy map — very fast.",
            ["FFA 3 игрока — Классика"]                 = "Three each for themselves on a balanced map.",
            ["FFA 4 игрока — Классика"]                 = "Four on a medium-large balanced map.",
            ["FFA 4 игрока — Хаб"]                      = "Four around a shared central hub.",
            ["FFA 6 игроков — Кольцо"]                  = "Six players around a ring.",
            ["FFA 8 игроков — Большая"]                 = "Eight players on a large balanced map.",
            ["Король горы (4 игрока)"]                  = "Win by holding the central hub town. Four players.",
            ["Бойня — быстрая FFA (4)"]                 = "Small map, everything close, abundant resources — constant skirmishes.",
            ["ПвЕ — 1 против 2"]                        = "One player vs two computers. Balanced 3-side map.",
            ["ПвЕ — 1 против 3"]                        = "One player vs three computers. Balanced 4-side map.",
            ["ПвЕ — 1 против 4"]                        = "One player vs four computers. Five on a balanced map.",
            ["ПвЕ — 1 против 5"]                        = "One player vs five computers. Six on a large map.",
            ["ПвЕ — 1 против 6"]                        = "One player vs six computers. Seven on a large map.",
            ["ПвЕ — 1 против 7"]                        = "One player vs seven computers. Eight sides on the largest map.",
            ["1v1 Один герой — Турнир"]                 = "One hero, 1v1 tournament mode: a series of mirrored battles.",
            ["1v1 Один герой — Удержание города"]       = "One hero, win by holding the central neutral town.",
            ["1v1 Один герой — Острова"]                = "One hero, zones separated by water, with portals.",
            ["1v1 Один герой — Цепь"]                   = "One hero, linear chain map.",
            ["1v1 Классика — Сокровищница хаба"]        = "A rich, well-guarded central hub — a race for the center.",
            ["1v1 Классика — Порталы"]                  = "Random layout, players isolated, many portals between zones.",
            ["1v1 Классика — Мега-богатая (песочница)"] = "Maximum resources and structures, passive monsters, two starting towns.",
            ["1v1 Классика — Аскеза (выживание)"]       = "Few resources and structures, strong neutrals — a fight for every mine.",
            ["1v1 Классика — Глубокая вода"]            = "Wide water borders and portals — a naval map.",
            ["FFA 8 игроков — Хаб"]                     = "Eight players around one big central hub.",
        };

        /// <summary>English submenu names, keyed by the (stable) Russian preset name.</summary>
        public static readonly Dictionary<string, string> EnShort = new()
        {
            ["1v1 Классика — Дуэль (быстрая)"]          = "Duel (fast)",
            ["1v1 Классика — Стандарт"]                 = "Standard",
            ["1v1 Классика — Богатые земли"]            = "Rich lands",
            ["1v1 Классика — Удержание города"]         = "City hold",
            ["1v1 Классика — Турнир"]                   = "Tournament",
            ["1v1 Классика — Острова (вода)"]           = "Islands (water)",
            ["1v1 Один герой — Блиц"]                   = "Blitz",
            ["1v1 Один герой — Дуэль"]                  = "Duel",
            ["1v1 Один герой — Эпопея"]                 = "Epic",
            ["1v1 Классика — Хаб"]                      = "Hub",
            ["1v1 Классика — Цепь"]                     = "Chain",
            ["1v1 Классика — Изоляция"]                 = "Isolation",
            ["1v1 Классика — Снежная"]                  = "Snowy",
            ["1v1 Классика — Лавовая"]                  = "Lava",
            ["1v1 Классика — Пустыня"]                  = "Desert",
            ["1v1 Классика — Хардкор"]                  = "Hardcore",
            ["1v1 Классика — Мирная (эконом)"]          = "Peaceful (economy)",
            ["1v1 Классика — Два замка"]                = "Two towns",
            ["1v1 Один герой — Хаб"]                    = "Hub",
            ["1v1 Один герой — Снежный блиц"]           = "Snow blitz",
            ["FFA 3 игрока — Классика"]                 = "3 players — Classic",
            ["FFA 4 игрока — Классика"]                 = "4 players — Classic",
            ["FFA 4 игрока — Хаб"]                      = "4 players — Hub",
            ["FFA 6 игроков — Кольцо"]                  = "6 players — Ring",
            ["FFA 8 игроков — Большая"]                 = "8 players — Large",
            ["Король горы (4 игрока)"]                  = "King of the Hill (4 players)",
            ["Бойня — быстрая FFA (4)"]                 = "Massacre — fast FFA (4)",
            ["ПвЕ — 1 против 2"]                        = "1 vs 2",
            ["ПвЕ — 1 против 3"]                        = "1 vs 3",
            ["ПвЕ — 1 против 4"]                        = "1 vs 4",
            ["ПвЕ — 1 против 5"]                        = "1 vs 5",
            ["ПвЕ — 1 против 6"]                        = "1 vs 6",
            ["ПвЕ — 1 против 7"]                        = "1 vs 7",
            ["1v1 Один герой — Турнир"]                 = "Tournament",
            ["1v1 Один герой — Удержание города"]       = "City hold",
            ["1v1 Один герой — Острова"]                = "Islands",
            ["1v1 Один герой — Цепь"]                   = "Chain",
            ["1v1 Классика — Сокровищница хаба"]        = "Hub treasury",
            ["1v1 Классика — Порталы"]                  = "Portals",
            ["1v1 Классика — Мега-богатая (песочница)"] = "Mega-rich (sandbox)",
            ["1v1 Классика — Аскеза (выживание)"]       = "Asceticism (survival)",
            ["1v1 Классика — Глубокая вода"]            = "Deep water",
            ["FFA 8 игроков — Хаб"]                     = "8 players — Hub",
        };

        /// <summary>All built-in presets, in display order.</summary>
        public static readonly Preset[] All =
        [
            new("1v1 Классика — Дуэль (быстрая)",
                "Малая карта, кольцо, минимум нейтралов — быстрая партия на двоих.",
                new SettingsFile
                {
                    TemplateName = "1v1 Дуэль", PlayerCount = 2, MapSize = 96,
                    Topology = MapTopology.Default, NeutralZoneCount = 2, NeutralZoneCastles = 0,
                    PlayerZoneCastles = 1, HeroCountMin = 3, HeroCountMax = 6, HeroCountIncrement = 1,
                    Terrain = TerrainTheme.FactionBased,
                }),

            new("1v1 Классика — Стандарт",
                "Средняя сбалансированная карта с нейтральными зонами разного качества.",
                new SettingsFile
                {
                    TemplateName = "1v1 Стандарт", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2, NeutralHighNoCastleCount = 1,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                    Terrain = TerrainTheme.FactionBased,
                }),

            new("1v1 Классика — Богатые земли",
                "Больше ресурсов и построек, случайный ландшафт, агрессивные нейтралы.",
                new SettingsFile
                {
                    TemplateName = "1v1 Богатые земли", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighCastleCount = 2, NeutralHighNoCastleCount = 2,
                    PlayerZoneCastles = 1, ResourceDensityPercent = 140, StructureDensityPercent = 130,
                    NeutralStackStrengthPercent = 120, MonsterAggression = MonsterAggression.Aggressive,
                    Terrain = TerrainTheme.Random, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Удержание города",
                "Победа за удержание центрального нейтрального города (City Hold).",
                new SettingsFile
                {
                    TemplateName = "1v1 Удержание города", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowNoCastleCount = 2, NeutralMediumCastleCount = 2, NeutralHighCastleCount = 1,
                    PlayerZoneCastles = 1, VictoryCondition = "win_condition_5", CityHold = true, CityHoldDays = 7,
                    Terrain = TerrainTheme.FactionBased, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Турнир",
                "Турнирный режим 1v1: изолированные зеркальные кластеры, серия боёв.",
                new SettingsFile
                {
                    TemplateName = "1v1 Турнир", PlayerCount = 2, MapSize = 128,
                    Topology = MapTopology.Default, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2,
                    PlayerZoneCastles = 1, VictoryCondition = "win_condition_6",
                    Tournament = true, TournamentFirstTournamentDay = 14, TournamentInterval = 7,
                    TournamentPointsToWin = 2, TournamentSaveArmy = true,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Острова (вода)",
                "Зоны разделены водой, случайный ландшафт — упор на разведку и порталы.",
                new SettingsFile
                {
                    TemplateName = "1v1 Острова", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighNoCastleCount = 2,
                    PlayerZoneCastles = 1, WaterLevel = WaterLevel.Medium, Terrain = TerrainTheme.Random,
                    RandomPortals = true, MaxPortalConnections = 8,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Один герой — Блиц",
                "Один герой на игрока, малая карта — очень быстрые партии.",
                new SettingsFile
                {
                    TemplateName = "1v1 Блиц (один герой)", PlayerCount = 2, MapSize = 80,
                    SingleHeroMode = true, Topology = MapTopology.Default,
                    NeutralZoneCount = 1, NeutralZoneCastles = 0, PlayerZoneCastles = 1,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                    Terrain = TerrainTheme.FactionBased,
                }),

            new("1v1 Один герой — Дуэль",
                "Один герой, средняя карта, сбалансированные нейтралы.",
                new SettingsFile
                {
                    TemplateName = "1v1 Дуэль (один герой)", PlayerCount = 2, MapSize = 112,
                    SingleHeroMode = true, Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumNoCastleCount = 1,
                    PlayerZoneCastles = 1, HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                    Terrain = TerrainTheme.FactionBased,
                }),

            new("1v1 Один герой — Эпопея",
                "Один герой, большая карта, много нейтралов и агрессивные монстры.",
                new SettingsFile
                {
                    TemplateName = "1v1 Эпопея (один герой)", PlayerCount = 2, MapSize = 176,
                    SingleHeroMode = true, Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2, NeutralHighCastleCount = 2,
                    NeutralHighNoCastleCount = 2, PlayerZoneCastles = 1,
                    MonsterAggression = MonsterAggression.Aggressive, StructureDensityPercent = 120,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                    Terrain = TerrainTheme.Random,
                }),

            // ── More 1v1 variations: topologies ──
            new("1v1 Классика — Хаб",
                "Все зоны вокруг общего центрального хаба; игроки не граничат напрямую.",
                new SettingsFile
                {
                    TemplateName = "1v1 Хаб", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2,
                    HubZoneSize = 1.5, HubZoneCastles = 0, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Цепь",
                "Линейная карта: зоны вытянуты в цепь от одного игрока к другому.",
                new SettingsFile
                {
                    TemplateName = "1v1 Цепь", PlayerCount = 2, MapSize = 128,
                    Topology = MapTopology.Chain, AdvancedMode = true,
                    NeutralLowCastleCount = 1, NeutralMediumCastleCount = 1, NeutralHighCastleCount = 1,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Изоляция",
                "Игроки соединены только через нейтральные зоны — встреча лишь через центр.",
                new SettingsFile
                {
                    TemplateName = "1v1 Изоляция", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Default, NoDirectPlayerConn = true, AdvancedMode = true,
                    MinNeutralZonesBetweenPlayers = 2,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            // ── More 1v1 variations: terrain themes ──
            new("1v1 Классика — Снежная",
                "Заснеженный ландшафт по всей карте.",
                new SettingsFile
                {
                    TemplateName = "1v1 Снежная", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighNoCastleCount = 2, PlayerZoneCastles = 1,
                    Terrain = TerrainTheme.Snow, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Лавовая",
                "Выжженные лавовые земли с агрессивными монстрами.",
                new SettingsFile
                {
                    TemplateName = "1v1 Лавовая", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighCastleCount = 1, PlayerZoneCastles = 1,
                    Terrain = TerrainTheme.Lava, MonsterAggression = MonsterAggression.Aggressive,
                    LakeAmountPercent = 50,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Пустыня",
                "Песчаные открытые пространства, мало препятствий.",
                new SettingsFile
                {
                    TemplateName = "1v1 Пустыня", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.Random, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2, PlayerZoneCastles = 1,
                    Terrain = TerrainTheme.Sand, TerrainRoughnessPercent = 60, LakeAmountPercent = 40,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            // ── More 1v1 variations: difficulty / economy ──
            new("1v1 Классика — Хардкор",
                "Сильная охрана, агрессивные монстры, изрезанный ландшафт.",
                new SettingsFile
                {
                    TemplateName = "1v1 Хардкор", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighCastleCount = 2, PlayerZoneCastles = 1,
                    MonsterAggression = MonsterAggression.Aggressive,
                    NeutralStackStrengthPercent = 150, BorderGuardStrengthPercent = 130,
                    TerrainRoughnessPercent = 160, ResourceDensityPercent = 70, GuardRandomization = 0.15,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Мирная (эконом)",
                "Пассивные монстры, слабая охрана, быстрый экономический старт.",
                new SettingsFile
                {
                    TemplateName = "1v1 Мирная", PlayerCount = 2, MapSize = 128,
                    Topology = MapTopology.Default, AdvancedMode = true,
                    NeutralLowNoCastleCount = 2, NeutralMediumNoCastleCount = 2, PlayerZoneCastles = 1,
                    MonsterAggression = MonsterAggression.Passive,
                    NeutralStackStrengthPercent = 60, BorderGuardStrengthPercent = 60,
                    NeutralDiplomacyModifier = 0.0, ResourceDensityPercent = 150, StructureDensityPercent = 130,
                    EncounterHoles = true,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Два замка",
                "Игроки стартуют с двумя замками своей фракции в зоне.",
                new SettingsFile
                {
                    TemplateName = "1v1 Два замка", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighCastleCount = 1,
                    PlayerZoneCastles = 2, PlayerStartsWithCastles = true, MatchPlayerCastleFactions = true,
                    HeroCountMin = 5, HeroCountMax = 10, HeroCountIncrement = 2,
                }),

            new("1v1 Один герой — Хаб",
                "Один герой на игрока, центральный хаб.",
                new SettingsFile
                {
                    TemplateName = "1v1 Хаб (один герой)", PlayerCount = 2, MapSize = 128,
                    SingleHeroMode = true, Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralLowCastleCount = 2, HubZoneSize = 1.5, PlayerZoneCastles = 1,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            new("1v1 Один герой — Снежный блиц",
                "Один герой, малая заснеженная карта — очень быстро.",
                new SettingsFile
                {
                    TemplateName = "1v1 Снежный блиц (один герой)", PlayerCount = 2, MapSize = 96,
                    SingleHeroMode = true, Topology = MapTopology.Default,
                    NeutralZoneCount = 2, NeutralZoneCastles = 0, PlayerZoneCastles = 1,
                    Terrain = TerrainTheme.Snow, HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            // ── FFA / multiplayer ──
            new("FFA 3 игрока — Классика",
                "Трое каждый сам за себя на сбалансированной карте.",
                new SettingsFile
                {
                    TemplateName = "FFA 3", PlayerCount = 3, MapSize = 160,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 3, NeutralMediumCastleCount = 3, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("FFA 4 игрока — Классика",
                "Четверо на средне-большой сбалансированной карте.",
                new SettingsFile
                {
                    TemplateName = "FFA 4", PlayerCount = 4, MapSize = 176,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 4, NeutralMediumCastleCount = 4, NeutralHighNoCastleCount = 2,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("FFA 4 игрока — Хаб",
                "Четверо вокруг общего центрального хаба.",
                new SettingsFile
                {
                    TemplateName = "FFA 4 Хаб", PlayerCount = 4, MapSize = 176,
                    Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralMediumCastleCount = 4, HubZoneSize = 2.0, HubZoneCastles = 1, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("FFA 6 игроков — Кольцо",
                "Шестеро игроков по кольцу.",
                new SettingsFile
                {
                    TemplateName = "FFA 6 Кольцо", PlayerCount = 6, MapSize = 192,
                    Topology = MapTopology.Default, AdvancedMode = true,
                    NeutralLowCastleCount = 6, NeutralMediumNoCastleCount = 6, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("FFA 8 игроков — Большая",
                "Восемь игроков на большой сбалансированной карте.",
                new SettingsFile
                {
                    TemplateName = "FFA 8", PlayerCount = 8, MapSize = 208,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 4, NeutralMediumNoCastleCount = 4, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            // ── Special modes ──
            new("Король горы (4 игрока)",
                "Победа за удержание центрального города-хаба. Четыре игрока.",
                new SettingsFile
                {
                    TemplateName = "Король горы 4", PlayerCount = 4, MapSize = 176,
                    Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralMediumCastleCount = 4, HubZoneSize = 2.0, HubZoneCastles = 1, PlayerZoneCastles = 1,
                    VictoryCondition = "win_condition_5", CityHold = true, CityHoldDays = 7,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("Бойня — быстрая FFA (4)",
                "Малая карта, всё рядом, изобилие ресурсов — постоянные стычки.",
                new SettingsFile
                {
                    TemplateName = "Бойня 4", PlayerCount = 4, MapSize = 112,
                    Topology = MapTopology.Random, NeutralZoneCount = 4, NeutralZoneCastles = 0,
                    PlayerZoneCastles = 1, MonsterAggression = MonsterAggression.Passive,
                    ResourceDensityPercent = 160, StructureDensityPercent = 140,
                    HeroCountMin = 5, HeroCountMax = 10, HeroCountIncrement = 2,
                }),

            // ── PvE: one player vs N computers (1×N) ──
            new("ПвЕ — 1 против 2",
                "Один игрок против двух компьютеров. Сбалансированная карта на троих.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 2", PlayerCount = 3, MapSize = 160,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 3, NeutralMediumCastleCount = 3, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("ПвЕ — 1 против 3",
                "Один игрок против трёх компьютеров. Сбалансированная карта на четверых.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 3", PlayerCount = 4, MapSize = 176,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 4, NeutralMediumCastleCount = 4, NeutralHighNoCastleCount = 2,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("ПвЕ — 1 против 4",
                "Один игрок против четырёх компьютеров. Пятеро на сбалансированной карте.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 4", PlayerCount = 5, MapSize = 184,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 5, NeutralMediumCastleCount = 5, NeutralHighNoCastleCount = 2,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("ПвЕ — 1 против 5",
                "Один игрок против пяти компьютеров. Шестеро на большой карте.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 5", PlayerCount = 6, MapSize = 192,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 6, NeutralMediumCastleCount = 6, NeutralHighNoCastleCount = 3,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("ПвЕ — 1 против 6",
                "Один игрок против шести компьютеров. Семеро на большой карте.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 6", PlayerCount = 7, MapSize = 200,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 7, NeutralMediumCastleCount = 6, NeutralHighNoCastleCount = 3,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("ПвЕ — 1 против 7",
                "Один игрок против семи компьютеров. Восемь сторон на самой большой карте.",
                new SettingsFile
                {
                    TemplateName = "ПвЕ 1 против 7", PlayerCount = 8, MapSize = 208,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowCastleCount = 8, NeutralMediumCastleCount = 6, NeutralHighNoCastleCount = 4,
                    PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            // ── More Single-Hero 1v1 variations ──
            new("1v1 Один герой — Турнир",
                "Один герой, турнирный режим 1v1: серия зеркальных боёв.",
                new SettingsFile
                {
                    TemplateName = "1v1 Турнир (один герой)", PlayerCount = 2, MapSize = 112,
                    SingleHeroMode = true, Topology = MapTopology.Default, AdvancedMode = true,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2,
                    VictoryCondition = "win_condition_6", Tournament = true,
                    TournamentFirstTournamentDay = 10, TournamentInterval = 6,
                    TournamentPointsToWin = 2, TournamentSaveArmy = true,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            new("1v1 Один герой — Удержание города",
                "Один герой, победа за удержание центрального нейтрального города.",
                new SettingsFile
                {
                    TemplateName = "1v1 Удержание (один герой)", PlayerCount = 2, MapSize = 128,
                    SingleHeroMode = true, Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowNoCastleCount = 2, NeutralMediumCastleCount = 2, NeutralHighCastleCount = 1,
                    VictoryCondition = "win_condition_5", CityHold = true, CityHoldDays = 6,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            new("1v1 Один герой — Острова",
                "Один герой, зоны разделены водой, есть порталы.",
                new SettingsFile
                {
                    TemplateName = "1v1 Острова (один герой)", PlayerCount = 2, MapSize = 128,
                    SingleHeroMode = true, Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighNoCastleCount = 2,
                    WaterLevel = WaterLevel.Medium, Terrain = TerrainTheme.Random,
                    RandomPortals = true, MaxPortalConnections = 8,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            new("1v1 Один герой — Цепь",
                "Один герой, линейная карта-цепь.",
                new SettingsFile
                {
                    TemplateName = "1v1 Цепь (один герой)", PlayerCount = 2, MapSize = 112,
                    SingleHeroMode = true, Topology = MapTopology.Chain, AdvancedMode = true,
                    NeutralLowCastleCount = 1, NeutralMediumCastleCount = 1, NeutralHighCastleCount = 1,
                    HeroCountMin = 1, HeroCountMax = 1, HeroCountIncrement = 1,
                }),

            // ── More Classic 1v1 variations ──
            new("1v1 Классика — Сокровищница хаба",
                "Богатый, хорошо охраняемый центральный хаб — гонка за центр.",
                new SettingsFile
                {
                    TemplateName = "1v1 Сокровищница хаба", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, HubZoneSize = 2.0, HubZoneCastles = 1,
                    StructureDensityPercent = 140, NeutralStackStrengthPercent = 140,
                    MonsterAggression = MonsterAggression.Aggressive, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Порталы",
                "Случайная раскладка, игроки изолированы, много порталов между зонами.",
                new SettingsFile
                {
                    TemplateName = "1v1 Порталы", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.Random, NoDirectPlayerConn = true, AdvancedMode = true,
                    MinNeutralZonesBetweenPlayers = 1,
                    NeutralLowCastleCount = 2, NeutralMediumCastleCount = 2,
                    RandomPortals = true, MaxPortalConnections = 16, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Мега-богатая (песочница)",
                "Максимум ресурсов и построек, пассивные монстры, два стартовых замка.",
                new SettingsFile
                {
                    TemplateName = "1v1 Мега-богатая", PlayerCount = 2, MapSize = 176,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighCastleCount = 2,
                    ResourceDensityPercent = 200, StructureDensityPercent = 180,
                    MonsterAggression = MonsterAggression.Passive, NeutralDiplomacyModifier = 0.2,
                    PlayerZoneCastles = 2, PlayerStartsWithCastles = true, MatchPlayerCastleFactions = true,
                    HeroCountMin = 5, HeroCountMax = 12, HeroCountIncrement = 2,
                }),

            new("1v1 Классика — Аскеза (выживание)",
                "Мало ресурсов и построек, сильные нейтралы — борьба за каждый рудник.",
                new SettingsFile
                {
                    TemplateName = "1v1 Аскеза", PlayerCount = 2, MapSize = 144,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralLowNoCastleCount = 2, NeutralMediumNoCastleCount = 2,
                    ResourceDensityPercent = 50, StructureDensityPercent = 60,
                    NeutralStackStrengthPercent = 120, PlayerZoneCastles = 1,
                    HeroCountMin = 3, HeroCountMax = 6, HeroCountIncrement = 1,
                }),

            new("1v1 Классика — Глубокая вода",
                "Широкие водные границы и порталы — морская карта.",
                new SettingsFile
                {
                    TemplateName = "1v1 Глубокая вода", PlayerCount = 2, MapSize = 160,
                    Topology = MapTopology.Balanced, AdvancedMode = true,
                    NeutralMediumCastleCount = 2, NeutralHighNoCastleCount = 2,
                    WaterLevel = WaterLevel.Large, Terrain = TerrainTheme.Random,
                    RandomPortals = true, MaxPortalConnections = 12, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),

            // ── More multiplayer ──
            new("FFA 8 игроков — Хаб",
                "Восемь игроков вокруг одного большого центрального хаба.",
                new SettingsFile
                {
                    TemplateName = "FFA 8 Хаб", PlayerCount = 8, MapSize = 208,
                    Topology = MapTopology.HubAndSpoke, AdvancedMode = true,
                    NeutralMediumNoCastleCount = 8, HubZoneSize = 2.5, HubZoneCastles = 1, PlayerZoneCastles = 1,
                    HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1,
                }),
        ];

        /// <summary>
        /// Converts a persisted <see cref="SettingsFile"/> into a <see cref="GeneratorSettings"/> for
        /// headless generation. Mirrors the UI's BuildSettings mapping and seeds each player zone with the
        /// standard guarded mines so generated maps have a healthy economy even without the editor open.
        /// </summary>
        public static GeneratorSettings ToGeneratorSettings(SettingsFile s)
        {
            // The generator reads neutral zones ONLY from the per-tier counts. A "simple" preset that
            // uses NeutralZoneCount must be converted to medium-tier zones first (the editor does the
            // same on load), otherwise the map would generate with zero neutral zones.
            int lowNo = s.NeutralLowNoCastleCount, lowC = s.NeutralLowCastleCount;
            int medNo = s.NeutralMediumNoCastleCount, medC = s.NeutralMediumCastleCount;
            int highNo = s.NeutralHighNoCastleCount, highC = s.NeutralHighCastleCount;
            if (!s.AdvancedMode && s.NeutralZoneCount > 0
                && lowNo == 0 && lowC == 0 && medNo == 0 && medC == 0 && highNo == 0 && highC == 0)
            {
                if (s.NeutralZoneCastles > 0) medC = s.NeutralZoneCount;
                else medNo = s.NeutralZoneCount;
            }

            return new GeneratorSettings
            {
            TemplateName = s.TemplateName,
            GameMode = "Classic",
            SingleHeroMode = s.SingleHeroMode,
            PlayerCount = s.PlayerCount,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = s.HeroCountMin,
                HeroCountMax = s.HeroCountMax,
                HeroCountIncrement = s.HeroCountIncrement,
            },
            MapSize = s.MapSize,
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = s.VictoryCondition,
                LostStartCity = s.LostStartCity,
                LostStartCityDay = s.LostStartCityDay,
                LostStartHero = s.LostStartHero,
                CityHold = s.CityHold,
                CityHoldDays = s.CityHoldDays,
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = s.NeutralZoneCount,
                PlayerZoneCastles = s.PlayerZoneCastles,
                NeutralZoneCastles = s.NeutralZoneCastles,
                ResourceDensityPercent = s.EffectiveResourceDensityPercent,
                StructureDensityPercent = s.EffectiveStructureDensityPercent,
                NeutralStackStrengthPercent = s.NeutralStackStrengthPercent,
                BorderGuardStrengthPercent = s.BorderGuardStrengthPercent,
                HubZoneSize = s.HubZoneSize,
                HubZoneCastles = s.HubZoneCastles,
                Advanced = new AdvancedSettings
                {
                    Enabled = s.AdvancedMode,
                    NeutralLowNoCastleCount = lowNo,
                    NeutralLowCastleCount = lowC,
                    NeutralMediumNoCastleCount = medNo,
                    NeutralMediumCastleCount = medC,
                    NeutralHighNoCastleCount = highNo,
                    NeutralHighCastleCount = highC,
                    PlayerZoneSize = s.AdvancedMode ? s.PlayerZoneSize : 1.0,
                    NeutralZoneSize = s.AdvancedMode ? s.NeutralZoneSize : 1.0,
                    GuardRandomization = s.GuardRandomization,
                },
            },
            MinNeutralZonesBetweenPlayers = s.AdvancedMode ? s.MinNeutralZonesBetweenPlayers : 0,
            MatchPlayerCastleFactions = s.MatchPlayerCastleFactions,
            PlayerStartsWithCastles = s.PlayerStartsWithCastles,
            NoDirectPlayerConnections = s.NoDirectPlayerConn,
            RandomPortals = s.RandomPortals,
            MaxPortalConnections = s.MaxPortalConnections,
            SpawnRemoteFootholds = s.SpawnRemoteFootholds,
            GenerateRoads = s.GenerateRoads,
            Topology = s.Topology,
            Terrain = s.Terrain,
            MonsterAggression = s.MonsterAggression,
            WaterLevel = s.WaterLevel,
            NeutralDiplomacyModifier = s.NeutralDiplomacyModifier,
            EncounterHoles = s.EncounterHoles,
            TerrainRoughnessPercent = s.TerrainRoughnessPercent,
            LakeAmountPercent = s.LakeAmountPercent,
            FactionLawsExpPercent = s.FactionLawsExpPercent,
            AstrologyExpPercent = s.AstrologyExpPercent,
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = s.GladiatorArena,
                DaysDelayStart = s.GladiatorArenaDaysDelayStart,
                CountDay = s.GladiatorArenaCountDay,
            },
            TournamentRules = new TournamentRules
            {
                Enabled = s.Tournament,
                FirstTournamentDay = s.TournamentFirstTournamentDay,
                Interval = s.TournamentInterval,
                PointsToWin = s.TournamentPointsToWin,
                SaveArmy = s.TournamentSaveArmy,
            },
            PlayerZoneMandatoryContent = DefaultPlayerMines(),
            };
        }

        /// <summary>Standard guarded resource mines anchored near the player castle (Wood, Ore, Gold).</summary>
        private static List<ContentItem> DefaultPlayerMines() =>
        [
            ContentItemBuilder.Create(ContentIds.MineWood.Sid).WithName("name_mine_wood").Mine().Guarded().Build(),
            ContentItemBuilder.Create(ContentIds.MineOre.Sid).WithName("name_mine_ore").Mine().Guarded().Build(),
            ContentItemBuilder.Create(ContentIds.MineGold.Sid).WithName("name_mine_gold").Mine().Guarded().Build(),
        ];
    }
}
