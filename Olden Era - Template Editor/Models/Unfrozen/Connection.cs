using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class Connection : RmgNode
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("connectionType")]
        public string? ConnectionType { get; set; }

        [JsonPropertyName("guardZone")]
        public string? GuardZone { get; set; }

        [JsonPropertyName("guardEscape")]
        public bool? GuardEscape { get; set; }

        [JsonPropertyName("simTurnSquad")]
        public bool? SimTurnSquad { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("guardMatchGroup")]
        public string? GuardMatchGroup { get; set; }

        [JsonPropertyName("portalPlacementRulesFrom")]
        public List<ContentPlacementRule>? PortalPlacementRulesFrom { get; set; }

        [JsonPropertyName("portalPlacementRulesTo")]
        public List<ContentPlacementRule>? PortalPlacementRulesTo { get; set; }

        [JsonPropertyName("road")]
        public bool? Road { get; set; }

        /// <summary>
        /// Per-connection guard-strength jitter (engine <c>guardRandomization</c>, e.g. <c>0.15</c>).
        /// Used by 166 connections across the stock templates; the generator leaves it unset so the
        /// zone-level randomization applies.
        /// </summary>
        [JsonPropertyName("guardRandomization")]
        public double? GuardRandomization { get; set; }

        [JsonPropertyName("gatePlacement")]
        public string? GatePlacement { get; set; }

        /// <summary>Arguments for <see cref="GatePlacement"/> (e.g. the zone a gate is placed near).</summary>
        [JsonPropertyName("gatePlacementArgs")]
        public List<string>? GatePlacementArgs { get; set; }

        [JsonPropertyName("length")]
        public double? Length { get; set; }
    }
}
