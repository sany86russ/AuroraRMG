using System.Collections.Generic;
using System.Text.Json;
using Olden_Era___Template_Editor.Services;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Edits an independent draft; closing without Apply leaves the template unchanged.
    /// From/To stay read-only. The parent commits the draft and updates road references together.
    /// </summary>
    public partial class ConnectionManagerWindow : Window
    {
        public List<Connection> EditedConnections { get; }
        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public ConnectionManagerWindow(List<Connection> connections)
        {
            InitializeComponent();


            EditedConnections = JsonSerializer.Deserialize<List<Connection>>(
                JsonSerializer.Serialize(connections, JsonExport.Options), JsonExport.Options)!;
            Grid.ItemsSource = EditedConnections;
            RefreshLanguage();
            LocalizationManager.Observe(this, RefreshLanguage);
        }

        private void RefreshLanguage()
        {
            if (Grid.CommitEdit(DataGridEditingUnit.Cell, true) && Grid.CommitEdit(DataGridEditingUnit.Row, true)
                && Grid.ItemsSource is not null) Grid.Items.Refresh();
            // Localised headers — DataGrid columns live outside the visual tree, so set in code.
            ColName.Header = L("S.CM.ColName");
            ColFrom.Header = L("S.CM.ColFrom");
            ColTo.Header = L("S.CM.ColTo");
            ColType.Header = L("S.CM.ColType");
            ColGuard.Header = L("S.CM.ColGuard");
            ColRandom.Header = L("S.CM.ColRandom");
            ColRoad.Header = L("S.CM.ColRoad");
            ColEscape.Header = L("S.CM.ColEscape");

            TxtCount.Text = L("S.CM.Count", EditedConnections.Count);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!Grid.CommitEdit(DataGridEditingUnit.Cell, true) || !Grid.CommitEdit(DataGridEditingUnit.Row, true)) return;
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (EditedConnections.Any(c => string.IsNullOrWhiteSpace(c.Name) || !names.Add(c.Name)))
            {
                MessageBox.Show(this, L("S.CM.InvalidName"), L("S.EC.SaveValidateTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
