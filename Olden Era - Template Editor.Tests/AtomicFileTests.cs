using System.IO;
using System.Text;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AuroraRMG-tests-" + Guid.NewGuid().ToString("N"));
    public AtomicFileTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CreateAndReplace_PreservesLiteralUnicodeWithoutBom()
    {
        string path = Path.Combine(_dir, "карта.rmg.json");
        AtomicFile.WriteAllText(path, "old contents");
        const string text = "{\"name\":\"Крепость 🛡\"}";
        AtomicFile.WriteAllText(path, text);
        Assert.Equal(Encoding.UTF8.GetBytes(text), File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void FailedReplacement_KeepsOriginalAndRemovesTemporaryFile()
    {
        string path = Path.Combine(_dir, "map.rmg.json");
        File.WriteAllText(path, "original");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllText(path, "replacement"));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void EncodingFailure_KeepsOriginalAndRemovesTemporaryFile()
    {
        string path = Path.Combine(_dir, "settings.oetgs");
        File.WriteAllText(path, "original");
        Assert.Throws<EncoderFallbackException>(() => AtomicFile.WriteAllText(path, "\uD800"));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_dir));
    }
}
