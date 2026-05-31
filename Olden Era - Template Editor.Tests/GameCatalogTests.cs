using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

public class GameCatalogTests
{
    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var s = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    private static ZipArchive BuildSyntheticCore()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "Lang/russian/texts/heroInfo.json",
                "{\"tokens\":[" +
                "{\"sid\":\"human_hero_8\",\"text\":\"Lord Edgar\"}," +
                "{\"sid\":\"human_hero_8_motto\",\"text\":\"noise\"}," +
                "{\"sid\":\"demon_hero_3\",\"text\":\"Azrael\"}]}");

            AddEntry(zip, "DB/heroes/humans/human_hero_8.json",
                "{\"array\":[{\"id\":\"human_hero_8\",\"fraction\":\"human\",\"icon\":\"hero_human_8_lord_edgar\"}]}");

            AddEntry(zip, "DB/heroes/demons/demon_hero_3.json",
                "{\"array\":[{\"id\":\"demon_hero_3\",\"fraction\":\"demon\",\"icon\":\"hero_demon_3\"}]}");

            // Campaign heroes must be excluded from the pickable roster.
            AddEntry(zip, "DB/heroes/campaign/campaign_hero_1.json",
                "{\"array\":[{\"id\":\"campaign_hero_1\",\"fraction\":\"dungeon\",\"icon\":\"x\"}]}");
        }
        ms.Position = 0;
        return new ZipArchive(ms, ZipArchiveMode.Read);
    }

    [Fact]
    public void BuildHeroes_ParsesNamesFactionsAndIcons()
    {
        using var core = BuildSyntheticCore();
        var heroes = CatalogBuilder.BuildHeroes(core, "russian");

        Assert.Equal(2, heroes.Count); // campaign hero excluded

        var edgar = heroes.Single(h => h.Sid == "human_hero_8");
        Assert.Equal("Lord Edgar", edgar.Name);
        Assert.Equal("Human", edgar.Faction);                       // capitalized from "fraction"
        Assert.Equal("hero_human_8_lord_edgar", edgar.IconSid);

        Assert.DoesNotContain(heroes, h => h.Sid == "campaign_hero_1");
    }

    [Fact]
    public void BuildHeroes_FallsBackToEnglishThenPrettifiedName()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // No russian loc; english present for one hero only.
            AddEntry(zip, "Lang/english/texts/heroInfo.json",
                "{\"tokens\":[{\"sid\":\"nature_hero_9\",\"text\":\"Sylvan\"}]}");
            AddEntry(zip, "DB/heroes/nature/nature_hero_9.json",
                "{\"array\":[{\"id\":\"nature_hero_9\",\"fraction\":\"nature\",\"icon\":\"i\"}]}");
            AddEntry(zip, "DB/heroes/nature/nature_hero_99.json",
                "{\"array\":[{\"id\":\"nature_hero_99\",\"fraction\":\"nature\",\"icon\":\"i\"}]}");
        }
        ms.Position = 0;
        using var core = new ZipArchive(ms, ZipArchiveMode.Read);

        var heroes = CatalogBuilder.BuildHeroes(core, "russian"); // russian missing → english fallback
        Assert.Equal("Sylvan", heroes.Single(h => h.Sid == "nature_hero_9").Name);
        // Unknown in loc → prettified id.
        Assert.Equal("Nature hero 99", heroes.Single(h => h.Sid == "nature_hero_99").Name);
    }

    [Fact]
    public async Task RealGameCatalog_ResolvesKnownHero_WhenInstalled()
    {
        var svc = GameCatalogService.Instance;
        if (svc.LocateCoreZip() is null) return; // game not installed (CI) → skip silently

        var catalog = await svc.GetCatalogAsync("russian");
        Assert.True(catalog.Heroes.Count > 40, $"expected a sizeable roster, got {catalog.Heroes.Count}");

        var edgar = catalog.Heroes.FirstOrDefault(h => h.Sid == "human_hero_8");
        Assert.NotNull(edgar);
        Assert.Equal("Human", edgar!.Faction);
        Assert.False(string.IsNullOrWhiteSpace(edgar.Name));
        Assert.False(string.IsNullOrWhiteSpace(edgar.IconSid));
    }
}
