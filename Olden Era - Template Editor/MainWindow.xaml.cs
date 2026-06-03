using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OldenEraTemplateEditor.Services.ContentManagement;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class MainWindow : Window
    {
        private const int SimpleModeMaxZones = 32;
        private const int AdvancedModeMaxZones = 32;

        // Literal UTF-8 (no \uXXXX), UTF-8 no-BOM via File.WriteAllText. See Services.JsonExport.
        private static readonly JsonSerializerOptions JsonOptions = Services.JsonExport.Options;

        // Currently open settings file path (null = unsaved / untitled)
        private string? _currentSettingsPath = null;
        private bool _isDirty = false;

        // Ban lists
        private readonly ObservableCollection<BanEntry>   _bannedItems  = [];
        private readonly ObservableCollection<BanEntry>   _bannedMagics = [];
        private readonly ObservableCollection<BanEntry>   _bannedHeroes = [];
        private readonly ObservableCollection<BonusEntry> _bonuses      = [];
        private bool _suppressAssetToggle;
        private bool _refreshingLists;
        private bool _isRefreshingMapSizes = false;
        private string _baseTitle = string.Empty;

        private ZoneMandatoryContent _playerZoneMandatoryContent = new();
        private ZoneMandatoryContent _lowNeutralMandatoryContent = new();
        private ZoneMandatoryContent _mediumNeutralMandatoryContent = new();
        private ZoneMandatoryContent _highNeutralMandatoryContent = new();
        private ZoneMandatoryContent _hubZoneMandatoryContent = new();

        // Option-combo sources. The string fields hold LOCALIZATION KEYS (resolved via L.Get),
        // not literal text. The enum is the stable value; combos map back by SelectedIndex.
        private static readonly (MapTopology Topology, string Label, string Description)[] TopologyOptions =
        [
            (MapTopology.Balanced,    "S.Topo.Balanced", "S.TopoDesc.Balanced"),
            (MapTopology.Random,      "S.Topo.Random",   "S.TopoDesc.Random"),
            (MapTopology.Default,     "S.Topo.Ring",     "S.TopoDesc.Ring"),
            (MapTopology.HubAndSpoke, "S.Topo.Hub",      "S.TopoDesc.Hub"),
            (MapTopology.Chain,       "S.Topo.Chain",    "S.TopoDesc.Chain"),
        ];

        private static readonly (TerrainTheme Theme, string Label)[] TerrainOptions =
        [
            (TerrainTheme.FactionBased, "S.Terrain.FactionBased"),
            (TerrainTheme.Random,       "S.Terrain.Random"),
            (TerrainTheme.Grass,        "S.Terrain.Grass"),
            (TerrainTheme.Snow,         "S.Terrain.Snow"),
            (TerrainTheme.Lava,         "S.Terrain.Lava"),
            (TerrainTheme.Sand,         "S.Terrain.Sand"),
            (TerrainTheme.Dirt,         "S.Terrain.Dirt"),
            (TerrainTheme.Deathland,    "S.Terrain.Deathland"),
            (TerrainTheme.Autumn,       "S.Terrain.Autumn"),
        ];

        private static readonly (MonsterAggression Level, string Label)[] AggressionOptions =
        [
            (MonsterAggression.Passive,    "S.Aggr.Passive"),
            (MonsterAggression.Normal,     "S.Aggr.Normal"),
            (MonsterAggression.Aggressive, "S.Aggr.Aggressive"),
        ];

        private static readonly (WaterLevel Level, string Label)[] WaterOptions =
        [
            (WaterLevel.None,   "S.Water.None"),
            (WaterLevel.Small,  "S.Water.Small"),
            (WaterLevel.Medium, "S.Water.Medium"),
            (WaterLevel.Large,  "S.Water.Large"),
        ];

        /// <summary>Shorthand for the localization manager.</summary>
        private static Services.Localization.LocalizationManager L =>
            Services.Localization.LocalizationManager.Instance;

        public MainWindow()
        {
            InitializeComponent();

            // Launch quietly when started with --minimized (e.g. from a shortcut while gaming):
            // start in the taskbar without grabbing focus, so it never pops over a fullscreen game.
            var cmdLine = Environment.GetCommandLineArgs();
            bool startMinimized = cmdLine.Any(a =>
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-m", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/min", StringComparison.OrdinalIgnoreCase));
            if (startMinimized)
            {
                ShowActivated = false;            // do not steal foreground from a fullscreen game
                WindowState = WindowState.Minimized;
            }

            // --shoot <dir>: documentation screenshot mode. Render each tab to a PNG via
            // RenderTargetBitmap (renders the visual tree to a bitmap with NO need to show
            // the window on-screen), then exit. Never activates / never pops over a game.
            int shootIdx = Array.FindIndex(cmdLine, a => a.Equals("--shoot", StringComparison.OrdinalIgnoreCase));
            if (shootIdx >= 0 && shootIdx + 1 < cmdLine.Length)
            {
                ShowActivated = false;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = -5000; Top = 100;
                string shotDir = cmdLine[shootIdx + 1];
                // ContentRendered fires after the visual tree has actually rendered once,
                // so Content/MainTabs are guaranteed live (Loaded was too early off-screen).
                ContentRendered += async (_, _) => await ShootTabsAsync(shotDir);
            }

            // --shoot-editor <dir>: render the visual zone editor to a PNG (off-screen), then exit.
            int shootEditorIdx = Array.FindIndex(cmdLine, a => a.Equals("--shoot-editor", StringComparison.OrdinalIgnoreCase));
            if (shootEditorIdx >= 0 && shootEditorIdx + 1 < cmdLine.Length)
            {
                ShowActivated = false;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = -5000; Top = 100;
                string editorShotDir = cmdLine[shootEditorIdx + 1];
                ContentRendered += async (_, _) => await ShootEditorAsync(editorShotDir);
            }

            // --gen-readymaps <dir>: headless regen of the built-in presets to .rmg.json, then exit.
            int genIdx = Array.FindIndex(cmdLine, a => a.Equals("--gen-readymaps", StringComparison.OrdinalIgnoreCase));
            if (genIdx >= 0 && genIdx + 1 < cmdLine.Length)
            {
                ShowActivated = false;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = -5000; Top = 100;
                string genDir = cmdLine[genIdx + 1];
                ContentRendered += (_, _) => GenerateReadyMaps(genDir);
            }

            // Clamp startup size to the available work area so the window never
            // overflows the screen at high-DPI scaling (e.g. 125 %, 150 %, 200 %).
            var area = SystemParameters.WorkArea;
            if (Height > area.Height) { Height = area.Height; MinHeight = area.Height; }
            if (Width  > area.Width)  { Width  = area.Width;  MinWidth  = area.Width;  }

            // Stamp version from assembly metadata into all visible locations.
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionLabel = version != null ? FormatVersion(version) : "v?";
            _baseTitle = $"AuroraRMG {versionLabel}";           // taskbar / window title
            TxtVersionBadge.Text = versionLabel;                 // badge next to the wordmark
            TxtAppTitle.Text = $"AuroraRMG {versionLabel}";
            TxtWipWarning.Text = L.Get("S.CB.Wip");

            CmbGameMode.ItemsSource = KnownValues.GameModes;
            CmbGameMode.SelectedIndex = 0;
            RefreshMapSizeOptions(160);
            RefreshLocalizedLists();      // fills the option combos in the current language
            CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            CmbTopology.SelectedIndex = 0;
            CmbMapView.SelectedIndex = 0;
            CmbTerrain.SelectedIndex = 0; // Faction-based
            CmbMonsterAggression.SelectedIndex = 1; // Normal
            CmbWaterLevel.SelectedIndex = 0; // None
            BuildPresetMenu();
            Services.Localization.LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();

            // Wire ban-list ObservableCollections to the ListBoxes.
            LbBannedItems.ItemsSource  = _bannedItems;
            LbBannedMagics.ItemsSource = _bannedMagics;
            LbBannedHeroes.ItemsSource = _bannedHeroes;
            LbBonuses.ItemsSource      = _bonuses;

            // Game-asset integration is opt-in (default OFF → fully works out of the box).
            _suppressAssetToggle = true;
            ChkUseGameAssets.IsChecked = Services.GameData.AppSettings.Current.UseGameAssets;
            _suppressAssetToggle = false;
            if (Services.GameData.AppSettings.Current.UseGameAssets)
                _ = PrimeGameCatalogAsync(); // warm names/icons only when the user has opted in

            UpdateLanguageButtons();
            InitSimpleMode();   // populate simple-mode combos/seed + apply the saved Simple/Advanced mode

            InitializeZoneContentPresets();
            InitializeDefaultPlayerZoneContents();
            InitializeDefaultLowNeutralContents();
            InitializeDefaultMediumNeutralContents();
            InitializeDefaultHighNeutralContents();
            InitializeDefaultHubZoneContents();
            DataContext = new
            {
                MineContentItems = _playerZoneMandatoryContent.mines,
                TreasureContentItems = _playerZoneMandatoryContent.treasures,
                UnitRecruitmentContentItems = _playerZoneMandatoryContent.unitRecruitment,
                ResourceBankContentItems = _playerZoneMandatoryContent.resourceBanks,
                UtilityStructureContentItems = _playerZoneMandatoryContent.utilityStructures,
                HeroImprovementStructureContentItems = _playerZoneMandatoryContent.heroImprovementStructures,
                LowNeutralMineContentItems = _lowNeutralMandatoryContent.mines,
                LowNeutralTreasureContentItems = _lowNeutralMandatoryContent.treasures,
                LowNeutralUnitRecruitmentContentItems = _lowNeutralMandatoryContent.unitRecruitment,
                LowNeutralResourceBankContentItems = _lowNeutralMandatoryContent.resourceBanks,
                LowNeutralUtilityStructureContentItems = _lowNeutralMandatoryContent.utilityStructures,
                LowNeutralHeroImprovementStructureContentItems = _lowNeutralMandatoryContent.heroImprovementStructures,
                MediumNeutralMineContentItems = _mediumNeutralMandatoryContent.mines,
                MediumNeutralTreasureContentItems = _mediumNeutralMandatoryContent.treasures,
                MediumNeutralUnitRecruitmentContentItems = _mediumNeutralMandatoryContent.unitRecruitment,
                MediumNeutralResourceBankContentItems = _mediumNeutralMandatoryContent.resourceBanks,
                MediumNeutralUtilityStructureContentItems = _mediumNeutralMandatoryContent.utilityStructures,
                MediumNeutralHeroImprovementStructureContentItems = _mediumNeutralMandatoryContent.heroImprovementStructures,
                HighNeutralMineContentItems = _highNeutralMandatoryContent.mines,
                HighNeutralTreasureContentItems = _highNeutralMandatoryContent.treasures,
                HighNeutralUnitRecruitmentContentItems = _highNeutralMandatoryContent.unitRecruitment,
                HighNeutralResourceBankContentItems = _highNeutralMandatoryContent.resourceBanks,
                HighNeutralUtilityStructureContentItems = _highNeutralMandatoryContent.utilityStructures,
                HighNeutralHeroImprovementStructureContentItems = _highNeutralMandatoryContent.heroImprovementStructures,
                HubZoneMineContentItems = _hubZoneMandatoryContent.mines,
                HubZoneTreasureContentItems = _hubZoneMandatoryContent.treasures,
                HubZoneUnitRecruitmentContentItems = _hubZoneMandatoryContent.unitRecruitment,
                HubZoneResourceBankContentItems = _hubZoneMandatoryContent.resourceBanks,
                HubZoneUtilityStructureContentItems = _hubZoneMandatoryContent.utilityStructures,
                HubZoneHeroImprovementStructureContentItems = _hubZoneMandatoryContent.heroImprovementStructures,
            };

            TxtTemplateName.TextChanged += (_, _) => { MarkDirtyNameOnly(); Validate(); };
            UpdateTitle();
            TxtWindowTitle.Text = Title;

            // Ctrl+Tab / Ctrl+Shift+Tab cycle the top navigation tabs (works regardless of focus).
            PreviewKeyDown += OnGlobalPreviewKeyDown;
        }

        private void OnGlobalPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Tab) return;
            var mods = System.Windows.Input.Keyboard.Modifiers;
            if ((mods & System.Windows.Input.ModifierKeys.Control) != System.Windows.Input.ModifierKeys.Control) return;
            int n = MainTabs.Items.Count;
            if (n == 0) return;
            bool back = (mods & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            int idx = MainTabs.SelectedIndex < 0 ? 0 : MainTabs.SelectedIndex;
            MainTabs.SelectedIndex = back ? (idx - 1 + n) % n : (idx + 1) % n;
            e.Handled = true;
        }
        private void InitializeDefaultPlayerZoneContents()
        {
            // ── Basic mines — guarded, anchored near the player castle (every template). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineWood, nearCastle: true, roadDistance: "Near"));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineOre, nearCastle: true, roadDistance: "Near"));
            // ── Gold mine (Exodus/Staircase/Yin Yang pattern). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, roadDistance: "Near"));
            // ── Rare mines spread along roads (Exodus/Staircase/Yin Yang pattern). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals, roadDistance: "Next To"));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury, roadDistance: "Next To"));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones, roadDistance: "Next To"));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, roadDistance: "Next To"));
            // ── Loot — epic items + army pandora (Exodus/Blitz pattern). ──
            _playerZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _playerZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));

            // ── Hiring — low-tier × 2 + high-tier × 1 + full pool × 1 (Kerberos + Universe blend). ──
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier, count: 2));
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresAllTier));

            // ── Guarded resource banks — tier 1 × 2 + tier 2 × 1 (Exodus pattern). ──
            _playerZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier1, count: 2));
            _playerZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));

            // ── Utility buildings (Blitz/Kerberos/Exodus pattern). ──
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Market, roadDistance: "Near"));
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.ManaWell, roadDistance: "Near"));
            
            // ── Hero training — tier-2 stat building (fort/university/orb_observatory) ──
            //    + uncommon hero bank (university/wise_owl/tree_of_knowledge) (Blitz/Exodus pattern).
            _playerZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            _playerZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroImprovementUncommon));

        }

        private void InitializeDefaultLowNeutralContents()
        {
            // Mines — biome rare mine + one random rare mine
            _lowNeutralMandatoryContent.mines.Add(CreateZoneContentItem(IncludeListIds.RandomRareMinesBiomeRestricted));
            _lowNeutralMandatoryContent.mines.Add(CreateZoneContentItem(IncludeListIds.RandomRareMines));
            // Utility — guarded market + vision building
            _lowNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Market));
            _lowNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            // Buff buildings — two hero buff tier-1 picks
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stat building — tier-1
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            // Hiring — two low-tier random hires
            _lowNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier, count: 2));
            // Loot — pandora box + random pickup item
            _lowNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _lowNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.RandomPickupItems));
            // Magic buildings — tier 1
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
        }

        private void InitializeDefaultMediumNeutralContents()
        {
            // Mines — full rare set + gold + alchemy lab
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals, roadDistance: "Next To"));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury, roadDistance: "Next To"));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones, roadDistance: "Next To"));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, roadDistance: "Next To"));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, roadDistance: "Near"));
            // Utility — guarded watchtower + vision building
            _mediumNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _mediumNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            // Buff buildings
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stats — tier 1 + tier 2
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            // Magic buildings — tier 1 + tier 2
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier2));
            // Hiring — low + high tier
            _mediumNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier));
            _mediumNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            // Unit banks — biome-restricted
            _mediumNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksBiomeRestricted));
            // Guarded resource banks — tier 2
            _mediumNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            // Loot — epic items + pandora boxes
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.PandoraBoxArmyLowTier));
        }

        private void InitializeDefaultHighNeutralContents()
        {
            // Epic encounters — utopias + epic resource banks
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.UtopiaBuildings, count: 2));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.EpicGuardedResourceBanks, count: 2));
            // Utility — vision + buff buildings
            _highNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stats — tier 2 + tier 3 × 2
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier3, count: 2));
            // Magic buildings — tier 2 × 2
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier2, count: 2));
            // Hiring — high-tier × 2 + all-tier
            _highNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier, count: 2));
            _highNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresAllTier));
            // Unit banks — biome-restricted + no-restriction × 2
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksBiomeRestricted));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksNoBiome, count: 2));
            // Guarded resource banks — tier 2 + tier 3
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedBanksTier3));
            // Loot — mythic scrolls × 2, legendary × 2, epic, pandoras + high-tier army × 2
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.MythicScrollBoxPickup, count: 2));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemLegendary));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemLegendary));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.PandoraBoxArmyHighTier, count: 2));
            // Mines — gold-heavy with full rare set
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, count: 3));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, count: 2));
        }

        private void InitializeDefaultHubZoneContents()
        {
            // Hub zones are connector/transit zones; give them medium-quality defaults
            // as a sensible starting point (user can customize from here)
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab));
            _hubZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _hubZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            _hubZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier));
            _hubZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            _hubZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            _hubZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            _hubZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
            _hubZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _hubZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
        }

        // Registered content pick-lists, so their displayed names can be re-localized on language change.
        private readonly List<(ComboBox Combo, ComboBox? Sticky, List<SidMapping> Group)> _contentMenus = [];

        private void PopulateZoneContentMenu(ComboBox comboBox, ComboBox? comboBoxSticky, List<SidMapping> contentGroup)
        {
            _contentMenus.Add((comboBox, comboBoxSticky, contentGroup));
            SetContentMenuItems(comboBox, comboBoxSticky, contentGroup);
            if (comboBoxSticky != null)
            {
                comboBox.SelectionChanged       += (_, _) => comboBoxSticky.SelectedIndex = comboBox.SelectedIndex;
                comboBoxSticky.SelectionChanged += (_, _) => comboBox.SelectedIndex       = comboBoxSticky.SelectedIndex;
            }
        }

        /// <summary>(Re)fills a content pick-list with current-language names, preserving the selection.</summary>
        private void SetContentMenuItems(ComboBox comboBox, ComboBox? sticky, List<SidMapping> group)
        {
            bool en = L.CurrentLanguage == Services.Localization.AppLanguage.En;
            var names = group.Select(sm => OldenEraTemplateEditor.Services.ContentManagement.ContentNamesEn.Of(sm, en)).ToList();
            int sel = comboBox.SelectedIndex;
            comboBox.ItemsSource = names;
            comboBox.SelectedIndex = sel >= 0 && sel < names.Count ? sel : (names.Count > 0 ? 0 : -1);
            if (sticky != null)
            {
                int ss = sticky.SelectedIndex;
                sticky.ItemsSource = names;
                sticky.SelectedIndex = ss >= 0 && ss < names.Count ? ss : (names.Count > 0 ? 0 : -1);
            }
        }

        /// <summary>Re-localizes all registered content pick-lists (called on language change).</summary>
        private void RefreshContentMenuLanguage()
        {
            foreach (var (combo, sticky, group) in _contentMenus)
                SetContentMenuItems(combo, sticky, group);
        }

        private void InitializeZoneContentPresets()
        {
            /* Populate the Mines dropdown menu */
            PopulateZoneContentMenu(CmbZoneContentPreset, CmbZoneContentPresetSticky, ContentItemGroup.Mines);
            /* Populate the Treasures dropdown menu */
            PopulateZoneContentMenu(CmbTreasureContentPreset, CmbTreasureContentPresetSticky, ContentItemGroup.Treasures);
            /* Populate the Unit Recruitment dropdown menu */
            PopulateZoneContentMenu(CmbUnitRecruitmentContentPreset, CmbUnitRecruitmentContentPresetSticky, ContentItemGroup.UnitRecruitment);
             /* Populate the Resource Banks dropdown menu */
            PopulateZoneContentMenu(CmbResourceBankContentPreset, CmbResourceBankContentPresetSticky, ContentItemGroup.ResourceBanks);
            /* Populate the Utility Structures dropdown menu */
            PopulateZoneContentMenu(CmbUtilityStructureContentPreset, CmbUtilityStructureContentPresetSticky, ContentItemGroup.UtilityStructures);
            /* Populate the Hero Improvement Structures dropdown menu */
            PopulateZoneContentMenu(CmbHeroImprovementContentPreset, CmbHeroImprovementContentPresetSticky, ContentItemGroup.HeroImprovementStructures);
            /* Populate Low Neutral dropdowns */
            PopulateZoneContentMenu(CmbLowNeutralMineContentPreset,               null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbLowNeutralTreasureContentPreset,           null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbLowNeutralUnitRecruitmentContentPreset,    null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbLowNeutralResourceBankContentPreset,       null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbLowNeutralUtilityStructureContentPreset,   null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbLowNeutralHeroImprovementContentPreset,    null, ContentItemGroup.HeroImprovementStructures);
            /* Populate Medium Neutral dropdowns */
            PopulateZoneContentMenu(CmbMediumNeutralMineContentPreset,            null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbMediumNeutralTreasureContentPreset,        null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbMediumNeutralUnitRecruitmentContentPreset, null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbMediumNeutralResourceBankContentPreset,    null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbMediumNeutralUtilityStructureContentPreset,null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbMediumNeutralHeroImprovementContentPreset, null, ContentItemGroup.HeroImprovementStructures);
            /* Populate High Neutral dropdowns */
            PopulateZoneContentMenu(CmbHighNeutralMineContentPreset,              null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbHighNeutralTreasureContentPreset,          null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbHighNeutralUnitRecruitmentContentPreset,   null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbHighNeutralResourceBankContentPreset,      null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbHighNeutralUtilityStructureContentPreset,  null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbHighNeutralHeroImprovementContentPreset,   null, ContentItemGroup.HeroImprovementStructures);
            /* Populate Hub Zone dropdowns */
            PopulateZoneContentMenu(CmbHubZoneMineContentPreset,                  null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbHubZoneTreasureContentPreset,              null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbHubZoneUnitRecruitmentContentPreset,       null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbHubZoneResourceBankContentPreset,          null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbHubZoneUtilityStructureContentPreset,      null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbHubZoneHeroImprovementContentPreset,       null, ContentItemGroup.HeroImprovementStructures);
        }


        // Formats a Version as "vMajor.Minor" or "vMajor.Minor.Build" when build > 0.
        private static string FormatVersion(Version v)
            => v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";


        private void MarkDirty()
        {
            if (!IsInitialized || _refreshingLists) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _templateOutdated = true;
            UpdateOutdatedWarning();
            UpdateTitle();
        }

        private void MarkDirtyNameOnly()
        {
            if (!IsInitialized) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _generatedTemplate.Name = TxtTemplateName.Text.Trim();
            UpdateTitle();
        }

        private void UpdateOutdatedWarning()
        {
            if (TxtOutdatedWarning == null) return;
            bool outdated = _templateOutdated && _generatedTemplate is not null;
            TxtOutdatedWarning.Visibility = outdated ? Visibility.Visible : Visibility.Hidden;
            if (BtnSaveGenerated != null)
                BtnSaveGenerated.IsEnabled = _generatedTemplate is not null && !outdated;
        }

        private void UpdateTitle()
        {
            string file = _currentSettingsPath is not null
                ? System.IO.Path.GetFileName(_currentSettingsPath)
                : L.Get("S.CB.Untitled");
            string fileLabel = _isDirty ? $"{file}*" : file;
            // Taskbar / OS window title keeps the full brand + version + file.
            Title = $"{_baseTitle}  —  {fileLabel}";
            // In-app header shows only the current file (brand & version are separate elements).
            if (IsInitialized) TxtWindowTitle.Text = fileLabel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else if (e.ClickCount == 1)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            ToggleMaximize();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (BtnMaximize == null) return;
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = "🗗";
                BtnMaximize.ToolTip = L.Get("S.CB.Restore");
            }
            else
            {
                BtnMaximize.Content = "🗖";
                BtnMaximize.ToolTip = L.Get("S.CB.Maximize");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            Close();

        // Keep value labels in sync with slider positions.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            UpdateValueLabels();
            UpdatePlayerCastleFactionVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            MarkDirty();
            Validate();
        }

        // ── Free numeric entry for hero-count fields ──────────────────────────────
        // The editable TextBoxes feed their value into the backing slider (the single
        // source of truth read everywhere else). The slider's ValueChanged then
        // normalises the TextBox text. Commit on Enter or focus loss.
        private void HeroBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CommitHeroBox(sender as System.Windows.Controls.TextBox);
                e.Handled = true;
            }
        }

        private void HeroBox_LostFocus(object sender, RoutedEventArgs e)
            => CommitHeroBox(sender as System.Windows.Controls.TextBox);

        // Stepper (−/+) buttons. Tag is "+SldHeroMin" / "-SldHeroMin" etc.
        private void HeroStep_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || ChkSingleHeroMode.IsChecked == true) return;
            if (sender is not System.Windows.Controls.Button { Tag: string tag } || tag.Length < 2) return;
            int dir = tag[0] == '+' ? 1 : -1;
            if (FindName(tag[1..]) is not System.Windows.Controls.Slider slider) return;
            slider.Value = System.Math.Clamp(slider.Value + dir, slider.Minimum, slider.Maximum);
        }

        private void CommitHeroBox(System.Windows.Controls.TextBox? box)
        {
            if (box is null || !IsInitialized) return;
            var slider = box.Name switch
            {
                nameof(TxtHeroMin)       => SldHeroMin,
                nameof(TxtHeroMax)       => SldHeroMax,
                nameof(TxtHeroIncrement) => SldHeroIncrement,
                _                        => null,
            };
            if (slider is null) return;

            if (int.TryParse(box.Text.Trim(), out int value))
                slider.Value = System.Math.Clamp(value, (int)slider.Minimum, (int)slider.Maximum);

            // Re-sync the text to the (possibly clamped / corrected) slider value.
            box.Text = ((int)slider.Value).ToString();
        }

        private void UpdateValueLabels()
        {
            TxtPlayers.Text = ((int)SldPlayers.Value).ToString();
            TxtHeroMin.Text = ((int)SldHeroMin.Value).ToString();
            TxtHeroMax.Text = ((int)SldHeroMax.Value).ToString();
            TxtHeroIncrement.Text = ((int)SldHeroIncrement.Value).ToString();
            TxtNeutral.Text = ((int)SldNeutral.Value).ToString();
            TxtPlayerCastles.Text = ((int)SldPlayerCastles.Value).ToString();
            TxtNeutralCastles.Text = ((int)SldNeutralCastles.Value).ToString();
            TxtResourceDensity.Text = $"{(int)SldResourceDensity.Value}%";
            TxtStructureDensity.Text = $"{(int)SldStructureDensity.Value}%";
            TxtNeutralStackStrength.Text = $"{(int)SldNeutralStackStrength.Value}%";
            TxtBorderGuardStrength.Text = $"{(int)SldBorderGuardStrength.Value}%";
            TxtFactionLawsExp.Text = $"{(int)SldFactionLawsExp.Value}%";
            TxtAstrologyExp.Text = $"{(int)SldAstrologyExp.Value}%";
            TxtNeutralLowNoCastle.Text = ((int)SldNeutralLowNoCastle.Value).ToString();
            TxtNeutralLowCastle.Text = ((int)SldNeutralLowCastle.Value).ToString();
            TxtNeutralMediumNoCastle.Text = ((int)SldNeutralMediumNoCastle.Value).ToString();
            TxtNeutralMediumCastle.Text = ((int)SldNeutralMediumCastle.Value).ToString();
            TxtNeutralHighNoCastle.Text = ((int)SldNeutralHighNoCastle.Value).ToString();
            TxtNeutralHighCastle.Text = ((int)SldNeutralHighCastle.Value).ToString();
            TxtMinNeutralBetweenPlayers.Text = ((int)SldMinNeutralBetweenPlayers.Value).ToString();
            TxtPlayerZoneSize.Text = $"{SldPlayerZoneSize.Value:F2}x";
            TxtNeutralZoneSize.Text = $"{SldNeutralZoneSize.Value:F2}x";
            TxtHubZoneSize.Text = $"{SldHubZoneSize.Value:F2}x";
            TxtHubCastles.Text = ((int)SldHubCastles.Value).ToString();
            TxtGuardRandomization.Text = $"{(int)SldGuardRandomization.Value}%";
            TxtLostStartCityDay.Text = ((int)SldLostStartCityDay.Value).ToString();
            TxtCityHoldDays.Text = ((int)SldCityHoldDays.Value).ToString();
            TxtGladiatorDelay.Text = ((int)SldGladiatorDelay.Value).ToString();
            TxtGladiatorCountDay.Text = ((int)SldGladiatorCountDay.Value).ToString();
            TxtTournamentPointsToWin.Text = ((int)SldTournamentPointsToWin.Value).ToString();
            TxtTournamentFirstTournamentDay.Text = ((int)SldTournamentFirstTournamentDay.Value).ToString();
            TxtTournamentInterval.Text = ((int)SldTournamentInterval.Value).ToString();
            TxtDiplomacy.Text = $"{SldDiplomacy.Value / 100.0:F2}";
            TxtTerrainRoughness.Text = $"{(int)SldTerrainRoughness.Value}%";
            TxtLakeAmount.Text = $"{(int)SldLakeAmount.Value}%";
        }

        private record ValidationMessage(string Text, System.Windows.Media.Brush Foreground);

        private void SetValidationMessages(IEnumerable<ValidationMessage> messages)
        {
            var list = messages.ToList();
            LstValidation.ItemsSource = list;
            PnlValidation.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetValidationError(string text)
        {
            SetValidationMessages([new ValidationMessage(text, (System.Windows.Media.Brush)FindResource("BrushError"))]);
        }

        private bool Validate()
        {
            int heroMin = (int)SldHeroMin.Value;
            int heroMax = (int)SldHeroMax.Value;
            int players = (int)SldPlayers.Value;
            int neutral = TotalNeutralZonesFromUi();

            if (heroMin > heroMax)
            {
                SetValidationError(L.Get("S.CB.V.HeroMinMax"));
                BtnPreview.IsEnabled = false;
                return false;
            }

            int maxZones = _advancedZoneSettings ? AdvancedModeMaxZones : SimpleModeMaxZones;
            if (players + neutral > maxZones)
            {
                SetValidationError(L.Get("S.CB.V.MaxZones", maxZones));
                BtnPreview.IsEnabled = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtTemplateName.Text))
            {
                SetValidationError(L.Get("S.CB.V.NameEmpty"));
                BtnPreview.IsEnabled = false;
                return false;
            }

            var warnBrush = (System.Windows.Media.Brush)FindResource("BrushWarnText");
            var warnings = new System.Collections.Generic.List<ValidationMessage>();

            if (TxtTemplateName.Text.Trim().Equals(L.Get("S.M.014"), StringComparison.OrdinalIgnoreCase))
                warnings.Add(new ValidationMessage(L.Get("S.CB.V.DefaultName"), warnBrush));

            int selectedMapSize = SelectedMapSize();
            int totalZones = players + neutral;
            var selectedTopology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default;
            // Hub layout has an extra central zone that also occupies map area.
            int totalZonesIncludingHub = selectedTopology == MapTopology.HubAndSpoke ? totalZones + 1 : totalZones;
            if (totalZonesIncludingHub > 0 && (selectedMapSize * selectedMapSize) / totalZonesIncludingHub < 1024)
                warnings.Add(new ValidationMessage(L.Get("S.CB.V.ZoneTooSmall"), warnBrush));

            if (selectedMapSize > KnownValues.MaxOfficialMapSize)
                warnings.Add(new ValidationMessage(L.Get("S.CB.V.ExpSize"), warnBrush));

            if (totalZones > 10)
            {
                int playerCastles = (int)SldPlayerCastles.Value;
                int neutralCastles = (int)SldNeutralCastles.Value;
                if (playerCastles > 1 || neutralCastles > 1)
                    warnings.Add(new ValidationMessage(L.Get("S.CB.V.ManyCastles"), warnBrush));
            }

            int minNeutralBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value;
            if (minNeutralBetweenPlayers > 0)
            {
                var separationSettings = new GeneratorSettings
                {
                    PlayerCount = players,
                    Topology = selectedTopology,
                    RandomPortals = ChkRandomPortals.IsChecked == true,
                    MinNeutralZonesBetweenPlayers = minNeutralBetweenPlayers
                };

                if (!TemplateGenerator.CanHonorNeutralSeparation(separationSettings, neutral))
                        warnings.Add(new ValidationMessage(L.Get("S.CB.V.MinSepIgnored"), warnBrush));
            }

            bool cityHoldActive = ChkCityHold.IsChecked == true;
            if (cityHoldActive)
            {
                if (selectedTopology != MapTopology.HubAndSpoke && neutral == 0)
                {
                    SetValidationError(L.Get("S.CB.V.CityHoldNeedNeutral"));
                    BtnPreview.IsEnabled = false;
                    return false;
                }
            }

            if (ChkNoDirectPlayerConn.IsChecked == true && neutral == 0)
            {
                SetValidationError(L.Get("S.CB.V.NeutralOnlyNeed"));
                BtnPreview.IsEnabled = false;
                return false;
            }

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";
            if (selectedVictoryCondition == "win_condition_6" && players != 2)
            {
                SetValidationError(L.Get("S.CB.V.Tournament2"));
                BtnPreview.IsEnabled = false;
                return false;
            }

            if (selectedVictoryCondition == "win_condition_6")
            {
                // Each neutral zone tier must be divisible by 2 so both players get an identical cluster.
                var oddTiers = new System.Collections.Generic.List<string>();
                if ((int)SldNeutralLowNoCastle.Value    % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.WeakNo"));
                if ((int)SldNeutralLowCastle.Value      % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.WeakYes"));
                if ((int)SldNeutralMediumNoCastle.Value % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.MedNo"));
                if ((int)SldNeutralMediumCastle.Value   % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.MedYes"));
                if ((int)SldNeutralHighNoCastle.Value   % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.StrongNo"));
                if ((int)SldNeutralHighCastle.Value     % 2 != 0) oddTiers.Add(L.Get("S.CB.Tier.StrongYes"));
                if (oddTiers.Count > 0)
                {
                    SetValidationError(L.Get("S.CB.V.TournamentEven", string.Join(", ", oddTiers)));
                    BtnPreview.IsEnabled = false;
                    return false;
                }
            }

            if ((int)SldBorderGuardStrength.Value > 100)
                warnings.Add(new ValidationMessage(L.Get("S.CB.V.GuardHigh"), warnBrush));

            SetValidationMessages(warnings);

            BtnPreview.IsEnabled = true;
            return true;
        }

        private int TotalNeutralZonesFromUi()
        {
            return (int)SldNeutralLowNoCastle.Value
                + (int)SldNeutralLowCastle.Value
                + (int)SldNeutralMediumNoCastle.Value
                + (int)SldNeutralMediumCastle.Value
                + (int)SldNeutralHighNoCastle.Value
                + (int)SldNeutralHighCastle.Value;
        }

        private int SelectedMapSize() =>
            CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize)
                ? parsedSize
                : 160;

        private static string FormatMapSize(int size) =>
            KnownValues.IsExperimentalMapSize(size)
                ? $"{size}x{size} ({KnownValues.MapSizeLabel(size)}) {L.Get("S.MapExp")}"
                : $"{size}x{size} ({KnownValues.MapSizeLabel(size)})";

        private static double GuardRandomizationPercent(double guardRandomization)
        {
            if (double.IsNaN(guardRandomization) || double.IsInfinity(guardRandomization))
                return 5.0;

            return Math.Clamp(guardRandomization * 100.0, 0.0, 50.0);
        }

        private void RefreshMapSizeOptions(int? requestedSize = null)
        {
            if (CmbMapSize == null) return;

            int selectedSize = requestedSize ?? SelectedMapSize();
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
            int[] sizes = includeExperimental ? KnownValues.AllMapSizes : KnownValues.MapSizes;

            if (!includeExperimental && KnownValues.IsExperimentalMapSize(selectedSize))
                selectedSize = KnownValues.MaxOfficialMapSize;
            else if (!sizes.Contains(selectedSize))
                selectedSize = KnownValues.MapSizes.Contains(selectedSize) ? selectedSize : 160;

            _isRefreshingMapSizes = true;
            try
            {
                CmbMapSize.ItemsSource = sizes.Select(FormatMapSize).ToList();
                CmbMapSize.SelectedItem = FormatMapSize(selectedSize);
                if (CmbMapSize.SelectedIndex < 0)
                    CmbMapSize.SelectedItem = FormatMapSize(160);
            }
            finally
            {
                _isRefreshingMapSizes = false;
            }

            UpdateExperimentalMapSizeWarningVisibility();
        }

        private void CmbTopology_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            int idx = CmbTopology.SelectedIndex;
            if (idx >= 0 && idx < TopologyOptions.Length)
                TxtTopologyDesc.Text = L.Get(TopologyOptions[idx].Description);

            // Isolate option is only meaningful for Random and Chain topologies.
            var topo = idx >= 0 && idx < TopologyOptions.Length ? TopologyOptions[idx].Topology : MapTopology.Default;
            bool isolateApplicable = topo is MapTopology.Random;
            ChkNoDirectPlayerConn.Visibility = isolateApplicable ? Visibility.Visible : Visibility.Collapsed;
            if (!isolateApplicable) ChkNoDirectPlayerConn.IsChecked = false;
            UpdateIsolateDescVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            PnlHubZoneSize.Visibility = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;
            PnlHubCastles.Visibility  = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;

            SyncMapViewFromTopology(idx);
            MarkDirty();
            Validate();
        }

        private bool _suppressTopologySync;

        /// <summary>"Вид карты" in the Template block mirrors the topology combo on the Zones tab.</summary>
        private void CmbMapView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _suppressTopologySync) return;
            int idx = CmbMapView.SelectedIndex;
            if (idx >= 0 && idx < CmbTopology.Items.Count && idx != CmbTopology.SelectedIndex)
            {
                _suppressTopologySync = true;
                CmbTopology.SelectedIndex = idx;   // drives CmbTopology_SelectionChanged (desc, panels, dirty)
                _suppressTopologySync = false;
            }
        }

        /// <summary>Keeps the "Вид карты" combo in sync when the topology changes on the Zones tab.</summary>
        private void SyncMapViewFromTopology(int idx)
        {
            if (_suppressTopologySync || CmbMapView == null) return;
            if (idx >= 0 && idx < CmbMapView.Items.Count && CmbMapView.SelectedIndex != idx)
            {
                _suppressTopologySync = true;
                CmbMapView.SelectedIndex = idx;
                _suppressTopologySync = false;
            }
        }

        /// <summary>"Нежелательные герои" jumps to the Bonuses &amp; Bans tab (hero-ban list).</summary>
        private void BtnUnwantedHeroes_Click(object sender, RoutedEventArgs e)
        {
            MainTabs.SelectedItem = TabBonusesBans;
        }

        private void HeroLighting_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            if (TxtHeroLightingDay != null)
                TxtHeroLightingDay.IsEnabled = ChkHeroLighting.IsChecked == true;
            MarkDirty();
        }

        private void HeroLightingDay_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            TxtHeroLightingDay.Text = HeroLightingDayValue().ToString();
            MarkDirty();
        }

        /// <summary>Parses the heroLightingDay box, clamped to 1..30 (default 1).</summary>
        private int HeroLightingDayValue() =>
            int.TryParse(TxtHeroLightingDay?.Text, out int d) ? Math.Clamp(d, 1, 30) : 1;

        private void BansOverrides_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;
            MarkDirty();
        }

        private void BtnPickValueOverride_Click(object sender, RoutedEventArgs e)
        {
            // Collect SIDs already in the text box so the picker hides them
            var existing = TxtValueOverrides.Text
                .Split('\n')
                .Select(l => { var eq = l.IndexOf('='); return eq > 0 ? l[..eq].Trim() : ""; })
                .Where(s => s.Length > 0)
                .ToHashSet();

            var picker = new ValueOverridePickerWindow(existing) { Owner = this };
            if (picker.ShowDialog() == true && picker.ResultLines.Count > 0)
            {
                var current = TxtValueOverrides.Text.TrimEnd('\r', '\n');
                var appended = string.Join("\n", picker.ResultLines);
                TxtValueOverrides.Text = string.IsNullOrEmpty(current)
                    ? appended
                    : current + "\n" + appended;
                MarkDirty();
            }
        }

        // ── Ban list picker helpers ───────────────────────────────────────────────

        /// <summary>Builds a BanEntry from an artifact ID using the catalog, or a plain fallback entry.</summary>
        private static BanEntry ItemEntryFromId(string id)
        {
            var known = System.Array.Find(KnownValues.BannableItems, b => b.Id == id);
            if (known != null)
                return new BanEntry { Id = id, DisplayName = known.DisplayName, Category = known.Category };
            return new BanEntry { Id = id, DisplayName = KnownValues.SidToDisplayName(id), Category = "Misc" };
        }

        /// <summary>Builds a BanEntry from a spell ID using the catalog, or a plain fallback entry.</summary>
        private static BanEntry MagicEntryFromId(string id)
        {
            var known = System.Array.Find(KnownValues.KnownSpells, s => s.Id == id);
            if (known != null)
                return new BanEntry { Id = id, DisplayName = known.Name, Category = "Spell" };
            return new BanEntry { Id = id, DisplayName = KnownValues.SidToDisplayName(id), Category = "Spell" };
        }

        /// <summary>
        /// Builds a BanEntry from a hero ID. Prefers the live game catalog (real localized name +
        /// faction), then the built-in verified list, then a pattern-derived fallback.
        /// </summary>
        private static BanEntry HeroEntryFromId(string id)
        {
            if (Services.GameData.GameCatalogService.Instance.TryResolveHero(id, out var liveName, out var liveFaction, out var iconSid))
                return new BanEntry
                {
                    Id = id, DisplayName = liveName, Category = liveFaction,
                    Icon = Services.GameData.IconResolver.Resolve(iconSid),
                };
            var known = System.Array.Find(KnownValues.BannableHeroes, h => h.Id == id);
            if (known != null)
                return new BanEntry { Id = id, DisplayName = known.DisplayName, Category = known.Category };
            var (displayName, faction) = KnownValues.DescribeHeroSid(id);
            return new BanEntry { Id = id, DisplayName = displayName, Category = faction };
        }

        /// <summary>Reloads an ObservableCollection from a newline-separated string of IDs.</summary>
        private static void LoadBanList(System.Collections.ObjectModel.ObservableCollection<BanEntry> col,
                                        string raw, System.Func<string, BanEntry> factory)
        {
            col.Clear();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var id in raw.Split('\n'))
            {
                var trimmed = id.Trim();
                if (trimmed.Length == 0) continue;
                col.Add(factory(trimmed));
            }
        }

        private void BtnAddBannedItem_Click(object sender, RoutedEventArgs e)
        {
            var entries = KnownValues.BannableItems
                .Select(b => new BanEntry { Id = b.Id, DisplayName = b.DisplayName, Category = b.Category });
            var picker = new ItemPickerWindow(entries, _bannedItems.Select(b => b.Id), L.Get("S.CB.BanItems")) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var id in picker.SelectedIds)
                    if (!_bannedItems.Any(e => e.Id == id))
                        _bannedItems.Add(ItemEntryFromId(id));
                MarkDirty();
            }
        }

        private void BtnAddBannedMagic_Click(object sender, RoutedEventArgs e)
        {
            var picker = new SpellPickerWindow(_bannedMagics.Select(b => b.Id), showMakeFree: false) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var id in picker.SelectedIds)
                    if (!_bannedMagics.Any(e => e.Id == id))
                        _bannedMagics.Add(MagicEntryFromId(id));
                MarkDirty();
            }
        }

        private void RemoveBannedItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                var entry = _bannedItems.FirstOrDefault(b => b.Id == id);
                if (entry != null) { _bannedItems.Remove(entry); MarkDirty(); }
            }
        }

        private void RemoveBannedMagic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                var entry = _bannedMagics.FirstOrDefault(b => b.Id == id);
                if (entry != null) { _bannedMagics.Remove(entry); MarkDirty(); }
            }
        }

        private async void BtnAddBannedHero_Click(object sender, RoutedEventArgs e)
        {
            // Full roster with real localized names from the installed game ONLY if the user
            // opted into game assets; otherwise the built-in verified subset (out-of-the-box).
            var catalog = Services.GameData.AppSettings.Current.UseGameAssets
                ? await Services.GameData.GameCatalogService.Instance.GetCatalogAsync()
                : new Services.GameData.GameCatalog();
            IEnumerable<BanEntry> entries = catalog.Heroes.Count > 0
                ? catalog.Heroes.Select(h => new BanEntry
                  {
                      Id = h.Sid, DisplayName = h.Name, Category = h.Faction,
                      Icon = Services.GameData.IconResolver.Resolve(h.IconSid),
                  })
                : KnownValues.BannableHeroes.Select(h => new BanEntry { Id = h.Id, DisplayName = h.DisplayName, Category = h.Category });

            var picker = new ItemPickerWindow(entries, _bannedHeroes.Select(b => b.Id), L.Get("S.CB.BanHeroes")) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var id in picker.SelectedIds)
                    if (!_bannedHeroes.Any(e => e.Id == id))
                        _bannedHeroes.Add(HeroEntryFromId(id));
                MarkDirty();
            }
        }

        private void RemoveBannedHero_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                var entry = _bannedHeroes.FirstOrDefault(b => b.Id == id);
                if (entry != null) { _bannedHeroes.Remove(entry); MarkDirty(); }
            }
        }

        /// <summary>Opt-in toggle for reading installed-game assets (full hero roster + icons).</summary>
        private async void ChkUseGameAssets_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || _suppressAssetToggle) return;

            if (ChkUseGameAssets.IsChecked == true)
            {
                var ok = MessageBox.Show(this,
                    L.Get("S.GA.DisclaimerMsg"),
                    L.Get("S.GA.DisclaimerTitle"),
                    MessageBoxButton.OKCancel, MessageBoxImage.Information);

                if (ok != MessageBoxResult.OK)
                {
                    _suppressAssetToggle = true;
                    ChkUseGameAssets.IsChecked = false;
                    _suppressAssetToggle = false;
                    return;
                }

                Services.GameData.AppSettings.Current.UseGameAssets = true;
                Services.GameData.AppSettings.Current.Save();

                await PrimeGameCatalogAsync();
                var catalog = await Services.GameData.GameCatalogService.Instance.GetCatalogAsync();
                if (catalog.Heroes.Count == 0)
                    MessageBox.Show(this,
                        L.Get("S.GA.NotFoundMsg"),
                        L.Get("S.GA.NotFoundTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(this,
                        L.Get("S.GA.ConnectedMsg", catalog.Heroes.Count),
                        L.Get("S.GA.ConnectedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Services.GameData.AppSettings.Current.UseGameAssets = false;
                Services.GameData.AppSettings.Current.Save();
            }
        }

        /// <summary>Loads the game-data catalog off-thread; once ready, upgrades any loaded hero-ban rows to real names.</summary>
        private async Task PrimeGameCatalogAsync()
        {
            try
            {
                var catalog = await Services.GameData.GameCatalogService.Instance.GetCatalogAsync();
                if (catalog.Heroes.Count > 0 && _bannedHeroes.Count > 0)
                    RefreshBannedHeroNames();
            }
            catch { /* catalog is best-effort */ }
        }

        /// <summary>Re-resolves every loaded hero-ban row through the (now warm) catalog.</summary>
        private void RefreshBannedHeroNames()
        {
            for (int i = 0; i < _bannedHeroes.Count; i++)
                _bannedHeroes[i] = HeroEntryFromId(_bannedHeroes[i].Id);
        }

        // ── Bonus list handlers ───────────────────────────────────────────────────

        private void BtnAddBonus_Click(object sender, RoutedEventArgs e)
        {
            var picker = new BonusPickerWindow(_bonuses) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var entry in picker.Results)
                    _bonuses.Add(entry);
                MarkDirty();
            }
        }

        private void RemoveBonus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: BonusEntry entry })
            {
                _bonuses.Remove(entry);
                MarkDirty();
            }
        }

        private void LoadBonusList(string raw)
        {
            _bonuses.Clear();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var line in raw.Split('\n'))
            {
                var entry = BonusEntry.FromString(line.Trim());
                if (entry != null) _bonuses.Add(entry);
            }
        }

        private void CmbMapSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _isRefreshingMapSizes) return;
            UpdateExperimentalMapSizeWarningVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateIsolateDescVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkRandomPortals_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            PnlMaxPortals.Visibility = ChkRandomPortals.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            MarkDirty();
            Validate();
        }

        /// <summary>Shared handler for the environment combo boxes (terrain, aggression, water).</summary>
        private void EnvironmentOption_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            MarkDirty();
            Validate();
        }

        /// <summary>Builds the grouped preset menu (one submenu per mode group) attached to the Пресет button.</summary>
        private void BuildPresetMenu()
        {
            bool en = L.CurrentLanguage == Services.Localization.AppLanguage.En;
            var menu = new ContextMenu();
            foreach (var group in Presets.All.GroupBy(p => p.GroupKey))
            {
                var groupItem = new MenuItem { Header = L.Get(group.Key) };
                foreach (var preset in group)
                {
                    var item = new MenuItem { Header = preset.ShortNameLocalized(en), Tag = preset, ToolTip = preset.DescriptionLocalized(en) };
                    item.Click += PresetMenuItem_Click;
                    groupItem.Items.Add(item);
                }
                menu.Items.Add(groupItem);
            }
            BtnPreset.ContextMenu = menu;
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (BtnPreset.ContextMenu is { } m)
            {
                m.PlacementTarget = BtnPreset;
                m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                m.IsOpen = true;
            }
        }

        private void PresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: Presets.Preset preset })
                ApplyPreset(preset);
        }

        /// <summary>Loads a built-in quick-start preset into the whole UI.</summary>
        private void ApplyPreset(Presets.Preset preset)
        {
            if (!IsInitialized) return;
            ApplySettings(preset.Settings);
            _currentSettingsPath = null;
            _isDirty = true;
            UpdateTitle();
            Validate();
        }

        private readonly Random _presetRng = new();
        private string? _lastRandomPreset;

        /// <summary>
        /// "🎲" — picks a random built-in preset whose player count matches the slider and applies it
        /// (size, name, heroes, view, etc. come from the preset). Repeated clicks cycle the matching pool
        /// without immediately repeating the last pick. A plain slider drag never triggers this.
        /// </summary>
        private void BtnRandomPreset_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            int players = (int)SldPlayers.Value;
            var matches = Presets.All.Where(p => p.Settings.PlayerCount == players).ToList();
            if (matches.Count == 0) matches = [.. Presets.All];
            if (matches.Count == 0) return;
            var pool = matches.Count > 1 && _lastRandomPreset != null
                ? matches.Where(p => p.Name != _lastRandomPreset).ToList()
                : matches;
            if (pool.Count == 0) pool = matches;
            var pick = pool[_presetRng.Next(pool.Count)];
            _lastRandomPreset = pick.Name;
            ApplyPreset(pick);
        }

        private void SldMaxPortals_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            LblMaxPortals.Text = ((int)SldMaxPortals.Value).ToString();
            MarkDirty();
        }

        private void WinConditionOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkSingleHeroMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            bool single = ChkSingleHeroMode.IsChecked == true;
            SldHeroMin.IsEnabled = !single;
            SldHeroMax.IsEnabled = !single;
            SldHeroIncrement.IsEnabled = !single;
            TxtHeroMin.IsEnabled = !single;
            TxtHeroMax.IsEnabled = !single;
            TxtHeroIncrement.IsEnabled = !single;
            if (single)
            {
                TxtHeroMin.Text = "1";
                TxtHeroMax.Text = "1";
                TxtHeroIncrement.Text = "1";
                ChkLostStartHero.IsChecked = true;
            }
            else
            {
                UpdateValueLabels();
                // Restore the checkbox to unchecked unless a win condition forces it
                ChkLostStartHero.IsChecked = false;
                UpdateWinConditionDetailVisibility();
            }
            MarkDirty();
            Validate();
        }
        private bool _advancedZoneSettings = false;

        private void BtnAdvancedZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _advancedZoneSettings = ChkAdvancedZoneSettings.IsChecked == true;
            UpdateAdvancedZoneSettingsVisibility();
            UpdateValueLabels();
            MarkDirty();
            Validate();
        }

        private void ChkExperimentalMapSizes_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            RefreshMapSizeOptions();
            MarkDirty();
            Validate();
        }

        private int TotalAdvancedNeutralZonesFromSliders() =>
            (int)SldNeutralLowNoCastle.Value
            + (int)SldNeutralLowCastle.Value
            + (int)SldNeutralMediumNoCastle.Value
            + (int)SldNeutralMediumCastle.Value
            + (int)SldNeutralHighNoCastle.Value
            + (int)SldNeutralHighCastle.Value;

        private void UpdateAdvancedZoneSettingsVisibility()
        {
            if (PnlAdvancedNeutralZones == null) return;
            bool advanced = _advancedZoneSettings;
            // Neutral zone quality panels are always visible — advanced mode is always used for zone generation.
            PnlAdvancedNeutralZones.Visibility = Visibility.Visible;
            PnlSimpleNeutralCountLabel.Visibility = Visibility.Collapsed;
            SldNeutral.Visibility = Visibility.Collapsed;
            // Zone size / guard tuning remain gated by the Advanced settings checkbox.
            PnlAdvancedZoneSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            if (ChkAdvancedZoneSettings != null)
                ChkAdvancedZoneSettings.IsChecked = advanced;
        }

        private void CmbVictory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            int idx = CmbVictory.SelectedIndex;
            if (idx >= 0 && idx < KnownValues.VictoryConditionIds.Length)
                ApplyVictoryPreset(KnownValues.VictoryConditionIds[idx]);

            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ApplyVictoryPreset(string victoryCondition)
        {
            ChkLostStartCity.IsChecked = false;
            if (ChkSingleHeroMode.IsChecked != true) ChkLostStartHero.IsChecked = false;            ChkCityHold.IsChecked = false;
            ChkGladiatorArena.IsChecked = false;
            ChkTournament.IsChecked = false;

            SldLostStartCityDay.Value = 3;
            SldCityHoldDays.Value = 6;
            SldGladiatorDelay.Value = 30;
            SldGladiatorCountDay.Value = 3;

            SldTournamentPointsToWin.Value = 2;
            SldTournamentInterval.Value = 7;
            SldTournamentFirstTournamentDay.Value = 14;
            ChkTournamentSaveArmy.IsChecked = true;

            switch (victoryCondition)
            {
                case "win_condition_3":
                    ChkLostStartCity.IsChecked = true;
                    break;
                case "win_condition_4":
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                    break;
                case "win_condition_5":
                    ChkCityHold.IsChecked = true;
                    break;
                case "win_condition_6":
                    ChkLostStartHero.IsChecked = true;
                    ChkTournament.IsChecked = true;
                    SldGladiatorDelay.Value = 21;
                    SldGladiatorCountDay.Value = 8;
                    break;
            }
        }

        private void UpdateWinConditionDetailVisibility()
        {
            if (PnlLostStartCityDetails == null) return;

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";

            bool isTournament = selectedVictoryCondition == "win_condition_6";

            if (isTournament)
            {
                // Tournament is exclusive — force it on and disable all other conditions.
                ChkTournament.IsChecked = true;
                ChkLostStartCity.IsChecked = false;
                ChkCityHold.IsChecked = false;
                ChkGladiatorArena.IsChecked = false;
            }
            else
            {
                // Tournament is unavailable outside of the Tournament win condition.
                ChkTournament.IsChecked = false;
                if (selectedVictoryCondition == "win_condition_3")
                    ChkLostStartCity.IsChecked = true;
                if (selectedVictoryCondition == "win_condition_4")
                {
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                }
                if (selectedVictoryCondition == "win_condition_5")
                    ChkCityHold.IsChecked = true;
            }

            ChkLostStartCity.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_3";
            ChkLostStartHero.IsEnabled = ChkSingleHeroMode.IsChecked != true && selectedVictoryCondition != "win_condition_4";
            if (ChkSingleHeroMode.IsChecked == true) ChkLostStartHero.IsChecked = true;
            ChkCityHold.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_5";
            ChkGladiatorArena.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            ChkTournament.IsChecked = isTournament;
            ChkTournament.IsEnabled = isTournament;

            PnlLostStartCityDetails.Visibility = ChkLostStartCity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlCityHoldDetails.Visibility = ChkCityHold.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGladiatorDetails.Visibility = ChkGladiatorArena.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlTournamentDetails.Visibility = isTournament ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExperimentalMapSizeWarningVisibility()
        {
            if (TxtExperimentalMapSizeWarning == null) return;
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
            TxtExperimentalMapSizeWarning.Visibility = includeExperimental ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayerCastleFactionVisibility()
        {
            if (PnlPlayerCastleFactionOption == null || SldPlayerCastles == null) return;
            bool hasExtraCastles = (int)SldPlayerCastles.Value > 1;
            PnlPlayerCastleFactionOption.Visibility = hasExtraCastles ? Visibility.Visible : Visibility.Collapsed;
            if (!hasExtraCastles)
            {
                ChkMatchPlayerCastleFactions.IsChecked = false;
                ChkPlayerStartsWithCastles.IsChecked = false;
            }
        }

        private void UpdateIsolateDescVisibility()
        {
            if (TxtIsolateDesc == null || ChkNoDirectPlayerConn == null) return;
            TxtIsolateDesc.Visibility = ChkNoDirectPlayerConn.IsChecked == true && ChkNoDirectPlayerConn.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AddZoneContentItemFromName(ObservableCollection<ZoneContentItemUI> collection, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? mapping = GlobalContent.GetByName(name);
            if (mapping == null)
                return;
            
            collection.Add(CreateZoneContentItem(mapping));
            MarkDirty();
        }

        private void BtnAddMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbZoneContentPreset.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.mines, name);
        }

        private void BtnAddTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbTreasureContentPreset.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.treasures, name);
        }

        private void BtnAddUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.unitRecruitment, name);
        }

        private void BtnAddResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty; 
            AddZoneContentItemFromName(_playerZoneMandatoryContent.resourceBanks, name);   
        }

        private void BtnAddUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.utilityStructures, name);
        }

        private void BtnAddHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.heroImprovementStructures, name);
        }

        private void BtnRemoveZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            if (sender is not Button button || button.DataContext is not ZoneContentItemUI item)
                return;


            if(_playerZoneMandatoryContent.Remove(item))
                MarkDirty();
            else if (_lowNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_mediumNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_highNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_hubZoneMandatoryContent.Remove(item))
                MarkDirty();
        }

        private void BtnResetPlayerZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            _playerZoneMandatoryContent.Clear();

            InitializeDefaultPlayerZoneContents();
            MarkDirty();
        }

        private void BtnResetLowNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _lowNeutralMandatoryContent.Clear();
            InitializeDefaultLowNeutralContents();
            MarkDirty();
        }

        private void BtnResetMediumNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _mediumNeutralMandatoryContent.Clear();
            InitializeDefaultMediumNeutralContents();
            MarkDirty();
        }

        private void BtnResetHighNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _highNeutralMandatoryContent.Clear();
            InitializeDefaultHighNeutralContents();
            MarkDirty();
        }

        private void BtnResetHubZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _hubZoneMandatoryContent.Clear();
            InitializeDefaultHubZoneContents();
            MarkDirty();
        }

        // -- Low Neutral add handlers --
        private void BtnAddLowNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.mines, name);
        }
        private void BtnAddLowNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddLowNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddLowNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddLowNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddLowNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- Medium Neutral add handlers --
        private void BtnAddMediumNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.mines, name);
        }
        private void BtnAddMediumNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddMediumNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddMediumNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddMediumNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddMediumNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- High Neutral add handlers --
        private void BtnAddHighNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.mines, name);
        }
        private void BtnAddHighNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddHighNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddHighNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddHighNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddHighNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- Hub Zone add handlers --
        private void BtnAddHubZoneMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.mines, name);
        }
        private void BtnAddHubZoneTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.treasures, name);
        }
        private void BtnAddHubZoneUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddHubZoneResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.resourceBanks, name);
        }
        private void BtnAddHubZoneUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.utilityStructures, name);
        }
        private void BtnAddHubZoneHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.heroImprovementStructures, name);
        }

        private static ZoneContentItemUI CreateZoneContentItem(SidMapping preset, int count = 1, bool isGuarded = true, bool nearCastle = false, string roadDistance = "Any")
        {
            bool isGroup = false;
            if(preset.Sid.ToLower().Contains(IncludeListIds.Identifier))
            {
                // Mark the content item as an include list group for proper generation.
                isGroup = true;
            }
            return new ZoneContentItemUI
            {
                SidMapping = preset,
                Count = count,
                IsGuarded = isGuarded,
                NearCastle = nearCastle,
                RoadDistance = roadDistance,
                IsGroup = isGroup
            };
        }

        // -- Settings persistence -----------------------------------------------

        /// <summary>
        /// Restores a <see cref="ZoneMandatoryContent"/> from the new row-based save format.
        /// Each <see cref="ZoneContentRowSave"/> maps to exactly one UI row, preserving Count
        /// and row identity.  Falls back to <paramref name="defaultInit"/> when the list is empty.
        /// </summary>
        private void ApplyZoneContentRows(ZoneMandatoryContent target, List<ZoneContentRowSave>? rows, Action defaultInit)
        {
            target.Clear();

            if (rows is null || rows.Count == 0)
            {
                defaultInit();
                return;
            }

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Sid)) continue;

                SidMapping? sidMapping = GlobalContent.GetBySid(row.Sid);
                if (sidMapping is null) continue;

                var item = CreateZoneContentItem(
                    sidMapping,
                    count:        row.Count,
                    isGuarded:    row.IsGuarded,
                    nearCastle:   row.NearCastle,
                    roadDistance: row.RoadDistance);

                if (row.IsMine)
                    target.mines.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.UnitRecruitment))
                    target.unitRecruitment.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.ResourceBanks))
                    target.resourceBanks.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.UtilityStructures))
                    target.utilityStructures.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.HeroImprovementStructures))
                    target.heroImprovementStructures.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.Treasures))
                    target.treasures.Add(item);
                else
                    target.utilityStructures.Add(item); // safe fallback
            }
        }

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = TxtTemplateName.Text.Trim(),
            MapSize               = SelectedMapSize(),
            PlayerCount           = (int)SldPlayers.Value,
            NeutralZoneCount      = (int)SldNeutral.Value,
            PlayerZoneCastles     = (int)SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)SldNeutralCastles.Value,
            AdvancedMode          = _advancedZoneSettings,
            NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
            NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
            NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
            NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
            NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
            NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
            MatchPlayerCastleFactions   = ChkMatchPlayerCastleFactions.IsChecked == true,
            PlayerStartsWithCastles     = ChkPlayerStartsWithCastles.IsChecked == true,
            MinNeutralZonesBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value,
            ExperimentalMapSizes  = ChkExperimentalMapSizes.IsChecked == true,
            PlayerZoneSize        = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
            NeutralZoneSize       = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
            HubZoneSize           = SldHubZoneSize.Value,
            HubZoneCastles        = (int)SldHubCastles.Value,
            GuardRandomization    = SldGuardRandomization.Value / 100.0,
            HeroCountMin          = (int)SldHeroMin.Value,
            HeroCountMax          = (int)SldHeroMax.Value,
            HeroCountIncrement    = (int)SldHeroIncrement.Value,
            SingleHeroMode        = ChkSingleHeroMode.IsChecked == true,
            Topology              = TopologyOptions[CmbTopology.SelectedIndex].Topology,
            Terrain               = CmbTerrain.SelectedIndex >= 0 ? TerrainOptions[CmbTerrain.SelectedIndex].Theme : TerrainTheme.FactionBased,
            MonsterAggression     = CmbMonsterAggression.SelectedIndex >= 0 ? AggressionOptions[CmbMonsterAggression.SelectedIndex].Level : MonsterAggression.Normal,
            WaterLevel            = CmbWaterLevel.SelectedIndex >= 0 ? WaterOptions[CmbWaterLevel.SelectedIndex].Level : WaterLevel.None,
            NeutralDiplomacyModifier = SldDiplomacy.Value / 100.0,
            EncounterHoles        = ChkEncounterHoles.IsChecked == true,
            TerrainRoughnessPercent = (int)SldTerrainRoughness.Value,
            LakeAmountPercent     = (int)SldLakeAmount.Value,
            RandomPortals         = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections  = (int)SldMaxPortals.Value,
            SpawnRemoteFootholds  = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads         = ChkGenerateRoads.IsChecked == true,
            NoDirectPlayerConn    = ChkNoDirectPlayerConn.IsChecked == true,
            ResourceDensityPercent = (int)SldResourceDensity.Value,
            StructureDensityPercent = (int)SldStructureDensity.Value,
            NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
            BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
            VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1",
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent   = (int)SldAstrologyExp.Value,
            LostStartCity         = ChkLostStartCity.IsChecked == true,
            LostStartCityDay = (int)SldLostStartCityDay.Value,
            LostStartHero         = ChkLostStartHero.IsChecked == true,
            CityHold              = ChkCityHold.IsChecked == true,
            CityHoldDays = (int)SldCityHoldDays.Value,
            HeroHireBan          = ChkHeroHireBan.IsChecked == true,
            HeroLighting         = ChkHeroLighting.IsChecked == true,
            HeroLightingDay      = HeroLightingDayValue(),
            GladiatorArena               = ChkGladiatorArena.IsChecked == true,
            GladiatorArenaDaysDelayStart = (int)SldGladiatorDelay.Value,
            GladiatorArenaCountDay       = (int)SldGladiatorCountDay.Value,
            Tournament                   = ChkTournament.IsChecked == true,
            TournamentFirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
            TournamentInterval = (int)SldTournamentInterval.Value,
            TournamentPointsToWin = (int)SldTournamentPointsToWin.Value,
            TournamentSaveArmy = ChkTournamentSaveArmy.IsChecked == true,
            BannedItems        = string.Join("\n", _bannedItems.Select(e => e.Id)),
            BannedMagics       = string.Join("\n", _bannedMagics.Select(e => e.Id)),
            BannedHeroes       = string.Join("\n", _bannedHeroes.Select(e => e.Id)),
            ValueOverridesText = TxtValueOverrides.Text,
            BonusesJson        = string.Join("\n", _bonuses.Select(b => b.ToString())),
            // New format: save UI rows verbatim so Count and row identity are preserved.
            PlayerZoneContentRows      = BuildZoneContentRows(_playerZoneMandatoryContent),
            LowNeutralContentRows      = BuildZoneContentRows(_lowNeutralMandatoryContent),
            MediumNeutralContentRows   = BuildZoneContentRows(_mediumNeutralMandatoryContent),
            HighNeutralContentRows     = BuildZoneContentRows(_highNeutralMandatoryContent),
            HubZoneContentRows         = BuildZoneContentRows(_hubZoneMandatoryContent),
        };

        private void ApplySettings(SettingsFile s)
        {
            TxtTemplateName.Text    = s.TemplateName;
            bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
            bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
            _advancedZoneSettings = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;
            ChkExperimentalMapSizes.IsChecked = needsExperimentalMapSizes;
            RefreshMapSizeOptions(s.MapSize);
            SldPlayers.Value        = s.PlayerCount;
            SldNeutral.Value        = s.NeutralZoneCount;
            SldPlayerCastles.Value  = s.PlayerZoneCastles;
            SldNeutralCastles.Value = s.NeutralZoneCastles;

            // Legacy compatibility: if the settings were saved with Advanced mode disabled,
            // the neutral zones were all stored as a single count in NeutralZoneCount.
            // Convert them to medium-quality neutrals with castles (or without, if NeutralZoneCastles was 0).
            int legacyLowNoCastle    = s.NeutralLowNoCastleCount;
            int legacyLowCastle      = s.NeutralLowCastleCount;
            int legacyMedNoCastle    = s.NeutralMediumNoCastleCount;
            int legacyMedCastle      = s.NeutralMediumCastleCount;
            int legacyHighNoCastle   = s.NeutralHighNoCastleCount;
            int legacyHighCastle     = s.NeutralHighCastleCount;
            if (!s.AdvancedMode && s.NeutralZoneCount > 0
                && legacyLowNoCastle == 0 && legacyLowCastle == 0
                && legacyMedNoCastle == 0 && legacyMedCastle == 0
                && legacyHighNoCastle == 0 && legacyHighCastle == 0)
            {
                if (s.NeutralZoneCastles > 0)
                    legacyMedCastle = s.NeutralZoneCount;
                else
                    legacyMedNoCastle = s.NeutralZoneCount;
            }

            SldNeutralLowNoCastle.Value    = legacyLowNoCastle;
            SldNeutralLowCastle.Value      = legacyLowCastle;
            SldNeutralMediumNoCastle.Value = legacyMedNoCastle;
            SldNeutralMediumCastle.Value   = legacyMedCastle;
            SldNeutralHighNoCastle.Value   = legacyHighNoCastle;
            SldNeutralHighCastle.Value     = legacyHighCastle;
            ChkMatchPlayerCastleFactions.IsChecked = s.MatchPlayerCastleFactions;
            ChkPlayerStartsWithCastles.IsChecked   = s.PlayerStartsWithCastles;
            SldMinNeutralBetweenPlayers.Value = s.MinNeutralZonesBetweenPlayers;
            SldPlayerZoneSize.Value = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0);
            SldNeutralZoneSize.Value = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0);
            SldHubZoneSize.Value = Math.Clamp(s.HubZoneSize, 0.25, 3.0);
            SldHubCastles.Value = Math.Clamp(s.HubZoneCastles, 0, 4);
            SldGuardRandomization.Value = GuardRandomizationPercent(s.GuardRandomization);
            SldHeroMin.Value        = s.HeroCountMin;
            SldHeroMax.Value        = s.HeroCountMax;
            SldHeroIncrement.Value  = s.HeroCountIncrement;
            ChkSingleHeroMode.IsChecked = s.SingleHeroMode;
            ChkHeroHireBan.IsChecked = s.HeroHireBan;
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) CmbTopology.SelectedIndex = topoIdx;
            int terrainIdx = Array.FindIndex(TerrainOptions, t => t.Theme == s.Terrain);
            CmbTerrain.SelectedIndex = terrainIdx >= 0 ? terrainIdx : 0;
            int aggressionIdx = Array.FindIndex(AggressionOptions, a => a.Level == s.MonsterAggression);
            CmbMonsterAggression.SelectedIndex = aggressionIdx >= 0 ? aggressionIdx : 1;
            int waterIdx = Array.FindIndex(WaterOptions, w => w.Level == s.WaterLevel);
            CmbWaterLevel.SelectedIndex = waterIdx >= 0 ? waterIdx : 0;
            SldDiplomacy.Value = Math.Clamp(s.NeutralDiplomacyModifier, -1.0, 1.0) * 100.0;
            ChkEncounterHoles.IsChecked = s.EncounterHoles;
            SldTerrainRoughness.Value = Math.Clamp(s.TerrainRoughnessPercent, 0, 200);
            SldLakeAmount.Value = Math.Clamp(s.LakeAmountPercent, 0, 200);
            ChkRandomPortals.IsChecked        = s.RandomPortals;
            SldMaxPortals.Value               = Math.Clamp(s.MaxPortalConnections, 1, 32);
            PnlMaxPortals.Visibility          = s.RandomPortals ? Visibility.Visible : Visibility.Collapsed;
            ChkSpawnFootholds.IsChecked       = s.SpawnRemoteFootholds;
            ChkGenerateRoads.IsChecked        = s.GenerateRoads;
            ChkNoDirectPlayerConn.IsChecked   = s.NoDirectPlayerConn;
            SldResourceDensity.Value          = s.EffectiveResourceDensityPercent;
            SldStructureDensity.Value         = s.EffectiveStructureDensityPercent;
            SldNeutralStackStrength.Value     = s.NeutralStackStrengthPercent;
            SldBorderGuardStrength.Value      = s.BorderGuardStrengthPercent;
            int victoryIdx = Array.IndexOf(KnownValues.VictoryConditionIds, s.VictoryCondition);
            CmbVictory.SelectedIndex = victoryIdx >= 0 ? victoryIdx : 0;
            SldFactionLawsExp.Value = Math.Clamp(s.FactionLawsExpPercent, 20, 200);
            SldAstrologyExp.Value = Math.Clamp(s.AstrologyExpPercent, 20, 200);
            ChkLostStartCity.IsChecked = s.LostStartCity;
            SldLostStartCityDay.Value = Math.Clamp(s.LostStartCityDay, 1, 30);
            ChkLostStartHero.IsChecked = s.LostStartHero || s.SingleHeroMode;
            ChkCityHold.IsChecked = s.CityHold;
            SldCityHoldDays.Value = Math.Clamp(s.CityHoldDays, 1, 30);
            ChkHeroLighting.IsChecked = s.HeroLighting;
            TxtHeroLightingDay.Text = Math.Clamp(s.HeroLightingDay, 1, 30).ToString();
            TxtHeroLightingDay.IsEnabled = s.HeroLighting;
            ChkGladiatorArena.IsChecked = s.GladiatorArena;
            SldGladiatorDelay.Value = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60);
            SldGladiatorCountDay.Value = Math.Clamp(s.GladiatorArenaCountDay, 1, 30);
            ChkTournament.IsChecked = s.Tournament;
            SldTournamentFirstTournamentDay.Value = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60);
            SldTournamentInterval.Value = Math.Clamp(s.TournamentInterval, 1, 30);
            SldTournamentPointsToWin.Value = Math.Clamp(s.TournamentPointsToWin, 1, 10);
            ChkTournamentSaveArmy.IsChecked = s.TournamentSaveArmy;
            LoadBanList(_bannedItems,  s.BannedItems,   ItemEntryFromId);
            LoadBanList(_bannedMagics, s.BannedMagics,  MagicEntryFromId);
            LoadBanList(_bannedHeroes, s.BannedHeroes,  HeroEntryFromId);
            LoadBonusList(s.BonusesJson);
            TxtValueOverrides.Text = s.ValueOverridesText;
            ApplyZoneContentRows(_playerZoneMandatoryContent,  s.PlayerZoneContentRows,    InitializeDefaultPlayerZoneContents);
            ApplyZoneContentRows(_lowNeutralMandatoryContent,  s.LowNeutralContentRows,    InitializeDefaultLowNeutralContents);
            ApplyZoneContentRows(_mediumNeutralMandatoryContent, s.MediumNeutralContentRows, InitializeDefaultMediumNeutralContents);
            ApplyZoneContentRows(_highNeutralMandatoryContent, s.HighNeutralContentRows,   InitializeDefaultHighNeutralContents);
            ApplyZoneContentRows(_hubZoneMandatoryContent,     s.HubZoneContentRows,       InitializeDefaultHubZoneContents);
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
        }

        private bool SaveToPath(string path)
        {
            try
            {
                var json = JsonSerializer.Serialize(GatherSettings(), JsonOptions);
                File.WriteAllText(path, json);
                _currentSettingsPath = path;
                _isDirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.Get("S.D.SaveSettingsErr", ex.Message), L.Get("S.D.SaveErrTitle"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(L.Get("S.D.ResetConfirm"), L.Get("S.D.ResetTitle"),
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // Simple Mode has its own controls (the Advanced ApplySettings does not touch them),
            // so the reset must be routed to whichever view is currently shown.
            if (SimpleView != null && SimpleView.Visibility == Visibility.Visible)
            {
                ResetSimpleToDefaults();
                return;
            }

            ApplySettings(new SettingsFile());
            _currentSettingsPath = null;
            _isDirty = false;
            UpdateTitle();
        }

        /// <summary>Resets the Simple-mode options to defaults (+ a fresh seed) and clears the last result,
        /// mirroring what BtnNew does for the Advanced view. Used by the header "Reset settings" in Simple mode.</summary>
        private void ResetSimpleToDefaults()
        {
            ApplySimpleState(new Services.GameData.SimpleModeState()); // default selections
            TxtSimpleSeed.Text = ((uint)NewSeed()).ToString("X8");      // a fresh seed too
            SaveSimpleState();                                          // persist the reset

            // Clear the result panel + collapse the optional preview.
            _generatedTemplate = null;
            _lastQuickSettings = null;
            if (ImgSimplePreview   != null) ImgSimplePreview.Source = null;
            if (TxtSimpleNoPreview != null) TxtSimpleNoPreview.Visibility = Visibility.Visible;
            if (SimplePreviewBox   != null) SimplePreviewBox.Visibility   = Visibility.Collapsed;
            if (TxtSimpleSummary   != null) TxtSimpleSummary.Text = string.Empty;
            BtnSimpleSaveToGame.IsEnabled   = false;
            BtnSimpleSave.IsEnabled         = false;
            BtnSimpleOpenAdvanced.IsEnabled = false;
            if (BtnSimpleTogglePreview != null) BtnSimpleTogglePreview.IsEnabled = false;
            BtnSaveGenerated.Visibility = Visibility.Collapsed;
            UpdateSimplePreviewToggle();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = L.Get("S.D.OpenTitle"),
                Filter = L.Get("S.D.SettingsFilter"),
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var s = JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions);
                if (s is null) throw new InvalidDataException(L.Get("S.D.FileEmpty"));
                ApplySettings(s);
                _currentSettingsPath = dlg.FileName;
                _isDirty = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.Get("S.D.OpenErr", ex.Message), L.Get("S.D.OpenErrTitle"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSettingsPath is not null)
                SaveToPath(_currentSettingsPath);
            else
                BtnSaveAs_Click(sender, e);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = L.Get("S.D.SaveAsTitle"),
                Filter     = L.Get("S.D.SettingsFilter"),
                FileName   = TxtTemplateName.Text.Trim().Length > 0 ? TxtTemplateName.Text.Trim() : L.Get("S.D.MySettings"),
                DefaultExt = ".oetgs",
            };
            if (dlg.ShowDialog() == true)
                SaveToPath(dlg.FileName);
        }

        // -- Generate ----------------------------------------------------------

        // The most recently generated template — used by BtnSaveGenerated_Click
        private RmgTemplate? _generatedTemplate;
        private MapTopology  _generatedTopology;
        private bool _templateOutdated = false;

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;
            var settings = BuildSettings();
            _generatedTemplate = TemplateGenerator.Generate(settings);
            _generatedTopology = settings.Topology;
            _templateOutdated = false;
            ImgPreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
            lblNoPreview.Content = "?";
            BtnSaveGenerated.Visibility = Visibility.Visible;
            UpdateBalanceReport();
            UpdateOutdatedWarning();
            Validate(); // refresh warnings now that template is up to date
        }

        private void BtnOpenEditor_Click(object sender, RoutedEventArgs e)
        {
            // Open the just-generated template if there is one; otherwise an empty editor
            // where the user can load a .rmg.json directly.
            var editor = new TemplateEditorWindow(_generatedTemplate, _generatedTopology) { Owner = this };
            editor.Show();
        }

        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string tag }) return;
            var lang = tag == "en"
                ? Services.Localization.AppLanguage.En
                : Services.Localization.AppLanguage.Ru;
            Services.Localization.LocalizationManager.Instance.SetLanguage(lang);
            Services.GameData.AppSettings.Current.Language = tag;
            Services.GameData.AppSettings.Current.Save();
            UpdateLanguageButtons();
        }

        /// <summary>Re-applies localized text everywhere that is driven by code (combos, preset menu, hero names).</summary>
        private void OnLanguageChanged(object? sender, System.EventArgs e)
        {
            RefreshLocalizedLists();
            _refreshingLists = true;
            try { RefreshContentMenuLanguage(); } finally { _refreshingLists = false; }
            foreach (var grp in new[] { _playerZoneMandatoryContent, _lowNeutralMandatoryContent,
                                        _mediumNeutralMandatoryContent, _highNeutralMandatoryContent, _hubZoneMandatoryContent })
                foreach (var item in grp.AllItems) item.RefreshDisplayName();
            BuildPresetMenu();          // submenu group names + preset display names
            RefreshBannedHeroNames();   // ban rows re-resolve names
            UpdateLanguageButtons();
            UpdateSimplePreviewToggle();   // Simple-mode preview toggle caption (code-set, re-localize here)
            if (TxtWipWarning != null) TxtWipWarning.Text = L.Get("S.CB.Wip");
            if (IsInitialized) { Validate(); UpdateTitle(); }  // refresh validation hints in the new language
        }

        /// <summary>Rebuilds option-combo item sources in the current language, preserving each selection.</summary>
        private void RefreshLocalizedLists()
        {
            _refreshingLists = true;
            try
            {
                void Rebind(System.Windows.Controls.ComboBox cb, System.Collections.Generic.List<string> items)
                {
                    int sel = cb.SelectedIndex;
                    cb.ItemsSource = items;
                    cb.SelectedIndex = sel >= 0 && sel < items.Count ? sel : (items.Count > 0 ? 0 : -1);
                }

                Rebind(CmbVictory, [.. KnownValues.VictoryConditionLabels.Select((_, i) => L.Get($"S.Victory.{i}"))]);
                Rebind(CmbTopology, [.. TopologyOptions.Select(t => L.Get(t.Label))]);
                Rebind(CmbMapView, [.. TopologyOptions.Select(t => L.Get(t.Label))]);
                Rebind(CmbTerrain, [.. TerrainOptions.Select(t => L.Get(t.Label))]);
                Rebind(CmbMonsterAggression, [.. AggressionOptions.Select(a => L.Get(a.Label))]);
                Rebind(CmbWaterLevel, [.. WaterOptions.Select(w => L.Get(w.Label))]);

                // Simple Mode / Quick Generate option combos
                Rebind(CmbSimpleType,   [.. SimpleTypeKeys.Select(k => L.Get(k))]);
                Rebind(CmbSimpleScale,  [.. SimpleScaleKeys.Select(k => L.Get(k))]);
                Rebind(CmbSimpleLength, [.. SimpleLengthKeys.Select(k => L.Get(k))]);
                Rebind(CmbSimpleChaos,  [.. SimpleChaosKeys.Select(k => L.Get(k))]);
                // Same victory-condition set the Advanced tab exposes (the real in-game modes).
                Rebind(CmbSimpleVictory, [.. KnownValues.VictoryConditionLabels.Select((_, i) => L.Get($"S.Victory.{i}"))]);

                RefreshMapSizeOptions();   // re-format sizes (localized "(experimental)" suffix)

                int topoIdx = CmbTopology.SelectedIndex;
                if (topoIdx >= 0 && topoIdx < TopologyOptions.Length && TxtTopologyDesc != null)
                    TxtTopologyDesc.Text = L.Get(TopologyOptions[topoIdx].Description);
            }
            finally { _refreshingLists = false; }
        }

        private void UpdateLanguageButtons()
        {
            if (BtnLangRu is null || BtnLangEn is null) return;
            bool en = Services.Localization.LocalizationManager.Instance.CurrentLanguage == Services.Localization.AppLanguage.En;
            BtnLangRu.FontWeight = en ? FontWeights.Normal : FontWeights.Bold;
            BtnLangEn.FontWeight = en ? FontWeights.Bold   : FontWeights.Normal;
            BtnLangRu.Opacity    = en ? 0.55 : 1.0;
            BtnLangEn.Opacity    = en ? 1.0  : 0.55;
        }

        // ── Simple Mode / Quick Generate ──────────────────────────────────────────

        private static readonly string[] SimpleTypeKeys   = ["S.Simple.Type.Duel", "S.Simple.Type.FFA", "S.Simple.Type.Pve", "S.Simple.Type.Team"];
        private static readonly string[] SimpleScaleKeys  = ["S.Simple.Scale.Small", "S.Simple.Scale.Medium", "S.Simple.Scale.Large", "S.Simple.Scale.Huge"];
        private static readonly string[] SimpleLengthKeys = ["S.Simple.Len.Short", "S.Simple.Len.Medium", "S.Simple.Len.Long"];
        private static readonly string[] SimpleChaosKeys  = ["S.Simple.Chaos.Tame", "S.Simple.Chaos.Normal", "S.Simple.Chaos.Wild"];

        private GeneratorSettings? _lastQuickSettings;

        /// <summary>Sets sensible defaults for the simple-mode combos + seed and applies the saved mode.</summary>
        private void InitSimpleMode()
        {
            ApplySimpleState(Services.GameData.AppSettings.Current.Simple); // restore last-used selections
            TxtSimpleSeed.Text = ((uint)NewSeed()).ToString("X8");           // always a fresh seed per launch

            UpdateSimplePreviewToggle();   // initial caption ("Show preview"); button stays disabled until a map exists

            bool advanced = string.Equals(Services.GameData.AppSettings.Current.Mode, "advanced", StringComparison.OrdinalIgnoreCase);
            SetMode(advanced, persist: false);
        }

        private void ApplySimpleState(Services.GameData.SimpleModeState st)
        {
            void SetCombo(System.Windows.Controls.ComboBox cb, int idx)
            { if (cb.Items.Count > 0) cb.SelectedIndex = Math.Clamp(idx, 0, cb.Items.Count - 1); }

            SldSimplePlayers.Value = Math.Clamp(st.Players, (int)SldSimplePlayers.Minimum, (int)SldSimplePlayers.Maximum);
            SetCombo(CmbSimpleType, st.Type);
            SetCombo(CmbSimpleScale, st.Scale);
            SetCombo(CmbSimpleLength, st.Length);
            SetCombo(CmbSimpleChaos, st.Chaos);
            SetCombo(CmbSimpleVictory, st.Victory);
            ChkSimpleWater.IsChecked = st.Water;
            ChkSimplePortals.IsChecked = st.Portals;
            ChkSimpleStrong.IsChecked = st.StrongNeutrals;
        }

        private void SaveSimpleState()
        {
            var st = Services.GameData.AppSettings.Current.Simple;
            st.Players        = (int)SldSimplePlayers.Value;
            st.Type           = CmbSimpleType.SelectedIndex;
            st.Scale          = CmbSimpleScale.SelectedIndex;
            st.Length         = CmbSimpleLength.SelectedIndex;
            st.Chaos          = CmbSimpleChaos.SelectedIndex;
            st.Victory        = CmbSimpleVictory.SelectedIndex;
            st.Water          = ChkSimpleWater.IsChecked == true;
            st.Portals        = ChkSimplePortals.IsChecked == true;
            st.StrongNeutrals = ChkSimpleStrong.IsChecked == true;
            Services.GameData.AppSettings.Current.Save();
        }

        private void SetMode(bool advanced, bool persist = true)
        {
            if (AdvancedView == null || SimpleView == null) return;
            AdvancedView.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            SimpleView.Visibility   = advanced ? Visibility.Collapsed : Visibility.Visible;
            UpdateModeButtons(advanced);
            if (persist)
            {
                Services.GameData.AppSettings.Current.Mode = advanced ? "advanced" : "simple";
                Services.GameData.AppSettings.Current.Save();
            }
        }

        private void UpdateModeButtons(bool advanced)
        {
            if (BtnModeSimple == null || BtnModeAdvanced == null) return;
            BtnModeSimple.FontWeight   = advanced ? FontWeights.Normal : FontWeights.Bold;
            BtnModeAdvanced.FontWeight = advanced ? FontWeights.Bold   : FontWeights.Normal;
            BtnModeSimple.Opacity      = advanced ? 0.55 : 1.0;
            BtnModeAdvanced.Opacity    = advanced ? 1.0  : 0.55;
        }

        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string tag })
                SetMode(tag == "advanced");
        }

        private static int NewSeed() => new Random().Next(int.MinValue, int.MaxValue);

        private static int ParseSeed(string? text)
        {
            text = (text ?? "").Trim();
            if (uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint hex))
                return unchecked((int)hex);
            if (int.TryParse(text, out int dec)) return dec;
            return NewSeed();
        }

        private void BtnSimpleRoll_Click(object sender, RoutedEventArgs e)
            => TxtSimpleSeed.Text = ((uint)NewSeed()).ToString("X8");

        private QuickGenerateOptions BuildQuickOptions() => new()
        {
            PlayerCount    = (int)SldSimplePlayers.Value,
            GameType       = (QuickGameType)Math.Clamp(CmbSimpleType.SelectedIndex, 0, 3),
            Scale          = (QuickMapScale)Math.Clamp(CmbSimpleScale.SelectedIndex, 0, 3),
            Length         = (QuickGameLength)Math.Clamp(CmbSimpleLength.SelectedIndex, 0, 2),
            Chaos          = (QuickChaos)Math.Clamp(CmbSimpleChaos.SelectedIndex, 0, 2),
            Water          = ChkSimpleWater.IsChecked == true,
            Portals        = ChkSimplePortals.IsChecked == true,
            StrongNeutrals = ChkSimpleStrong.IsChecked == true,
            VictoryCondition = CmbSimpleVictory.SelectedIndex >= 0 && CmbSimpleVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbSimpleVictory.SelectedIndex]
                : "win_condition_1",
            Seed           = ParseSeed(TxtSimpleSeed.Text),
        };

        private void BtnSimpleGenerate_Click(object sender, RoutedEventArgs e)
        {
            var opts = BuildQuickOptions();
            TxtSimpleSeed.Text = ((uint)opts.Seed).ToString("X8"); // normalise display
            var settings = Olden_Era___Template_Editor.Services.Generation.RandomTemplateBuilder.Build(opts);

            _generatedTemplate = TemplateGenerator.Generate(settings);
            _generatedTopology = settings.Topology;
            _templateOutdated  = false;
            _lastQuickSettings = settings;
            TxtTemplateName.Text = settings.TemplateName; // so the Save dialog uses the generated name

            ImgSimplePreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
            TxtSimpleNoPreview.Visibility = Visibility.Collapsed;
            TxtSimpleSummary.Text = BuildSimpleSummary(settings, opts);
            BtnSimpleSaveToGame.IsEnabled = true;
            BtnSimpleSave.IsEnabled = true;
            BtnSimpleOpenAdvanced.IsEnabled = true;
            BtnSimpleTogglePreview.IsEnabled = true;          // preview is now available on demand
            UpdateSimplePreviewToggle();                       // (re)sync the toggle caption; preview stays hidden by default
            BtnSaveGenerated.Visibility = Visibility.Visible; // keep the advanced save path in sync too
            SaveSimpleState();                                 // remember these selections for next launch
        }

        /// <summary>Toggles the optional template-structure preview in Simple mode. Hidden by default so the
        /// generator feels like a "real random map generator"; the player reveals the layout only if they wish.</summary>
        private void BtnSimpleTogglePreview_Click(object sender, RoutedEventArgs e)
        {
            if (SimplePreviewBox == null) return;
            bool show = SimplePreviewBox.Visibility != Visibility.Visible;
            SimplePreviewBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            UpdateSimplePreviewToggle();
        }

        /// <summary>Syncs the Simple-mode preview toggle caption to the current preview visibility (re-localizable).</summary>
        private void UpdateSimplePreviewToggle()
        {
            if (BtnSimpleTogglePreview == null) return;
            bool shown = SimplePreviewBox != null && SimplePreviewBox.Visibility == Visibility.Visible;
            BtnSimpleTogglePreview.Content = L.Get(shown ? "S.Simple.HidePreview" : "S.Simple.ShowPreview");
        }

        private static readonly string[] SimpleLenLabelKeys = ["S.Simple.Len.Short", "S.Simple.Len.Medium", "S.Simple.Len.Long"];

        // ── Balance report (shared by the Simple summary + the Advanced result panel) ──

        /// <summary>Formats the balance report for <paramref name="template"/> as a "⚖ score + findings"
        /// block (warnings first, max 3 findings); null when the map has fewer than two players.</summary>
        private static string? FormatBalanceSummary(RmgTemplate? template)
        {
            if (template is null) return null;
            var report = Services.Analysis.TemplateBalanceReport.Analyze(template);
            if (!report.Applicable) return null;

            var sb = new System.Text.StringBuilder();
            sb.Append(L.Get("S.Bal.Score", report.Score));
            foreach (var f in report.Findings
                .OrderByDescending(f => f.Severity == Services.Analysis.BalanceSeverity.Warning)
                .Take(3))
                sb.Append("\n•  ").Append(L.Get(f.Key, f.Args));
            return sb.ToString();
        }

        /// <summary>Formats the "what's inside" content breakdown for <paramref name="template"/> as a
        /// compact 2-line block (zones-by-role + total treasure/resources); null when there are no zones.</summary>
        private static string? FormatContentSummary(RmgTemplate? template)
        {
            if (template is null) return null;
            var c = Services.Analysis.TemplateContentSummary.Analyze(template);
            if (c.ZoneCount == 0) return null;
            return L.Get("S.Content.Zones", c.ZoneCount, c.PlayerZones, c.NeutralZones, c.CastleZones, c.ConnectionCount)
                 + "\n" + L.Get("S.Content.Wealth", FormatBig(c.TotalTreasure), FormatBig(c.TotalResources));
        }

        /// <summary>Compact human number: 1234→"1k", 2500000→"2.5M".</summary>
        private static string FormatBig(long v)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (v >= 1_000_000) return (v / 1_000_000.0).ToString("0.#", ci) + "M";
            if (v >= 1_000) return (v / 1_000.0).ToString("0", ci) + "k";
            return v.ToString(ci);
        }

        /// <summary>Combined analysis block (content breakdown + balance report) shared by both modes.</summary>
        private static string? FormatAnalysis(RmgTemplate? template)
        {
            var parts = new System.Collections.Generic.List<string>();
            string? content = FormatContentSummary(template);
            if (content != null) parts.Add(content);
            string? balance = FormatBalanceSummary(template);
            if (balance != null) parts.Add(balance);
            return parts.Count > 0 ? string.Join("\n\n", parts) : null;
        }

        /// <summary>Refreshes the Advanced-mode analysis line (content + balance) from the last generated template.</summary>
        private void UpdateBalanceReport()
        {
            if (TxtBalanceReport is null) return;
            string? text = FormatAnalysis(_generatedTemplate);
            TxtBalanceReport.Text = text ?? string.Empty;
            TxtBalanceReport.Visibility = text != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private string BuildSimpleSummary(GeneratorSettings s, QuickGenerateOptions opts)
        {
            var topo = TopologyOptions.FirstOrDefault(t => t.Topology == s.Topology);
            string topoLabel = topo.Label != null ? L.Get(topo.Label) : s.Topology.ToString();
            string lengthLabel = L.Get(SimpleLenLabelKeys[EstimateLengthIndex(s.MapSize, opts.Length)]);

            string summary = L.Get("S.Simple.Sum.Length", lengthLabel)
                           + "\n" + L.Get("S.Simple.Sum.Line", s.PlayerCount, s.MapSize, topoLabel);

            var extras = new System.Collections.Generic.List<string>();
            if (s.WaterLevel != WaterLevel.None) extras.Add(L.Get("S.Simple.Water"));
            if (s.RandomPortals) extras.Add(L.Get("S.Simple.Portals"));
            if (s.MonsterAggression == MonsterAggression.Aggressive) extras.Add(L.Get("S.Simple.StrongNeutrals"));
            if (extras.Count > 0) summary += "\n" + L.Get("S.Simple.SumExtras", string.Join(", ", extras));

            // Flag maps beyond the official 240×240 cap so the player knows it's an experimental large map.
            if (s.MapSize > KnownValues.MaxOfficialMapSize)
                summary += "\n" + L.Get("S.Simple.ExpNote");

            string? analysis = FormatAnalysis(_generatedTemplate);
            if (analysis != null) summary += "\n\n" + analysis;
            return summary;
        }

        /// <summary>Maps map size + desired length to a 0/1/2 (short/medium/long) bucket for the summary.</summary>
        private static int EstimateLengthIndex(int mapSize, QuickGameLength length)
        {
            int sizeBucket = mapSize <= 96 ? 0 : (mapSize <= 160 ? 1 : 2);
            double score = sizeBucket + length switch
            {
                QuickGameLength.Short => -0.5,
                QuickGameLength.Long => 0.5,
                _ => 0.0,
            };
            return score < 0.5 ? 0 : (score < 1.5 ? 1 : 2);
        }

        private void BtnSimpleSave_Click(object sender, RoutedEventArgs e) => BtnSaveGenerated_Click(sender, e);

        private void BtnSimpleOpenAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (_lastQuickSettings != null) SyncAdvancedFromSettings(_lastQuickSettings);
            SetMode(advanced: true);
        }

        /// <summary>Pushes the principal fields of a quick-generated settings object into the advanced
        /// controls so the user can tweak the random map by hand. Best-effort, not every field.</summary>
        private void SyncAdvancedFromSettings(GeneratorSettings s)
        {
            _suppressTopologySync = true;
            try
            {
                TxtTemplateName.Text = s.TemplateName;
                SldPlayers.Value = s.PlayerCount;
                RefreshMapSizeOptions(s.MapSize);

                int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
                if (topoIdx >= 0) { CmbTopology.SelectedIndex = topoIdx; CmbMapView.SelectedIndex = topoIdx; }

                int vicIdx = Array.IndexOf(KnownValues.VictoryConditionIds, s.GameEndConditions.VictoryCondition);
                if (vicIdx >= 0) CmbVictory.SelectedIndex = vicIdx;

                int terrIdx = Array.FindIndex(TerrainOptions, t => t.Theme == s.Terrain);
                if (terrIdx >= 0) CmbTerrain.SelectedIndex = terrIdx;
                int watIdx = Array.FindIndex(WaterOptions, w => w.Level == s.WaterLevel);
                if (watIdx >= 0) CmbWaterLevel.SelectedIndex = watIdx;
                int aggIdx = Array.FindIndex(AggressionOptions, ag => ag.Level == s.MonsterAggression);
                if (aggIdx >= 0) CmbMonsterAggression.SelectedIndex = aggIdx;

                SldResourceDensity.Value     = s.ZoneCfg.ResourceDensityPercent;
                SldStructureDensity.Value    = s.ZoneCfg.StructureDensityPercent;
                SldNeutralStackStrength.Value = s.ZoneCfg.NeutralStackStrengthPercent;
                SldBorderGuardStrength.Value = s.ZoneCfg.BorderGuardStrengthPercent;
                SldDiplomacy.Value           = s.NeutralDiplomacyModifier * 100.0;
                SldTerrainRoughness.Value    = s.TerrainRoughnessPercent;
                SldLakeAmount.Value          = s.LakeAmountPercent;

                // Advanced reads neutral zones ONLY from the per-tier sliders (the SldNeutral aggregate is
                // hidden in Advanced mode), so push every tier here — otherwise opening a quick map in
                // Advanced shows zero neutral zones (the reported Simple→Advanced transfer bug).
                var a = s.ZoneCfg.Advanced;
                SldNeutralLowNoCastle.Value    = a.NeutralLowNoCastleCount;
                SldNeutralLowCastle.Value      = a.NeutralLowCastleCount;
                SldNeutralMediumNoCastle.Value = a.NeutralMediumNoCastleCount;
                SldNeutralMediumCastle.Value   = a.NeutralMediumCastleCount;
                SldNeutralHighNoCastle.Value   = a.NeutralHighNoCastleCount;
                SldNeutralHighCastle.Value     = a.NeutralHighCastleCount;
                SldPlayerCastles.Value         = s.ZoneCfg.PlayerZoneCastles;
                SldNeutralCastles.Value        = s.ZoneCfg.NeutralZoneCastles;
                SldNeutral.Value = a.NeutralLowNoCastleCount + a.NeutralLowCastleCount
                                 + a.NeutralMediumNoCastleCount + a.NeutralMediumCastleCount
                                 + a.NeutralHighNoCastleCount + a.NeutralHighCastleCount;

                ChkRandomPortals.IsChecked    = s.RandomPortals;
                SldMaxPortals.Value           = s.MaxPortalConnections;
                ChkEncounterHoles.IsChecked   = s.EncounterHoles;
                ChkNoDirectPlayerConn.IsChecked = s.NoDirectPlayerConnections;
            }
            finally { _suppressTopologySync = false; }

            if (_generatedTemplate != null)
            {
                ImgPreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
                lblNoPreview.Content = "?";
                BtnSaveGenerated.Visibility = Visibility.Visible;
                BtnSaveGenerated.IsEnabled = true;
            }
        }

        private async void BtnSimpleCopySeed_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(TxtSimpleSeed.Text.Trim()); }
            catch { return; } // clipboard can be transiently locked — ignore
            if (sender is Button btn)
            {
                object? prev = btn.Content;
                btn.Content = "✓";
                await System.Threading.Tasks.Task.Delay(900);
                btn.Content = prev;
            }
        }

        /// <summary>One-click save straight into the game's map_templates folder (no dialog).</summary>
        private void BtnSimpleSaveToGame_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedTemplate is null) return;

            string? gameDir = FindOldenEraTemplatesPath();
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show(L.Get("S.Simple.NoGameFolder"), L.Get("S.D.SavedTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnSimpleSave_Click(sender, e); // fall back to the manual save dialog
                return;
            }

            // Ask the player what to call the map (this is the name the game shows in its picker).
            string suggested = TxtTemplateName.Text.Trim();
            if (suggested.Length == 0) suggested = "AuroraQuick";
            var prompt = new NamePromptWindow(suggested) { Owner = this };
            if (prompt.ShowDialog() != true) return;
            string chosen = prompt.MapName;

            _generatedTemplate.Name = chosen;  // in-game template name
            TxtTemplateName.Text = chosen;     // keep the rest of the UI in sync

            string fileName = chosen;
            foreach (char c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            string path = Path.Combine(gameDir, fileName + ".rmg.json");

            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(_generatedTemplate, JsonOptions));
                try { TemplatePreviewPngWriter.Save(_generatedTemplate, TemplatePreviewPngWriter.GetSidecarPath(path), _generatedTopology); }
                catch { /* preview sidecar is best-effort */ }
                MessageBox.Show(L.Get("S.Simple.SavedToGame", path), L.Get("S.D.SavedTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L.Get("S.D.SavedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedTemplate is null) return;

            string? gameTemplatesPath = FindOldenEraTemplatesPath();

            string currentTemplateName = TxtTemplateName.Text.Trim();

            var dlg = new SaveFileDialog
            {
                Title = L.Get("S.D.SaveTplTitle"),
                Filter = L.Get("S.D.TplFilter"),
                FileName = $"{(currentTemplateName.Length > 0 ? currentTemplateName : L.Get("S.M.014"))}.rmg.json",
                DefaultExt = ".rmg.json"
            };

            if (gameTemplatesPath != null)
                dlg.InitialDirectory = gameTemplatesPath;

            if (dlg.ShowDialog() != true) return;

            if (!IsInsideGameTemplatesFolder(dlg.FileName, gameTemplatesPath))
            {
                string expectedDesc = gameTemplatesPath != null
                    ? L.Get("S.D.ExpectedPath", gameTemplatesPath)
                    : L.Get("S.D.ExpectedStruct");
                var wrongFolderResult = MessageBox.Show(
                    L.Get("S.D.WrongFolderMsg", expectedDesc, Path.GetDirectoryName(dlg.FileName) ?? ""),
                    L.Get("S.D.WrongFolderTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (wrongFolderResult != MessageBoxResult.Yes) return;
            }

            string chosenBaseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dlg.FileName));
            if (!chosenBaseName.Equals(currentTemplateName, StringComparison.Ordinal))
            {
                var mismatchResult = MessageBox.Show(
                    L.Get("S.D.NameMismatchMsg", Path.GetFileName(dlg.FileName), currentTemplateName),
                    L.Get("S.D.NameMismatchTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (mismatchResult != MessageBoxResult.Yes) return;
            }

            string json = JsonSerializer.Serialize(_generatedTemplate, JsonOptions);
            File.WriteAllText(dlg.FileName, json);

            string previewPath = TemplatePreviewPngWriter.GetSidecarPath(dlg.FileName);
            string? previewError = null;
            if (ChkSavePreviewImage.IsChecked == true)
            {
                try
                {
                    TemplatePreviewPngWriter.Save(_generatedTemplate, previewPath, _generatedTopology);
                }
                catch (Exception ex)
                {
                    previewError = ex.Message;
                }
            }

            string savedMsg = L.Get("S.D.SavedMsg", dlg.FileName);
            if (ChkSavePreviewImage.IsChecked == true)
            {
                if (previewError == null)
                    savedMsg += L.Get("S.D.SavedPreview", previewPath);
                else
                    savedMsg += L.Get("S.D.SavedPreviewErr", previewError);
            }
            if (gameTemplatesPath == null)
                savedMsg += L.Get("S.D.SavedHint");

            MessageBox.Show(savedMsg, L.Get("S.D.SavedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private GeneratorSettings BuildSettings() => new()
        {
            TemplateName = TxtTemplateName.Text.Trim(),
            GameMode = CmbGameMode.SelectedItem as string ?? "Classic",
            SingleHeroMode = ChkSingleHeroMode.IsChecked == true,
            PlayerCount = (int)SldPlayers.Value,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = (int)SldHeroMin.Value,
                HeroCountMax = (int)SldHeroMax.Value,
                HeroCountIncrement = (int)SldHeroIncrement.Value,
                HeroHireBan = ChkHeroHireBan.IsChecked == true
            },
            MapSize = SelectedMapSize(),
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                    ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                    : "win_condition_1",
                LostStartCity = ChkLostStartCity.IsChecked == true,
                LostStartCityDay = (int)SldLostStartCityDay.Value,
                LostStartHero = ChkLostStartHero.IsChecked == true,
                CityHold = ChkCityHold.IsChecked == true,
                CityHoldDays = (int)SldCityHoldDays.Value,
                HeroLighting = ChkHeroLighting.IsChecked == true,
                HeroLightingDay = HeroLightingDayValue(),
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = (int)SldNeutral.Value,
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                ResourceDensityPercent = (int)SldResourceDensity.Value,
                StructureDensityPercent = (int)SldStructureDensity.Value,
                NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
                BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
                HubZoneSize = SldHubZoneSize.Value,
                HubZoneCastles = (int)SldHubCastles.Value,
                Advanced = new AdvancedSettings
                {
                    Enabled = _advancedZoneSettings,
                    NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
                    NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
                    NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
                    NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
                    NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
                    NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
                    PlayerZoneSize = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
                    NeutralZoneSize = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
                    GuardRandomization = _advancedZoneSettings ? SldGuardRandomization.Value / 100.0 : 0.05,
                }
            },
            PlayerZoneMandatoryContent = BuildPlayerZoneMandatoryContentFromUi(),
            LowNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_lowNeutralMandatoryContent),
            MediumNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_mediumNeutralMandatoryContent),
            HighNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_highNeutralMandatoryContent),
            HubZoneMandatoryContent = BuildZoneMandatoryContentFromUi(_hubZoneMandatoryContent),
            // Neutral zones between players can be influenced by advanced zone settings, but is functionally independent.
            MinNeutralZonesBetweenPlayers = _advancedZoneSettings ? (int)SldMinNeutralBetweenPlayers.Value : 0,
            MatchPlayerCastleFactions = ChkMatchPlayerCastleFactions.IsChecked == true,
            PlayerStartsWithCastles   = ChkPlayerStartsWithCastles.IsChecked == true,
            NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
            RandomPortals = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections = (int)SldMaxPortals.Value,
            SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads = ChkGenerateRoads.IsChecked == true,
            Topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default,
            Terrain = CmbTerrain.SelectedIndex >= 0 ? TerrainOptions[CmbTerrain.SelectedIndex].Theme : TerrainTheme.FactionBased,
            MonsterAggression = CmbMonsterAggression.SelectedIndex >= 0 ? AggressionOptions[CmbMonsterAggression.SelectedIndex].Level : MonsterAggression.Normal,
            WaterLevel = CmbWaterLevel.SelectedIndex >= 0 ? WaterOptions[CmbWaterLevel.SelectedIndex].Level : WaterLevel.None,
            NeutralDiplomacyModifier = SldDiplomacy.Value / 100.0,
            EncounterHoles = ChkEncounterHoles.IsChecked == true,
            TerrainRoughnessPercent = (int)SldTerrainRoughness.Value,
            LakeAmountPercent = (int)SldLakeAmount.Value,
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent = (int)SldAstrologyExp.Value,
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = ChkGladiatorArena.IsChecked == true,
                DaysDelayStart = (int)SldGladiatorDelay.Value,
                CountDay = (int)SldGladiatorCountDay.Value
            },
            TournamentRules = new TournamentRules
            {
                Enabled = ChkTournament.IsChecked == true,
                FirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
                Interval = (int)SldTournamentInterval.Value,
                PointsToWin = (int)SldTournamentPointsToWin.Value,
                SaveArmy = ChkTournamentSaveArmy.IsChecked == true
            },
            BannedItems        = string.Join("\n", _bannedItems.Select(e => e.Id)),
            BannedMagics       = string.Join("\n", _bannedMagics.Select(e => e.Id)),
            BannedHeroes       = string.Join("\n", _bannedHeroes.Select(e => e.Id)),
            ValueOverridesText = TxtValueOverrides.Text,
            Bonuses            = [.. _bonuses],
        };
        
        /* Creates list of ContentItems for the player zone mandatory content, according to the UI settings. */
        private List<ContentItem> BuildPlayerZoneMandatoryContentFromUi()
        {
            var result = new List<ContentItem>();

            foreach (var item in _playerZoneMandatoryContent.AllItems)
            {
                /* Some initial sanity checks*/
                if (item.Count <= 0) continue;
                if(item.SidMapping == null) continue;

                /* Parse the road distance from the UI setting. "Any" is handled separately. */
                var distance = item.RoadDistance switch
                {
                    "Next To" => DistancePresets.NextTo,
                    "Near" => DistancePresets.Near,
                    "Far" => DistancePresets.Far,
                    "Very Far" => DistancePresets.VeryFar,
                    _ => DistancePresets.Medium
                };

                for (int i = 0; i < item.Count; i++)
                {
                    if (item.IsGroup)
                    {
                        var groupItem = new ContentItem
                        {
                            IncludeLists = new List<string> { item.SidMapping.Sid },
                            IsGuarded = item.IsGuarded
                        };

                        if (item.RoadDistance != "Any")
                        {
                            groupItem.Rules = new List<ContentPlacementRule>
                            {
                                RulePresets.RoadDistance(distance)
                            };
                        }

                        result.Add(groupItem);
                        continue;
                    }

                    var builder = ContentItemBuilder
                        .Create(item.SidMapping.Sid)
                        .Guarded(item.IsGuarded);
                    
                    if(_playerZoneMandatoryContent.mines.Contains(item))
                        builder.Mine();
                    
                    if (item.NearCastle)
                        builder.AddRule(RulePresets.NearCastle());

                    /* Do not include road placement for "Any" distance */
                    if(item.RoadDistance != "Any")
                        builder.RoadDistance(distance);
                    
                    result.Add(builder.Build());
                }
            }

            return result;
        }

        /* Generic version of BuildPlayerZoneMandatoryContentFromUi for neutral/hub zone collections. */
        private static List<ContentItem> BuildZoneMandatoryContentFromUi(ZoneMandatoryContent content)
        {
            var result = new List<ContentItem>();

            foreach (var item in content.AllItems)
            {
                if (item.Count <= 0) continue;
                if (item.SidMapping == null) continue;

                var distance = item.RoadDistance switch
                {
                    "Next To" => DistancePresets.NextTo,
                    "Near" => DistancePresets.Near,
                    "Far" => DistancePresets.Far,
                    "Very Far" => DistancePresets.VeryFar,
                    _ => DistancePresets.Medium
                };

                for (int i = 0; i < item.Count; i++)
                {
                    if (item.IsGroup)
                    {
                        var groupItem = new ContentItem
                        {
                            IncludeLists = new List<string> { item.SidMapping.Sid },
                            IsGuarded = item.IsGuarded
                        };

                        if (item.RoadDistance != "Any")
                        {
                            groupItem.Rules = new List<ContentPlacementRule>
                            {
                                RulePresets.RoadDistance(distance)
                            };
                        }

                        result.Add(groupItem);
                        continue;
                    }

                    var builder = ContentItemBuilder
                        .Create(item.SidMapping.Sid)
                        .Guarded(item.IsGuarded);

                    if (content.mines.Contains(item))
                        builder.Mine();

                    if (item.NearCastle)
                        builder.AddRule(RulePresets.NearCastle());

                    if (item.RoadDistance != "Any")
                        builder.RoadDistance(distance);

                    result.Add(builder.Build());
                }
            }

            return result;
        }

        /// <summary>
        /// Serializes a zone's UI rows directly as <see cref="ZoneContentRowSave"/> records,
        /// preserving Count and row identity so that two separate sawmill rows remain two rows
        /// after a round-trip (unlike the legacy ContentItem expansion path).
        /// </summary>
        private static List<ZoneContentRowSave> BuildZoneContentRows(ZoneMandatoryContent content)
        {
            var rows = new List<ZoneContentRowSave>();
            foreach (var item in content.AllItems)
            {
                if (item.SidMapping == null) continue;
                rows.Add(new ZoneContentRowSave
                {
                    Sid          = item.SidMapping.Sid,
                    Count        = item.Count,
                    IsGroup      = item.IsGroup,
                    IsGuarded    = item.IsGuarded,
                    NearCastle   = item.NearCastle,
                    RoadDistance = item.RoadDistance ?? "Any",
                    IsMine       = content.mines.Contains(item),
                });
            }
            return rows;
        }

        /* Helper function for checking if a SID belongs to a content item group */
        private static bool IsContentItemGroupSid(string sid, List<SidMapping> groupItems)
            => groupItems.Any(item => string.Equals(item.Sid, sid, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns true when
        /// (including any sub-folders, since the game supports those).
        /// If <paramref name="gameTemplatesPath"/> was resolved, a prefix match is used.
        /// Otherwise the chosen directory is checked against the known folder-structure tail
        /// <c>HeroesOldenEra_Data\StreamingAssets\map_templates</c>.
        /// </summary>
        private static bool IsInsideGameTemplatesFolder(string filePath, string? gameTemplatesPath)
        {
            string chosenDir = Path.GetDirectoryName(filePath) ?? string.Empty;

            if (gameTemplatesPath != null)
            {
                // Normalise both paths to ensure consistent separator and casing comparison.
                string normalised = Path.GetFullPath(chosenDir);
                string expected   = Path.GetFullPath(gameTemplatesPath);
                // Accept the folder itself or any sub-folder inside it.
                return normalised.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    || normalised.StartsWith(expected + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            // Game not found via registry/fallback paths — match on the known folder-structure tail.
            const string expectedTail = @"HeroesOldenEra_Data\StreamingAssets\map_templates";
            return chosenDir.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase)
                || chosenDir.Contains(expectedTail + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tries to locate the Olden Era map_templates folder via the Steam registry.
        /// Returns null if the game installation cannot be found.
        /// </summary>
        private static string? FindOldenEraTemplatesPath()
        {
            // Olden Era Steam App ID
            const string appId = "3105440";

            // Steam stores per-app install paths under this key.
            string[] registryRoots =
            [
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
            ];

            foreach (var keyPath in registryRoots)
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key?.GetValue("InstallLocation") is string installDir && Directory.Exists(installDir))
                    {
                        string templatesDir = Path.Combine(installDir, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                        if (Directory.Exists(templatesDir))
                            return templatesDir;
                    }
                }
                catch { /* registry access denied — skip */ }
            }

            // Fallback: check common Steam library locations manually.
            string[] steamLibraryRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
            ];

            foreach (var candidate in steamLibraryRoots)
            {
                string templatesDir = Path.Combine(candidate, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                if (Directory.Exists(templatesDir))
                    return templatesDir;
            }
            return null;
        }
        private void ChkSavePreviewImage_Click(object sender, RoutedEventArgs e)
        {
            if (ChkSavePreviewImage.IsChecked == true)
            {
                ImgPreview.Visibility = Visibility.Visible;
                lblNoPreview.Visibility = Visibility.Collapsed;

            }
            else
            {
                ImgPreview.Visibility = Visibility.Collapsed;
                lblNoPreview.Visibility = Visibility.Visible;
            }
        }

        private void PlayerZonesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Walk headers from last to first; show the sticky row for the last one
            // whose top edge has scrolled above the viewport top.
            var headers = new[]
            {
                (Element: TxtHeaderHeroImprovementStructures, Sticky: StickyHeroImprovementStructures),
                (Element: TxtHeaderUtilityStructures, Sticky: StickyUtilityStructures),
                (Element: TxtHeaderResourceBanks, Sticky: StickyResourceBanks),
                (Element: TxtHeaderUnitRecruitment, Sticky: StickyUnitRecruitment),
                (Element: TxtHeaderTreasures,     Sticky: StickyTreasures),
                (Element: TxtHeaderMines,         Sticky: StickyMines),
            };

            System.Windows.Controls.DockPanel? active = null;
            foreach (var (element, sticky) in headers)
            {
                var pos = element.TranslatePoint(new System.Windows.Point(0, 0), PlayerZonesScrollViewer);
                if (pos.Y < 0)
                {
                    active = sticky;
                    break;
                }
            }

            // If nothing has scrolled out of view, hide the sticky panel entirely (no duplication).
            if (active == null)
            {
                StickyHeaderPanel.Visibility = Visibility.Collapsed;
                return;
            }

            StickyHeaderPanel.Visibility   = Visibility.Visible;
            StickyMines.Visibility         = Visibility.Collapsed;
            StickyTreasures.Visibility     = Visibility.Collapsed;
            StickyUnitRecruitment.Visibility = Visibility.Collapsed;
            StickyResourceBanks.Visibility = Visibility.Collapsed;
            StickyUtilityStructures.Visibility = Visibility.Collapsed;
            StickyHeroImprovementStructures.Visibility = Visibility.Collapsed;
            active.Visibility              = Visibility.Visible;
        }



    }
}
