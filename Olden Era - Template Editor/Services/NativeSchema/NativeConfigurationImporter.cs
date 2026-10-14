using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Models.NativeSchema;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>
/// Maps verified native input fields to Aurora controls without reproducing native geometry or its RNG.
/// Missing optional fields preserve the caller's values; unmapped inputs remain in the detached raw result.
/// </summary>
public static class NativeConfigurationImporter
{
    private static readonly HashSet<string> InputFields = new(StringComparer.Ordinal)
    {
        "flags", "size", "spawnCount", "richness", "difficulty", "heroCountMin", "heroCountMax",
        "finalBattleDay", "tournamentDays", "singleHero", "symmetrical", "classicWinCon", "holdCapitalWinCon",
        "holdCityWinCon", "startingHeroWinCon", "finalBattleWinCon", "tournamentWinCon"
    };

    public static NativeConfigurationImport Import(NativeGeneratorConfiguration configuration,
        GeneratorSettings currentSettings, QuickGenerateOptions? currentQuickOptions = null,
        NativeSchemaDocument? schema = null)
    {
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(currentSettings);
        NativeConfigurationCodec.Validate(configuration);
        var flags = configuration.Flags;
        var specialBattle = flags & (NativeSchemaFlags.FinalBattleWinCon | NativeSchemaFlags.TournamentWinCon);
        if (specialBattle != 0 && (specialBattle == (NativeSchemaFlags.FinalBattleWinCon | NativeSchemaFlags.TournamentWinCon)
            || (flags & (NativeSchemaFlags.ClassicWinCon | NativeSchemaFlags.HoldCapitalWinCon | NativeSchemaFlags.HoldCityWinCon)) != 0))
            throw new NotSupportedException("Native Final Battle/Tournament cannot be combined with Classic, Capital Hold, City Hold or each other.");
        if (specialBattle != 0 && configuration.Players != 2)
            throw new NotSupportedException("The verified native Final Battle/Tournament import requires two individual opposing players.");
        if ((flags & (NativeSchemaFlags.ClassicWinCon | NativeSchemaFlags.HoldCapitalWinCon | NativeSchemaFlags.HoldCityWinCon
            | NativeSchemaFlags.StartingHeroWinCon | NativeSchemaFlags.FinalBattleWinCon | NativeSchemaFlags.TournamentWinCon)) == 0)
            throw new NotSupportedException("Native inputs do not select a supported victory condition.");

        var settings = SettingsSnapshotMapper.Clone(currentSettings);
        var quick = Copy(currentQuickOptions ?? new QuickGenerateOptions());
        var warnings = new List<NativeImportWarning>();
        var profile = settings.ProfileOptions ??= new();
        profile.ProfileId = GenerationProfileOptions.CurrentProfile;
        if (configuration.Raw["richness"] is not null) profile.Richness = (MapRichness)configuration.Richness;
        if (configuration.Raw["difficulty"] is not null) profile.Challenge = (MapChallenge)configuration.Difficulty;
        profile.Symmetry = flags.HasFlag(NativeSchemaFlags.Symmetrical) ? MapSymmetry.EqualStarts : MapSymmetry.Asymmetric;
        if (flags.HasFlag(NativeSchemaFlags.Symmetrical))
            Warn("SymmetryIsAurora", "Native symmetry is represented by equal Aurora starts; the native geometry is not reproduced.", "flags.Symmetrical");
        if (flags.HasFlag(NativeSchemaFlags.RandomTeams))
            Warn("RandomTeamsUnmapped", "Native random-team allocation is not verified. Existing explicit alliances are preserved.", "flags.RandomTeams");
        if (profile.Alliances?.Enabled == true)
            Warn("ExistingAlliances", "Native inputs do not encode an explicit alliance roster. Existing Aurora alliances are preserved.", "spawnInfos");
        if (specialBattle != 0 && profile.Alliances?.Enabled == true)
            throw new NotSupportedException("Disable the existing Aurora alliances before importing the two-player native battle mode.");

        settings.MapSize = configuration.MapSize; settings.PlayerCount = configuration.Players;
        if (settings.MapSize > 256)
            Warn("ExperimentalSize", "Imported dimensions exceed the verified current 256 size and need game validation.", "size");
        settings.SingleHeroMode = flags.HasFlag(NativeSchemaFlags.SingleHero);
        settings.GameMode = settings.SingleHeroMode ? "SingleHero" : "Classic";
        settings.HeroSettings ??= new();
        if (configuration.HeroCountMin is { } minimum) settings.HeroSettings.HeroCountMin = minimum;
        if (configuration.HeroCountMax is { } maximum) settings.HeroSettings.HeroCountMax = maximum;
        if (settings.HeroSettings.HeroCountMin < 1 || settings.HeroSettings.HeroCountMax < settings.HeroSettings.HeroCountMin)
            throw new ArgumentException("Imported and retained hero limits conflict. Import both valid limits or adjust the existing limits first.");
        if (!configuration.HeroCountMin.HasValue || !configuration.HeroCountMax.HasValue)
            Warn("HeroLimitsRetained", "Absent native hero limits preserve the existing Aurora limits.", "heroCountMin/heroCountMax");
        if (settings.HeroSettings.HeroCountIncrement != 1 || settings.PlayerStartsWithCastles && settings.ZoneCfg.PlayerZoneCastles > 1)
            Warn("HeroTownArithmetic", "Aurora's retained per-town hero increment or multiple start towns can change the native starting hire capacity.", "heroCountIncrement");

        var end = settings.GameEndConditions ??= new();
        end.LostStartCity = flags.HasFlag(NativeSchemaFlags.HoldCapitalWinCon);
        end.LostStartHero = flags.HasFlag(NativeSchemaFlags.StartingHeroWinCon);
        end.CityHold = flags.HasFlag(NativeSchemaFlags.HoldCityWinCon);
        if (end.LostStartCity) end.LostStartCityDay = 3;
        if (end.CityHold) end.CityHoldDays = 3;
        profile.ClassicVictoryEnabled = flags.HasFlag(NativeSchemaFlags.ClassicWinCon);
        end.VictoryCondition = flags.HasFlag(NativeSchemaFlags.TournamentWinCon) ? "win_condition_6"
            : flags.HasFlag(NativeSchemaFlags.FinalBattleWinCon) ? "win_condition_4"
            : end.CityHold ? "win_condition_5" : end.LostStartCity ? "win_condition_3"
            : end.LostStartHero ? "win_condition_8" : "win_condition_1";
        settings.TournamentRules ??= new(); settings.GladiatorArenaRules ??= new();
        settings.TournamentRules.Enabled = flags.HasFlag(NativeSchemaFlags.TournamentWinCon);
        settings.GladiatorArenaRules.Enabled = flags.HasFlag(NativeSchemaFlags.FinalBattleWinCon);
        profile.TournamentBattleDays = settings.TournamentRules.Enabled
            ? configuration.Raw["tournamentDays"]!.AsArray().Select(day => checked(day!.GetValue<int>() + 1)).ToList() : [];
        profile.UseNativeFinalBattleSchedule = settings.GladiatorArenaRules.Enabled;
        profile.FinalBattleDay = settings.GladiatorArenaRules.Enabled ? checked(configuration.Raw["finalBattleDay"]!.GetValue<int>() + 1) : null;
        if (settings.GladiatorArenaRules.Enabled) { settings.GladiatorArenaRules.DaysDelayStart = profile.FinalBattleDay!.Value; settings.GladiatorArenaRules.CountDay = 1; }
        if (settings.TournamentRules.Enabled) settings.TournamentRules.FirstTournamentDay = profile.TournamentBattleDays[0];
        if (schema is not null) ImportSchema(schema, profile, warnings);

        // Quick mode does not have exact dimension, single-hero or hero-limit fields. Do not guess a size band.
        quick.PlayerCount = settings.PlayerCount; quick.VictoryCondition = end.VictoryCondition;
        quick.ProfileOptions = profile.Clone();
        if (quick.GameType == QuickGameType.Solo || quick.GameType == QuickGameType.Duel && settings.PlayerCount != 2)
        { quick.GameType = QuickGameType.FreeForAll; Warn("QuickGameTypeAdjusted", "Quick game type was changed to FFA so it does not override the imported player count or victory.", "GameType"); }
        Warn("QuickUsesSizeBand", "Advanced settings retain the exact native size, hero mode and combined victory rules. Quick generation chooses its own size and heroes and a single primary victory; use Advanced to retain the imported inputs.", "QuickOptions");
        foreach (var property in configuration.Raw)
            if (!InputFields.Contains(property.Key))
                Warn("UnmappedInput", "The native field is preserved in raw inputs but has no verified Aurora mapping.", property.Key);
        return new(settings, quick, configuration.Raw.DeepClone().AsObject(), schema?.Root.DeepClone().AsObject(), warnings.ToArray());

        void Warn(string code, string message, string? field = null) => warnings.Add(new(code, message, field));
    }

    private static void ImportSchema(NativeSchemaDocument schema, GenerationProfileOptions profile, List<NativeImportWarning> warnings)
    {
        if (schema.Root["river"] is JsonObject river)
        {
            if (river["createRiverSystem"] is JsonValue enabled && enabled.TryGetValue<bool>(out var create)) profile.EnableRivers = create;
            if (river["tributarySpacing"] is JsonValue spacing && spacing.TryGetValue<double>(out var value))
            {
                if (!double.IsFinite(value) || value <= 0) throw new JsonException("Native river tributary spacing must be positive and finite.");
                profile.RiverTributarySpacing = value;
            }
        }
        var towns = (schema.Root["mainObjectClasses"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(item => item["type"]?.GetValue<string>() is "Spawn" or "City" or "AbandonedOutpost").ToArray();
        var zones = (schema.Root["zoneClasses"] as JsonArray ?? []).OfType<JsonObject>().ToArray();
        if (Uniform(towns, "enableWeeklyUnitIncrement", out bool weekly)
            && UniformArray(zones, "randomHireEnableWeeklyUnitIncrement", out bool randomWeekly) && weekly == randomWeekly)
            profile.EnableWeeklyUnitIncrement = weekly;
        else Warning("enableWeeklyUnitIncrement");
        if (Uniform(towns, "weekOneOnlyModificatorGrowth", out bool weekOne)) profile.WeekOneOnlyModificatorGrowth = weekOne;
        else Warning("weekOneOnlyModificatorGrowth");
        if (Uniform(towns, "initialUnitIncrement", out int initial) && initial >= 0
            && UniformArray(zones, "randomHireInitialUnitIncrement", out int randomInitial) && initial == randomInitial)
            profile.InitialUnitIncrement = initial;
        else Warning("initialUnitIncrement");
        warnings.Add(new("NativeSchemaRemainsOffline", "Native layout, role-specific budgets, selectors and unknown fields remain in the raw schema. Aurora does not reproduce the native generator's layout or random stream.", "schema"));
        void Warning(string field) => warnings.Add(new("SchemaGrowthNotUniform", "Missing or role-dependent growth tables cannot be represented by a single Aurora growth control; the existing control is preserved.", field));
    }

    private static bool Uniform<T>(IReadOnlyList<JsonObject> owners, string parameter, out T value) where T : IEquatable<T>
    {
        value = default!; bool first = true;
        foreach (var owner in owners)
        {
            if (!ConstantRows(owner, parameter, node => node is JsonValue json && json.TryGetValue<T>(out _), out var nodes)) return false;
            foreach (var node in nodes)
            {
                T candidate = node!.GetValue<T>();
                if (!first && !candidate.Equals(value)) return false;
                value = candidate; first = false;
            }
        }
        return !first;
    }

    private static bool UniformArray<T>(IReadOnlyList<JsonObject> owners, string parameter, out T value) where T : IEquatable<T>
    {
        value = default!; bool first = true;
        foreach (var owner in owners)
        {
            if (!ConstantRows(owner, parameter, node => node is JsonArray { Count: 7 } array
                && array.All(item => item is JsonValue json && json.TryGetValue<T>(out _)), out var nodes)) return false;
            foreach (var node in nodes)
                foreach (var item in node!.AsArray())
                {
                    T candidate = item!.GetValue<T>();
                    if (!first && !candidate.Equals(value)) return false;
                    value = candidate; first = false;
                }
        }
        return !first;
    }

    // Import only constants valid for every context. No sample RNG or missing zone quantities are guessed.
    private static bool ConstantRows(JsonObject owner, string parameter, Func<JsonNode?, bool> accepts, out JsonNode?[] values)
    {
        values = [];
        var rows = NativeSchemaDocument.ReadTable(owner, parameter);
        if (rows.Count == 0 || rows.Any(row => !NativeSchemaWeight.IsRepresentable(row.Weight) || row.Weight <= 0)) return false;
        var candidates = rows.Where(row => row.Weight > 0).ToArray();
        if (candidates.Length == 0 || candidates.Any(row => !accepts(row.Raw["value"]))) return false;
        if (!candidates.Any(row => row.IsFallback || Empty(row.Raw["conditionsAnd"]) && Empty(row.Raw["conditionsOr"]))) return false;
        values = candidates.Select(row => row.Raw["value"]).ToArray(); return true;
        static bool Empty(JsonNode? node) => node is null || node is JsonObject obj && obj.All(property => property.Key.StartsWith('_'));
    }

    private static T Copy<T>(T source) where T : class => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))
        ?? throw new InvalidOperationException("The current generator configuration could not be copied.");
}
