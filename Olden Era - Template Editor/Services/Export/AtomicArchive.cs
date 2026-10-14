using System.IO.Compression;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Services.Export;

internal static class AtomicArchive
{
    public static async Task WriteAsync(string path, Func<ZipArchive, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        string destination = Path.GetFullPath(path);
        ExportDestination.EnsureOutsideGame(destination);
        string directory = Path.GetDirectoryName(destination)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The selected export directory does not exist.");
        string temporary = Path.Combine(directory, $".aurora-package-{Guid.NewGuid():N}.tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, useAsync: true))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                    await write(zip, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            ExportDestination.EnsureOutsideGame(destination);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        finally
        {
            try { if (File.Exists(temporary)) { ExportDestination.EnsureOutsideGame(temporary); File.Delete(temporary); } }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
    }

    public static async Task AddAsync(ZipArchive zip, string name, byte[] bytes, CancellationToken token)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        // The files carry reproducibility information; avoid leaking machine-local file timestamps.
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }
    public static Task AddTextAsync(ZipArchive zip, string name, string text, CancellationToken token) =>
        AddAsync(zip, name, new UTF8Encoding(false, true).GetBytes(text), token);
}

internal static class ExportDestination
{
    public static void EnsureOutsideGame(string destination) => EnsureOutsideGame(destination, GameInstallationService.Instance.Discover());

    internal static void EnsureOutsideGame(string destination, GameInstallation? game)
    {
        var full = Path.GetFullPath(destination);
        RejectDestinationLinks(full);
        string physical = PhysicalPath(full);
        if (game is not null)
        {
            var root = PhysicalPath(Path.GetFullPath(game.InstallPath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (physical.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || physical.Equals(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Package, batch and diagnostic exports cannot write inside the game installation.");
        }
        // Also cover a manually chosen Steam install that has not been registered in this process.
        var normalized = physical.Replace('\\', '/');
        if (normalized.Contains("/HeroesOldenEra_Data/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/HeroesOldenEra_Data", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Package, batch and diagnostic exports cannot write inside game data.");
    }

    private static void RejectDestinationLinks(string full)
    {
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Export files must not be symbolic links or reparse points.");
        for (var current = new DirectoryInfo(Directory.Exists(full) ? full : Path.GetDirectoryName(full)!); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Export destinations must not traverse directory links or reparse points.");
    }

    private static string PhysicalPath(string full)
    {
        string existing = full; var remainder = new Stack<string>();
        while (!File.Exists(existing) && !Directory.Exists(existing))
        {
            var parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent) || parent == existing) throw new DirectoryNotFoundException("Export path has no accessible existing ancestor.");
            remainder.Push(Path.GetFileName(existing)); existing = parent;
        }
        string resolved;
        if (OperatingSystem.IsWindows())
        {
            using var handle = CreateFile(existing, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero); // OPEN_EXISTING, backup semantics; read-only metadata query
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "The physical export/source path could not be verified.");
            var buffer = new StringBuilder(512);
            uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "The physical export/source path could not be verified.");
            if (length >= buffer.Capacity)
            {
                if (length > 65536) throw new IOException("The physical export/source path is too long.");
                buffer = new StringBuilder(checked((int)length + 1));
                length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                if (length == 0 || length >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error(), "The physical export/source path could not be verified.");
            }
            resolved = buffer.ToString();
            if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
            else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) resolved = resolved[4..];
        }
        else
        {
            FileSystemInfo info = Directory.Exists(existing) ? new DirectoryInfo(existing) : new FileInfo(existing);
            resolved = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
        }
        while (remainder.Count > 0) resolved = Path.Combine(resolved, remainder.Pop());
        return Path.GetFullPath(resolved);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true, EntryPoint = "GetFinalPathNameByHandleW")]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
}
