using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Olden_Era___Template_Editor.Services.Update;

namespace Olden_Era___Template_Editor.Tests;

public sealed class UpdateDownloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AuroraRMG-update-test-" + Guid.NewGuid().ToString("N"));
    public UpdateDownloadTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);
    private static UpdateInfo Info(byte[] bytes) => new(new Version(2, 0, 0), "v2.0.0",
        "https://example.invalid/update.exe", "AuroraRMG.exe", bytes.Length, null, "https://example.invalid/",
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)));

    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(response()); }
    }
    private static HttpClient Client(byte[] bytes, long? declared = null) => new(new Handler(() =>
    {
        var content = new ByteArrayContent(bytes);
        if (declared is not null) content.Headers.ContentLength = declared;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }));

    [Fact]
    public async Task ValidDownload_IsVerified_AndConcurrentDownloadsHaveIndependentPaths()
    {
        byte[] bytes = File.ReadAllBytes(typeof(UpdateService).Assembly.Location);
        using var client = Client(bytes);
        var paths = await Task.WhenAll(UpdateService.DownloadAsync(Info(bytes), client, _dir),
                                      UpdateService.DownloadAsync(Info(bytes), client, _dir));
        Assert.NotEqual(paths[0], paths[1]);
        foreach (var path in paths) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_dir, "*.partial", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("checksum")]
    [InlineData("not-pe")]
    public async Task InvalidDownload_IsRejectedAndCleaned(string failure)
    {
        byte[] bytes = File.ReadAllBytes(typeof(UpdateService).Assembly.Location);
        var info = Info(bytes);
        byte[] response = failure switch
        {
            "truncated" => bytes[..64],
            "oversized" => [.. bytes, 0],
            "checksum" => new byte[bytes.Length],
            "not-pe" => new byte[64],
            _ => bytes
        };
        if (failure == "not-pe") info = Info(response);
        using var client = Client(response, info.AssetSize);
        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.DownloadAsync(info, client, _dir));
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public async Task CancelledDownload_DoesNotLeaveAnExecutable()
    {
        byte[] bytes = File.ReadAllBytes(typeof(UpdateService).Assembly.Location);
        using var client = Client(bytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateService.DownloadAsync(Info(bytes), client, _dir, ct: cts.Token));
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    private sealed class ImmediateProgress(Action<double> report) : IProgress<double>
    { public void Report(double value) => report(value); }

    [Fact]
    public async Task CancellationAfterBytesArrive_RemovesThePartialFile()
    {
        byte[] bytes = File.ReadAllBytes(typeof(UpdateService).Assembly.Location);
        using var client = Client(bytes);
        using var cts = new CancellationTokenSource();
        bool bytesArrived = false;
        var progress = new ImmediateProgress(_ => { bytesArrived = true; cts.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateService.DownloadAsync(Info(bytes), client, _dir, progress, cts.Token));
        Assert.True(bytesArrived);
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public async Task HeaderSizeMismatch_IsRejectedBeforeInstallation()
    {
        byte[] bytes = File.ReadAllBytes(typeof(UpdateService).Assembly.Location);
        using var client = Client(bytes, bytes.Length + 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateService.DownloadAsync(Info(bytes), client, _dir));
        Assert.Empty(Directory.GetFileSystemEntries(_dir));
    }

    [Fact]
    public void PickAsset_DoesNotInstallAnUnrelatedExecutable()
    {
        Assert.Null(UpdateService.PickAsset([new() { Name = "helper.exe" }]));
        var expected = new UpdateService.GhAsset { Name = "AuroraRMG.exe" };
        Assert.Same(expected, UpdateService.PickAsset([new() { Name = "helper.exe" }, expected]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installer_AtomicallyReplacesOrPreservesOriginal(bool locked)
    {
        string source = Path.Combine(_dir, "new.update");
        string target = Path.Combine(_dir, "app.exe");
        File.WriteAllText(source, "new");
        File.WriteAllText(target, "original");
        using var held = locked ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        int exitCode = await RunInstallerAsync(source, target, noRestart: true);
        Assert.Equal(locked ? 1 : 0, exitCode);
        Assert.Equal(locked ? "original" : "new", File.ReadAllText(target));
        if (!locked) Assert.Equal("original", File.ReadAllText(target + ".previous"));
    }

    [Fact]
    public async Task Installer_RestoresOriginalWhenNewProcessExitsWithAnError()
    {
        string source = Path.Combine(_dir, "new.update");
        string target = Path.Combine(_dir, "app.exe");
        byte[] original = File.ReadAllBytes(Path.Combine(Environment.SystemDirectory, "whoami.exe"));
        File.WriteAllBytes(target, original);
        File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), source);

        Assert.Equal(1, await RunInstallerAsync(source, target, noRestart: false));
        Assert.Equal(original, File.ReadAllBytes(target));
        Assert.Contains("exited during startup", File.ReadAllText(target + ".update-error.log"));
    }

    [Fact]
    public async Task Installer_KeepsUpdateWhenNewProcessExitsNormally()
    {
        string source = Path.Combine(_dir, "new.update");
        string target = Path.Combine(_dir, "app.exe");
        byte[] update = File.ReadAllBytes(Path.Combine(Environment.SystemDirectory, "whoami.exe"));
        File.WriteAllText(target, "original");
        File.WriteAllBytes(source, update);

        Assert.Equal(0, await RunInstallerAsync(source, target, noRestart: false));
        Assert.Equal(update, File.ReadAllBytes(target));
        Assert.Equal("original", File.ReadAllText(target + ".previous"));
        Assert.False(File.Exists(target + ".update-error.log"));
    }

    private async Task<int> RunInstallerAsync(string source, string target, bool noRestart)
    {
        string helper = Path.Combine(_dir, "apply.ps1");
        File.WriteAllText(helper, UpdateService.HelperScript);
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", helper,
            "-ProcessId", int.MaxValue.ToString(), "-Source", source, "-Target", target, "-Attempts", "1" })
            start.ArgumentList.Add(arg);
        if (noRestart) start.ArgumentList.Add("-NoRestart");
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        await Task.WhenAll(output, errors);
        return process.ExitCode;
    }
}
