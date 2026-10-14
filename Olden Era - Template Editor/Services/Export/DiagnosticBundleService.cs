using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Export;

public sealed record DiagnosticBundleRequest
{
    public RmgTemplate? Template { get; init; }
    public SettingsFile? Settings { get; init; }
    public GameInstallation? Installation { get; init; }
    public int? TemplateSeed { get; init; }
    public int? GameSeed { get; init; }
    public string AlgorithmVersion { get; init; } = "aurora-generator-2";
    /// <summary>Only files explicitly selected by the user. There is no automatic log or dump discovery.</summary>
    public IReadOnlyList<string> SelectedLogPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TemplateValidationIssue> ValidationIssues { get; init; } = Array.Empty<TemplateValidationIssue>();
}
public sealed record DiagnosticLogReceipt(int Index, int BytesIncluded, bool Truncated, string? Error);
public sealed record DiagnosticOmissionReceipt(string Field, int CompactJsonByteCount, string CompactJsonSha256, string Reason);
public sealed record DiagnosticBundleReceipt(string Path, int Redactions, IReadOnlyList<DiagnosticLogReceipt> Logs)
{
    public IReadOnlyList<DiagnosticOmissionReceipt> Omissions { get; init; } = Array.Empty<DiagnosticOmissionReceipt>();
}

public static class DiagnosticBundleService
{
    public const int MaximumLogs = 8;
    public const int MaximumLogBytes = 64 * 1024;
    public const int MaximumJsonBytes = 1024 * 1024;
    private const string NativeSchemaOmissionReason = "Native schema content is omitted from the minimal diagnostic copy; compact JSON size and SHA256 are retained. The original settings are unchanged.";
    private static readonly JsonSerializerOptions CompactJsonOptions = new(JsonExport.Options) { WriteIndented = false, MaxDepth = 128 };
    private static readonly JsonSerializerOptions MinimalSettingsOptions = CreateMinimalSettingsOptions();

    public static async Task<DiagnosticBundleReceipt> CreateAsync(string zipPath, DiagnosticBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SelectedLogPaths);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SelectedLogPaths.Count > MaximumLogs) throw new ArgumentOutOfRangeException(nameof(request), $"Select at most {MaximumLogs} log files.");
        var files = new List<(string Name, byte[] Bytes)>();
        var logs = new List<DiagnosticLogReceipt>();
        var omissions = new List<DiagnosticOmissionReceipt>();
        var actualSettings = request.Settings is null ? null : SettingsSnapshotMapper.RestoreSnapshot(request.Settings);
        int redactions = 0;
        if (request.Template is not null) files.Add(("template.sanitized.rmg.json", SanitizeJson(request.Template, ref redactions)));
        if (request.Settings is not null) files.Add(("settings.sanitized.oetgs", SanitizeJson(MinimalSettingsCopy(request.Settings, omissions), ref redactions)));
        files.Add(("validation.json", SanitizeJson(request.ValidationIssues, ref redactions)));
        for (int i = 0; i < request.SelectedLogPaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string path = Path.GetFullPath(request.SelectedLogPaths[i]);
                var extension = Path.GetExtension(path);
                if (!new[] { ".log", ".txt", ".jsonl" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only explicitly selected .log, .txt and .jsonl text files may be included.");
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
                long available = stream.Length;
                bool truncated = available > MaximumLogBytes;
                var header = new byte[(int)Math.Min(available, 4)];
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                bool utf16Le = header.Length >= 2 && header[0] == 0xff && header[1] == 0xfe;
                bool utf16Be = header.Length >= 2 && header[0] == 0xfe && header[1] == 0xff;
                long start = truncated ? available - MaximumLogBytes : 0;
                if ((utf16Le || utf16Be) && start % 2 != 0) start++;
                stream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[(int)Math.Min(available - start, MaximumLogBytes)];
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
                int leading = 0;
                if (truncated && !utf16Le && !utf16Be)
                    while (leading < Math.Min(4, buffer.Length) && (buffer[leading] & 0xc0) == 0x80) leading++;
                string text = utf16Le ? Encoding.Unicode.GetString(buffer) : utf16Be ? Encoding.BigEndianUnicode.GetString(buffer) :
                    new UTF8Encoding(false, true).GetString(buffer, leading, buffer.Length - leading);
                text = text.TrimStart('\ufeff');
                if (text.Contains('\0')) throw new InvalidDataException("The selected file contains binary data and was excluded.");
                // A tail may begin inside a password/path value after its identifying prefix was cut off.
                // Only complete physical lines are safe to run through the text sanitizer.
                if (start > 0) text = DropIncompleteFirstLine(text);
                string sanitized = DiagnosticSanitizer.SanitizeText(text, out int count);
                redactions += count;
                byte[] bytes = Encoding.UTF8.GetBytes(sanitized);
                if (bytes.Length > MaximumLogBytes)
                { bytes = Encoding.UTF8.GetBytes(Utf8Tail(sanitized, MaximumLogBytes)); truncated = true; }
                files.Add(($"logs/selected-{i + 1}.txt", bytes));
                logs.Add(new(i + 1, bytes.Length, truncated, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
                string error = DiagnosticSanitizer.SanitizeText(ex.Message, out int count); redactions += count;
                logs.Add(new(i + 1, 0, false, error));
            }
        }
        var source = request.Installation;
        var manifest = new
        {
            FormatVersion = 3,
            AuroraVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            request.AlgorithmVersion,
            ProfileId = source?.Profile.Id ?? GameCompatibilityProfile.Legacy.Id,
            GenerationProfileId = actualSettings?.ProfileOptions.ProfileId ?? request.Settings?.ProfileOptions.ProfileId,
            GameDataProfileId = source?.Profile.Id ?? GameCompatibilityProfile.Legacy.Id,
            GameVersionMarker = source?.VersionMarker,
            SteamBuildId = source?.SteamBuildId,
            CoreSha256 = source?.SourceFingerprint,
            SchemaSha256 = source?.SchemaFingerprint,
            request.TemplateSeed, request.GameSeed,
            RedactionCount = redactions,
            ExplicitlySelectedLogs = logs,
            OmittedPayloads = omissions,
            Files = files.Select(f => new { f.Name, ByteCount = f.Bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(f.Bytes)) }).ToArray(),
            KnownSensitivePatternsRedacted = true,
            AutomaticallyIncludesGameAssetsOrDumps = false,
            PrivacyReviewRequiredBeforeSharing = true,
            SentExternally = false,
            GamePlayabilityVerified = false
        };
        byte[] manifestBytes = SanitizeJson(manifest, ref redactions);
        var manifestNode = JsonNode.Parse(manifestBytes)!.AsObject();
        manifestNode["RedactionCount"] = redactions;
        manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifestNode, JsonExport.Options);
        files.Add(("manifest.json", manifestBytes));
        files.Add(("readme.ru.txt", Encoding.UTF8.GetBytes("Диагностический пакет AuroraRMG. Создан локально, никуда не отправлен.\n" +
            "Шаблон и настройки прошли удаление известных секретов и путей; это может изменить исходные текстовые поля.\n" +
            "Логи добавлены только по явному выбору пользователя; большие файлы представлены последними 64 КиБ.\n" +
            "Импортированные native-схемы исключены из диагностической копии; размер компактного JSON, SHA256 и причина указаны в manifest.json. Исходные настройки сохранены.\n" +
            "Игровые файлы, дампы, настройки учётных записей и адреса папок автоматически не включаются.\n" +
            "Удаляются известные признаки секретов и путей; произвольные текстовые данные нужно просмотреть перед передачей пакета.\n")));
        await AtomicArchive.WriteAsync(zipPath, async (zip, token) =>
        {
            foreach (var file in files) await AtomicArchive.AddAsync(zip, file.Name, file.Bytes, token);
        }, cancellationToken).ConfigureAwait(false);
        return new(Path.GetFullPath(zipPath), redactions, logs) { Omissions = omissions.ToArray() };
    }

    private static JsonSerializerOptions CreateMinimalSettingsOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(SettingsFile) && info.Type != typeof(GenerationProfileOptions)) return;
            foreach (var property in info.Properties)
                if (info.Type == typeof(SettingsFile) && property.Name == "generatorSnapshot"
                    || info.Type == typeof(GenerationProfileOptions) && property.Name == nameof(GenerationProfileOptions.ImportedNativeSchema))
                    property.ShouldSerialize = (_, _) => false;
        });
        return new(JsonExport.Options) { TypeInfoResolver = resolver };
    }

    private static JsonObject MinimalSettingsCopy(SettingsFile settings, List<DiagnosticOmissionReceipt> omissions)
    {
        // Exclude the known schema before serializing settings, rather than duplicating its game payload into the bundle.
        var copy = JsonSerializer.SerializeToNode(settings, MinimalSettingsOptions)!.AsObject();
        if (settings.ProfileOptions?.ImportedNativeSchema is { } schema)
            RecordOmission("profileOptions.ImportedNativeSchema", JsonSerializer.SerializeToUtf8Bytes(schema, CompactJsonOptions));
        if (settings.GeneratorSnapshot is { } snapshot)
        {
            if (snapshot.ValueKind != JsonValueKind.Object) throw new JsonException("The generator snapshot must be an object.");
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in snapshot.EnumerateObject())
                {
                    if (property.Name != "ProfileOptions" || property.Value.ValueKind != JsonValueKind.Object)
                    { property.WriteTo(writer); continue; }
                    writer.WritePropertyName(property.Name); writer.WriteStartObject();
                    foreach (var profileProperty in property.Value.EnumerateObject())
                    {
                        if (profileProperty.Name == nameof(GenerationProfileOptions.ImportedNativeSchema) && profileProperty.Value.ValueKind != JsonValueKind.Null)
                            RecordOmission("generatorSnapshot.ProfileOptions.ImportedNativeSchema", JsonSerializer.SerializeToUtf8Bytes(profileProperty.Value, CompactJsonOptions));
                        else profileProperty.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            copy["generatorSnapshot"] = JsonNode.Parse(stream.ToArray());
        }
        return copy;
        void RecordOmission(string field, byte[] compactJson) => omissions.Add(new(field, compactJson.Length,
            Convert.ToHexString(SHA256.HashData(compactJson)), NativeSchemaOmissionReason));
    }

    private static byte[] SanitizeJson<T>(T value, ref int redactions)
    {
        byte[] raw = JsonSerializer.SerializeToUtf8Bytes(value, JsonExport.Options);
        if (raw.Length > MaximumJsonBytes) throw new InvalidDataException("A diagnostic JSON snapshot exceeds the 1 MiB limit.");
        var node = JsonNode.Parse(raw);
        DiagnosticSanitizer.SanitizeNode(node, ref redactions);
        return JsonSerializer.SerializeToUtf8Bytes(node, JsonExport.Options);
    }
    private static string Utf8Tail(string text, int maxBytes)
    {
        int low = 0, high = text.Length;
        while (low < high)
        {
            int midpoint = low + (high - low) / 2;
            if (Encoding.UTF8.GetByteCount(text.AsSpan(midpoint)) > maxBytes) low = midpoint + 1;
            else high = midpoint;
        }
        if (low > 0 && low < text.Length && char.IsLowSurrogate(text[low]) && char.IsHighSurrogate(text[low - 1])) low++;
        return text[low..];
    }

    private static string DropIncompleteFirstLine(string text)
    {
        int next = NextLine(0);
        // A complete physical continuation is still part of the header whose identifying prefix was cut off.
        while (next < text.Length && text[next] is ' ' or '\t') next = NextLine(next);
        return text[next..];
        int NextLine(int start)
        {
            int end = text.IndexOfAny(['\r', '\n'], start);
            if (end < 0) return text.Length;
            int result = end + 1;
            if (text[end] == '\r' && result < text.Length && text[result] == '\n') result++;
            return result;
        }
    }
}

public static class DiagnosticSanitizer
{
    // HTTP credentials contain a scheme and may contain spaces, commas, cookie attributes and folded lines.
    // Mask the complete header value before a generic key/value rule could remove only its scheme.
    private static readonly Regex CredentialHeaders = new(@"(?im)(\b[A-Za-z0-9_-]*(?:authorization|cookie)\b[""']?[ \t]*[:=][ \t]*)[^\r\n]*(?:(?:\r\n|\r|\n)[ \t]+[^\r\n]*)*", RegexOptions.Compiled);
    private static readonly Regex Credentials = new(@"(?i)(\b[A-Za-z0-9_-]*(?:api[_-]?key|token|password|passwd|secret|authorization|cookie|connection[_-]?string|private[_-]?key)\b[""']?\s*[:=]\s*)(?:""(?:[^""\\]|\\.)*""|'[^']*'|[^\s,;]+)", RegexOptions.Compiled);
    private static readonly Regex Bearer = new(@"(?i)\bBearer\s+[A-Za-z0-9\-_.~+/]+=*", RegexOptions.Compiled);
    private static readonly Regex ApiTokens = new(@"\bsk-[A-Za-z0-9_-]{16,}\b", RegexOptions.Compiled);
    private static readonly Regex UrlCredentials = new(@"(?i)(https?://)[^/\s:@]+:[^/\s@]+@", RegexOptions.Compiled);
    private static readonly Regex WindowsPaths = new(@"(?<![A-Za-z0-9])(?:[A-Za-z]:[\\/]|\\\\)[^\r\n""'<>|]*", RegexOptions.Compiled);
    private static readonly Regex UserPaths = new(@"(?i)/(?:home|Users)/[^\r\n""'<>|]+", RegexOptions.Compiled);
    private static readonly Regex Emails = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly string[] SecretKeySuffixes =
    ["password", "passwd", "secret", "token", "apikey", "authorization", "cookie", "connectionstring", "privatekey", "secretkey"];

    public static string SanitizeText(string text, out int redactions)
    {
        int count = 0;
        string Replace(Regex regex, string replacement) => regex.Replace(text, m =>
        {
            string result = m.Result(replacement);
            if (result != m.Value) count++;
            return result;
        });
        text = Replace(CredentialHeaders, "$1[REDACTED]");
        text = Replace(Credentials, "$1[REDACTED]");
        text = Replace(Bearer, "Bearer [REDACTED]");
        text = Replace(ApiTokens, "[REDACTED]");
        text = Replace(UrlCredentials, "$1[REDACTED]@");
        text = Replace(WindowsPaths, "[PATH]"); text = Replace(UserPaths, "[PATH]"); text = Replace(Emails, "[EMAIL]");
        foreach (var identity in new[] { Environment.UserName, Environment.MachineName }.Where(i => i.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase))
            text = Regex.Replace(text, @"(?i)(?<!\w)" + Regex.Escape(identity) + @"(?!\w)", _ => { count++; return "[LOCAL_IDENTITY]"; });
        redactions = count;
        return text;
    }

    internal static void SanitizeNode(JsonNode? node, ref int redactions)
    {
        if (node is JsonObject obj)
        {
            int keyIndex = 0;
            foreach (var property in obj.ToArray())
            {
                string key = property.Key;
                _ = SanitizeText(key, out int keyRedactions);
                if (keyRedactions > 0)
                {
                    do { key = "[REDACTED_KEY_" + (++keyIndex).ToString(System.Globalization.CultureInfo.InvariantCulture) + "]"; } while (obj.ContainsKey(key));
                    obj.Remove(property.Key); obj[key] = property.Value; redactions += keyRedactions;
                }
                if (IsSensitiveKey(property.Key)) { obj[key] = "[REDACTED]"; redactions++; }
                else if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                { obj[key] = SanitizeText(text, out int count); redactions += count; }
                else SanitizeNode(property.Value, ref redactions);
            }
        }
        else if (node is JsonArray array)
            for (int i = 0; i < array.Count; i++)
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                { array[i] = SanitizeText(text, out int count); redactions += count; }
                else SanitizeNode(array[i], ref redactions);
    }

    private static bool IsSensitiveKey(string key)
    {
        string normalized = new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return SecretKeySuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.Ordinal));
    }

    internal static bool HasKnownCredentials(JsonNode? node)
    {
        if (node is JsonObject obj) return obj.Any(property => IsSensitiveKey(property.Key) || IsKnownCredentialText(property.Key) || HasKnownCredentials(property.Value));
        if (node is JsonArray array) return array.Any(HasKnownCredentials);
        if (node is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
            return IsKnownCredentialText(text);
        return false;
    }
    private static bool IsKnownCredentialText(string text) => CredentialHeaders.IsMatch(text) || Credentials.IsMatch(text) || Bearer.IsMatch(text) || ApiTokens.IsMatch(text) || UrlCredentials.IsMatch(text);
}
