using System.IO;
using System.Text;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Writes beside the destination, then replaces it only after the complete UTF-8 file is flushed.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".aurorarmg-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
                    writer.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(destination))
                File.Replace(temporary, destination, destinationBackupFileName: null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            // Cleanup must not hide the original write/replace error.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
