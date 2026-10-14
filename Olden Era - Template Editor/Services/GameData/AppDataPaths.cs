namespace Olden_Era___Template_Editor.Services.GameData;

/// <summary>Test/verification runs can isolate preferences without touching the owner's normal data.</summary>
internal static class AppDataPaths
{
    public static string Root => Environment.GetEnvironmentVariable("AURORARMG_DATA_DIRECTORY") is { Length: > 0 } selected
        ? Path.GetFullPath(selected)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraRMG");
}
