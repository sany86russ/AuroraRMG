using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    public partial class ContentPoolCreatorWindow : Window
    {
        private readonly List<ContentListInfo> _allLists = new();
        private readonly List<ContentListInfo> _selectedLists = new();

        public string? CreatedPoolName { get; private set; }
        public List<string>? CreatedPoolLists { get; private set; }

        public event Action<string, List<string>>? PoolCreated;

        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public ContentPoolCreatorWindow()
        {
            InitializeComponent();
            LoadContentLists();
            LocalizationManager.Observe(this, () => { AvailableLists.Items.Refresh(); SelectedLists.Items.Refresh(); });
        }

        private void LoadContentLists()
        {
            // Real content lists, read at runtime from the installed game's Core.zip.
            _allLists.Clear();
            foreach (var list in GamePoolDataLoader.GetAllContentLists().OrderBy(l => l.Name))
            {
                _allLists.Add(new ContentListInfo
                {
                    Name = list.Name,
                    ItemCount = list.Content?.Count ?? 0,
                });
            }
            AvailableLists.ItemsSource = _allLists;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = SearchBox.Text;
            AvailableLists.ItemsSource = string.IsNullOrWhiteSpace(q)
                ? _allLists
                : _allLists.Where(l => l.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void AddList_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string listName })
            {
                var list = _allLists.FirstOrDefault(l => l.Name == listName);
                if (list != null && !_selectedLists.Any(s => s.Name == listName))
                {
                    _selectedLists.Add(list);
                    RefreshSelected();
                }
            }
        }

        private void RemoveList_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string listName })
            {
                var list = _selectedLists.FirstOrDefault(l => l.Name == listName);
                if (list != null)
                {
                    _selectedLists.Remove(list);
                    RefreshSelected();
                }
            }
        }

        private void AddAll_Click(object sender, RoutedEventArgs e)
        {
            var selected = _selectedLists.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var list in AvailableLists.ItemsSource.Cast<ContentListInfo>())
                if (selected.Add(list.Name))
                    _selectedLists.Add(list);
            RefreshSelected();
        }

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedLists.Clear();
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            SelectedLists.ItemsSource = null;
            SelectedLists.ItemsSource = _selectedLists;
        }

        private void CreatePool_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PoolNameBox.Text))
            {
                MessageBox.Show(this, L("S.PC.ErrNoName"), L("S.PV.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_selectedLists.Count == 0)
            {
                MessageBox.Show(this, L("S.PC.ErrNoLists"), L("S.PV.ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreatedPoolName = PoolNameBox.Text.Trim();
            string fullName = CreatedPoolName.StartsWith("custom_", StringComparison.Ordinal) ? CreatedPoolName : "custom_" + CreatedPoolName;
            if (GamePoolDataLoader.GetAllPools().Any(p => p.Name == fullName))
            {
                MessageBox.Show(this, L("S.PC.Duplicate"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CreatedPoolLists = _selectedLists.Select(l => l.Name).ToList();
            PoolCreated?.Invoke(CreatedPoolName, CreatedPoolLists);
            DialogResult = true;
            Close();
        }
    }

    /// <summary>Lightweight display item for the content-list picker (name + a short description).</summary>
    public class ContentListInfo
    {
        public string Name { get; set; } = "";
        public int ItemCount { get; set; }
        public string Description => LocalizationManager.T("S.PC.ListItems", ItemCount);
    }
}
