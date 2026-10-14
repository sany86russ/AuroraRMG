using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;
using Olden_Era___Template_Editor.Services.ContentPools;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolViewerWindow : Window
    {
        private List<GamePool> _allPools = new();
        private Window? _debugWindow;
        private string _placeholder = "";
        private readonly RmgTemplate? _template;
        private readonly List<GamePool> _templatePools;
        private PoolContentInspection? _inspection;

        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public ContentPoolViewerWindow(List<string>? allPoolSids = null, List<string>? allSelectedPids = null, IEnumerable<GamePool>? templatePools = null, RmgTemplate? template = null)
        {
            _template = template; _templatePools = (templatePools ?? []).ToList();
            InitializeComponent();
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;

            // Localised DataGrid headers (columns are outside the visual tree → set in code).
            ColList.Header = L("S.PV.ColList");
            ColSid.Header = L("S.PV.ColSid");
            ColWeight.Header = L("S.PV.ColWeight");
            ColBiome.Header = L("S.PV.ColBiome");
            RefreshExtraHeaders();

            // Category filter — items are localised display strings; the filter switches on
            // SelectedIndex so translation never breaks the logic.
            CategoryCombo.ItemsSource = new List<string>
            {
                L("S.PV.Cat.All"),
                L("S.PV.Cat.Guarded"),
                L("S.PV.Cat.Unguarded"),
                L("S.PV.Cat.Resources"),
                L("S.PV.Cat.Random"),
                L("S.PV.Cat.Default"),
                L("S.PV.Cat.Template"),
                L("S.PV.Cat.Custom"),
            };
            CategoryCombo.SelectedIndex = 0;

            // Search placeholder.
            _placeholder = L("S.PV.SearchPlaceholder");
            SearchBox.GotFocus += (_, _) =>
            {
                if (SearchBox.Text == _placeholder)
                {
                    SearchBox.Text = "";
                    SearchBox.Foreground = (Brush)FindResource("BrushText");
                }
            };
            SearchBox.LostFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    SearchBox.Text = _placeholder;
                    SearchBox.Foreground = (Brush)FindResource("BrushTextDim");
                }
            };
            SearchBox.Text = _placeholder;
            SearchBox.Foreground = (Brush)FindResource("BrushTextDim");

            Loaded += async (_, _) => await LoadPoolsAsync();
        }

        private async System.Threading.Tasks.Task LoadPoolsAsync()
        {
            try
            {
                var loaded = await System.Threading.Tasks.Task.Run(() => GamePoolDataLoader.GetAllPools().ToList());
                _allPools = loaded.Concat(_templatePools)
                    .GroupBy(pool => pool.Name, StringComparer.Ordinal).Select(group => group.Last()).ToList();
                if (_allPools.Count == 0)
                {
                    ResultCount.Text = GamePoolDataLoader.Status;
                    return;
                }
                FilterPools();
            }
            catch (Exception ex)
            {
                ResultCount.Text = L("S.PV.StatusError", ex.Message);
            }
        }

        private void FilterPools()
        {
            var query = _allPools.AsEnumerable();

            var searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != _placeholder)
                query = query.Where(p => p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            int cat = CategoryCombo.SelectedIndex;
            if (cat > 0)
                query = query.Where(p => MatchesCategory(p.Name, cat));

            var filtered = query.ToList();
            PoolList.ItemsSource = filtered;

            ResultCount.Text = filtered.Count == 0
                ? L("S.PV.NothingFound")
                : L("S.PV.Found", filtered.Count, _allPools.Count);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            ColList.Header = L("S.PV.ColList"); ColSid.Header = L("S.PV.ColSid");
            ColWeight.Header = L("S.PV.ColWeight"); ColBiome.Header = L("S.PV.ColBiome");
            bool placeholderShown = SearchBox.Text == _placeholder;
            _placeholder = L("S.PV.SearchPlaceholder");
            if (placeholderShown) SearchBox.Text = _placeholder;
            int category = CategoryCombo.SelectedIndex;
            CategoryCombo.ItemsSource = new[] { "All", "Guarded", "Unguarded", "Resources", "Random", "Default", "Template", "Custom" }
                .Select(key => L("S.PV.Cat." + key)).ToList();
            CategoryCombo.SelectedIndex = category;
            FilterPools();
            RefreshExtraHeaders();
            if (PoolList.SelectedItem is GamePool pool) ShowPool(pool);
            if (_debugWindow?.Content is ScrollViewer { Content: TextBlock text }) { _debugWindow.Title = L("S.PV.Debug"); text.Text = GetDebugInfo(); }
        }

        private static bool MatchesCategory(string poolName, int categoryIndex)
        {
            var name = poolName.ToLowerInvariant();
            return categoryIndex switch
            {
                1 => name.Contains("guarded") && !name.Contains("unguarded"),
                2 => name.Contains("unguarded"),
                3 => name.Contains("resources"),
                4 => name.Contains("random"),
                5 => name.Contains("default"),
                6 => name.Contains("template_pool_") && !name.Contains("random"),
                7 => name.StartsWith("custom_"),
                _ => true,
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterPools();

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => FilterPools();

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            FilterPools();
        }

        private void ClearCategoryBtn_Click(object sender, RoutedEventArgs e)
        {
            CategoryCombo.SelectedIndex = 0;
            FilterPools();
        }

        private void PoolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PoolList.SelectedItem is GamePool pool)
                ShowPool(pool);
            else { PoolDataGrid.ItemsSource = null; GroupSummary.Text = ""; _inspection = null; }
        }

        private void RefreshExtraHeaders()
        {
            ColGroup.Header = L("S.PV.ColGroup"); ColGroupWeight.Header = L("S.PV.ColGroupWeight");
            ColVariant.Header = L("S.PV.ColVariant"); ColMissing.Header = L("S.PV.ColMissing");
            ColGroupShare.Header = L("S.PV.ColGroupShare"); ColListShare.Header = L("S.PV.ColListShare");
        }

        private void ShowPool(GamePool pool)
        {
            _inspection = GamePoolDataLoader.InspectPool(pool, _template);
            PoolDataGrid.ItemsSource = GamePoolDataLoader.GetPoolItems(pool, _template);
            GroupSummary.Text = string.Join("\n", _inspection.Groups.Select(group => L("S.PV.GroupSummary", group.Index, group.Weight,
                group.DirectItemCount, group.Dependencies.Count == 0 ? L("S.PV.DependenciesEmpty") : string.Join(", ", group.Dependencies.Select(dependency =>
                    dependency.Name + " [" + L("S.PV.Origin." + dependency.Origin) + "]")))))
                + (_inspection.Warnings.Count == 0 ? "" : "\n⚠ " + string.Join("\n⚠ ", _inspection.Warnings));
        }

        private void DebugBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_debugWindow != null)
            {
                _debugWindow.Activate();
                return;
            }

            _debugWindow = new Window
            {
                Title = L("S.PV.Debug"),
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)FindResource("BrushPanel"),
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = new TextBlock
            {
                Text = GetDebugInfo(),
                Foreground = (Brush)FindResource("BrushText"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8),
                FontFamily = new FontFamily("Consolas"),
            };
            _debugWindow.Content = scroll;
            _debugWindow.Closed += (_, _) => _debugWindow = null;
            _debugWindow.Show();
        }

        private string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(GamePoolDataLoader.Status);
            foreach (var warning in GamePoolDataLoader.Warnings) sb.AppendLine("⚠ " + warning);
            if (GamePoolDataLoader.Source is { } source)
                sb.AppendLine($"Build: {source.SteamBuildId ?? "?"}; version marker: {source.VersionMarker ?? "?"}\nCore SHA256: {source.SourceFingerprint}\nSchema SHA256: {source.SchemaFingerprint}");
            sb.AppendLine();
            sb.AppendLine(L("S.PV.DebugPoolsInMemory", _allPools.Count));
            foreach (var pool in _allPools.Take(20))
                sb.AppendLine($"- {pool.Name} ({pool.Groups?.Count ?? 0})");
            return sb.ToString();
        }
    }
}
