using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for writing <c>.rmg.json</c> templates and
    /// <c>.oetgs</c> settings.
    /// <para>
    /// Critically, the encoder is <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> so that
    /// non-ASCII text (e.g. Cyrillic template names) is written as <b>literal UTF-8</b> instead of
    /// <c>\uXXXX</c> escapes. The game's JSON reader expects no-BOM UTF-8 and was mis-reading the
    /// escapes, which broke the template. <see cref="System.IO.File.WriteAllText(string,string)"/>
    /// already emits UTF-8 without a BOM, so together this produces game-compatible output.
    /// "Unsafe" here only refers to HTML/JS-injection contexts, which are irrelevant for a local
    /// game-template file.
    /// </para>
    /// </summary>
    public static class JsonExport
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }
}
