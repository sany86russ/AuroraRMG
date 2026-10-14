using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

public sealed class TemplateBulkEditWindow : Window
{
    public RmgTemplate? ResultTemplate { get; private set; }
    public TemplateBulkEditWindow(RmgTemplate source)
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
        apply.IsEnabled = source.Variants is { Count: > 0 };
        buttons.Children.Add(cancel); buttons.Children.Add(apply); bottom.Children.Add(buttons);
        var form = new StackPanel(); DockPanel.SetDock(form, Dock.Top); root.Children.Add(form);
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; hint.SetResourceReference(TextBlock.TextProperty, "S.Patch.BulkHint"); form.Children.Add(hint);
        var variant = new ComboBox { ItemsSource = Enumerable.Range(0, source.Variants?.Count ?? 0).ToArray(), SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 10) }; form.Children.Add(variant);
        var field = new ComboBox { ItemsSource = TemplateBulkEditor.ZoneProperties, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 10) }; form.Children.Add(field);
        var value = new TextBox { Text = "[60, 20, 10, 10, 2, 0]", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 55, Margin = new Thickness(0, 0, 0, 14) }; form.Children.Add(value);
        var zones = new ListBox { SelectionMode = SelectionMode.Multiple }; root.Children.Add(zones);
        void RefreshZones() => zones.ItemsSource = variant.SelectedItem is int index ? source.Variants![index].Zones?.Select(z => z.Name).ToArray() : [];
        variant.SelectionChanged += (_, _) => RefreshZones(); RefreshZones();
        apply.Click += (_, _) =>
        {
            try
            {
                if (variant.SelectedItem is not int selectedVariant || field.SelectedItem is not string selectedField)
                    throw new ArgumentException(LocalizationManager.T("S.Patch.SelectOptions"));
                ResultTemplate = TemplateBulkEditor.Apply(source, selectedVariant, zones.SelectedItems.Cast<string>().ToArray(), selectedField, value.Text);
                DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
            { error.Text = ex.Message; }
        };
    }
}
