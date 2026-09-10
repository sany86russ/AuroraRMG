using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Olden_Era___Template_Editor;

public partial class ValueOverridePickerWindow : Window
{
    public List<string> ResultLines { get; private set; } = [];
    private readonly List<SelectableSid> _choices = [];

    public ValueOverridePickerWindow(IEnumerable<string> alreadyOverridden)
    {
        InitializeComponent();
        var existing = new HashSet<string>(alreadyOverridden, System.StringComparer.Ordinal);
        foreach (string sid in KnownValues.ObjectSids.Distinct().Where(s => !existing.Contains(s)).OrderBy(s => s))
        {
            var choice = new SelectableSid(sid);
            choice.PropertyChanged += (_, _) => UpdateAddButton();
            _choices.Add(choice);
        }
        TxtSearch.SetResourceReference(ToolTipProperty, "S.P.SearchKeepsSelection");
        LocalizationManager.Observe(this, () => { foreach (var choice in _choices) choice.RefreshLanguage(); RefreshList(); });
        RefreshList();
    }

    private void UpdateAddButton()
    {
        int count = _choices.Count(c => c.IsSelected);
        BtnAdd.Content = count > 1 ? LocalizationManager.T("S.P.AddSelectedN", count) : LocalizationManager.T("S.P.AddSelected");
        BtnAdd.IsEnabled = count > 0;
    }

    private void RefreshList()
    {
        if (LbSids is null) return;
        LbSids.ItemsSource = _choices.Where(c => (c.Id.Contains(TxtSearch.Text.Trim(), System.StringComparison.OrdinalIgnoreCase) || c.DisplayName.Contains(TxtSearch.Text.Trim(), System.StringComparison.OrdinalIgnoreCase))).ToList();
        UpdateAddButton();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!NumericInput.TryOptionalInt(TxtGuardValue.Text, out int? guard) || guard is null or < 0)
        {
            MessageBox.Show(this, LocalizationManager.T("S.Vov.InvalidValue"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtGuardValue.Focus(); TxtGuardValue.SelectAll();
            return;
        }
        ResultLines = _choices.Where(c => c.IsSelected).Select(c => $"{c.Id}={guard.Value}").ToList();
        if (ResultLines.Count > 0) DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
