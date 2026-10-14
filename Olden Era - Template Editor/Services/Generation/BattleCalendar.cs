using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

public sealed record TournamentCalendar(IReadOnlyList<int> Announcements, IReadOnlyList<int> Durations)
{
    /// <summary>Export arithmetic, not a claim that native runtime day boundaries have been tested.</summary>
    public IReadOnlyList<int> BattleDays => Announcements.Zip(Durations, (a, d) => checked(a + d)).ToArray();
}

public static class BattleCalendar
{
    public static TournamentCalendar Encode(IReadOnlyList<int> battleDays, int? announcementLeadDays = null)
    {
        ArgumentNullException.ThrowIfNull(battleDays);
        if (battleDays.Count == 0 || battleDays.Count > 19 || battleDays[0] <= 1
            || battleDays.Zip(battleDays.Skip(1)).Any(pair => pair.Second <= pair.First))
            throw new ArgumentException("Choose 1…19 increasing tournament dates, all after day 1.");
        if (announcementLeadDays is < 1) throw new ArgumentOutOfRangeException(nameof(announcementLeadDays));
        var announcements = new List<int>();
        var durations = new List<int>();
        int previousFight = 0;
        foreach (int battle in battleDays)
        {
            int announce = announcementLeadDays.HasValue ? Math.Max(1, battle - announcementLeadDays.Value)
                : previousFight + 1;
            // Adjacent dates are supported by the explicit calendar: announce immediately after the
            // previous fight, at latest one day before this fight. The engine-level behavior still needs acceptance.
            announce = Math.Min(announce, battle - 1);
            if (announcements.Count > 0) announce = Math.Max(announce, announcements[^1] + 1);
            if (announce >= battle) throw new ArgumentException("The announcement window overlaps this tournament date.");
            announcements.Add(announce); durations.Add(checked(battle - announce)); previousFight = battle;
        }
        return new(announcements, durations);
    }

    public static TournamentCalendar Decode(IReadOnlyList<int> announcements, IReadOnlyList<int> durations)
    {
        ArgumentNullException.ThrowIfNull(announcements); ArgumentNullException.ThrowIfNull(durations);
        if (announcements.Count == 0 || announcements.Count != durations.Count || announcements.Any(a => a < 1) || durations.Any(d => d < 0))
            throw new ArgumentException("A tournament calendar needs positive announcements and nonnegative durations of equal length.");
        var result = new TournamentCalendar(announcements.ToArray(), durations.ToArray());
        var days = result.BattleDays;
        if (days[0] <= 1 || days.Zip(days.Skip(1)).Any(pair => pair.Second <= pair.First))
            throw new ArgumentException("Tournament dates must increase and cannot start on day 1.");
        return result;
    }

    public static void ApplyNativeFinalBattle(WinConditions conditions, int selectedDay, int playerCount)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (playerCount != 2) throw new ArgumentException("The verified native final-battle profile requires exactly two players.");
        if (selectedDay <= 1) throw new ArgumentOutOfRangeException(nameof(selectedDay));
        conditions.GladiatorArena = true;
        conditions.GladiatorArenaRegistrationStartWork = true;
        conditions.GladiatorArenaRegistrationStartFight = true;
        // Native UI stores human day - 1; its converter adds 1. Our input is already the human day.
        conditions.GladiatorArenaDaysDelayStart = selectedDay;
        conditions.GladiatorArenaCountDay = 1;
        conditions.ChampionSelectRule = "StartHero";
    }
}
