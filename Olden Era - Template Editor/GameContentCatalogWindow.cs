using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

/// <summary>Search the installed data without presenting parser presence as in-game placement proof.</summary>
public sealed class GameContentCatalogWindow : Window
{
    private GameCatalog? _catalog;
    private readonly Action<GameContentEntry>? _choose;
    private readonly Action<GameContentEntry, GameRewardVariant?>? _chooseVariant;
    private readonly GameContentKind? _allowedKind;
    private readonly TextBox _search = new() { MinHeight = 30 };
    private readonly ComboBox _kind = new() { MinWidth = 150, MinHeight = 30 };
    private readonly CheckBox _rmgOnly = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _details = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
    private readonly DataGrid _entries = GridView();
    private readonly DataGrid _rewards = GridView();
    private readonly Button _select = new() { MinWidth = 100, MinHeight = 30, IsEnabled = false };
    private readonly Button _refresh = new() { MinWidth = 100, MinHeight = 30 };
    private bool _loading;
    public GameContentEntry? SelectedEntry => _entries.SelectedItem as GameContentEntry;
    public GameRewardVariant? SelectedVariant => _rewards.SelectedItem is RewardRow row ? row.Variant : null;

    public GameContentCatalogWindow(GameCatalog? catalog = null, Action<GameContentEntry>? choose = null,
        Action<GameContentEntry, GameRewardVariant?>? chooseVariant = null, GameContentKind? allowedKind = null)
    {
        _catalog = catalog; _choose = choose; _chooseVariant = chooseVariant; _allowedKind = allowedKind;
        if (allowedKind == GameContentKind.Object) _rmgOnly.IsChecked = true;
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 60); Height = Math.Min(760, SystemParameters.WorkArea.Height - 60);
        MinWidth = 640; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetResourceReference(BackgroundProperty, "BrushBackground"); SetResourceReference(ForegroundProperty, "BrushText");
        SetResourceReference(TitleProperty, "S.Patch.CatalogTitle");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 32, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var outer = new Border { BorderThickness = new Thickness(1) }; outer.SetResourceReference(Border.BorderBrushProperty, "BrushBorder");
        var root = new DockPanel(); outer.Child = root; Content = outer;
        var header = new DockPanel { Height = 32 }; header.SetResourceReference(Panel.BackgroundProperty, "BrushPanel"); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var close = new Button { Content = "✕", Width = 40 }; close.SetResourceReference(StyleProperty, "TitleBarCloseButton");
        WindowChrome.SetIsHitTestVisibleInChrome(close, true); close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        var title = Label("S.Patch.CatalogTitle"); title.Margin = new Thickness(12, 0, 0, 0); title.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(title);
        var content = new DockPanel { Margin = new Thickness(14) }; root.Children.Add(content);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); content.Children.Add(footer);
        _select.SetResourceReference(ContentControl.ContentProperty, "S.Patch.CatalogChoose"); _select.Margin = new Thickness(0, 0, 8, 0);
        _select.Visibility = choose is not null || chooseVariant is not null ? Visibility.Visible : Visibility.Collapsed;
        _select.Click += (_, _) => Choose(); footer.Children.Add(_select);
        var closeBottom = new Button { MinWidth = 100, MinHeight = 30, IsCancel = true }; closeBottom.SetResourceReference(ContentControl.ContentProperty, "S.Patch.CatalogClose"); closeBottom.Click += (_, _) => Close(); footer.Children.Add(closeBottom);
        var controls = new StackPanel(); DockPanel.SetDock(controls, Dock.Top); content.Children.Add(controls);
        var searchLabel = Label("S.Patch.CatalogSearch"); searchLabel.Margin = new Thickness(0, 0, 0, 5); controls.Children.Add(searchLabel); controls.Children.Add(_search);
        var filters = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; _kind.Margin = new Thickness(0, 0, 12, 0); filters.Children.Add(_kind);
        _rmgOnly.SetResourceReference(ContentControl.ContentProperty, "S.Patch.CatalogRmgOnly"); _rmgOnly.Margin = new Thickness(0, 0, 12, 0); filters.Children.Add(_rmgOnly);
        _refresh.SetResourceReference(ContentControl.ContentProperty, "S.Patch.CatalogRefresh"); filters.Children.Add(_refresh); controls.Children.Add(filters); controls.Children.Add(_status);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        content.Children.Add(grid); grid.Children.Add(_entries);
        var side = new DockPanel(); Grid.SetColumn(side, 2); grid.Children.Add(side);
        var detailTop = new StackPanel(); DockPanel.SetDock(detailTop, Dock.Top); side.Children.Add(detailTop); detailTop.Children.Add(_details); detailTop.Children.Add(Label("S.Patch.CatalogVariants")); side.Children.Add(_rewards);
        _entries.Columns.Add(Column("SID", "Sid", 200));
        _entries.Columns.Add(Column("S.Patch.CatalogNameRu", "NameRu", 170)); _entries.Columns.Add(Column("S.Patch.CatalogNameEn", "NameEn", 150));
        _entries.Columns.Add(Column("S.Patch.CatalogCategory", "Category", 100)); _entries.Columns.Add(Column("S.Patch.CatalogLevel", "Level", 60));
        _rewards.Columns.Add(Column("S.Patch.CatalogVariant", "Index", 65)); _rewards.Columns.Add(Column("S.Patch.CatalogValue", "Value", 85));
        _rewards.Columns.Add(Column("S.Patch.CatalogWeight", "Weight", 70)); _rewards.Columns.Add(Column("S.Patch.CatalogReward", "Type", 140));
        _rewards.Columns.Add(Column("S.Patch.CatalogLevel", "SpellRanks", 65));
        _rewards.Columns.Add(Column("S.Patch.CatalogParameters", "Parameters", 240));
        _search.TextChanged += (_, _) => Filter(); _kind.SelectionChanged += (_, _) => Filter(); _rmgOnly.Checked += (_, _) => Filter(); _rmgOnly.Unchecked += (_, _) => Filter();
        _entries.SelectionChanged += (_, _) => ShowDetails(); _entries.MouseDoubleClick += (_, _) => { if (_choose is not null || _chooseVariant is not null) Choose(); };
        _refresh.Click += async (_, _) => await LoadAsync(force: true);
        LocalizationManager.Observe(this, RefreshLanguage); RefreshLanguage();
        Loaded += async (_, _) => { if (_catalog is null) await LoadAsync(); else Filter(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    private async Task LoadAsync(bool force = false)
    {
        if (_loading) return; _loading = true; _refresh.IsEnabled = false; _status.Text = LocalizationManager.T("S.Patch.CatalogLoading");
        try { _catalog = await GameCatalogService.Instance.GetCatalogAsync(forceRefresh: force); Filter(); }
        finally { _loading = false; _refresh.IsEnabled = true; }
    }
    private void RefreshLanguage()
    {
        int selection = Math.Max(0, _kind.SelectedIndex);
        if (_allowedKind is { } fixedKind)
        {
            _kind.ItemsSource = new[] { LocalizationManager.T("S.Patch." + (fixedKind == GameContentKind.Object ? "CatalogObjects" : fixedKind == GameContentKind.Spell ? "CatalogSpells" : "CatalogArtifacts")) };
            _kind.SelectedIndex = 0; _kind.IsEnabled = false;
        }
        else
        {
            _kind.ItemsSource = new[] { "CatalogAll", "CatalogObjects", "CatalogSpells", "CatalogArtifacts" }.Select(k => LocalizationManager.T("S.Patch." + k)).ToArray();
            _kind.SelectedIndex = selection;
        }
        Filter();
    }
    private void Filter()
    {
        if (_catalog is null) return;
        GameContentKind? kind = _allowedKind ?? (_kind.SelectedIndex switch { 1 => GameContentKind.Object, 2 => GameContentKind.Spell, 3 => GameContentKind.Artifact, _ => (GameContentKind?)null });
        var found = _catalog.Search(_search.Text, kind, _rmgOnly.IsChecked == true ? true : null).ToArray();
        _entries.ItemsSource = found;
        _status.Text = _catalog.Source is null ? LocalizationManager.T("S.Patch.CatalogOffline") :
            LocalizationManager.T("S.Patch.CatalogCounts", found.Length, $"{_catalog.Source.VersionMarker ?? "?"} / {_catalog.Source.SteamBuildId ?? "?"}");
        ShowDetails();
    }
    private void ShowDetails()
    {
        var entry = SelectedEntry; _select.IsEnabled = entry is not null;
        if (entry is null) { _details.Text = ""; _rewards.ItemsSource = null; return; }
        _details.Text = entry.NameRu + "\n" + entry.NameEn + "\nSID: " + entry.Sid + "\n" +
            LocalizationManager.T(entry.ReferencedByRmgContent ? "S.Patch.CatalogStatusRmg" : "S.Patch.CatalogStatusData") + "\n" +
            LocalizationManager.T("S.Patch.CatalogSource") + ": " + entry.SourceEntry;
        bool russian = LocalizationManager.Instance.CurrentLanguage == AppLanguage.Ru;
        _rewards.ItemsSource = entry.RewardVariants.Select(v => new RewardRow(v, string.Join(", ", v.RewardTypes), string.Join(", ", v.RewardParameters) +
            (v.Spells.Count == 0 ? "" : " · " + string.Join(", ", v.Spells.Select(s => russian ? s.NameRu : s.NameEn))))).ToArray();
    }

    internal void DebugSelectRewardObject(string sid)
    {
        _kind.SelectedIndex = 1; _rmgOnly.IsChecked = true; _search.Text = sid; Filter();
        _entries.SelectedItem = _entries.Items.OfType<GameContentEntry>().Single(entry => entry.Sid == sid);
        if (SelectedEntry?.RewardVariants.Count is not > 0) throw new InvalidOperationException("The selected object has no configured reward variants.");
        _rewards.SelectedIndex = 0;
    }
    private void Choose()
    {
        if (SelectedEntry is not { } entry) return;
        _choose?.Invoke(entry); _chooseVariant?.Invoke(entry, SelectedVariant); Close();
    }
    private static TextBlock Label(string key)
    { var label = new TextBlock { TextWrapping = TextWrapping.Wrap }; label.SetResourceReference(TextBlock.TextProperty, key); return label; }
    private static DataGridTextColumn Column(string key, string path, double width)
    {
        return new DataGridTextColumn { Header = key == "SID" ? "SID" : Label(key), Binding = new Binding(path), Width = width };
    }
    private static DataGrid GridView()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, RowHeight = 30, HeadersVisibility = DataGridHeadersVisibility.Column,
            EnableRowVirtualization = true, EnableColumnVirtualization = true, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        grid.SetResourceReference(Control.BackgroundProperty, "BrushPanel"); grid.SetResourceReference(Control.ForegroundProperty, "BrushText");
        grid.SetResourceReference(Control.BorderBrushProperty, "BrushBorder"); grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "BrushBorder");
        var header = new Style(typeof(DataGridColumnHeader)); header.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("BrushPanelAlt")));
        header.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("BrushText"))); header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6)));
        grid.ColumnHeaderStyle = header;
        var row = new Style(typeof(DataGridRow)); row.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("BrushPanel"))); row.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("BrushText")));
        var selected = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true }; selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("BrushHover"))); row.Triggers.Add(selected); grid.RowStyle = row;
        return grid;
    }
    private sealed record RewardRow(GameRewardVariant Variant, string Type, string Parameters)
    {
        public int Index => Variant.Index; public double? Value => Variant.Value; public double? Weight => Variant.RollChance;
        public string SpellRanks => string.Join(", ", Variant.Spells.Where(s => s.Rank.HasValue).Select(s => s.Rank).Distinct());
    }
}
