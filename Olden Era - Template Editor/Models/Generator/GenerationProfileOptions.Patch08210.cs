using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Models;

public enum ChampionSelection { StartingHero, HellLightSpire }

public sealed partial class GenerationProfileOptions
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ChampionSelection ChampionSelection { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? BannedSkills { get; set; }
}
