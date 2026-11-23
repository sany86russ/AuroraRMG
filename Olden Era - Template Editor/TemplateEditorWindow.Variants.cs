using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor;

public partial class TemplateEditorWindow
{
    private int _variantIndex;
    private bool _refreshingVariantChoices;
    private readonly Dictionary<int, Dictionary<string, Point>> _variantPositions = [];

    private void RefreshVariantChoices(int preferredIndex)
    {
        var variants = _template.Variants ??= [];
        if (!variants.Any(variant => variant is not null)) variants.Add(new Variant());
        int[] available = Enumerable.Range(0, variants.Count).Where(index => variants[index] is not null).ToArray();
        _variantIndex = available.Contains(preferredIndex) ? preferredIndex : available[0];

        _refreshingVariantChoices = true;
        try
        {
            CmbVariant.Items.Clear();
            foreach (int index in available)
            {
                var item = new ComboBoxItem { Content = L("S.Patch08210.VariantEntry", index + 1, variants.Count), Tag = index };
                CmbVariant.Items.Add(item);
                if (index == _variantIndex) CmbVariant.SelectedItem = item;
            }
            VariantPicker.Visibility = available.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _refreshingVariantChoices = false; }
    }

    private void CmbVariant_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingVariantChoices || CmbVariant.SelectedItem is not ComboBoxItem { Tag: int index } || index == _variantIndex) return;
        if (_invalidWeightEditors.Count > 0)
        {
            RefreshVariantChoices(_variantIndex);
            UpdateStatus(L("S.Patch.InvalidEditor"));
            return;
        }

        RememberVariantLayout();
        ResetVariantInteraction(disableMirror: true);
        _variantIndex = index;
        RestoreVariantLayout();
        RebuildGraph();
        FitToView();
        BuildInspector();
        UpdateStatus(L("S.Patch08210.VariantSwitch", _variantIndex + 1, _template.Variants!.Count));
    }

    private void ResetVariantInteraction(bool disableMirror)
    {
        if (disableMirror && _mirrorMode) DisableMirrorMode();
        _selected = null;
        _connectFrom = null;
        _connectMode = false;
        _dragZone = null;
        _isPanning = false;
        _movedWhileDragging = false;
        if (CanvasHost.IsMouseCaptured) CanvasHost.ReleaseMouseCapture();
        BtnConnectMode.Background = null;
    }

    private void ResetVariantLayouts()
    {
        _variantPositions.Clear();
        RefreshVariantChoices(0);
        ResetVariantInteraction(disableMirror: true);
    }

    private void RememberVariantLayout() => _variantPositions[_variantIndex] = new(_positions, StringComparer.Ordinal);

    private void RestoreVariantLayout()
    {
        ComputePositions();
        if (!_variantPositions.TryGetValue(_variantIndex, out var remembered)) return;
        foreach (var zone in Zones)
            if (remembered.TryGetValue(zone.Name, out var point)) _positions[zone.Name] = point;
    }

    private void SaveVariantLayouts(string path)
    {
        RememberVariantLayout();
        for (int index = 0; index < _template.Variants!.Count; index++)
        {
            if (!_variantPositions.TryGetValue(index, out var positions) && _currentPath is not null)
                positions = EditorLayoutStore.LoadVariant(_currentPath, index);
            if (positions is not null) EditorLayoutStore.SaveVariant(path, index, positions);
        }
    }

    internal void DebugSelectVariant(int variantIndex)
    {
        CmbVariant.SelectedItem = CmbVariant.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, variantIndex));
        if (_variantIndex != variantIndex || VariantPicker.Visibility != Visibility.Visible || _nodeShapes.Count != Zones.Count)
            throw new InvalidOperationException("The selected stock variant did not render its graph.");
    }

    internal void DebugVerifyVariantEditing()
    {
        var sample = new RmgTemplate { Name = "Variant verification", SizeX = 32, SizeZ = 32,
            Variants = [new() { Zones = [new() { Name = "Shared", Size = 1 }] },
                        new() { Zones = [new() { Name = "Shared", Size = 2 }, new() { Name = "Second", Size = 1 }] }] };
        var window = new TemplateEditorWindow(sample, MapTopology.Default)
        {
            ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -6000, Top = 100,
        };
        try
        {
            window.Show();
            var firstPosition = new Point(180, 220);
            var secondPosition = new Point(400, 420);
            window._positions["Shared"] = firstPosition;
            string beforeSwitch = JsonSerializer.Serialize(window._template, SnapshotOptions);
            window.CmbVariant.SelectedIndex = 1;
            if (window._dirty || window._variantIndex != 1 || !window._nodeShapes.ContainsKey("Second")
                || JsonSerializer.Serialize(window._template, SnapshotOptions) != beforeSwitch)
                throw new InvalidOperationException("The variant picker did not switch the active graph without editing the document.");
            window._positions["Shared"] = secondPosition;
            window.Zones[0].Size = 3;
            window.MarkDirty();
            window.CmbVariant.SelectedIndex = 0;
            if (window.Zones[0].Size != 1 || window._positions["Shared"] != firstPosition)
                throw new InvalidOperationException("Editing another variant changed the first variant or its canvas layout.");
            window.DoUndo();
            if (window._variantIndex != 1 || window.Zones[0].Size != 2 || window._positions["Shared"] != secondPosition)
                throw new InvalidOperationException("Undo did not restore the edited variant and its canvas layout.");
            window.DoRedo();
            if (window.Zones[0].Size != 3) throw new InvalidOperationException("Redo did not restore the edited variant.");
            string layoutKey = Path.Combine(Services.GameData.AppDataPaths.Root, "variant-verification.rmg.json");
            window.SaveVariantLayouts(layoutKey);
            if (EditorLayoutStore.Load(layoutKey)?["Shared"] != firstPosition || EditorLayoutStore.LoadVariant(layoutKey, 1)?["Shared"] != secondPosition)
                throw new InvalidOperationException("Variant layout sidecars mixed positions for equal zone names.");
        }
        finally
        {
            window._dirty = false;
            window.Close();
        }
    }
}
