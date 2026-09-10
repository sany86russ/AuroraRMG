using System.Collections.Generic;
using System.Windows.Media;
using System.ComponentModel;
using System.Globalization;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace OldenEraTemplateEditor.Models
{
    public enum BonusPresetType
    {
        TownPortalFree   = 0,
        Spell            = 1,
        UnitMultiplier   = 2,
        MovementBonus    = 3,
        StartingItem     = 4,
        StartingGold     = 5,
        StartingGems     = 6,
        StartingCrystals = 7,
        StartingMercury  = 8,
        StartingWood     = 9,
        StartingOre      = 10,
    }

    /// <summary>UI view-model for a single configurable game-start bonus.</summary>
    public class BonusEntry : INotifyPropertyChanged
    {
        public BonusPresetType PresetType     { get; set; } = BonusPresetType.TownPortalFree;
        /// <summary>"start_hero" or "all_heroes"</summary>
        public string          ReceiverFilter { get; set; } = "start_hero";
        /// <summary>Spell sid / item sid / numeric value depending on type.</summary>
        public string          Param          { get; set; } = "";
        /// <summary>For Spell: "1" = free, "0" = normal. Unused for other types.</summary>
        public string          Param2         { get; set; } = "0";

        public string ReceiverLabel => LocalizationManager.T(ReceiverFilter == "start_hero" ? "S.Bonus.014" : "S.Bonus.015");
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RefreshLanguage()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReceiverLabel)));
        }

        public bool ShowReceiverLabel => PresetType is not (
            BonusPresetType.StartingGold or
            BonusPresetType.StartingGems or
            BonusPresetType.StartingCrystals or
            BonusPresetType.StartingMercury or
            BonusPresetType.StartingWood or
            BonusPresetType.StartingOre);

        public string DisplayName => PresetType switch
        {
            BonusPresetType.TownPortalFree                  => LocalizationManager.T("S.Bonus.TownPortalFree"),
            BonusPresetType.Spell when Param2 == "1"        => LocalizationManager.T("S.Bonus.FreeSpell", SpellLabel(Param)),
            BonusPresetType.Spell                           => $"{LocalizationManager.T("S.Bonus.003")}: {SpellLabel(Param)}",
            BonusPresetType.UnitMultiplier                  => $"{LocalizationManager.T("S.Bonus.004")} ×{Param}",
            BonusPresetType.MovementBonus                   => $"{LocalizationManager.T("S.Bonus.005")} +{Param}",
            BonusPresetType.StartingItem                    => $"{LocalizationManager.T("S.Bonus.006")}: {GameLabels.Name(Param)}",
            BonusPresetType.StartingGold                    => $"{LocalizationManager.T("S.Bonus.007")}: {Param}",
            BonusPresetType.StartingGems                    => $"{LocalizationManager.T("S.Bonus.008")}: {Param}",
            BonusPresetType.StartingCrystals                => $"{LocalizationManager.T("S.Bonus.009")}: {Param}",
            BonusPresetType.StartingMercury                 => $"{LocalizationManager.T("S.Bonus.010")}: {Param}",
            BonusPresetType.StartingWood                    => $"{LocalizationManager.T("S.Bonus.011")}: {Param}",
            BonusPresetType.StartingOre                     => $"{LocalizationManager.T("S.Bonus.012")}: {Param}",
            _                                               => PresetType.ToString(),
        };

        private static string SpellLabel(string sid)
        {
            return GameLabels.Name(sid);
        }

        private static readonly Brush MagicDotBrush    = CreateFrozenBrush(Color.FromRgb(147, 112, 219));
        private static readonly Brush CombatDotBrush   = CreateFrozenBrush(Color.FromRgb(205,  92,  92));
        private static readonly Brush MovementDotBrush = CreateFrozenBrush(Color.FromRgb(100, 149, 237));
        private static readonly Brush SetDotBrush      = CreateFrozenBrush(Color.FromRgb(186,  85, 211));
        private static readonly Brush ResourceDotBrush = CreateFrozenBrush(Color.FromRgb(218, 165,  32));

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public Brush DotBrush => PresetType switch
        {
            BonusPresetType.TownPortalFree or BonusPresetType.Spell
                => MagicDotBrush,    // medium purple (magic)
            BonusPresetType.UnitMultiplier
                => CombatDotBrush,   // indian red (combat)
            BonusPresetType.MovementBonus
                => MovementDotBrush, // cornflower blue (movement)
            BonusPresetType.StartingItem
                => SetDotBrush,      // medium orchid (set)
            _ /* resources */
                => ResourceDotBrush, // goldenrod (resources)
        };

        /// <summary>Expands this entry into one or two raw Bonus objects for the template.</summary>
        public List<Bonus> ToBonuses()
        {
            var list = new List<Bonus>();
            switch (PresetType)
            {
                case BonusPresetType.TownPortalFree:
                    list.Add(new Bonus { Sid = "add_bonus_hero_spell", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["neutral_magic_town_portal"] });
                    list.Add(new Bonus { Sid = "add_bonus_hero_stat",  ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["magicCostSidSet", "neutral_magic_town_portal", "-999", "0"] });
                    break;
                case BonusPresetType.Spell:
                    list.Add(new Bonus { Sid = "add_bonus_hero_spell", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [Param] });
                    if (Param2 == "1")
                        list.Add(new Bonus { Sid = "add_bonus_hero_stat", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["magicCostSidSet", Param, "-999", "0"] });
                    break;
                case BonusPresetType.UnitMultiplier:
                    string multiplier = NumericInput.TryDouble(Param, out double factor)
                        ? factor.ToString(CultureInfo.InvariantCulture) : Param;
                    list.Add(new Bonus { Sid = "add_bonus_hero_unit_multipler", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [multiplier] });
                    break;
                case BonusPresetType.MovementBonus:
                    list.Add(new Bonus { Sid = "add_bonus_hero_stat", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["movementBonus", Param] });
                    break;
                case BonusPresetType.StartingItem:
                    list.Add(new Bonus { Sid = "add_bonus_hero_item", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [Param] });
                    break;
                case BonusPresetType.StartingGold:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["gold",      Param] });
                    break;
                case BonusPresetType.StartingGems:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["gemstones", Param] });
                    break;
                case BonusPresetType.StartingCrystals:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["crystals",  Param] });
                    break;
                case BonusPresetType.StartingMercury:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["mercury",   Param] });
                    break;
                case BonusPresetType.StartingWood:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["wood",      Param] });
                    break;
                case BonusPresetType.StartingOre:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["ore",       Param] });
                    break;
            }
            return list;
        }

        public bool HasValidParameters() => PresetType switch
        {
            BonusPresetType.TownPortalFree => true,
            BonusPresetType.Spell or BonusPresetType.StartingItem => !string.IsNullOrWhiteSpace(Param),
            BonusPresetType.UnitMultiplier => NumericInput.TryDouble(Param, out var value) && value > 0,
            _ => System.Enum.IsDefined(PresetType) && NumericInput.TryOptionalInt(Param, out var amount) && amount is >= 0,
        };

        // ── Serialization ─────────────────────────────────────────────────────────

        /// <summary>Serializes to a compact pipe-separated string for storage.</summary>
        public override string ToString() =>
            $"{PresetType}|{ReceiverFilter}|{Param}|{Param2}";

        /// <summary>Deserializes from a pipe-separated string produced by <see cref="ToString"/>.</summary>
        public static BonusEntry? FromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var p = s.Split('|');
            if (p.Length < 4) return null;

            // Support both legacy numeric format and current name-based format.
            BonusPresetType presetType;
            if (int.TryParse(p[0], out int t))
                presetType = (BonusPresetType)t;
            else if (!System.Enum.TryParse(p[0], ignoreCase: true, out presetType))
                return null;

            return new BonusEntry
            {
                PresetType     = presetType,
                ReceiverFilter = p[1],
                Param          = p[2],
                Param2         = p[3],
            };
        }
    }
}
