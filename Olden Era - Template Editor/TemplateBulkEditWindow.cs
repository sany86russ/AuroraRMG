using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

public sealed class TemplateBulkEditWindow : Window
{
    public RmgTemplate? ResultTemplate { get; private set; }
    internal int SelectedVariantIndex => (_variant.SelectedItem as ComboBoxItem)?.Tag is int index ? index : -1;
    private readonly ComboBox _variant = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly ListBox _zones = new() { SelectionMode = SelectionMode.Multiple };

    public TemplateBulkEditWindow(RmgTemplate source, int activeVariantIndex = 0)
    {
        Width = 730; Height = 650; MinWidth = 530; MinHeight = 420; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(TitleProperty, "S.Patch.BulkTitle"); SetResourceReference(BackgroundProperty, "BrushBackground"); SetResourceReference(ForegroundProperty, "BrushText");
        var root = new DockPanel { Margin = new Thickness(18) }; Content = root;
        var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.OrangeRed }; bottom.Children.Add(error);
        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = LocalizationManager.T("S.Patch.Cancel"), IsCancel = true, MinWidth = 90 };
        var apply = new Button { Content = LocalizationManager.T("S.Patch.OK"), IsDefault = true, MinWidth = 90, Margin = new Thickness(10, 0, 0, 0) };
        apply.IsEnabled = false;
        buttons.Children.Add(cancel); buttons.Children.Add(apply); bottom.Children.Add(buttons);
        var form = new StackPanel(); DockPanel.SetDock(form, Dock.Top); root.Children.Add(form);
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; hint.SetResourceReference(TextBlock.TextProperty, "S.Patch.BulkHint"); form.Children.Add(hint);
        foreach (int index in TemplateBulkEditor.GetEditableVariants(source))
            _variant.Items.Add(new ComboBoxItem { Tag = index, Content = LocalizationManager.T("S.Patch08210.VariantEntry", index + 1, source.Variants!.Count) });
        _variant.SelectedItem = _variant.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, activeVariantIndex))
            ?? _variant.Items.OfType<ComboBoxItem>().FirstOrDefault();
        form.Children.Add(_variant);
        var field = new ComboBox { ItemsSource = TemplateBulkEditor.ZoneProperties, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 10) }; form.Children.Add(field);
        var value = new TextBox { Text = "[60, 20, 10, 10, 2, 0]", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 55, Margin = new Thickness(0, 0, 0, 14) }; form.Children.Add(value);
        _zones.SetResourceReference(Control.BackgroundProperty, "BrushInput");
        _zones.SetResourceReference(Control.ForegroundProperty, "BrushText");
        _zones.SetResourceReference(Control.BorderBrushProperty, "BrushBorder");
        _zones.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = FindResource("BrushHover");
        _zones.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = FindResource("BrushText");
        root.Children.Add(_zones);
        void RefreshZones()
        {
            int index = SelectedVariantIndex;
            _zones.ItemsSource = index >= 0
                ? source.Variants![index].Zones!.Where(zone => !string.IsNullOrWhiteSpace(zone?.Name)).Select(zone => zone.Name).ToArray()
                : [];
        }
        _variant.SelectionChanged += (_, _) => RefreshZones();
        _zones.SelectionChanged += (_, _) => apply.IsEnabled = _zones.SelectedItems.Count > 0;
        RefreshZones();
        apply.Click += (_, _) =>
        {
            try
            {
                int selectedVariant = SelectedVariantIndex;
                if (selectedVariant < 0 || field.SelectedItem is not string selectedField)
                    throw new ArgumentException(LocalizationManager.T("S.Patch.SelectOptions"));
                ResultTemplate = TemplateBulkEditor.Apply(source, selectedVariant, _zones.SelectedItems.Cast<string>().ToArray(), selectedField, value.Text);
                DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
            { error.Text = ex.Message; }
        };
    }

    internal void DebugSelectFirstZone() => _zones.SelectedIndex = 0;
}
