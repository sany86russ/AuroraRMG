using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

public sealed record AllianceGroup(string Key, IReadOnlyList<int> Players);

/// <summary>Resolves actual player teams independently of lobby human/AI choices.</summary>
public sealed class AlliancePlan
{
    public IReadOnlyList<AllianceSlotSettings> Slots { get; }
    public IReadOnlyList<AllianceGroup> Groups { get; }
    public bool Enabled => Slots.Count > 0;
    public int MaximumAlliances => Groups.Count;
    private readonly Dictionary<string, string> groupBySpawn;

    private AlliancePlan(List<AllianceSlotSettings> slots)
    {
        Slots = slots;
        Groups = slots.GroupBy(s => s.Alliance == -1 ? $"player{s.Player}" : $"team{s.Alliance}", StringComparer.Ordinal)
            .Select(g => new AllianceGroup(g.Key, g.Select(s => s.Player).Order().ToArray())).ToArray();
        groupBySpawn = Groups.SelectMany(g => g.Players.Select(p => ($"Player{p}", g.Key)))
            .ToDictionary(p => p.Item1, p => p.Key, StringComparer.Ordinal);
    }

    public bool AreAllies(string firstSpawn, string secondSpawn) => firstSpawn != secondSpawn
        && groupBySpawn.TryGetValue(firstSpawn, out string? first) && groupBySpawn.TryGetValue(secondSpawn, out string? second)
        && first == second;

    public string GroupFor(string spawn) => groupBySpawn.TryGetValue(spawn, out var key) ? key : spawn;

    public static AlliancePlan Resolve(int playerCount, AllianceSettings settings, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Slots is null || settings.Preset is null) throw new ArgumentException("Alliance preset and slot list cannot be null.");
        if (!settings.Enabled) return new([]);
        if (playerCount is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(playerCount), "Alliance templates support one to eight player slots.");
        if (!double.IsFinite(settings.SmallerTeamEconomyMultiplier) || settings.SmallerTeamEconomyMultiplier is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(settings), "Smaller-team economy support must be within 1…8.");
        List<AllianceSlotSettings> slots;
        if (settings.Slots.Count > 0)
        {
            if (settings.RandomizePlayerAssignment) throw new ArgumentException("Random team assignment requires a preset with no explicit manual slot assignments.");
            slots = settings.Slots.Select(s => s is null ? throw new ArgumentException("An alliance slot cannot be null.")
                : new AllianceSlotSettings { Player = s.Player, Alliance = s.Alliance, Locked = s.Locked || settings.Locked }).ToList();
            if (slots.Count != playerCount || slots.Select(s => s.Player).Distinct().Count() != playerCount
                || slots.Any(s => s.Player < 1 || s.Player > playerCount || s.Alliance is < -1 or 0 or > 8))
                throw new ArgumentException("Assign each player exactly once, using -1 for FFA or team indices 1…8.");
        }
        else
        {
            string preset = settings.Preset.ToLowerInvariant();
            if (preset is "ffa" or "independent")
                slots = Enumerable.Range(1, playerCount).Select(p => new AllianceSlotSettings { Player = p, Alliance = -1, Locked = settings.Locked }).ToList();
            else
            {
                int[] sizes;
                if (preset is "auto" or "") sizes = playerCount == 1 ? [1] : [(playerCount + 1) / 2, playerCount / 2];
                else
                {
                    var parts = preset.Split(['v', 'V', 'х', '×', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length < 2 || parts.Any(p => !int.TryParse(p, out int size) || size < 1))
                        throw new ArgumentException("Team preset must be FFA, auto or a composition such as 1v7 or 2v2v2v2.");
                    sizes = parts.Select(int.Parse).ToArray();
                    if (sizes.Sum() != playerCount) throw new ArgumentException("Team composition must match the selected number of players.");
                }
                slots = [];
                int[] playerOrder = Enumerable.Range(1, playerCount).ToArray();
                if (settings.RandomizePlayerAssignment)
                {
                    // Separate RNG keeps the legacy map-placement stream intact.
                    var random = new Random((seed ?? Random.Shared.Next()) ^ 0x51A11A5);
                    for (int i = playerOrder.Length - 1; i > 0; i--)
                    { int chosen = random.Next(i + 1); (playerOrder[i], playerOrder[chosen]) = (playerOrder[chosen], playerOrder[i]); }
                }
                int player = 0;
                for (int team = 0; team < sizes.Length; team++)
                    for (int member = 0; member < sizes[team]; member++)
                        slots.Add(new() { Player = playerOrder[player++], Alliance = team + 1, Locked = settings.Locked });
            }
        }
        // Dense 1-based output retains membership but avoids sparse indices exceeding the lobby team count.
        var indices = slots.Where(s => s.Alliance > 0).Select(s => s.Alliance).Distinct().Order()
            .Select((team, index) => (team, index: index + 1)).ToDictionary(p => p.team, p => p.index);
        foreach (var slot in slots) if (slot.Alliance > 0) slot.Alliance = indices[slot.Alliance];
        return new(slots.OrderBy(s => s.Player).ToList());
    }

    public List<SpawnInfo> ToSpawnInfos() => Slots.Select(s => new SpawnInfo
    { Spawn = $"Player{s.Player}", AllianceIndex = s.Alliance, IsAllianceLocked = s.Locked }).ToList();
}
