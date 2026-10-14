using System.IO.Compression;
using System.Text;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

internal sealed class SyntheticInstalledGame : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AuroraGameFixture-" + Guid.NewGuid().ToString("N"));
    public string SteamRoot => Path.Combine(Root, "Steam");
    public string InstallPath => Path.Combine(SteamRoot, "steamapps", "common", "Heroes of Might and Magic Olden Era");
    public string CorePath => Path.Combine(InstallPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip");
    public string GlobalsPath => Path.Combine(InstallPath, "HeroesOldenEra_Data", "globalgamemanagers");
    public string ManifestPath => Path.Combine(SteamRoot, "steamapps", "appmanifest_3105440.acf");
    public SyntheticInstalledGame()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CorePath)!);
        WriteManifest(); WriteMarker(); WriteCore();
    }
    public void WriteManifest(string build = "25672315", string app = "3105440", string? installDir = null) =>
        File.WriteAllText(ManifestPath, "\"AppState\" { \"appid\" \"" + app + "\" \"installdir\" \"" +
            (installDir ?? Path.GetFileName(InstallPath)) + "\" \"buildid\" \"" + build + "\" \"LastOwner\" \"private-account-not-retained\" }");
    public void WriteMarker(params string[] versions)
    {
        if (versions.Length == 0) versions = ["0.81.04"];
        using var writer = new BinaryWriter(File.Create(GlobalsPath));
        writer.Write(new byte[32]);
        foreach (var version in versions) { writer.Write(version.Length); writer.Write(Encoding.ASCII.GetBytes(version)); writer.Write(new byte[8]); }
    }
    public void WriteCore(string hero = "human_hero_8", string pool = "current_pool")
    {
        using var zip = ZipFile.Open(CorePath, ZipArchiveMode.Create);
        Add(zip, "DB/heroes/humans/hero.json", "{\"array\":[{\"id\":\"" + hero + "\",\"fraction\":\"human\",\"icon\":\"test_icon\"}]}");
        Add(zip, "Lang/russian/texts/heroInfo.json", "{\"tokens\":[{\"sid\":\"" + hero + "\",\"text\":\"Имя героя\"}]}");
        Add(zip, "generator/content_pools/current.json", "[{\"name\":\"" + pool + "\",\"groups\":[{\"weight\":100,\"includeLists\":[\"current_list\"]}]}]");
        Add(zip, "generator/content_lists/current.json", "[{\"name\":\"current_list\",\"content\":[{\"sid\":\"scroll_box\",\"weight\":100}]}]");
        Add(zip, "DB/objects_logic/chests/scroll_box.json", "{\"array\":[{\"id\":\"scroll_box\",\"variants\":[{\"rollChance\":100,\"value\":500,\"rewardSet\":{\"rewards\":[{\"rewardType\":\"HeroItemReward\",\"parameters\":[\"actual_scroll_artifact\"]}]}}]}]}");
        Add(zip, "DB/map/objects/4_interactables.json", "{\"array\":[{\"id\":\"scroll_box\",\"tag\":\"Interact\",\"generatorConfig\":{}},{\"id\":\"unreferenced_object\",\"tag\":\"Interact\"}]}");
        Add(zip, "Lang/russian/texts/mapObjects.json", "{\"tokens\":[{\"sid\":\"scroll_box_name\",\"text\":\"Сундук свитков\"}]}");
        Add(zip, "Lang/english/texts/mapObjects.json", "{\"tokens\":[{\"sid\":\"scroll_box_name\",\"text\":\"Scroll Box\"}]}");
    }
    public static void Add(ZipArchive zip, string path, string json)
    { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(json); }
    public GameInstallationService Service() => new([SteamRoot]);
    public void Dispose()
    {
        // Only our unique, owned temporary fixture directory is deleted.
        var full = Path.GetFullPath(Root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(full).StartsWith("AuroraGameFixture-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}

public class GameInstallationTests
{
    [Fact]
    public void Discover_UsesManifestAndExposesOnlySelectedInstallationFields()
    {
        using var game = new SyntheticInstalledGame();
        var source = Assert.IsType<GameInstallation>(game.Service().Discover());
        Assert.Equal(game.InstallPath, source.InstallPath);
        Assert.Equal("25672315", source.SteamBuildId);
        Assert.Equal("0.81.04", source.VersionMarker);
        Assert.Equal(64, source.SourceFingerprint.Length);
        Assert.Equal(Path.Combine(game.InstallPath, "HeroesOldenEra_Data", "StreamingAssets", "map_templates"), source.TemplatesPath);
        Assert.DoesNotContain("private-account", System.Text.Json.JsonSerializer.Serialize(source));
        Assert.Contains("PlayerSettings", source.VersionEvidence);
    }

    [Fact]
    public void Discover_ReadsSecondarySteamLibraryWithEscapedVdfPath()
    {
        using var game = new SyntheticInstalledGame();
        var primary = Path.Combine(game.Root, "Primary Steam");
        Directory.CreateDirectory(Path.Combine(primary, "steamapps"));
        File.WriteAllText(Path.Combine(primary, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" +
            game.SteamRoot.Replace("\\", "\\\\") + "\" \"apps\" { \"3105440\" \"123\" } } }");
        Assert.Equal(game.CorePath, new GameInstallationService([primary]).Discover()?.CoreZipPath);
    }

    [Theory]
    [InlineData("../../outside")]
    [InlineData("..\\..\\outside")]
    public void Discover_IgnoresManifestDirectoryTraversal(string installDir)
    {
        using var game = new SyntheticInstalledGame();
        game.WriteManifest(installDir: installDir);
        var service = game.Service();
        Assert.Null(service.Discover());
        Assert.Contains(service.LastWarnings, text => text.Contains("invalid install directory"));
    }

    [Fact]
    public void Discover_ExplicitPathOverridesAutomaticInstallAndInvalidPathIsReported()
    {
        using var game = new SyntheticInstalledGame();
        var service = new GameInstallationService(Array.Empty<string>()) { ManualInstallPath = game.CorePath };
        Assert.Equal(game.InstallPath, service.Discover()?.InstallPath);
        service.ManualInstallPath = Path.Combine(game.Root, "missing");
        Assert.Null(service.Discover());
        Assert.Contains(service.LastWarnings, text => text.Contains("selected game path"));
    }

    [Fact]
    public void Discover_RejectsWrongAppManifest()
    {
        using var game = new SyntheticInstalledGame(); game.WriteManifest(app: "1");
        Assert.Null(game.Service().Discover());
    }

    [Fact]
    public void DiscoveryCache_RefreshesWhenCoreChangesAndSupportsExplicitRefresh()
    {
        using var game = new SyntheticInstalledGame(); var service = game.Service();
        var original = service.Discover()!;
        Assert.Same(original, service.Discover());
        File.Delete(game.CorePath); game.WriteCore(pool: "different_pool");
        File.SetLastWriteTimeUtc(game.CorePath, DateTime.UtcNow.AddMinutes(1));
        var updated = service.Discover()!;
        Assert.NotEqual(original.SourceFingerprint, updated.SourceFingerprint);
        Assert.NotSame(updated, service.Discover(forceRefresh: true));
    }

    [Fact]
    public void VersionMarker_AmbiguousAndUnprefixedStringsAreNotClaimedAsVersion()
    {
        using var game = new SyntheticInstalledGame();
        game.WriteMarker("0.81.04", "0.81.05");
        Assert.Null(game.Service().Discover()?.VersionMarker);
        File.WriteAllText(game.GlobalsPath, "0.81.04");
        Assert.Null(game.Service().Discover()?.VersionMarker);
    }
}
