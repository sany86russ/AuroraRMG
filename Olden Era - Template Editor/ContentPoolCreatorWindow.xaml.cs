using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.ContentPools;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

public partial class ContentPoolCreatorWindow : Window
{
    private readonly ObservableCollection<PoolGroupDraft> _groups = [];
    private readonly List<ContentListInfo> _allLists = [];
    private readonly RmgTemplate? _template;
    private readonly GamePool? _original;
    private GameCatalog? _catalog;
    public string? CreatedPoolName { get; private set; }
    public List<string>? CreatedPoolLists { get; private set; }
    public GamePool? CreatedPool { get; private set; }
    public bool EmbedListDefinitions => EmbedListsBox.IsChecked == true;
    public bool IsEditing => _original is not null;
    public event Action<string, List<string>>? PoolCreated;
    private static string L(string key, params object[] args) => LocalizationManager.T(key, args);
    private PoolGroupDraft? SelectedGroup => GroupsGrid.SelectedItem as PoolGroupDraft;

    public ContentPoolCreatorWindow(RmgTemplate? template = null, GamePool? initialPool = null)
    {
        _template = template; _original = initialPool;
        InitializeComponent();
        PoolNameBox.Text = initialPool?.Name ?? "";
        foreach (var draft in ContentPoolComposer.Drafts(initialPool)) _groups.Add(draft);
        if (_groups.Count == 0) _groups.Add(new() { Label = "1" });
        GroupsGrid.ItemsSource = _groups; GroupsGrid.SelectedIndex = 0;
        LocalizationManager.Observe(this, RefreshLanguage); RefreshLanguage();
        Loaded += async (_, _) => await LoadContentListsAsync();
    }

    private async Task LoadContentListsAsync()
    {
        SourceStatus.Text = L("S.Patch.CatalogLoading");
        try
        {
            var installed = await Task.Run(() => GamePoolDataLoader.GetAllContentLists().ToList());
            _catalog = await GameCatalogService.Instance.GetCatalogAsync();
            var definitions = installed.ToDictionary(l => l.Name, StringComparer.Ordinal);
            foreach (var definition in _template?.ContentLists ?? [])
            {
                var list = JsonSerializer.Deserialize<ContentListEntry>(JsonSerializer.Serialize(definition, JsonExport.Options), JsonExport.Options);
                if (list is not null && !string.IsNullOrWhiteSpace(list.Name)) definitions[list.Name] = list;
            }
            _allLists.Clear();
            _allLists.AddRange(definitions.Values.OrderBy(l => l.Name, StringComparer.Ordinal).Select(l => new ContentListInfo { Name = l.Name, ItemCount = l.Content?.Count ?? 0 }));
            FilterLists();
            SourceStatus.Text = GamePoolDataLoader.Status;
            if (GamePoolDataLoader.Warnings.Count > 0) SourceStatus.Text += "\n" + string.Join("\n", GamePoolDataLoader.Warnings.Take(3));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        { SourceStatus.Text = L("S.Patch.OperationFailed", ex.Message); }
    }
    private void RefreshLanguage()
    {
        GroupNumberColumn.Header = L("S.Patch.PoolGroup"); GroupWeightColumn.Header = L("S.Patch.CatalogWeight");
        ItemWeightColumn.Header = L("S.Patch.CatalogWeight"); VariantColumn.Header = L("S.Patch.CatalogVariant"); BiomeColumn.Header = L("S.PV.ColBiome");
    }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterLists();
    private void FilterLists()
    {
        if (AvailableLists is null) return;
        string query = SearchBox?.Text ?? "";
        AvailableLists.ItemsSource = _allLists.Where(l => l.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (SelectedLists is not null) SelectedLists.ItemsSource = SelectedGroup?.IncludeLists; if (ContentGrid is not null) ContentGrid.ItemsSource = SelectedGroup?.Content; }
    private void AddGroup_Click(object sender, RoutedEventArgs e)
    { var group = new PoolGroupDraft { Label = (_groups.Count + 1).ToString() }; _groups.Add(group); GroupsGrid.SelectedItem = group; }
    private void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not { } group) return; _groups.Remove(group);
        for (int i = 0; i < _groups.Count; i++) _groups[i].Label = (i + 1).ToString();
        GroupsGrid.Items.Refresh(); if (_groups.Count > 0) GroupsGrid.SelectedIndex = 0;
    }
    private void AddList_Click(object sender, RoutedEventArgs e)
    { if (SelectedGroup is { } group && AvailableLists.SelectedItem is ContentListInfo list && !group.IncludeLists.Contains(list.Name)) group.IncludeLists.Add(list.Name); }
    private void RemoveList_Click(object sender, RoutedEventArgs e)
    { if (SelectedGroup is { } group && SelectedLists.SelectedItem is string sid) group.IncludeLists.Remove(sid); }
    private async void AddCatalogObject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup is not { } group) return;
        _catalog ??= await GameCatalogService.Instance.GetCatalogAsync();
        var picker = new GameContentCatalogWindow(_catalog, chooseVariant: (entry, variant) =>
        {
            if (entry.Kind != GameContentKind.Object) { MessageBox.Show(this, L("S.Patch.PoolObjectOnly"), Title); return; }
            group.Content.Add(new() { Sid = entry.Sid, VariantText = variant?.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", WeightText = "1" });
        }, allowedKind: GameContentKind.Object) { Owner = this };
        picker.ShowDialog();
    }
    private void AddManualObject_Click(object sender, RoutedEventArgs e)
    { if (SelectedGroup is { } group) { var row = new PoolContentDraft(); group.Content.Add(row); ContentGrid.SelectedItem = row; ContentGrid.ScrollIntoView(row); } }
    private void RemoveObject_Click(object sender, RoutedEventArgs e)
    { if (SelectedGroup is { } group && ContentGrid.SelectedItem is PoolContentDraft row) group.Content.Remove(row); }
    private void CreatePool_Click(object sender, RoutedEventArgs e)
    {
        GroupsGrid.CommitEdit(DataGridEditingUnit.Cell, true); GroupsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        ContentGrid.CommitEdit(DataGridEditingUnit.Cell, true); ContentGrid.CommitEdit(DataGridEditingUnit.Row, true);
        try
        {
            var pool = ContentPoolComposer.Build(PoolNameBox.Text, _groups, _original, _catalog, LocalizationManager.Instance.Culture);
            if (GamePoolDataLoader.GetAllPools().Any(p => p.Name == pool.Name) && _original?.Name != pool.Name)
                throw new ContentPoolValidationException("DuplicateName");
            CreatedPool = pool; CreatedPoolName = PoolNameBox.Text.Trim(); CreatedPoolLists = pool.Groups.SelectMany(g => g.IncludeLists).Distinct(StringComparer.Ordinal).ToList();
            PoolCreated?.Invoke(CreatedPoolName, CreatedPoolLists); DialogResult = true;
        }
        catch (ContentPoolValidationException ex)
        { MessageBox.Show(this, L("S.Patch.PoolError." + ex.Code, ex.Arguments), L("S.PV.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

public class ContentListInfo
{
    public string Name { get; set; } = "";
    public int ItemCount { get; set; }
    public string Description => LocalizationManager.T("S.PC.ListItems", ItemCount);
}
