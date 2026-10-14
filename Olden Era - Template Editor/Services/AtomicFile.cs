using System.IO;
using System.Text;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Writes beside the destination, then replaces it only after the complete UTF-8 file is flushed.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content, bool overwrite = true, Action<string>? validateDestination = null)
    {
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".aurorarmg-{Guid.NewGuid():N}.tmp");
        validateDestination?.Invoke(destination);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
                    writer.Write(content);
                stream.Flush(flushToDisk: true);
            }

            validateDestination?.Invoke(destination);
            if (!overwrite) File.Move(temporary, destination, overwrite: false);
            else if (File.Exists(destination))
                File.Replace(temporary, destination, destinationBackupFileName: null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            // Cleanup must not hide the original write/replace error.
            try { if (File.Exists(temporary)) { validateDestination?.Invoke(temporary); File.Delete(temporary); } }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
