using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Olden_Era___Template_Editor.Services.GameData;

public enum GameCapability
{
    Alliances, TemplateMetadata, Native256Maps, FractionalGuardWeights, Rivers,
    WeeklyRecruitment, MagicRules, StartingHeroVictory, SchemaInspection
}

public enum GameProfileStatus { LegacyAurora, InspectedGameData, UnknownVersion }

/// <summary>Data compatibility evidence, not a promise of playable in-game generation.</summary>
public sealed record GameCompatibilityProfile(string Id, GameProfileStatus Status,
    IReadOnlyList<GameCapability> ConfirmedCapabilities, string Evidence)
{
    public bool Supports(GameCapability capability) => ConfirmedCapabilities.Contains(capability);
    public static GameCompatibilityProfile Legacy { get; } = new("aurora-legacy", GameProfileStatus.LegacyAurora,
        Array.Empty<GameCapability>(), "Offline Aurora defaults; installed-game compatibility has not been checked.");

    // Hashes are provenance only; no proprietary configuration resources are embedded in the app.
    public const string InspectedCoreSha256 = "F11B90C19AAE60908A4D503E639F2ABF7A02C5D706CDB072879EF9773812D954";
    public const string InspectedSchemaSha256 = "CB77341C51860E4E1B4F6225B6BC380660B794986FD4407BF10432F3250B7509";
    public static GameCompatibilityProfile FromEvidence(string? version, string? build, string? coreFingerprint = null, string? schemaFingerprint = null)
        => version == "0.81.04" && build == "25672315" &&
            string.Equals(coreFingerprint, InspectedCoreSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(schemaFingerprint, InspectedSchemaSha256, StringComparison.OrdinalIgnoreCase)
            ? new("olden-era-0.81.04", GameProfileStatus.InspectedGameData, Enum.GetValues<GameCapability>(),
                "Inspected 0.81.04 data/build 25672315. Game-level placement and victory validation remain separate.")
            : version == "0.81.04" && build == "25672315"
                ? new("olden-era-unverified-data", GameProfileStatus.UnknownVersion, Array.Empty<GameCapability>(),
                    "The version/build is recognized, but content or schema fingerprints differ from the inspected snapshot. Existing Aurora generation remains available.")
            : new("olden-era-unverified", GameProfileStatus.UnknownVersion, Array.Empty<GameCapability>(),
                "This version/build has not been audited. Existing Aurora generation remains available.");
}

public sealed record GameInstallation
{
    public string InstallPath { get; init; } = "";
    public string CoreZipPath { get; init; } = "";
    public string TemplatesPath { get; init; } = "";
    public string SchemaPath { get; init; } = "";
    public string? SteamBuildId { get; init; }
    public string? VersionMarker { get; init; }
    public string VersionEvidence { get; init; } = "";
    public string SourceFingerprint { get; init; } = "";
    public string SchemaFingerprint { get; init; } = "";
    public long SourceStamp { get; init; }
    public long SourceLength { get; init; }
    public GameCompatibilityProfile Profile { get; init; } = GameCompatibilityProfile.Legacy;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>One read-only installation locator shared by catalog loading and export.</summary>
public sealed class GameInstallationService
{
    public static GameInstallationService Instance { get; } = new();
    private readonly string[]? _steamRoots;
    private readonly object _sync = new();
    private string? _manualInstallPath;
    private string _cachedIdentity = "";
    private GameInstallation? _cached;
    public IReadOnlyList<string> LastWarnings { get; private set; } = Array.Empty<string>();

    // Explicit roots make discovery testable without probing the user's other libraries.
    public GameInstallationService(IEnumerable<string>? steamRoots = null) => _steamRoots = steamRoots?.ToArray();
    public string? ManualInstallPath
    {
        get { lock (_sync) return _manualInstallPath; }
        set { lock (_sync) { if (_manualInstallPath != value) { _manualInstallPath = value; _cachedIdentity = ""; _cached = null; } } }
    }

    public GameInstallation? Discover(bool forceRefresh = false)
    {
        lock (_sync)
        {
            var warnings = new List<string>();
            try
            {
                string? install = null, manifest = null;
                if (!string.IsNullOrWhiteSpace(_manualInstallPath))
                {
                    install = NormalizeInstallPath(_manualInstallPath);
                    if (install is null || !File.Exists(CorePath(install)))
                    {
                        LastWarnings = new[] { "The selected game path does not contain HeroesOldenEra_Data/StreamingAssets/Core.zip." };
                        _cached = null; _cachedIdentity = "";
                        return null;
                    }
                    manifest = FindManifestForInstall(install);
                }
                else
                {
                    foreach (var library in Libraries(warnings))
                    {
                        var file = Path.Combine(library, "steamapps", "appmanifest_3105440.acf");
                        if (!File.Exists(file)) continue;
                        var values = ReadManifest(file);
                        if (values.AppId != "3105440" || string.IsNullOrWhiteSpace(values.InstallDir)) continue;
                        var common = Path.GetFullPath(Path.Combine(library, "steamapps", "common"));
                        var candidate = Path.GetFullPath(Path.Combine(common, values.InstallDir));
                        if (!candidate.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        { warnings.Add("A Steam manifest has an invalid install directory; it was ignored."); continue; }
                        if (!File.Exists(CorePath(candidate))) continue;
                        install = candidate; manifest = file; break;
                    }
                }
                if (install is null)
                {
                    LastWarnings = warnings;
                    _cached = null; _cachedIdentity = "";
                    return null;
                }
                var core = new FileInfo(CorePath(install));
                var globals = Path.Combine(install, "HeroesOldenEra_Data", "globalgamemanagers");
                var schema = Path.Combine(install, "HeroesOldenEra_Data", "StreamingAssets", "map_schemas", "Default.mrmg.json");
                var identity = install + "|" + Stamp(core.FullName) + "|" + Stamp(globals) + "|" + Stamp(manifest) + "|" + Stamp(schema);
                if (!forceRefresh && _cached is not null && identity == _cachedIdentity)
                { LastWarnings = _cached.Warnings; return _cached; }
                string? build = manifest is null ? null : ReadManifest(manifest).BuildId;
                var marker = ReadVersionMarker(globals);
                if (build is null) warnings.Add("Steam build is unavailable; no manifest was found for this installation.");
                if (marker is null) warnings.Add("A unique length-prefixed game version marker was not found.");
                using var stream = new FileStream(core.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var fingerprint = Convert.ToHexString(SHA256.HashData(stream));
                string schemaFingerprint = "";
                if (File.Exists(schema))
                {
                    using var schemaStream = new FileStream(schema, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    schemaFingerprint = Convert.ToHexString(SHA256.HashData(schemaStream));
                }
                var profile = GameCompatibilityProfile.FromEvidence(marker, build, fingerprint, schemaFingerprint);
                if (profile.Status == GameProfileStatus.UnknownVersion) warnings.Add(profile.Evidence);
                _cached = new GameInstallation
                {
                    InstallPath = install, CoreZipPath = core.FullName,
                    TemplatesPath = Path.Combine(install, "HeroesOldenEra_Data", "StreamingAssets", "map_templates"),
                    SchemaPath = schema,
                    SteamBuildId = build, VersionMarker = marker,
                    VersionEvidence = marker is null ? "Version marker unavailable." : "Unique length-prefixed Unity data string; PlayerSettings property has not been decoded.",
                    SourceFingerprint = fingerprint, SchemaFingerprint = schemaFingerprint, SourceStamp = core.LastWriteTimeUtc.Ticks, SourceLength = core.Length,
                    Profile = profile, Warnings = warnings.ToArray()
                };
                _cachedIdentity = identity;
                LastWarnings = _cached.Warnings;
                return _cached;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                warnings.Add("Game discovery failed: " + ex.Message);
                LastWarnings = warnings;
                _cached = null; _cachedIdentity = "";
                return null;
            }
        }
    }

    public GameCompatibilityProfile GetProfile() => Discover()?.Profile ?? GameCompatibilityProfile.Legacy;
    private static string CorePath(string install) => Path.Combine(install, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip");
    private static string Stamp(string? path)
    { var f = path is null ? null : new FileInfo(path); return f?.Exists == true ? f.Length + ":" + f.LastWriteTimeUtc.Ticks : "missing"; }

    private IEnumerable<string> Libraries(List<string> warnings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _steamRoots ?? DefaultRoots())
        {
            string full;
            try { full = Path.GetFullPath(root); }
            catch (ArgumentException) { continue; }
            if (seen.Add(full)) yield return full;
            foreach (var library in ReadLibraries(full, warnings))
                if (seen.Add(library)) yield return library;
        }
    }

    private static string[] DefaultRoots()
    {
        var roots = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path && path.Length > 0) roots.Add(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (programFiles.Length > 0) roots.Add(Path.Combine(programFiles, "Steam"));
        return roots.ToArray();
    }

    private static IEnumerable<string> ReadLibraries(string root, List<string> warnings)
    {
        var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(file)) return Array.Empty<string>();
        try
        {
            return KeyValues(File.ReadAllText(file)).Where(x => x.Key == "path")
                .Select(x => Path.GetFullPath(x.Value)).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { warnings.Add("Steam library list could not be read: " + ex.Message); return Array.Empty<string>(); }
    }

    private static string? NormalizeInstallPath(string path)
    {
        var full = Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (File.Exists(full) && Path.GetFileName(full).Equals("Core.zip", StringComparison.OrdinalIgnoreCase))
            return Directory.GetParent(full)?.Parent?.Parent?.FullName;
        if (Path.GetFileName(full).Equals("StreamingAssets", StringComparison.OrdinalIgnoreCase)) return Directory.GetParent(full)?.Parent?.FullName;
        if (Path.GetFileName(full).Equals("HeroesOldenEra_Data", StringComparison.OrdinalIgnoreCase)) return Directory.GetParent(full)?.FullName;
        return full;
    }

    private static string? FindManifestForInstall(string install)
    {
        var common = Directory.GetParent(install);
        if (common?.Name.Equals("common", StringComparison.OrdinalIgnoreCase) != true) return null;
        var manifest = Path.Combine(common.Parent!.FullName, "appmanifest_3105440.acf");
        if (!File.Exists(manifest)) return null;
        var values = ReadManifest(manifest);
        return values.AppId == "3105440" && string.Equals(values.InstallDir, Path.GetFileName(install), StringComparison.OrdinalIgnoreCase) ? manifest : null;
    }

    // Only these public installation fields are retained; never owner/account or other manifest values.
    private static (string? AppId, string? InstallDir, string? BuildId) ReadManifest(string file)
    {
        string? app = null, dir = null, build = null;
        foreach (var pair in KeyValues(File.ReadAllText(file)))
            switch (pair.Key) { case "appid": app = pair.Value; break; case "installdir": dir = pair.Value; break; case "buildid": build = pair.Value; break; }
        return (app, dir, build);
    }

    private static IEnumerable<KeyValuePair<string, string>> KeyValues(string text)
    {
        foreach (Match m in Regex.Matches(text, "\"((?:\\\\.|[^\"\\\\])*)\"\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
            yield return new(m.Groups[1].Value, m.Groups[2].Value.Replace("\\\\", "\\").Replace("\\\"", "\""));
    }

    internal static string? ReadVersionMarker(string file)
    {
        if (!File.Exists(file)) return null;
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        // globalgamemanagers is small; keep a bounded read if a corrupt/custom file is unusually large.
        var bytes = new byte[(int)Math.Min(stream.Length, 8 * 1024 * 1024)];
        stream.ReadExactly(bytes);
        var found = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 4; i + 7 <= bytes.Length; i++)
        {
            if (bytes[i] != (byte)'0' || bytes[i + 1] != (byte)'.') continue;
            int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i - 4, 4));
            if (length is < 6 or > 15 || i + length > bytes.Length) continue;
            var value = Encoding.ASCII.GetString(bytes, i, length);
            if (Regex.IsMatch(value, @"^0\.\d{2}\.\d{2}(?:\.\d+)?$")) found.Add(value);
        }
        return found.Count == 1 ? found.Single() : null;
    }
}
