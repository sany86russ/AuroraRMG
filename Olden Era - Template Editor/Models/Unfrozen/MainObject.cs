using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class MainObject : RmgNode
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("spawn")]
        public string? Spawn { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("guardChance")]
        public double? GuardChance { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("removeGuardIfHasOwner")]
        public bool? RemoveGuardIfHasOwner { get; set; }

        [JsonPropertyName("buildingsConstructionSid")]
        public string? BuildingsConstructionSid { get; set; }

        [JsonPropertyName("faction")]
        public TypedSelector? Faction { get; set; }

        [JsonPropertyName("placement")]
        public string? Placement { get; set; }

        [JsonPropertyName("placementArgs")]
        public List<string>? PlacementArgs { get; set; }

        /// <summary>Per-object guard-strength jitter (engine <c>guardRandomization</c>).</summary>
        [JsonPropertyName("guardRandomization")]
        public double? GuardRandomization { get; set; }

        /// <summary>Marks a town as the player's starting city (engine <c>isStartCity</c>).</summary>
        [JsonPropertyName("isStartCity")]
        public bool? IsStartCity { get; set; }

        /// <summary>Building ban list applied to this town (engine <c>buildingsBanSid</c>).</summary>
        [JsonPropertyName("buildingsBanSid")]
        public string? BuildingsBanSid { get; set; }

        /// <summary>Marks the object as a key objective (engine <c>isKeyObject</c>).</summary>
        [JsonPropertyName("isKeyObject")]
        public bool? IsKeyObject { get; set; }

        [JsonPropertyName("holdCityWinCon")]
        public bool? HoldCityWinCon { get; set; }
    }

    public class TypedSelector : RmgNode
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }
    }
}
