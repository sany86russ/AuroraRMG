using System.Windows;
using System.Windows.Media;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// UI view-model for a single row in the banned-items / spells / heroes ListBox.
    /// </summary>
    public class BanEntry
    {
        private static readonly Brush MovementCategoryBrush  = CreateFrozenBrush(Color.FromRgb(100, 149, 237)); // cornflower blue
        private static readonly Brush DiplomacyCategoryBrush = CreateFrozenBrush(Color.FromRgb(218, 165,  32)); // goldenrod
        private static readonly Brush CombatCategoryBrush    = CreateFrozenBrush(Color.FromRgb(205,  92,  92)); // indian red
        private static readonly Brush MagicCategoryBrush     = CreateFrozenBrush(Color.FromRgb(147, 112, 219)); // medium purple
        private static readonly Brush SetCategoryBrush       = CreateFrozenBrush(Color.FromRgb(186,  85, 211)); // medium orchid
        // Hero faction colours
        private static readonly Brush DemonFactionBrush      = CreateFrozenBrush(Color.FromRgb(205,  74,  62)); // ember red
        private static readonly Brush DungeonFactionBrush    = CreateFrozenBrush(Color.FromRgb(138,  92, 196)); // dark amethyst
        private static readonly Brush HumanFactionBrush      = CreateFrozenBrush(Color.FromRgb(212, 175,  85)); // royal gold
        private static readonly Brush NatureFactionBrush     = CreateFrozenBrush(Color.FromRgb( 94, 179, 110)); // forest green
        private static readonly Brush UnfrozenFactionBrush   = CreateFrozenBrush(Color.FromRgb(102, 197, 214)); // glacier cyan
        private static readonly Brush NecroFactionBrush      = CreateFrozenBrush(Color.FromRgb(140, 156, 120)); // bone/verdigris
        private static readonly Brush DefaultCategoryBrush   = CreateFrozenBrush(Color.FromRgb(150, 150, 150)); // gray

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public string Id          { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category    { get; set; } = "";

        /// <summary>Optional game icon for this row. When set, it replaces the coloured category dot.</summary>
        public ImageSource? Icon  { get; set; }

        public Visibility IconVisibility => Icon is null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility DotVisibility  => Icon is null ? Visibility.Visible  : Visibility.Collapsed;

        public Brush CategoryBrush => Category switch
        {
            "Movement"  => MovementCategoryBrush,
            "Diplomacy" => DiplomacyCategoryBrush,
            "Combat"    => CombatCategoryBrush,
            "Magic"     => MagicCategoryBrush,
            "Set"       => SetCategoryBrush,
            "Spell"     => MagicCategoryBrush,
            "Demon"     => DemonFactionBrush,
            "Dungeon"   => DungeonFactionBrush,
            "Human"     => HumanFactionBrush,
            "Nature"    => NatureFactionBrush,
            "Unfrozen"  => UnfrozenFactionBrush,
            "Necro"     => NecroFactionBrush,
            "Necros"    => NecroFactionBrush,
            "Undead"    => NecroFactionBrush,
            _           => DefaultCategoryBrush,
        };
    }
}
