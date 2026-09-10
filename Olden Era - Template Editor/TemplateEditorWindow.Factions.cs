using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor;

public partial class TemplateEditorWindow
{
    private static void LocalizeTokenOptions(ComboBox combo)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding { Converter = new GameTokenConverter() });
        text.SetBinding(TextBlock.ToolTipProperty, new Binding());
        combo.ItemTemplate = new DataTemplate { VisualTree = text };
    }

    private void RemoveEditorMainObject(Zone zone, int index)
    {
        int reset = TemplateRefactor.RemoveMainObject(Variant, zone, index);
        if (_mirrorMode && _mirrorProperties && TwinOf(zone) is { } twin && index < (twin.MainObjects?.Count ?? 0))
            reset += TemplateRefactor.RemoveMainObject(Variant, twin, index);
        MarkDirty();
        RefreshNode(zone);
        BuildInspector();
        UpdateStatus(L("S.Faction.ObjectRemoved", reset));
    }

    private sealed record FactionSource(string ZoneName, int Index, string Label)
    {
        public override string ToString() => Label;
    }

    private StackPanel CreateFactionEditor(MainObject mo)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        AddSectionLabel(L("S.EC.MoFactionType"), panel);
        var type = new ComboBox { Margin = new Thickness(0, 0, 0, 6), MaxDropDownHeight = 240 };
        foreach (var value in new[] { "", "Random" }.Concat(KnownValues.SelectorTypes).Append(mo.Faction?.Type ?? "").Distinct())
            type.Items.Add(value);
        type.SelectedItem = mo.Faction?.Type ?? "";
        LocalizeTokenOptions(type);
        panel.Children.Add(type);

        AddSectionLabel(L("S.Faction.Fixed"), panel);
        var fixedFaction = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var value in KnownValues.FromListFactionArgs) fixedFaction.Items.Add(value);
        fixedFaction.ItemTemplate = type.ItemTemplate;
        if (mo.Faction is { Type: "FromList", Args.Count: 1 }) fixedFaction.SelectedItem = mo.Faction.Args[0];
        panel.Children.Add(fixedFaction);

        AddSectionLabel(L("S.EC.MoFactionArgs"), panel);
        var args = new TextBox
        {
            Text = string.Join(Environment.NewLine, mo.Faction?.Args ?? []),
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 62, MaxHeight = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 4),
            ToolTip = L("S.Faction.ArgsHelp"),
        };
        panel.Children.Add(args);
        panel.Children.Add(new TextBlock { Text = L("S.Faction.ArgsHelp"), TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });

        AddSectionLabel(L("S.Faction.Source"), panel);
        var source = new ComboBox { MaxDropDownHeight = 260,
            Margin = new Thickness(0, 0, 0, 6) };
        foreach (var zone in Zones)
        for (int i = 0; i < (zone.MainObjects?.Count ?? 0); i++)
        {
            var candidate = zone.MainObjects![i];
            if (ReferenceEquals(candidate, mo) || candidate.Type is "AbandonedOutpost" or "GladiatorArena") continue;
            source.Items.Add(new FactionSource(zone.Name, i, L("S.Faction.SourceItem", zone.Name, i, GameLabels.Token(candidate.Type ?? ""))));
        }
        panel.Children.Add(source);
        var buttons = new WrapPanel();
        var same = new Button { Content = L("S.Faction.Same"), IsEnabled = false, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 4) };
        var different = new Button { Content = L("S.Faction.Different"), IsEnabled = false, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 4) };
        buttons.Children.Add(same); buttons.Children.Add(different); panel.Children.Add(buttons);
        panel.Children.Add(new TextBlock { Text = L("S.Faction.SourceHelp"), TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("BrushTextDim"), FontSize = 11 });

        bool updating = false;
        void Refresh()
        {
            updating = true;
            type.SelectedItem = mo.Faction?.Type ?? "";
            args.Text = string.Join(Environment.NewLine, mo.Faction?.Args ?? []);
            fixedFaction.SelectedItem = mo.Faction is { Type: "FromList", Args.Count: 1 } ? mo.Faction.Args[0] : null;
            updating = false;
        }
        type.SelectionChanged += (_, _) =>
        {
            if (updating || type.SelectedItem is not string selected) return;
            FactionSelectors.SetType(mo, selected); MarkDirty(); Refresh();
        };
        args.TextChanged += (_, _) =>
        {
            if (updating) return;
            var parsed = FactionSelectors.ParseArguments(args.Text);
            if ((mo.Faction?.Args ?? []).SequenceEqual(parsed)) return;
            mo.Faction ??= new TypedSelector(); mo.Faction.Args = parsed;
            updating = true; fixedFaction.SelectedItem = null; updating = false;
            MarkDirty();
        };
        fixedFaction.SelectionChanged += (_, _) =>
        {
            if (updating || fixedFaction.SelectedItem is not string selected) return;
            FactionSelectors.SetType(mo, "FromList"); mo.Faction!.Args = [selected]; MarkDirty(); Refresh();
        };
        source.SelectionChanged += (_, _) => same.IsEnabled = different.IsEnabled = source.SelectedItem is FactionSource;
        void Reference(bool exclude)
        {
            if (source.SelectedItem is not FactionSource selected) return;
            FactionSelectors.SetReference(mo, Zones, selected.ZoneName, selected.Index, exclude);
            MarkDirty(); Refresh();
        }
        same.Click += (_, _) => Reference(false);
        different.Click += (_, _) => Reference(true);
        return panel;
    }
}
