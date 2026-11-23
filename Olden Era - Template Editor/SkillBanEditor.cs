using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

internal sealed class SkillBanEditor : StackPanel
{
    private readonly TextBox _ids;
    private readonly string _initial;
    private readonly ComboBox? _catalogPicker;
    private readonly Button? _addSkill;

    public SkillBanEditor(IEnumerable<string>? existing, IEnumerable<GameContentEntry>? available)
    {
        _initial = string.Join(Environment.NewLine, existing ?? []);
        _ids = new TextBox { Text = _initial, AcceptsReturn = true, MinHeight = 64, MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _ids.SetResourceReference(ToolTipProperty, "S.Patch08210.SkillIds");
        Children.Add(_ids);

        string language = LocalizationManager.Instance.CurrentLanguage == AppLanguage.En ? "english" : "russian";
        var choices = (available ?? []).Select(entry => new Choice(entry.Sid, $"{entry.DisplayName(language)} · {entry.Sid}"))
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (choices.Length == 0) return;
        var controls = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var add = _addSkill = new Button { MinWidth = 90, Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };
        add.SetResourceReference(ContentControl.ContentProperty, "S.Patch08210.AddSkill");
        DockPanel.SetDock(add, Dock.Right);
        controls.Children.Add(add);
        var picker = _catalogPicker = new ComboBox { ItemsSource = choices, DisplayMemberPath = nameof(Choice.Label),
            IsTextSearchEnabled = true, MaxDropDownHeight = 320 };
        picker.SetResourceReference(AutomationProperties.NameProperty, "S.Patch08210.SkillBans");
        picker.SelectionChanged += (_, _) => add.IsEnabled = picker.SelectedItem is Choice;
        add.Click += (_, _) =>
        {
            if (picker.SelectedItem is Choice choice && !ReadIds().Contains(choice.Id, StringComparer.Ordinal))
                _ids.Text = string.Join(Environment.NewLine, ReadIds().Append(choice.Id));
        };
        controls.Children.Add(picker);
        Children.Add(controls);
    }

    public bool WasEdited => _ids.Text != _initial;

    public List<string> ReadIds() => _ids.Text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal).ToList();

    internal void DebugAddCatalogSkill(string id)
    {
        if (_catalogPicker is null || _addSkill is null)
            throw new InvalidOperationException("The installed game did not supply a skill list.");
        _catalogPicker.SelectedItem = _catalogPicker.Items.OfType<Choice>().Single(choice => choice.Id == id);
        if (!_addSkill.IsEnabled) throw new InvalidOperationException("Selecting a catalog skill did not enable Add.");
        _addSkill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private sealed record Choice(string Id, string Label)
    {
        public override string ToString() => Label;
    }
}
