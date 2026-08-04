using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// Base for every POCO that mirrors a <c>.rmg.json</c> object.
    /// <para>
    /// The models below cover the fields the generator writes and the editor edits, but the game's
    /// schema is larger and keeps growing with patches. Without a catch-all, <b>every unmodelled field
    /// is silently dropped</b> the moment a template is loaded and saved again — e.g. the stock
    /// templates' <c>connections[].guardRandomization</c> (166 occurrences), <c>mainObjects[].factions</c>
    /// (92) or a zone's <c>randomHire*</c> arrays would vanish from an imported map.
    /// </para>
    /// <para>
    /// <see cref="Extra"/> captures anything unknown as raw <see cref="JsonElement"/>s and writes it back
    /// verbatim, so a load→save round-trip is lossless and a future game patch cannot break an import.
    /// It stays <c>null</c> for templates the generator builds itself, so generated output is unchanged.
    /// </para>
    /// </summary>
    public abstract class RmgNode
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
