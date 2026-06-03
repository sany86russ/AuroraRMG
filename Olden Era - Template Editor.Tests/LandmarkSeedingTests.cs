using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Generation;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// Phase 2 — curated landmark seeding. These guard the promise that quick maps gain a guaranteed
/// "signature" encounter per neutral tier WITHOUT breaking the generator: every seeded sid is a real,
/// uncapped game object, seeding is deterministic, and dense maps actually place the landmark while
/// staying graph-valid and round-trip-serialisable.
/// </summary>
public class LandmarkSeedingTests
{
    private static readonly string[] ExpectedHigh =
        ["dragon_utopia", "research_laboratory", "unstable_ruins", "eternal_dragon"];
    private const string ExpectedMedium = "orb_observatory";

    private static QuickGenerateOptions Opts(int seed, bool dense = false, int players = 4) => new()
    {
        Seed = seed, PlayerCount = players, GameType = QuickGameType.FreeForAll,
        Scale = QuickMapScale.Large, Length = QuickGameLength.Long, Chaos = QuickChaos.Normal,
        StrongNeutrals = dense,
    };

    [Fact]
    public void Build_SeedsOneLandmarkIntoMediumAndHighLists()
    {
        GeneratorSettings s = RandomTemplateBuilder.Build(Opts(seed: 123));

        ContentItem high = Assert.Single(s.HighNeutralMandatoryContent);
        ContentItem medium = Assert.Single(s.MediumNeutralMandatoryContent);

        Assert.Contains(high.Sid, ExpectedHigh);
        Assert.Equal(ExpectedMedium, medium.Sid);

        // Low tier stays lean (no forced landmark there).
        Assert.Empty(s.LowNeutralMandatoryContent);
    }

    [Fact]
    public void SeededLandmarkSids_AreAllRealUncappedGameObjects()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(Opts(seed));
            foreach (ContentItem item in s.HighNeutralMandatoryContent.Concat(s.MediumNeutralMandatoryContent))
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Sid));
                Assert.Contains(item.Sid!, KnownValues.ObjectSids); // a verified game object
            }
        }
    }

    [Fact]
    public void LandmarkSeeding_IsDeterministic()
    {
        GeneratorSettings a = RandomTemplateBuilder.Build(Opts(seed: 7777));
        GeneratorSettings b = RandomTemplateBuilder.Build(Opts(seed: 7777));

        Assert.Equal(a.HighNeutralMandatoryContent.Single().Sid, b.HighNeutralMandatoryContent.Single().Sid);
        Assert.Equal(a.MediumNeutralMandatoryContent.Single().Sid, b.MediumNeutralMandatoryContent.Single().Sid);
    }

    [Fact]
    public void DenseMaps_PlaceTheLandmark_AndStayValidAndSerialisable()
    {
        bool highSeenSomewhere = false;
        bool mediumSeenSomewhere = false;

        for (int seed = 0; seed < 12; seed++)
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(Opts(seed, dense: true, players: 6));
            RmgTemplate tpl = TemplateGenerator.Generate(s);

            // The generated template must stay structurally sound + game-serialisable.
            Variant v = Assert.Single(tpl.Variants ?? []);
            Assert.Empty(ZoneGraphValidator.Validate(v.Zones!, v.Connections ?? []));
            string json = JsonSerializer.Serialize(tpl, JsonExport.Options);
            Assert.NotNull(JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options));

            var placedSids = (tpl.MandatoryContent ?? [])
                .SelectMany(g => g.Content ?? [])
                .Select(c => c.Sid)
                .ToList();
            if (placedSids.Any(sid => ExpectedHigh.Contains(sid))) highSeenSomewhere = true;
            if (placedSids.Contains(ExpectedMedium)) mediumSeenSomewhere = true;
        }

        // Across a spread of dense maps the landmarks must actually reach the zones (not just the settings).
        Assert.True(highSeenSomewhere, "no high-tier landmark was placed in any dense map");
        Assert.True(mediumSeenSomewhere, "no medium-tier landmark was placed in any dense map");
    }
}
