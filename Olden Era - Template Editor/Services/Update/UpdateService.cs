using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services.Update
{
    /// <summary>
    /// A pending update discovered on the public GitHub repository.
    /// </summary>
    public sealed record UpdateInfo(
        Version Version,
        string TagName,
        string DownloadUrl,
        string AssetName,
        long AssetSize,
        string? ReleaseNotes,
        string ReleaseUrl);

    /// <summary>
    /// AuroraRMG self-update.
    ///
    /// Looks at the public GitHub repository's *latest* release, compares the
    /// release tag with the running assembly version, and — on request —
    /// downloads the single-file <c>.exe</c> asset and swaps it in via a small
    /// PowerShell helper (a running single-file exe cannot overwrite itself).
    ///
    /// Everything is best-effort and never throws into the UI: a missing
    /// network, a private/empty repo, or a malformed release simply yields
    /// "no update".
    /// </summary>
    public static class UpdateService
    {
        // ── Public repository coordinates ───────────────────────────────────
        public const string Owner = "sany86russ";
        public const string Repo  = "AuroraRMG";

        /// <summary>Preferred asset file name published on each release.</summary>
        public const string PreferredAssetName = "AuroraRMG.exe";

        private static readonly Uri LatestReleaseApi =
            new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

        /// <summary>The running app version, normalised to major.minor.build.</summary>
        public static Version CurrentVersion
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return Normalize(v ?? new Version(0, 0, 0));
            }
        }

        // ── Check ───────────────────────────────────────────────────────────

        /// <summary>
        /// Queries the latest release. Returns the update when the published
        /// tag is strictly newer than the running version and ships a usable
        /// <c>.exe</c> asset; otherwise <c>null</c>.
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
        {
            try
            {
                using var http = CreateClient(TimeSpan.FromSeconds(15));
                var release = await http.GetFromJsonAsync<GhRelease>(LatestReleaseApi, ct).ConfigureAwait(false);
                if (release is null || release.Draft || release.Prerelease) return null;
                if (string.IsNullOrWhiteSpace(release.TagName)) return null;

                if (!TryParseTag(release.TagName, out var latest)) return null;
                if (latest <= CurrentVersion) return null;

                var asset = PickAsset(release.Assets);
                if (asset is null || string.IsNullOrWhiteSpace(asset.DownloadUrl)) return null;

                return new UpdateInfo(
                    Version:      latest,
                    TagName:      release.TagName!,
                    DownloadUrl:  asset.DownloadUrl!,
                    AssetName:    asset.Name ?? PreferredAssetName,
                    AssetSize:    asset.Size,
                    ReleaseNotes: release.Body,
                    ReleaseUrl:   release.HtmlUrl ?? $"https://github.com/{Owner}/{Repo}/releases/latest");
            }
            catch
            {
                // Offline, rate-limited, repo missing, etc. — silently report "no update".
                return null;
            }
        }

        // ── Download ──────────────────────────────────────────────────────────

        /// <summary>
        /// Downloads the update asset to a temp file and returns its path.
        /// <paramref name="progress"/> reports 0..1 (or negative when the total
        /// size is unknown). Throws on failure so the caller can surface it.
        /// </summary>
        public static async Task<string> DownloadAsync(
            UpdateInfo info,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            string dir = Path.Combine(Path.GetTempPath(), "AuroraRMG-update");
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, $"AuroraRMG-{info.Version}.exe");

            using var http = CreateClient(TimeSpan.FromMinutes(10));
            using var resp = await http.GetAsync(info.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? info.AssetSize;
            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
            {
                var buffer = new byte[1 << 16];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    read += n;
                    if (progress is not null)
                        progress.Report(total > 0 ? (double)read / total : -1);
                }
            }

            return dest;
        }

        // ── Install ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the running executable with <paramref name="newExePath"/>
        /// and relaunches. A detached PowerShell helper waits for this process
        /// to exit, copies the new file over the current one, and starts it.
        /// The caller should shut the app down immediately after this returns.
        /// </summary>
        public static void InstallAndRestart(string newExePath)
        {
            string? target = GetCurrentExecutablePath();
            if (string.IsNullOrEmpty(target))
                throw new InvalidOperationException("Не удалось определить путь к текущему исполняемому файлу.");

            string helper = Path.Combine(Path.GetTempPath(), "AuroraRMG-update", "apply-update.ps1");
            File.WriteAllText(helper, HelperScript);

            int pid = Environment.ProcessId;
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(helper)!,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-WindowStyle");
            psi.ArgumentList.Add("Hidden");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(helper);
            psi.ArgumentList.Add("-ProcessId");
            psi.ArgumentList.Add(pid.ToString());
            psi.ArgumentList.Add("-Source");
            psi.ArgumentList.Add(newExePath);
            psi.ArgumentList.Add("-Target");
            psi.ArgumentList.Add(target);

            Process.Start(psi);
        }

        // PowerShell helper: wait for the app to exit, swap the exe, relaunch.
        private const string HelperScript = @"
param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Target
)
$ErrorActionPreference = 'SilentlyContinue'
try { Wait-Process -Id $ProcessId -Timeout 60 } catch {}
Start-Sleep -Milliseconds 400
$copied = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        Copy-Item -LiteralPath $Source -Destination $Target -Force
        $copied = $true
        break
    } catch {
        Start-Sleep -Milliseconds 500
    }
}
if ($copied) {
    Start-Process -FilePath $Target
}
";

        // ── Helpers ───────────────────────────────────────────────────────────

        private static HttpClient CreateClient(TimeSpan timeout)
        {
            var http = new HttpClient { Timeout = timeout };
            // GitHub's API requires a User-Agent and a versioned Accept header.
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"AuroraRMG/{CurrentVersion}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return http;
        }

        private static GhAsset? PickAsset(List<GhAsset>? assets)
        {
            if (assets is null || assets.Count == 0) return null;
            // Prefer the exact preferred name, then any .exe.
            return assets.FirstOrDefault(a =>
                       string.Equals(a.Name, PreferredAssetName, StringComparison.OrdinalIgnoreCase))
                   ?? assets.FirstOrDefault(a =>
                       a.Name is not null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Parses tags like "v0.8.0", "0.8.0", "v0.8.0.0" into a normalised Version.</summary>
        internal static bool TryParseTag(string tag, out Version version)
        {
            version = new Version(0, 0, 0);
            string s = tag.Trim();
            if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
            // Keep only the leading numeric dotted part (drop any "-beta" suffix).
            int end = 0;
            while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.')) end++;
            s = s[..end];
            if (s.Length == 0) return false;
            if (!Version.TryParse(s.Contains('.') ? s : s + ".0", out var parsed) || parsed is null)
                return false;
            version = Normalize(parsed);
            return true;
        }

        private static Version Normalize(Version v)
            => new(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));

        private static string? GetCurrentExecutablePath()
        {
            string? p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p)) return p;
            try { return Process.GetCurrentProcess().MainModule?.FileName; }
            catch { return null; }
        }

        // ── GitHub API DTOs ────────────────────────────────────────────────────
        private sealed class GhRelease
        {
            [JsonPropertyName("tag_name")]   public string? TagName { get; set; }
            [JsonPropertyName("name")]       public string? Name { get; set; }
            [JsonPropertyName("body")]       public string? Body { get; set; }
            [JsonPropertyName("html_url")]   public string? HtmlUrl { get; set; }
            [JsonPropertyName("draft")]      public bool Draft { get; set; }
            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
            [JsonPropertyName("assets")]     public List<GhAsset>? Assets { get; set; }
        }

        private sealed class GhAsset
        {
            [JsonPropertyName("name")]                 public string? Name { get; set; }
            [JsonPropertyName("size")]                 public long Size { get; set; }
            [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; set; }
        }
    }
}
