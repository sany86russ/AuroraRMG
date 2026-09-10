using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Olden_Era___Template_Editor
{
    public partial class ItemPickerWindow : Window
    {
        /// <summary>First selected ID (kept for callers that only need one).</summary>
        public string?      SelectedId  => SelectedIds.Count > 0 ? SelectedIds[0] : null;
        public List<string> SelectedIds { get; private set; } = [];

        private readonly List<BanEntry>         _allEntries;
        private readonly HashSet<string>         _alreadyBanned;
        private readonly HashSet<string>         _collapsedCategories = [];
        private readonly HashSet<string>   _checkedLeaves       = [];

        private static readonly SolidColorBrush CheckedBrush  = new(Color.FromRgb(0x5A, 0x4A, 0x28));
        private static readonly SolidColorBrush CheckMarkBrush = new(Color.FromRgb(0xC9, 0xA8, 0x4C));

        public ItemPickerWindow(IEnumerable<BanEntry> entries, IEnumerable<string> alreadyBanned, string windowTitle)
        {
            InitializeComponent();
            Title = LocalizationManager.T(windowTitle);
            TxtWindowTitle.Text = Title;
            _allEntries    = [.. entries];
            _alreadyBanned = [.. alreadyBanned];
            RefreshTree(string.Empty);
            bool closed = false;
            Closed += (_, _) => closed = true;
            LocalizationManager.Observe(this, async () =>
            {
                Title = LocalizationManager.T(windowTitle); TxtWindowTitle.Text = Title; RefreshTree(TxtSearch.Text);
                if (Services.GameData.AppSettings.Current.UseGameAssets && _allEntries.Any(e => e.Id.Contains("_hero_")))
                {
                    await Services.GameData.GameCatalogService.Instance.GetCatalogAsync();
                    if (!closed) RefreshTree(TxtSearch.Text);
                }
            });
            TxtSearch.Focus();
        }

        // ── Tree building ─────────────────────────────────────────────────────────

        private void RefreshTree(string filter)
        {
            foreach (TreeViewItem ci in TvItems.Items)
                if (ci.Tag is string cat)
                {
                    if (ci.IsExpanded) _collapsedCategories.Remove(cat);
                    else               _collapsedCategories.Add(cat);
                }

            TvItems.Items.Clear();

            var groups = _allEntries
                .Where(e => !_alreadyBanned.Contains(e.Id))
                .Where(e => string.IsNullOrEmpty(filter)
                         || e.DisplayName.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || e.CategoryLabel.Contains(filter,    System.StringComparison.OrdinalIgnoreCase)
                         || e.Id.Contains(filter,          System.StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var catNode = new TreeViewItem
                {
                    Tag        = group.Key,
                    IsExpanded = !_collapsedCategories.Contains(group.Key),
                    Header     = BuildCategoryHeader(group.Key, group.Count()),
                };

                foreach (var entry in group.OrderBy(e => e.DisplayName))
                    catNode.Items.Add(BuildLeafItem(entry));

                TvItems.Items.Add(catNode);
            }

            UpdateAddButton();
        }

        private static FrameworkElement BuildCategoryHeader(string category, int count)
        {
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text       = GameLabels.Category(category),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xA8, 0x4C)),
                Margin     = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text      = $"({count})",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                FontSize  = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return sp;
        }

        private TreeViewItem BuildLeafItem(BanEntry entry)
        {
            // Check mark placeholder — toggled in code
            var chk = new TextBlock
            {
                Name              = "ChkMark",
                Text              = "",     // filled with ✓ when checked
                Width             = 16,
                Foreground        = CheckMarkBrush,
                FontWeight        = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 4, 0),
            };

            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(chk);
            sp.Children.Add(new Ellipse
            {
                Width  = 9, Height = 9,
                Fill   = entry.CategoryBrush,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text       = entry.DisplayName,
                Width      = 220,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xD5, 0xA3)),
                Margin     = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text       = entry.Id,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var leaf = new TreeViewItem { Header = sp, Tag = entry };
            if (_checkedLeaves.Contains(entry.Id))
            { leaf.Background = CheckedBrush; if (GetCheckMark(leaf) is { } mark) mark.Text = "✓"; }
            return leaf;
        }

        // ── Check-toggle helpers ─────────────────────────────────────────────────

        private static TextBlock? GetCheckMark(TreeViewItem item)
            => item.Header is StackPanel sp && sp.Children.Count > 0
                ? sp.Children[0] as TextBlock
                : null;

        private void ToggleLeaf(TreeViewItem item)
        {
            if (_checkedLeaves.Contains(((BanEntry)item.Tag).Id))
            {
                _checkedLeaves.Remove(((BanEntry)item.Tag).Id);
                item.Background = DependencyProperty.UnsetValue as Brush; // reset
                item.ClearValue(TreeViewItem.BackgroundProperty);
                if (GetCheckMark(item) is { } chk) chk.Text = "";
            }
            else
            {
                _checkedLeaves.Add(((BanEntry)item.Tag).Id);
                item.Background = CheckedBrush;
                if (GetCheckMark(item) is { } chk) chk.Text = "✓";
            }
            UpdateAddButton();
        }

        private void UpdateAddButton()
        {
            int n = _checkedLeaves.Count;
            BtnAdd.Content   = n > 1 ? Services.Localization.LocalizationManager.T("S.P.AddSelectedN", n) : Services.Localization.LocalizationManager.T("S.P.AddSelected");
            BtnAdd.IsEnabled = n > 0;
        }

        private static TreeViewItem? FindLeafFromSource(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is TreeViewItem tvi && tvi.Tag is BanEntry)
                    return tvi;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshTree(TxtSearch.Text);

        private void TvItems_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var leaf = FindLeafFromSource(e.OriginalSource);
            if (leaf == null) return; // category header click — let it expand/collapse normally
            ToggleLeaf(leaf);
            e.Handled = true; // suppress built-in selection highlight
        }

        private void TvItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var leaf = FindLeafFromSource(e.OriginalSource);
            if (leaf == null) return;
            // Ensure it's checked, then commit
            if (!_checkedLeaves.Contains(((BanEntry)leaf.Tag).Id)) ToggleLeaf(leaf);
            CommitAll();
        }

        private void CommitAll()
        {
            SelectedIds = _checkedLeaves
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToList();
            if (SelectedIds.Count > 0)
                DialogResult = true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
            => CommitAll();

        private void BtnAddCustom_Click(object sender, RoutedEventArgs e)
        {
            var id = TxtCustomId.Text.Trim();
            if (!string.IsNullOrEmpty(id))
            {
                SelectedIds  = [id];
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
