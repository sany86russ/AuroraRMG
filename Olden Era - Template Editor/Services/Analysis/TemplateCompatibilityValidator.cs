using System.Globalization;
using System.Text.Json;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Analysis;

public enum TemplateValidationMode { StrictExport, TolerantImport }
public enum TemplateIssueSeverity { Information, Warning, Error }

public sealed record TemplateValidationIssue(string Code, string Path, TemplateIssueSeverity Severity, string Message);

/// <summary>
/// Exact identifiers from a game-data snapshot. Null means that domain has not been inspected,
/// whereas an empty set means it was inspected and contains no entries. Identifiers are never trimmed.
/// </summary>
public sealed class TemplateReferenceCatalog
{
    public IReadOnlySet<string>? Pools { get; init; }
    public IReadOnlySet<string>? Lists { get; init; }
    public IReadOnlySet<string>? Layouts { get; init; }
    public IReadOnlySet<string>? Mandatory { get; init; }
    public IReadOnlySet<string>? CountLimits { get; init; }
    public IReadOnlySet<string>? Objects { get; init; }
    public IReadOnlySet<string>? CustomAI { get; init; }
    /// <summary>Exact configured reward indices for known objects with variant metadata; absent or empty entries impose no additional restriction.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<int>>? ObjectRewardVariantIndices { get; init; }
    public bool IsComplete { get; init; }
    public string? Source { get; init; }

    /// <summary>The editor's partial reference list is useful offline but cannot prove an SID absent from the game.</summary>
    public static TemplateReferenceCatalog FromKnownValues() => new()
    {
        Pools = KnownValues.GuardedContentPoolSids.Concat(KnownValues.UnguardedContentPoolSids)
            .Concat(KnownValues.ResourcesContentPoolSids).ToHashSet(StringComparer.Ordinal),
        Layouts = KnownValues.ZoneLayouts.ToHashSet(StringComparer.Ordinal),
        Mandatory = KnownValues.MandatoryContentNames.ToHashSet(StringComparer.Ordinal),
        CountLimits = KnownValues.ContentCountLimitNames.ToHashSet(StringComparer.Ordinal),
        Source = "Bundled reference identifiers (partial)",
    };
}

/// <summary>
/// Validates every variant and the rules it shares with the other variants without opening the game,
/// reading global settings, or mutating an imported template. Strict output errors become warnings
/// during import so older/custom templates remain inspectable. Static validity does not prove playability.
/// </summary>
public static class TemplateCompatibilityValidator
{
    public static IReadOnlyList<TemplateValidationIssue> Validate(RmgTemplate template,
        TemplateValidationMode mode = TemplateValidationMode.StrictExport,
        TemplateReferenceCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new Validation(template, mode, catalog ?? TemplateReferenceCatalog.FromKnownValues()).Run();
    }

    private sealed class Validation(RmgTemplate template, TemplateValidationMode mode, TemplateReferenceCatalog catalog)
    {
        private readonly List<TemplateValidationIssue> issues = [];
        private readonly HashSet<string> localPools = new(StringComparer.Ordinal);
        private readonly HashSet<string> localLists = new(StringComparer.Ordinal);
        private readonly HashSet<string> localLayouts = new(StringComparer.Ordinal);
        private readonly HashSet<string> localMandatory = new(StringComparer.Ordinal);
        private readonly HashSet<string> localLimits = new(StringComparer.Ordinal);
        private readonly List<HashSet<string>> variantSpawns = [];

        public IReadOnlyList<TemplateValidationIssue> Run()
        {
            if (string.IsNullOrWhiteSpace(template.Name)) Error("template.name", "name", "A template name is required.");
            Positive(template.SizeX, "sizeX"); Positive(template.SizeZ, "sizeZ");
            if (template.SizeX > 256 || template.SizeZ > 256)
                Warn("map.experimentalSize", "sizeX", "Sizes above 256 are experimental and need game validation.");
            Known(template.GameMode, KnownValues.GameModes, "gameMode", verified: true);
            ReadDefinitions();
            ValidateRules();
            if (template.Variants is not { Count: > 0 })
                Error("variants.empty", "variants", "At least one map variant is required.");
            else
                for (int i = 0; i < template.Variants.Count; i++)
                    if (template.Variants[i] is { } variant) ValidateVariant(variant, $"variants[{i}]");
                    else Error("node.null", $"variants[{i}]", "A variant cannot be null.");
            ValidateTeams();
            ValidateDescriptions();
            return issues;
        }

        private void ReadDefinitions()
        {
            AddNames(template.ZoneLayouts, z => z.Name, localLayouts, "zoneLayouts");
            AddNames(template.MandatoryContent, m => m.Name, localMandatory, "mandatoryContent");
            AddNames(template.ContentCountLimits, c => c.Name, localLimits, "contentCountLimits");
            ReadInline(template.ContentLists, localLists, "contentLists");
            ReadInline(template.ContentPools, localPools, "contentPools");

            for (int i = 0; i < (template.ZoneLayouts?.Count ?? 0); i++)
            {
                if (template.ZoneLayouts![i] is not { } layout) continue;
                string path = $"zoneLayouts[{i}]";
                Fraction(layout.ObstaclesFill, path + ".obstaclesFill");
                Fraction(layout.ObstaclesFillVoid, path + ".obstaclesFillVoid");
                Fraction(layout.LakesFill, path + ".lakesFill");
                Nonnegative(layout.MinLakeArea, path + ".minLakeArea");
                Nonnegative(layout.RoadClusterArea, path + ".roadClusterArea");
                Nonnegative(layout.ElevationClusterScale, path + ".elevationClusterScale");
                foreach (var elevation in layout.ElevationModes ?? [])
                    if (elevation is not null)
                    {
                        Nonnegative(elevation.Weight, path + ".elevationModes.weight");
                        Fraction(elevation.MinElevatedFraction, path + ".elevationModes.minElevatedFraction");
                        Fraction(elevation.MaxElevatedFraction, path + ".elevationModes.maxElevatedFraction");
                        Ordered(elevation.MinElevatedFraction, elevation.MaxElevatedFraction, path + ".elevationModes");
                    }
            }
            for (int i = 0; i < (template.MandatoryContent?.Count ?? 0); i++)
            {
                if (template.MandatoryContent![i] is not { } group) continue;
                for (int j = 0; j < (group.Content?.Count ?? 0); j++)
                    if (group.Content![j] is { } item)
                    {
                        string path = $"mandatoryContent[{i}].content[{j}]";
                        ObjectSid(item.Sid, path + ".sid");
                        ReferenceList(item.IncludeLists, localLists, catalog.Lists, path + ".includeLists");
                        if (item.Variant is < -1)
                            Error("number.variant", path + ".variant", "A content variant must be -1 (the native default) or nonnegative.");
                        ConfiguredRewardVariant(item.Sid, item.Variant, path + ".variant");
                        PlacementRules(item.Rules, path + ".rules", null);
                    }
            }
            for (int i = 0; i < (template.ContentCountLimits?.Count ?? 0); i++)
            {
                if (template.ContentCountLimits![i] is not { } limit) continue;
                string path = $"contentCountLimits[{i}]";
                Nonnegative(limit.PlayerMin, path + ".playerMin");
                Nonnegative(limit.PlayerMax, path + ".playerMax");
                Ordered(limit.PlayerMin, limit.PlayerMax, path);
                foreach (var entry in limit.Limits ?? [])
                    if (entry is not null)
                    {
                        ObjectSid(entry.Sid, path + ".limits.sid");
                        Nonnegative(entry.MaxCount, path + ".limits.maxCount");
                    }
            }
            foreach (var value in template.ValueOverrides ?? [])
                if (value is not null)
                {
                    ObjectSid(value.Sid, "valueOverrides.sid");
                    Nonnegative(value.GuardValue, "valueOverrides.guardValue");
                    if (value.Variant is < -1) Error("number.variant", "valueOverrides.variant", "An override variant must be -1 (all variants) or nonnegative.");
                }
        }

        private void AddNames<T>(List<T>? entries, Func<T, string> name, HashSet<string> target, string path) where T : class
        {
            if (entries is null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] is not { } entry) { Error("node.null", $"{path}[{i}]", "A definition cannot be null."); continue; }
                string id = name(entry);
                if (string.IsNullOrWhiteSpace(id)) Error("reference.emptyName", $"{path}[{i}].name", "A definition needs an exact nonempty identifier.");
                else if (!target.Add(id)) Error("reference.duplicate", $"{path}[{i}].name", $"Duplicate definition '{id}'.");
            }
        }

        private void ReadInline(List<object>? entries, HashSet<string> target, string path)
        {
            if (entries is null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] is null) { Error("node.null", $"{path}[{i}]", "An inline definition cannot be null."); continue; }
                JsonElement element = entries[i] is JsonElement json ? json : JsonSerializer.SerializeToElement(entries[i], JsonExport.Options);
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(name.GetString()))
                    Error("reference.emptyName", $"{path}[{i}].name", "An inline definition needs a nonempty name.");
                else if (!target.Add(name.GetString()!)) Error("reference.duplicate", $"{path}[{i}].name", $"Duplicate definition '{name.GetString()}'.");
            }
            // Once all names exist, references may point forwards in the same array.
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] is not null)
                {
                    JsonElement element = entries[i] is JsonElement json ? json : JsonSerializer.SerializeToElement(entries[i], JsonExport.Options);
                    ValidateInlineFields(element, $"{path}[{i}]");
                    ValidateInlineContentVariants(element, $"{path}[{i}]", path == "contentPools");
                }
        }

        private void ValidateInlineContentVariants(JsonElement definition, string path, bool isPool)
        {
            if (definition.ValueKind != JsonValueKind.Object) return;
            if (!isPool) { ContentVariants(definition, path); return; }
            if (!definition.TryGetProperty("groups", out var groups)) return;
            int groupIndex = 0;
            foreach (var group in Entries(groups)) ContentVariants(group, $"{path}.groups[{groupIndex++}]");

            void ContentVariants(JsonElement owner, string ownerPath)
            {
                if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty("content", out var content)) return;
                int itemIndex = 0;
                foreach (var item in Entries(content))
                {
                    string itemPath = $"{ownerPath}.content[{itemIndex++}].variant";
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("variant", out var variant) || variant.ValueKind == JsonValueKind.Null) continue;
                    if (variant.ValueKind != JsonValueKind.Number || !variant.TryGetInt32(out int index))
                        Error("content.variantType", itemPath, "A native content variant must be a 32-bit integer or null.");
                    else if (index < -1) Error("number.variant", itemPath, "A content variant must be -1 (the native default) or nonnegative.");
                    else if (item.TryGetProperty("sid", out var sid) && sid.ValueKind == JsonValueKind.String)
                        ConfiguredRewardVariant(sid.GetString(), index, itemPath);
                }
            }
            static IEnumerable<JsonElement> Entries(JsonElement node)
            {
                if (node.ValueKind == JsonValueKind.Array)
                    foreach (var item in node.EnumerateArray()) yield return item;
                else if (node.ValueKind == JsonValueKind.Object) yield return node;
            }
        }

        private void ValidateInlineFields(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                double weightSum = 0;
                float nativeWeightSum = 0;
                foreach (var item in element.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("weight", out var weightValue)
                        && weightValue.ValueKind == JsonValueKind.Number && weightValue.TryGetDouble(out double weight) && weight >= 0)
                    { weightSum += weight; nativeWeightSum += (float)weight; }
                if (!double.IsFinite(weightSum) || weightSum > float.MaxValue || !float.IsFinite(nativeWeightSum))
                    Error("pool.weightSum", path, "The sum of selection weights exceeds the native finite float range.");
                int index = 0;
                foreach (var item in element.EnumerateArray()) ValidateInlineFields(item, $"{path}[{index++}]");
            }
            else if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    string child = path + "." + property.Name;
                    if (property.Name == "includeLists")
                        ValidateInlineListReferences(property.Value, child);
                    else if (property.Name == "sid")
                    {
                        if (property.Value.ValueKind == JsonValueKind.String) ObjectSid(property.Value.GetString(), child);
                        else if (property.Value.ValueKind != JsonValueKind.Null) Error("reference.type", child, "An object SID must be a string.");
                    }
                    else if (property.Name == "weight")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out double weight))
                            Error("pool.weightType", child, "Selection weight must be a number.");
                        else
                        {
                            Nonnegative(weight, child);
                            if (weight > float.MaxValue) Error("pool.weightRange", child, "Selection weight exceeds the native finite float range.");
                            if ((float)weight == 0 && (weight > 0 || IsPositiveNonzeroNumber(property.Value)))
                                Error("pool.weightUnderflow", child, "A positive selection weight becomes zero in the native float array. Increase it or use an explicit zero.");
                        }
                    }
                    else ValidateInlineFields(property.Value, child);
                }
        }

        private static bool IsPositiveNonzeroNumber(JsonElement value)
        {
            string raw = value.GetRawText();
            if (raw.StartsWith("-", StringComparison.Ordinal)) return false;
            string significand = raw.Split(['e', 'E'])[0];
            return significand.Any(c => c is >= '1' and <= '9');
        }

        private void ValidateInlineListReferences(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Null) return;
            if (element.ValueKind == JsonValueKind.String)
            { Reference(element.GetString(), localLists, catalog.Lists, path, required: true); return; }
            if (element.ValueKind != JsonValueKind.Array)
            { Error("reference.type", path, "includeLists must be a string or an array of string identifiers."); return; }
            int index = 0;
            foreach (var entry in element.EnumerateArray())
            {
                string itemPath = $"{path}[{index++}]";
                if (entry.ValueKind != JsonValueKind.String) Error("reference.type", itemPath, "A list reference must be a string identifier.");
                else Reference(entry.GetString(), localLists, catalog.Lists, itemPath, required: true);
            }
        }

        private void ValidateVariant(Variant variant, string path)
        {
            var zones = (variant.Zones ?? []).Where(z => z is not null).ToList();
            var connections = (variant.Connections ?? []).Where(c => c is not null).ToList();
            if (zones.Count != (variant.Zones?.Count ?? 0) || connections.Count != (variant.Connections?.Count ?? 0))
                Error("node.null", path, "Zones and connections cannot contain null entries.");
            if (zones.Count == 0) Error("zones.empty", path + ".zones", "A map variant needs zones.");
            if (zones.Any(z => (z.MainObjects?.Any(o => o is null) ?? false) || (z.Roads?.Any(r => r is null) ?? false)))
                Error("node.null", path + ".zones", "Main objects and roads cannot contain null entries.");
            else
            {
                IReadOnlyList<Connection> graphConnections = connections;
                if (template.GameRules?.WinConditions?.Tournament == true)
                {
                    var tournamentStarts = zones.Where(z => z.MainObjects?.Any(o => o.Type == "Spawn" && IsPlayer(o.Spawn ?? "")) == true).ToList();
                    if (tournamentStarts.Count == 2)
                        graphConnections = [.. connections, new Connection { From = tournamentStarts[0].Name, To = tournamentStarts[1].Name, ConnectionType = "Direct" }];
                }
                foreach (string graphIssue in ZoneGraphValidator.Validate(zones, graphConnections))
                    Error("graph.invalid", path, graphIssue);
            }
            var zoneNames = zones.Select(z => z.Name).ToHashSet(StringComparer.Ordinal);
            var connectionNames = connections.Where(c => c.Name is not null).Select(c => c.Name!).ToHashSet(StringComparer.Ordinal);
            var spawns = new HashSet<string>(StringComparer.Ordinal);
            var objects = zones.SelectMany(z => z.MainObjects ?? []).Where(o => o is not null).ToList();
            foreach (var obj in objects)
                if (obj.Spawn is { Length: > 0 } spawn)
                {
                    if (IsPlayer(spawn)) spawns.Add(spawn);
                    else if (spawn != "None") Error("spawn.unknown", path + ".zones.mainObjects.spawn", $"Unknown player spawn '{spawn}'.");
                }
            variantSpawns.Add(spawns);
            if (variantSpawns.Count > 1 && !spawns.SetEquals(variantSpawns[0]))
                Error("variants.spawns", path, "All variants must provide the same player slots.");

            if (variant.Orientation is { } orientation)
            {
                if (!string.IsNullOrEmpty(orientation.ZeroAngleZone) && !zoneNames.Contains(orientation.ZeroAngleZone))
                    Error("reference.zone", path + ".orientation.zeroAngleZone", $"Unknown zone '{orientation.ZeroAngleZone}'.");
                Ordered(orientation.BaseAngleMin, orientation.BaseAngleMax, path + ".orientation");
                Nonnegative(orientation.RandomAngleAmplitude, path + ".orientation.randomAngleAmplitude");
                Nonnegative(orientation.RandomAngleStep, path + ".orientation.randomAngleStep");
            }
            if (variant.Border is { } border)
            {
                Nonnegative(border.CornerRadius, path + ".border.cornerRadius");
                Nonnegative(border.ObstaclesWidth, path + ".border.obstaclesWidth");
                Nonnegative(border.WaterWidth, path + ".border.waterWidth");
                foreach (var noise in (border.ObstaclesNoise ?? []).Concat(border.WaterNoise ?? []))
                    if (noise is not null) { Nonnegative(noise.Amp, path + ".border.noise.amp"); Nonnegative(noise.Freq, path + ".border.noise.freq"); }
            }
            if (variant.River is { } river)
            {
                Nonnegative(river.TributarySpacing, path + ".river.tributarySpacing");
                if (river.CreateRiverSystem == true && river.TributarySpacing is <= 0)
                    Error("river.spacing", path + ".river.tributarySpacing", "Enabled rivers require positive tributary spacing.");
            }
            for (int i = 0; i < zones.Count; i++) ValidateZone(zones[i], $"{path}.zones[{i}]", zoneNames, connectionNames, spawns);
            for (int i = 0; i < connections.Count; i++) ValidateConnection(connections[i], $"{path}.connections[{i}]", zoneNames);
            var win = template.GameRules?.WinConditions;
            if (win?.CityHold == true && !objects.Any(o => o.HoldCityWinCon == true && IsCity(o)))
                Error("win.cityHoldTarget", path, "City hold needs a City or Spawn marked holdCityWinCon in every variant.");
            if (win?.GladiatorArena == true && !objects.Any(o => o.Type == "GladiatorArena") && !connections.Any(c => c.ConnectionType == "GladiatorArena"))
                Error("win.arenaTarget", path, "Final battle needs a GladiatorArena object or connection in every variant.");
            if (win?.LostStartCity == true && !objects.Any(o => o.IsKeyObject == true && IsCity(o)))
                Warn("win.capitalUnverified", path, "Capital defeat is enabled without a key city; legacy start-city semantics need game verification.");
        }

        private void ValidateZone(Zone zone, string path, HashSet<string> names, HashSet<string> connectionNames, HashSet<string> spawns)
        {
            if (zone.Size.HasValue) Positive(zone.Size, path + ".size");
            Reference(zone.Layout, localLayouts, catalog.Layouts, path + ".layout");
            ReferenceList(zone.GuardedContentPool, localPools, catalog.Pools, path + ".guardedContentPool");
            ReferenceList(zone.UnguardedContentPool, localPools, catalog.Pools, path + ".unguardedContentPool");
            ReferenceList(zone.ResourcesContentPool, localPools, catalog.Pools, path + ".resourcesContentPool");
            ReferenceList(zone.MandatoryContent, localMandatory, catalog.Mandatory, path + ".mandatoryContent");
            ReferenceList(zone.ContentCountLimits, localLimits, catalog.CountLimits, path + ".contentCountLimits");
            Nonnegative(zone.GuardedContentValue, path + ".guardedContentValue");
            Nonnegative(zone.GuardedContentValuePerArea, path + ".guardedContentValuePerArea");
            Nonnegative(zone.UnguardedContentValue, path + ".unguardedContentValue");
            Nonnegative(zone.UnguardedContentValuePerArea, path + ".unguardedContentValuePerArea");
            Nonnegative(zone.ResourcesValue, path + ".resourcesValue");
            Nonnegative(zone.ResourcesValuePerArea, path + ".resourcesValuePerArea");
            foreach (var coefficient in new[] { zone.GuardedContentValuePerArea, zone.UnguardedContentValuePerArea, zone.ResourcesValuePerArea })
                if (coefficient is > 0 && template.SizeX > 0 && template.SizeZ > 0 && (long)template.SizeX * template.SizeZ > int.MaxValue / coefficient.Value)
                    Error("budget.overflowRisk", path, "This per-area coefficient can overflow the native integer product at the full-map area upper bound. Reduce density/coefficient or map size; actual eligible zone area is only known inside the game.");
            Nonnegative(zone.GuardCutoffValue, path + ".guardCutoffValue");
            Nonnegative(zone.GuardMultiplier, path + ".guardMultiplier");
            Fraction(zone.GuardRandomization, path + ".guardRandomization");
            Nonnegative(zone.GuardWeeklyIncrement, path + ".guardWeeklyIncrement");
            if (zone.DiplomacyModifier is { } diplomacy && (!double.IsFinite(diplomacy) || diplomacy < -1 || diplomacy > 1))
                Error("number.diplomacy", path + ".diplomacyModifier", "Diplomacy modifier must be finite and within -1…1.");
            if (zone.GuardReactionDistribution is { } distribution)
            {
                string reactionPath = path + ".guardReactionDistribution";
                if (distribution.Count != 6) Error("guard.reactionCount", reactionPath, "The six native guard reactions need six weights.");
                float nativeSum = 0;
                for (int i = 0; i < distribution.Count; i++)
                {
                    double weight = distribution[i];
                    Nonnegative(weight, $"{reactionPath}[{i}]");
                    float nativeWeight = (float)weight;
                    if (weight > 0 && nativeWeight == 0)
                        Error("guard.reactionUnderflow", $"{reactionPath}[{i}]", "A positive reaction weight becomes zero in the native float array. Increase it or use an explicit zero.");
                    nativeSum += nativeWeight;
                }
                if (!float.IsFinite(nativeSum)) Error("guard.reactionSum", reactionPath, "The reaction weight sum must fit the native finite float range.");
                if (distribution.Count > 0 && distribution.All(w => (float)w == 0))
                    Error("guard.reactionZero", reactionPath, "At least one native reaction weight must be positive.");
            }
            if (zone.EncounterHolesSettings is { } holes)
            { Fraction(holes.AffectedEncounters, path + ".encounterHolesSettings.affectedEncounters"); Fraction(holes.TwoHoleEncounters, path + ".encounterHolesSettings.twoHoleEncounters"); }
            foreach (int amount in zone.RandomHireInitialUnitIncrement ?? []) Nonnegative(amount, path + ".randomHireInitialUnitIncrement");
            // These arrays target native hire tiers. Their allowed length was not established, so no invented cardinality restriction.
            int[] lengths = [zone.RandomHireEnableWeeklyUnitIncrement?.Count ?? 0, zone.RandomHireEnableModificatorWeeklyUnitIncrement?.Count ?? 0, zone.RandomHireInitialUnitIncrement?.Count ?? 0];
            if (lengths.Where(n => n > 0).Distinct().Count() > 1)
                Warn("hire.arrayLengths", path, "Hire-growth arrays have different lengths; verify which tiers the game applies.");
            for (int i = 0; i < (zone.MainObjects?.Count ?? 0); i++)
                if (zone.MainObjects![i] is { } obj)
                {
                    string child = $"{path}.mainObjects[{i}]";
                    Known(obj.Type, KnownValues.MainObjectTypes, child + ".type", required: true, verified: true);
                    Known(obj.Placement, KnownValues.MainObjectPlacements, child + ".placement", verified: true);
                    Fraction(obj.GuardChance, child + ".guardChance");
                    Fraction(obj.GuardRandomization, child + ".guardRandomization");
                    Nonnegative(obj.GuardValue, child + ".guardValue");
                    Nonnegative(obj.GuardWeeklyIncrement, child + ".guardWeeklyIncrement");
                    Nonnegative(obj.InitialUnitIncrement, child + ".initialUnitIncrement");
                    if (obj.Owner is { Length: > 0 } owner && owner != "None" && !spawns.Contains(owner))
                        Error("spawn.owner", child + ".owner", $"Owner '{owner}' has no player start in this variant.");
                    if (obj.HoldCityWinCon == true && !IsCity(obj))
                        Error("win.holdNonCity", child + ".holdCityWinCon", "A City or Spawn is required for a city-hold objective.");
                    if (obj.Placement == "NearZone") RequiredTarget(obj.PlacementArgs, names, child + ".placementArgs", "zone");
                    if (obj.Placement == "Connection") RequiredTarget(obj.PlacementArgs, connectionNames, child + ".placementArgs", "connection");
                }
                else Error("node.null", $"{path}.mainObjects[{i}]", "A main object cannot be null.");
        }

        private void ValidateConnection(Connection connection, string path, HashSet<string> names)
        {
            Known(connection.ConnectionType, KnownValues.ConnectionTypes, path + ".connectionType", verified: true);
            Nonnegative(connection.GuardValue, path + ".guardValue");
            Nonnegative(connection.GuardWeeklyIncrement, path + ".guardWeeklyIncrement");
            Fraction(connection.GuardRandomization, path + ".guardRandomization");
            Nonnegative(connection.Length, path + ".length");
            if (connection.GuardZone is { Length: > 0 } zone && !names.Contains(zone))
                Error("reference.zone", path + ".guardZone", $"Unknown guard zone '{zone}'.");
            Known(connection.GuardReaction, ["Aggressive", "Negative", "Common", "Friendly", "Peaceful", "Docile"], path + ".guardReaction", verified: true);
            Known(connection.GatePlacement, ["Random", "Center", "NearZone"], path + ".gatePlacement", verified: true);
            if (connection.GatePlacement == "NearZone") RequiredTarget(connection.GatePlacementArgs, names, path + ".gatePlacementArgs", "zone");
            PlacementRules(connection.PortalPlacementRulesFrom, path + ".portalPlacementRulesFrom", names);
            PlacementRules(connection.PortalPlacementRulesTo, path + ".portalPlacementRulesTo", names);
            if (connection.ConnectionType == "Portal" && connection.PortalFromEnabled == false && connection.PortalToEnabled == false)
                Warn("portal.flags", path, "Both portal flags are disabled. Their effect on travel requires game verification.");
        }

        private void PlacementRules(List<ContentPlacementRule>? rules, string path, HashSet<string>? zones)
        {
            if (rules is null) return;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] is not { } rule) { Error("node.null", $"{path}[{i}]", "A placement rule cannot be null."); continue; }
                Nonnegative(rule.Weight, $"{path}[{i}].weight");
                Nonnegative(rule.TargetMin, $"{path}[{i}].targetMin");
                Nonnegative(rule.TargetMax, $"{path}[{i}].targetMax");
                Ordered(rule.TargetMin, rule.TargetMax, $"{path}[{i}]");
                if (zones is not null && rule.Type == "NearZone") RequiredTarget(rule.Args, zones, $"{path}[{i}].args", "zone");
            }
        }

        private void ValidateTeams()
        {
            if (template.MaxAlliancesCount is { } max && (max < 1 || max > 8))
                Error("alliance.max", "maxAlliancesCount", "Maximum alliances must be within 1…8.");
            if (template.SpawnInfos is null) return; // Legacy templates omit the alliance layer altogether.
            var players = variantSpawns.FirstOrDefault() ?? new HashSet<string>(StringComparer.Ordinal);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            var teams = new HashSet<int>();
            int independent = 0;
            for (int i = 0; i < template.SpawnInfos.Count; i++)
            {
                if (template.SpawnInfos[i] is not { } info) { Error("node.null", $"spawnInfos[{i}]", "A spawn assignment cannot be null."); continue; }
                string path = $"spawnInfos[{i}]";
                if (!IsPlayer(info.Spawn) || !players.Contains(info.Spawn))
                    Error("alliance.spawn", path + ".spawn", $"Assignment '{info.Spawn}' has no matching player start.");
                if (!declared.Add(info.Spawn)) Error("alliance.duplicateSpawn", path + ".spawn", $"Duplicate alliance assignment for '{info.Spawn}'.");
                if (info.AllianceIndex is null) Warn("alliance.unassigned", path + ".allianceIndex", "Alliance assignment omitted; native default must be checked.");
                else if (info.AllianceIndex == -1) independent++;
                else if (info.AllianceIndex <= 0 || info.AllianceIndex > 8)
                    Error("alliance.index", path + ".allianceIndex", "Use -1 for an independent player or a positive 1-based team index (1…8).");
                else
                {
                    teams.Add(info.AllianceIndex.Value);
                    if (template.MaxAlliancesCount is { } limit && info.AllianceIndex > limit)
                        Error("alliance.limit", path + ".allianceIndex", "Team index exceeds maxAlliancesCount.");
                }
            }
            foreach (string player in players.Except(declared)) Error("alliance.missingSpawn", "spawnInfos", $"Alliance assignment for '{player}' is missing.");
            if (template.MaxAlliancesCount is { } count && teams.Count + independent > count)
                Error("alliance.count", "maxAlliancesCount", "Team count including each independent player exceeds maxAlliancesCount.");
            if (players.Count > 1 && teams.Count == 1 && independent == 0 && template.SpawnInfos.All(s => s?.IsAllianceLocked == true))
                Warn("alliance.noOpponent", "spawnInfos", "All locked player slots belong to one alliance; there is no opposing player team.");
        }

        private void ValidateRules()
        {
            if (template.GameRules is not { } rules) return; // The game supplies defaults when rules are absent.
            Nonnegative(rules.HeroCountMin, "gameRules.heroCountMin");
            Nonnegative(rules.HeroCountMax, "gameRules.heroCountMax");
            Nonnegative(rules.HeroCountIncrement, "gameRules.heroCountIncrement");
            Ordered(rules.HeroCountMin, rules.HeroCountMax, "gameRules.heroCount");
            Nonnegative(rules.FactionLawsExpModifier, "gameRules.factionLawsExpModifier");
            Nonnegative(rules.AstrologyExpModifier, "gameRules.astrologyExpModifier");
            if (rules.HeroCountMax > 10) Warn("heroes.extendedLimit", "gameRules.heroCountMax", "Hero maximum exceeds the native Quick Start choices; verify this advanced profile in game.");
            if (template.GameMode == "SingleHero" && (rules.HeroCountMax > 1 || rules.HeroCountMin > 1))
                Error("heroes.singleHero", "gameRules.heroCountMax", "SingleHero conflicts with more than one starting/maximum hero.");
            if (rules.CustomAI is { Length: > 0 } ai)
            {
                if (catalog.CustomAI is null) Warn("ai.unverified", "gameRules.customAI", "Custom AI support has not been established for this data snapshot.");
                else if (!catalog.CustomAI.Contains(ai)) ReferenceIssue("gameRules.customAI", ai);
            }
            if (rules.DisableMagicGuild == true && rules.DisableMagicCustomLearning == true)
                Warn("magic.learningDisabled", "gameRules", "Both guild and custom spell learning are disabled; ensure the remaining content fits the intended rules.");
            if (rules.WinConditions is not { } win) return;
            if (win.Classic == false && win.CityHold != true && win.GladiatorArena != true && win.Tournament != true
                && win.LostStartCity != true && win.LostStartHero != true)
                Error("win.none", "gameRules.winConditions", "The explicit win-condition set provides no completion/defeat condition.");
            if (win.CityHold == true) Positive(win.CityHoldDays, "gameRules.winConditions.cityHoldDays", required: true);
            if (win.Desertion == true) { Positive(win.DesertionDay, "gameRules.winConditions.desertionDay", required: true); Nonnegative(win.DesertionValue, "gameRules.winConditions.desertionValue"); }
            if (win.HeroLighting == true) Positive(win.HeroLightingDay, "gameRules.winConditions.heroLightingDay", required: true);
            if (win.LostStartCity == true) Positive(win.LostStartCityDay, "gameRules.winConditions.lostStartCityDay", required: true);
            if (win.GladiatorArena == true)
            {
                Nonnegative(win.GladiatorArenaDaysDelayStart, "gameRules.winConditions.gladiatorArenaDaysDelayStart");
                Positive(win.GladiatorArenaCountDay, "gameRules.winConditions.gladiatorArenaCountDay", required: true);
                Known(win.ChampionSelectRule, KnownValues.ChampionSelectRules, "gameRules.winConditions.championSelectRule");
            }
            if (win.Tournament == true) ValidateTournament(win);
            if (win.Tournament == true && win.GladiatorArena == true)
                Warn("win.multipleBattleRules", "gameRules.winConditions", "Tournament and final battle are both enabled; combined scheduling requires game validation.");
        }

        private void ValidateTournament(WinConditions win)
        {
            string path = "gameRules.winConditions";
            Positive(win.TournamentPointsToWin, path + ".tournamentPointsToWin", required: true);
            if (win.TournamentDays is not { Count: > 0 } durations || win.TournamentAnnounceDays is not { Count: > 0 } announcements)
            { Error("tournament.schedule", path, "Tournament needs both duration and announcement arrays."); return; }
            if (durations.Count != announcements.Count)
                Error("tournament.arrayLengths", path, "Tournament durations and announcements must have equal lengths.");
            long previousFight = 1;
            for (int i = 0; i < Math.Min(durations.Count, announcements.Count); i++)
            {
                Nonnegative(durations[i], $"{path}.tournamentDays[{i}]");
                Positive(announcements[i], $"{path}.tournamentAnnounceDays[{i}]");
                // Calendar labels alone do not prove runtime day boundaries. Keep the exported
                // announce + duration arithmetic and do not impose the native short window on old maps.
                if (i == 0 && (long)durations[i] + announcements[i] <= 1)
                    Error("tournament.firstDay", $"{path}.tournamentDays[{i}]", "Patch 0.81.04 prohibits a tournament fight on day 1.");
                if (i > 0 && announcements[i] <= announcements[i - 1])
                    Error("tournament.order", $"{path}.tournamentDays[{i}]", "Tournament announcements and fights must progress in chronological order.");
                long fight = (long)announcements[i] + durations[i];
                if (fight <= previousFight) Error("tournament.fightOrder", $"{path}.tournamentDays[{i}]", "Tournament fight dates must be strictly increasing.");
                previousFight = fight;
            }
        }

        private void ValidateDescriptions()
        {
            if (template.DescBlocks?.Blocks is not { } blocks) return;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] is not { } block) Error("node.null", $"descBlocks.blocks[{i}]", "A description block cannot be null.");
                else if (string.IsNullOrWhiteSpace(block.Type)) Error("description.type", $"descBlocks.blocks[{i}].type", "A description block needs a type.");
            // Unknown block types and parameters are deliberately retained, not interpreted as invented game rules.
        }

        private static bool IsPlayer(string value) => Array.IndexOf(KnownValues.SpawnPlayers, value) >= 0;

        private static bool IsCity(MainObject value) => value.Type is "City" or "Spawn";

        private void RequiredTarget(List<string>? args, HashSet<string> names, string path, string kind)
        {
            if (args is not { Count: > 0 } || !names.Contains(args[0]))
                Error("reference." + kind, path, $"Missing or unknown {kind} target '{args?.FirstOrDefault() ?? ""}'.");
        }

        private void ReferenceList(List<string>? ids, HashSet<string> local, IReadOnlySet<string>? external, string path)
        {
            if (ids is null) return;
            for (int i = 0; i < ids.Count; i++) Reference(ids[i], local, external, $"{path}[{i}]", required: true);
        }

        private void Reference(string? id, HashSet<string> local, IReadOnlySet<string>? external, string path, bool required = false)
        {
            if (string.IsNullOrEmpty(id)) { if (required) Error("reference.empty", path, "A reference cannot be empty."); return; }
            if (local.Contains(id) || external?.Contains(id) == true) return;
            if (external is null) { Warn("reference.unverified", path, $"Reference '{id}' cannot be verified without this game-data domain."); return; }
            ReferenceIssue(path, id);
        }

        private void ObjectSid(string? id, string path)
        {
            if (id is null) return; // includeLists is also a valid content form.
            if (id.Length == 0) Error("reference.empty", path, "An object SID cannot be empty.");
            else if (catalog.Objects is not null && !catalog.Objects.Contains(id)) ReferenceIssue(path, id);
        }

        private void ConfiguredRewardVariant(string? sid, int? variant, string path)
        {
            if (variant is not >= 0 || sid is null || catalog.ObjectRewardVariantIndices is not { } metadata
                || !metadata.TryGetValue(sid, out var indices) || indices.Count == 0 || indices.Contains(variant.Value)) return;
            Error("content.unknownVariant", path, $"Object '{sid}' has no configured reward variant index {variant.Value} in {catalog.Source ?? "the reference catalog"}.");
        }

        private void ReferenceIssue(string path, string id)
        {
            string message = $"Unresolved exact identifier '{id}' in {catalog.Source ?? "the reference catalog"}.";
            if (catalog.IsComplete) Error("reference.missing", path, message);
            else Warn("reference.unverified", path, message + " The partial catalog cannot prove that the game does not support it.");
        }

        private void Known(string? value, IEnumerable<string> known, string path, bool required = false, bool verified = false)
        {
            if (string.IsNullOrEmpty(value)) { if (required) Error("value.empty", path, "A value is required."); return; }
            if (!known.Contains(value, StringComparer.Ordinal))
            {
                string message = $"Unknown value '{value}' is preserved and needs game verification.";
                if (verified) Error("value.unknown", path, message); else Warn("value.unknown", path, message);
            }
        }

        private void Nonnegative(double? value, string path)
        {
            FiniteFloat(value, path);
            if (value.HasValue && (!double.IsFinite(value.Value) || value < 0)) Error("number.nonnegative", path, "Value must be finite and nonnegative.");
        }

        private void Positive(double? value, string path, bool required = false)
        {
            FiniteFloat(value, path);
            if (value.HasValue ? !double.IsFinite(value.Value) || value <= 0 : required) Error("number.positive", path, "Value must be finite and positive.");
        }

        private void Fraction(double? value, string path)
        { if (value.HasValue && (!double.IsFinite(value.Value) || value < 0 || value > 1)) Error("number.fraction", path, "Value must be finite and within 0…1."); }

        private void Ordered(double? min, double? max, string path)
        {
            FiniteFloat(min, path); FiniteFloat(max, path);
            if (min.HasValue && max.HasValue && min > max) Error("number.range", path, "Minimum cannot exceed maximum.");
        }

        private void FiniteFloat(double? value, string path)
        { if (value.HasValue && (!double.IsFinite(value.Value) || value > float.MaxValue || value < -float.MaxValue)) Error("number.nativeFloat", path, "Value must fit the native finite float range."); }

        private void Error(string code, string path, string message) => issues.Add(new(code, path,
            mode == TemplateValidationMode.TolerantImport ? TemplateIssueSeverity.Warning : TemplateIssueSeverity.Error, message));
        private void Warn(string code, string path, string message) => issues.Add(new(code, path, TemplateIssueSeverity.Warning, message));
    }
}
