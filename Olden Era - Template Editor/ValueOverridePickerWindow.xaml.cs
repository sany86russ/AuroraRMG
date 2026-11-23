using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Olden_Era___Template_Editor;

public partial class ValueOverridePickerWindow : Window
{
    public List<string> ResultLines { get; private set; } = [];
    private readonly List<SelectableSid> _choices = [];
    private readonly HashSet<string> _existing;

    public ValueOverridePickerWindow(IEnumerable<string> alreadyOverridden)
    {
        InitializeComponent();
        _existing = alreadyOverridden.Select(target => ValueOverrideText.TryParseTarget(target, out string sid, out int variant)
            ? ValueOverrideText.FormatTarget(sid, variant) : target).ToHashSet(StringComparer.Ordinal);
        foreach (string sid in KnownValues.ObjectSids.Distinct().Where(s => !_existing.Contains(s)).OrderBy(s => s))
            AddChoice(new SelectableSid(sid));
        TxtSearch.SetResourceReference(ToolTipProperty, "S.P.SearchKeepsSelection");
        LocalizationManager.Observe(this, () => { foreach (var choice in _choices) choice.RefreshLanguage(); RefreshList(); });
        RefreshList();
    }

    private void AddChoice(SelectableSid choice)
    {
        choice.PropertyChanged += (_, _) => UpdateAddButton();
        _choices.Add(choice);
    }

    private void BtnCatalog_Click(object sender, RoutedEventArgs e)
    {
        var picker = new GameContentCatalogWindow(chooseVariant: SelectCatalogTarget, allowedKind: GameContentKind.Object)
        { Owner = this };
        picker.ShowDialog();
    }

    private void SelectCatalogTarget(GameContentEntry entry, GameRewardVariant? reward)
    {
        string target = ValueOverrideText.FormatTarget(entry.Sid, reward?.Index ?? -1);
        if (_existing.Contains(target))
        {
            MessageBox.Show(this, LocalizationManager.T("S.Vov.ExistingTarget", target), Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var choice = _choices.FirstOrDefault(c => c.Id == target);
        if (choice is null)
        {
            choice = new SelectableSid(target, () =>
            {
                string language = LocalizationManager.Instance.CurrentLanguage == AppLanguage.En ? "english" : "russian";
                return ValueOverrideText.FormatTarget(entry.DisplayName(language), reward?.Index ?? -1);
            });
            AddChoice(choice);
        }
        choice.IsSelected = true;
        if (TxtSearch.Text == target) RefreshList();
        else TxtSearch.Text = target;
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
        ResultLines = _choices.Where(c => c.IsSelected).Select(c => $"{c.Id}={guard.Value.ToString(CultureInfo.InvariantCulture)}").ToList();
        if (ResultLines.Count > 0) DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
