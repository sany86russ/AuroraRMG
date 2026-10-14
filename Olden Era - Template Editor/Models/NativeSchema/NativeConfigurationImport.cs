using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Models.NativeSchema;

/// <summary>Stable diagnostic code and optional exact field name, suitable for localization by the caller.</summary>
public sealed record NativeImportWarning(string Code, string Message, string? Field = null);

/// <summary>
/// Detached import result. Raw inputs remain available even when Aurora cannot represent a native feature.
/// Advanced settings retain exact dimensions; Quick mode continues to choose dimensions within its size band.
/// </summary>
public sealed record NativeConfigurationImport(
    GeneratorSettings Settings,
    QuickGenerateOptions QuickOptions,
    JsonObject RawConfiguration,
    JsonObject? RawSchema,
    IReadOnlyList<NativeImportWarning> Warnings)
{
    public GenerationProfileOptions ProfileOptions => Settings.ProfileOptions;
}
