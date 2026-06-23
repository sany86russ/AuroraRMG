using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolViewerWindow : Window
    {
        private List<GamePool> _allPools = new();
        private Window? _debugWindow;
        private string _placeholder = "";

        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public ContentPoolViewerWindow(List<string>? allPoolSids = null, List<string>? allSelectedPids = null)
        {
            InitializeComponent();

            // Localised DataGrid headers (columns are outside the visual tree → set in code).
            ColList.Header = L("S.PV.ColList");
            ColSid.Header = L("S.PV.ColSid");
            ColWeight.Header = L("S.PV.ColWeight");
            ColBiome.Header = L("S.PV.ColBiome");

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

            try
            {
                _allPools = GamePoolDataLoader.GetAllPools();
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
                PoolDataGrid.ItemsSource = GamePoolDataLoader.GetPoolItems(pool);
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
            sb.AppendLine();
            sb.AppendLine(L("S.PV.DebugPoolsInMemory", _allPools.Count));
            foreach (var pool in _allPools.Take(20))
                sb.AppendLine($"- {pool.Name} ({pool.Groups?.Count ?? 0})");
            return sb.ToString();
        }
    }
}
