using System.Diagnostics;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Export;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

public class ExportDestinationTests
{
    [WindowsJunctionFact]
    public async Task DirectoryJunctionCannotRedirectAnArchiveIntoTheGame()
    {
        using var fixture = new OwnedJunctionFixture(); string game = fixture.Directory("game"), link = fixture.Junction("outside-link", game);
        string marker = Path.Combine(game, "official.rmg.json"); File.WriteAllText(marker, "keep official");
        await Assert.ThrowsAsync<InvalidOperationException>(() => AtomicArchive.WriteAsync(Path.Combine(link, "package.zip"),
            (zip, token) => AtomicArchive.AddTextAsync(zip, "template.txt", "own output", token), CancellationToken.None));
        Assert.Equal("keep official", File.ReadAllText(marker)); Assert.Single(System.IO.Directory.GetFiles(game));
    }

    [WindowsJunctionFact]
    public void CanonicalGameRootAliasProtectsItsPhysicalDirectory()
    {
        using var fixture = new OwnedJunctionFixture(); string game = fixture.Directory("actual-game"), alias = fixture.Junction("game-alias", game);
        var source = new GameInstallation { InstallPath = alias };
        Assert.Throws<InvalidOperationException>(() => ExportDestination.EnsureOutsideGame(Path.Combine(game, "output.zip"), source));
        string allowed = fixture.Directory("outside"); ExportDestination.EnsureOutsideGame(Path.Combine(allowed, "output.zip"), source);
        Assert.Empty(System.IO.Directory.GetFiles(game));
    }

    [Fact]
    public void AnExistingDestinationCreatedAfterPreflightIsNeverOverwrittenWhenOverwriteIsFalse()
    {
        using var fixture = new SyntheticInstalledGame(); string destination = Path.Combine(fixture.Root, "concurrent.rmg.json"); int checks = 0;
        Assert.Throws<IOException>(() => AtomicFile.WriteAllText(destination, "candidate", overwrite: false, validateDestination: path =>
        {
            if (path == destination && ++checks == 2) File.WriteAllText(destination, "created by another writer");
        }));
        Assert.Equal("created by another writer", File.ReadAllText(destination));
        Assert.Empty(System.IO.Directory.GetFiles(fixture.Root, ".aurorarmg-*.tmp"));
    }

    [Fact]
    public async Task CancellationAfterWritingArchiveEntriesPreservesDestinationAndCleansOwnedTemporaryFile()
    {
        using var fixture = new SyntheticInstalledGame(); string destination = Path.Combine(fixture.Root, "previous.zip"); File.WriteAllText(destination, "previous result");
        using var token = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtomicArchive.WriteAsync(destination, async (zip, cancellation) =>
        {
            await AtomicArchive.AddTextAsync(zip, "own.txt", "payload", cancellation); token.Cancel();
        }, token.Token));
        Assert.Equal("previous result", File.ReadAllText(destination)); Assert.Empty(System.IO.Directory.GetFiles(fixture.Root, ".aurora-package-*.tmp"));
    }

    private sealed class OwnedJunctionFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "aurora-junction-test-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> links = [];
        public OwnedJunctionFixture() { Assert.False(System.IO.Directory.Exists(Root)); System.IO.Directory.CreateDirectory(Root); }
        public string Directory(string name) { string path = Owned(name); System.IO.Directory.CreateDirectory(path); return path; }
        public string Junction(string name, string target)
        {
            string link = Owned(name); _ = Owned(Path.GetRelativePath(Root, target));
            // Only fixture-owned, fully verified paths are passed to mklink. No shell deletion is used.
            if (link.Concat(target).Any(character => "&|<>^%!\"()\r\n".Contains(character)))
                throw new Xunit.Sdk.XunitException("UNAVAILABLE: junction fixture path contains shell metacharacters.");
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "/d", "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("UNAVAILABLE: junction helper could not start.");
            try { process.PriorityClass = ProcessPriorityClass.Idle; } catch (InvalidOperationException) { }
            string stdout = process.StandardOutput.ReadToEnd(), stderr = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new Xunit.Sdk.XunitException("UNAVAILABLE: Windows could not create the owned junction fixture: " + stdout + stderr);
            links.Add(link);
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Assert.Equal(Path.GetFullPath(target), new DirectoryInfo(link).ResolveLinkTarget(true)!.FullName, ignoreCase: true);
            return link;
        }
        private string Owned(string relative)
        {
            string full = Path.GetFullPath(Path.Combine(Root, relative));
            if (!full.StartsWith(Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Junction fixture target escaped its owned temporary directory.");
            return full;
        }
        public void Dispose()
        {
            foreach (string link in links)
            { _ = Owned(Path.GetRelativePath(Root, link)); System.IO.Directory.Delete(link, recursive: false); }
            string fullRoot = Path.GetFullPath(Root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullRoot).StartsWith("aurora-junction-test-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe junction fixture cleanup path.");
            System.IO.Directory.Delete(fullRoot, recursive: true);
        }
    }
}

public sealed class WindowsJunctionFactAttribute : FactAttribute
{
    public WindowsJunctionFactAttribute()
    { if (!OperatingSystem.IsWindows()) Skip = "Windows junction regression is unavailable on this platform."; }
}
