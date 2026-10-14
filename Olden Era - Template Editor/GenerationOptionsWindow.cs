using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

/// <summary>One transactional options dialog shared by Simple and Advanced generation.</summary>
public sealed class GenerationOptionsWindow : Window
{
    private readonly List<Action> _commit = [];
    private readonly List<TextBox> _teamSlots = [];
    private readonly List<CheckBox> _teamLocks = [];
    private bool _settingPresetSlots;
    private readonly ComboBox _players = new();
    private readonly ComboBox _preset = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _gamePath = new();
    private readonly List<UIElement> _currentOnlyControls = [];
    private readonly IReadOnlySet<string>? _customAiIds;
    public GenerationProfileOptions ResultOptions { get; }
    public int ResultPlayerCount { get; private set; }
    public string ResultGameInstallPath => _gamePath.Text.Trim();
    private static LocalizationManager L => LocalizationManager.Instance;

    public GenerationOptionsWindow(GenerationProfileOptions options, int players, string gamePath = "", GameInstallation? source = null, IReadOnlySet<string>? customAiIds = null)
    {
        _customAiIds = customAiIds;
        ResultOptions = options.Clone();
        ResultPlayerCount = Math.Clamp(players, 1, 8);
        Width = 800; Height = 700; MinWidth = 620; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        SetResourceReference(TitleProperty, "S.Patch.OptionsTitle");
        SetResourceReference(BackgroundProperty, "BrushBackground");
        SetResourceReference(ForegroundProperty, "BrushText");
        var root = new DockPanel { Margin = new Thickness(18) };
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _error.Foreground = System.Windows.Media.Brushes.OrangeRed;
        footer.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("S.Patch.Cancel"); cancel.IsCancel = true;
        var accept = Button("S.Patch.OK"); accept.IsDefault = true;
        accept.Click += (_, _) => Apply(); buttons.Children.Add(cancel); buttons.Children.Add(accept);
        footer.Children.Add(buttons); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var tabs = new TabControl(); root.Children.Add(tabs); Content = root;

        var map = Tab(tabs, "S.Patch.General");
        Help(map, "S.Patch.ProfileHelp");
        var profile = new ComboBox();
        profile.Items.Add(Item("S.Patch.Legacy", GenerationProfileOptions.LegacyProfile));
        profile.Items.Add(Item("S.Patch.Current", GenerationProfileOptions.CurrentProfile));
        profile.SelectedItem = profile.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag, options.ProfileId));
        Row(map, "S.Patch.Field.ProfileId", profile);
        _commit.Add(() => ResultOptions.ProfileId = (profile.SelectedItem as ComboBoxItem)?.Tag as string
            ?? throw ValueError("ProfileId", "profile"));
        AddField(map, ResultOptions, nameof(options.TemplateSeed));
        AddField(map, ResultOptions, nameof(options.GameSeed));
        foreach (var name in new[] { "Richness", "Challenge", "Symmetry", "Tempo", "NeutralTownCount", "MaximumTownsPerZone", "IncludeTemplateCard", "AuthorDescription" })
        {
            AddField(map, ResultOptions, name);
            if (name is "Richness" or "Challenge" or "Symmetry" or "Tempo") _currentOnlyControls.Add(map.Children[^1]);
        }
        Help(map, "S.Patch.AreaHelp"); AddField(map, ResultOptions, nameof(options.UseAreaBudgets)); _currentOnlyControls.Add(map.Children[^1]);
        void RefreshProfileControls()
        {
            bool current = (profile.SelectedItem as ComboBoxItem)?.Tag as string == GenerationProfileOptions.CurrentProfile;
            foreach (var control in _currentOnlyControls) control.IsEnabled = current;
        }
        profile.SelectionChanged += (_, _) => RefreshProfileControls(); RefreshProfileControls();

        var teams = Tab(tabs, "S.Patch.Teams"); Help(teams, "S.Patch.TeamHelp");
        foreach (var n in Enumerable.Range(1, 8)) _players.Items.Add(n);
        _players.SelectedItem = ResultPlayerCount; Row(teams, "S.Patch.PlayerCount", _players);
        foreach (var name in new[] { "Enabled", "Locked", "RandomizePlayerAssignment", "Layout", "SmallerTeamEconomyMultiplier" }) AddField(teams, ResultOptions.Alliances, name);
        foreach (var p in new[] { "auto", "ffa", "2v2", "3v3", "4v4", "2v2v2", "2v2v2v2", "1v7", "2v6", "3v5", "2v1", "2v2v1", "1v2v2v2", "custom" })
            _preset.Items.Add(new ComboBoxItem { Content = p, Tag = p });
        string selectedPreset = options.Alliances.Slots.Count > 0 ? "custom" : options.Alliances.Preset;
        _preset.SelectedItem = _preset.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag, selectedPreset));
        Row(teams, "S.Patch.Teams", _preset);
        IReadOnlyList<AllianceSlotSettings> displayedSlots = options.Alliances.Slots;
        if (displayedSlots.Count == 0 && options.Alliances.Enabled)
        {
            try { displayedSlots = Services.Generation.AlliancePlan.Resolve(ResultPlayerCount, options.Alliances, options.TemplateSeed).Slots; }
            catch (ArgumentException ex) { _error.Text = ex.Message; }
        }
        for (var i = 0; i < 8; i++)
        {
            var slot = i + 1;
            var entry = displayedSlots.FirstOrDefault(x => x.Player == slot);
            var box = new TextBox { Text = (entry?.Alliance ?? -1).ToString(CultureInfo.InvariantCulture), MinWidth = 60 };
            var locked = new CheckBox { Content = L.Get("S.Patch.SlotLocked"), IsChecked = options.Alliances.Slots.FirstOrDefault(x => x.Player == slot)?.Locked == true, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _teamSlots.Add(box);
            _teamLocks.Add(locked);
            var line = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            line.ColumnDefinitions.Add(new() { Width = new GridLength(270) }); line.ColumnDefinitions.Add(new());
            line.Children.Add(new TextBlock { Text = L.Get("S.Patch.TeamSlot", slot), VerticalAlignment = VerticalAlignment.Center });
            var fields = new StackPanel { Orientation = Orientation.Horizontal }; fields.Children.Add(box); fields.Children.Add(locked);
            Grid.SetColumn(fields, 1); line.Children.Add(fields); teams.Children.Add(line);
            void SelectCustom() { if (!_settingPresetSlots) _preset.SelectedItem = _preset.Items.Cast<ComboBoxItem>().First(x => Equals(x.Tag, "custom")); }
            box.TextChanged += (_, _) => SelectCustom(); locked.Click += (_, _) => SelectCustom();
            _players.SelectionChanged += (_, _) => line.Visibility = slot <= (int)(_players.SelectedItem ?? 1) ? Visibility.Visible : Visibility.Collapsed;
            line.Visibility = slot <= ResultPlayerCount ? Visibility.Visible : Visibility.Collapsed;
        }
        _preset.SelectionChanged += (_, _) => ApplyPresetSlots(changePlayerCount: true);
        ApplyPresetSlots(changePlayerCount: false);
        _commit.Add(() =>
        {
            ResultPlayerCount = (int)(_players.SelectedItem ?? throw ValueError("Enabled", "1–8"));
            ResultOptions.Alliances.Preset = (_preset.SelectedItem as ComboBoxItem)?.Tag as string ?? throw ValueError("Enabled", "preset");
            if (ResultOptions.Alliances.Preset != "custom") { ResultOptions.Alliances.Slots = []; return; }
            var slots = new List<AllianceSlotSettings>();
            for (var i = 0; i < ResultPlayerCount; i++)
            {
                if (!int.TryParse(_teamSlots[i].Text, out var alliance) || alliance is < -1 or > 8 || alliance == 0)
                    throw ValueError("Enabled", "−1, 1–8");
                slots.Add(new() { Player = i + 1, Alliance = alliance, Locked = _teamLocks[i].IsChecked == true });
            }
            ResultOptions.Alliances.Slots = slots;
        });

        var rules = Tab(tabs, "S.Patch.Rules"); Help(rules, "S.Patch.CalendarHelp");
        foreach (var name in new[] { "ClassicVictoryEnabled", "ResetCityHoldTimer", "TournamentAnnouncementLeadDays", "UseNativeFinalBattleSchedule", "FinalBattleDay" })
            AddField(rules, ResultOptions, name);
        var dates = new TextBox { Text = string.Join(", ", options.TournamentBattleDays) };
        Row(rules, "S.Patch.BattleDays", dates);
        _commit.Add(() =>
        {
            var days = new List<int>();
            foreach (var token in dates.Text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, out var day) || day < 2 || day > 256) throw ValueError("FinalBattleDay", "2–256");
                days.Add(day);
            }
            if (days.Zip(days.Skip(1)).Any(x => x.First >= x.Second)) throw ValueError("FinalBattleDay", "2…256 ↑");
            ResultOptions.TournamentBattleDays = days;
        });

        var environment = Tab(tabs, "S.Patch.Environment");
        foreach (var name in new[] { "EnableRivers", "RiverTributarySpacing", "EnableWeeklyUnitIncrement", "WeekOneOnlyModificatorGrowth", "InitialUnitIncrement", "DisableFactionLaws", "DisableMagicGuild", "DisableMagicCustomLearning", "PortalFromEnabled", "PortalToEnabled", "BorderGuardReaction", "CustomAI" })
            AddField(environment, ResultOptions, name);
        Help(environment, "S.Patch.PortalsHelp");
        Help(environment, "S.Patch.AiHelp");
        var install = Tab(tabs, "S.Patch.Source");
        _gamePath.Text = gamePath; Row(install, "S.Patch.GamePath", _gamePath);
        install.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 8),
            Text = source is null ? L.Get("S.Patch.GameMissing") : L.Get("S.Patch.GameSource", source.VersionMarker ?? L.Get("S.Patch.Unknown"), source.SteamBuildId ?? L.Get("S.Patch.Unknown"), source.TemplatesPath) });
        if (source?.Warnings.Count > 0) install.Children.Add(new TextBlock { Text = string.Join("\n", source.Warnings), TextWrapping = TextWrapping.Wrap });
        if (source is not null) install.Children.Add(new TextBlock { Text = source.Profile.Id + "\n" + source.VersionEvidence + "\nCore SHA256: " + source.SourceFingerprint + "\nSchema SHA256: " + source.SchemaFingerprint,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        Loaded += (_, _) => { Width = Math.Min(Width, SystemParameters.WorkArea.Width); Height = Math.Min(Height, SystemParameters.WorkArea.Height); };
    }

    private void ApplyPresetSlots(bool changePlayerCount)
    {
        if (_preset.SelectedItem is not ComboBoxItem { Tag: string selected } || selected is "auto" or "custom") return;
        _settingPresetSlots = true;
        try
        {
            foreach (var locked in _teamLocks) locked.IsChecked = false;
            if (selected == "ffa") { foreach (var box in _teamSlots) box.Text = "-1"; return; }
            var counts = selected.Split('v').Select(int.Parse).ToArray();
            if (changePlayerCount) _players.SelectedItem = counts.Sum();
            var player = 0;
            for (var team = 0; team < counts.Length; team++)
                for (var n = 0; n < counts[team]; n++) _teamSlots[player++].Text = (team + 1).ToString(CultureInfo.InvariantCulture);
        }
        finally { _settingPresetSlots = false; }
    }

    internal void CommitDraft()
    {
        foreach (var commit in _commit) commit();
        Services.Generation.AlliancePlan.Resolve(ResultPlayerCount, ResultOptions.Alliances, ResultOptions.TemplateSeed);
        if (ResultGameInstallPath.Length > 0 && !File.Exists(Path.Combine(ResultGameInstallPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip")))
            throw new ArgumentException(L.Get("S.Patch.GameMissing"));
    }

    internal void DebugSetAllianceSlot(int player, int alliance)
        => _teamSlots[player - 1].Text = alliance.ToString(CultureInfo.InvariantCulture);

    private void Apply()
    {
        try
        {
            CommitDraft();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        { _error.Text = ex.Message; }
    }

    private void AddField(Panel panel, object model, string propertyName)
    {
        var property = model.GetType().GetProperty(propertyName) ?? throw new ArgumentException(propertyName);
        var nullable = Nullable.GetUnderlyingType(property.PropertyType);
        var type = nullable ?? property.PropertyType;
        if (propertyName == "CustomAI" && _customAiIds is { Count: > 0 })
        {
            var box = new ComboBox { IsEditable = true, ItemsSource = new[] { "" }.Concat(_customAiIds.Order(StringComparer.Ordinal)).ToArray(),
                Text = property.GetValue(model) as string ?? "", MaxDropDownHeight = 280 };
            Row(panel, "S.Patch.Field.CustomAI", box);
            _commit.Add(() => property.SetValue(model, string.IsNullOrWhiteSpace(box.Text) ? null : box.Text)); return;
        }
        if (type == typeof(bool) && nullable is null)
        {
            var box = new CheckBox { IsChecked = (bool)property.GetValue(model)! };
            Row(panel, "S.Patch.Field." + propertyName, box); _commit.Add(() => property.SetValue(model, box.IsChecked == true)); return;
        }
        if (type == typeof(bool) && nullable is not null)
        {
            var box = new ComboBox(); box.Items.Add(Item("S.Patch.Default", null)); box.Items.Add(Item("S.Patch.Yes", true)); box.Items.Add(Item("S.Patch.No", false));
            box.SelectedItem = box.Items.Cast<ComboBoxItem>().First(x => Equals(x.Tag, property.GetValue(model)));
            Row(panel, "S.Patch.Field." + propertyName, box); _commit.Add(() => property.SetValue(model, ((ComboBoxItem)box.SelectedItem).Tag)); return;
        }
        if (type.IsEnum)
        {
            var box = new ComboBox(); foreach (var value in Enum.GetValues(type)) box.Items.Add(Item("S.Patch." + type.Name + "." + value, value));
            box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag, property.GetValue(model)));
            Row(panel, "S.Patch.Field." + propertyName, box);
            _commit.Add(() => property.SetValue(model, (box.SelectedItem as ComboBoxItem)?.Tag ?? throw ValueError(propertyName, "choice"))); return;
        }
        var text = new TextBox { Text = Convert.ToString(property.GetValue(model), CultureInfo.InvariantCulture) ?? "" };
        if (propertyName is "TemplateSeed" or "GameSeed" && property.GetValue(model) is int seed) text.Text = unchecked((uint)seed).ToString("X8");
        if (propertyName == "AuthorDescription") { text.AcceptsReturn = true; text.TextWrapping = TextWrapping.Wrap; text.MinHeight = 65; }
        Row(panel, "S.Patch.Field." + propertyName, text);
        _commit.Add(() =>
        {
            var value = text.Text.Trim();
            if (nullable is not null && value.Length == 0) { property.SetValue(model, null); return; }
            if (type == typeof(string)) { property.SetValue(model, value.Length == 0 && propertyName != "AuthorDescription" ? null : value); return; }
            if (propertyName is "TemplateSeed" or "GameSeed")
            {
                if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) property.SetValue(model, unchecked((int)hex));
                else if (int.TryParse(value, out var signed)) property.SetValue(model, signed);
                else throw new ArgumentException(L.Get("S.Patch.InvalidSeed"));
                return;
            }
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw ValueError(propertyName, "number");
            var (min, max) = propertyName switch { "SmallerTeamEconomyMultiplier" => (1.0, 8.0), "RiverTributarySpacing" => (1.0, 1000.0),
                "FinalBattleDay" => (2.0, 256.0), "MaximumTownsPerZone" => (0.0, 8.0), "NeutralTownCount" => (0.0, 96.0), _ => (0.0, 100.0) };
            if (number < min || number > max || type == typeof(int) && number != Math.Truncate(number)) throw ValueError(propertyName, $"{min}–{max}");
            property.SetValue(model, type == typeof(int) ? (object)(int)number : number);
        });
    }

    private static ArgumentException ValueError(string name, string range) => new(L.Get("S.Patch.InvalidValue", L.Get("S.Patch.Field." + name), range));
    private static ComboBoxItem Item(string key, object? tag) { var item = new ComboBoxItem { Tag = tag }; item.SetResourceReference(ContentControl.ContentProperty, key); return item; }
    private static Button Button(string key) { var button = new Button { Margin = new Thickness(8, 0, 0, 0), MinWidth = 110, Padding = new Thickness(14, 8, 14, 8) }; button.SetResourceReference(ContentControl.ContentProperty, key); return button; }
    private static StackPanel Tab(TabControl tabs, string key)
    {
        var tab = new TabItem(); tab.SetResourceReference(HeaderedContentControl.HeaderProperty, key);
        var panel = new StackPanel { Margin = new Thickness(10) }; tab.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        tabs.Items.Add(tab); return panel;
    }
    private static void Help(Panel panel, string key) { var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 14) }; text.SetResourceReference(TextBlock.TextProperty, key); panel.Children.Add(text); }
    private static void Row(Panel panel, string key, FrameworkElement control)
    {
        var line = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        line.ColumnDefinitions.Add(new() { Width = new GridLength(270) }); line.ColumnDefinitions.Add(new());
        var label = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        label.SetResourceReference(TextBlock.TextProperty, key); line.Children.Add(label); Grid.SetColumn(control, 1); line.Children.Add(control); panel.Children.Add(line);
    }
}
