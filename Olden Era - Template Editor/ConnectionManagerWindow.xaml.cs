using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Bulk editor for all connections in the template. Binds a <see cref="DataGrid"/> directly to
    /// the live connection list so name/type/guard/road/escape edits apply in place; From/To stay
    /// read-only (topology is changed on the canvas). "Apply" closes with <see cref="Window.DialogResult"/>
    /// <c>true</c> so the editor can refresh the graph.
    /// </summary>
    public partial class ConnectionManagerWindow : Window
    {
        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public ConnectionManagerWindow(List<Connection> connections)
        {
            InitializeComponent();

            // Localised headers — DataGrid columns live outside the visual tree, so set in code.
            ColName.Header = L("S.CM.ColName");
            ColFrom.Header = L("S.CM.ColFrom");
            ColTo.Header = L("S.CM.ColTo");
            ColType.Header = L("S.CM.ColType");
            ColGuard.Header = L("S.CM.ColGuard");
            ColRoad.Header = L("S.CM.ColRoad");
            ColEscape.Header = L("S.CM.ColEscape");

            ColType.ItemsSource = KnownValues.ConnectionTypes;

            Grid.ItemsSource = connections;
            TxtCount.Text = L("S.CM.Count", connections.Count);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            Grid.CommitEdit(DataGridEditingUnit.Row, true);
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
