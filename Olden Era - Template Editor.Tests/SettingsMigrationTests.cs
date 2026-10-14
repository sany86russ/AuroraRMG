using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Generation;

namespace Olden_Era___Template_Editor.Tests;

public class SettingsMigrationTests
{
    [Fact]
    public void LegacyIndicesBecomeStableIdsWithoutChangingMeaning()
    {
        var state = JsonSerializer.Deserialize<SimpleModeState>("""{"type":3,"scale":3,"victory":4,"guards":5,"castleFaction":4}""")!;
        state.MigrateSelections();
        Assert.Equal("teams", state.TypeId);
        Assert.Equal("huge", state.ScaleId);
        Assert.Equal("win_condition_3", state.VictoryId);
        Assert.Equal("extreme", state.GuardsId);
        Assert.Equal("Nature", state.CastleFactionId);
        Assert.Equal(GenerationProfileOptions.LegacyProfile, state.ProfileOptions!.ProfileId);
    }

    [Fact]
    public void StableIdsOverrideObsoleteIndices()
    {
        var state = new SimpleModeState { Type = 0, TypeId = "solo", Victory = 0, VictoryId = "win_condition_8" };
        state.MigrateSelections();
        Assert.Equal(5, state.Type);
        Assert.Equal(5, state.Victory);
    }

    [Fact]
    public void UnknownFutureSelectionIsPreservedWhenAnotherSettingChanges()
    {
        var state = new SimpleModeState { VictoryId = "future_condition", ScaleId = "small" };
        state.MigrateSelections();
        Assert.Equal(-1, state.Victory);
        state.Scale = 2;
        state.CaptureStableIds();
        var roundTrip = JsonSerializer.Deserialize<SimpleModeState>(JsonSerializer.Serialize(state))!;
        roundTrip.MigrateSelections();
        Assert.Equal("future_condition", roundTrip.VictoryId);
        Assert.Equal(-1, roundTrip.Victory);
        Assert.Equal("large", roundTrip.ScaleId);
    }

    [Fact]
    public void ProfileClonePreservesNullableRulesAndIndependentTeamSlots()
    {
        var source = GenerationProfileOptions.CurrentDefaults();
        source.TemplateSeed = -43;
        source.InitialUnitIncrement = 3;
        source.Alliances.Slots.Add(new() { Player = 1, Alliance = 2 });
        var clone = source.Clone();
        clone.Alliances.Slots[0].Alliance = 1;
        Assert.Equal(2, source.Alliances.Slots[0].Alliance);
        Assert.Equal(-43, clone.TemplateSeed);
        Assert.Equal(3, clone.InitialUnitIncrement);
        Assert.Null(clone.PortalFromEnabled);
    }

    [Fact]
    public void CurrentAggressionUsesActualNativeEnumOrder()
    {
        var passive = GuardReaction.NativeDistribution(GuardRole.StartZone, MonsterAggression.Passive);
        var aggressive = GuardReaction.NativeDistribution(GuardRole.StartZone, MonsterAggression.Aggressive);
        Assert.True(passive[5] > aggressive[5]);
        Assert.True(aggressive.Take(3).Sum() > passive.Take(3).Sum());
        Assert.Equal(new double[] { 60,20,10,10,2,0 }, GuardReaction.Distribution(GuardRole.StartZone, MonsterAggression.Normal));
    }
}
