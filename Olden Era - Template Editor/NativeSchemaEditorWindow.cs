using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;
using Olden_Era___Template_Editor.Models.NativeSchema;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;
using Olden_Era___Template_Editor.Services.NativeSchema;

namespace Olden_Era___Template_Editor;

/// <summary>Offline native-schema editing. Saves never write into the supplied game installation.</summary>
public sealed class NativeSchemaEditorWindow : Window
{
    private const long MaximumBytes = 4 * 1024 * 1024;
    private const int MaximumSchemaDepth = 128;
    private readonly string? _initialPath;
    private readonly HashSet<string> _gameRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly TreeView _tree = new();
    private readonly TextBlock _status = Label("S.NativeEditor.NoFile");
    private readonly TextBlock _path = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _raw = JsonEditor();
    private readonly TextBox _and = JsonEditor(100);
    private readonly TextBox _or = JsonEditor(100);
    private readonly TextBox _value = JsonEditor(140);
    private readonly TextBox _weight = new() { MinHeight = 28, MinWidth = 100 };
    private readonly CheckBox _fallback = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TabControl _tabs = new();
    private readonly TabItem _structured = new();
    private readonly TabItem _source = new();
    private readonly Button _apply = Button("S.NativeEditor.Apply");
    private readonly Button _save = Button("S.NativeEditor.SaveAs");
    private NativeSchemaDocument? _document;
    private Selection? _selection;
    private TreeViewItem? _selectedTreeItem;
    private bool _restoringSelection;
    private bool _loading;
    private bool _dirty;
    private bool _refreshing;
    private bool _pendingEdit;
    private TabItem? _draftTab;

    public NativeSchemaEditorWindow(string? initialPath = null, string? gameInstallPath = null)
    {
        _initialPath = initialPath;
        if (!string.IsNullOrWhiteSpace(gameInstallPath)) _gameRoots.Add(Path.GetFullPath(gameInstallPath));
        if (initialPath is not null) RememberGameRoot(initialPath);
        Width = Math.Min(1160, SystemParameters.WorkArea.Width - 60);
        Height = Math.Min(790, SystemParameters.WorkArea.Height - 60);
        MinWidth = 640; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetResourceReference(BackgroundProperty, "BrushBackground"); SetResourceReference(ForegroundProperty, "BrushText");
        SetResourceReference(TitleProperty, "S.NativeEditor.Title");
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 32, ResizeBorderThickness = new Thickness(5),
            GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var border = new Border { BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BorderBrushProperty, "BrushBorder"); Content = border;
        var outer = new DockPanel(); border.Child = outer;
        var header = new DockPanel { Height = 32 }; header.SetResourceReference(Panel.BackgroundProperty, "BrushPanel");
        DockPanel.SetDock(header, Dock.Top); outer.Children.Add(header);
        var close = new Button { Content = "✕", Width = 40 }; close.SetResourceReference(StyleProperty, "TitleBarCloseButton");
        WindowChrome.SetIsHitTestVisibleInChrome(close, true); close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        var title = Label("S.NativeEditor.Title"); title.Margin = new Thickness(12, 0, 0, 0); title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        var content = new DockPanel { Margin = new Thickness(14) }; outer.Children.Add(content);
        var controls = new StackPanel(); DockPanel.SetDock(controls, Dock.Top); content.Children.Add(controls);
        var notice = Label("S.NativeEditor.Notice"); notice.Margin = new Thickness(0, 0, 0, 10); controls.Children.Add(notice);
        var actions = new WrapPanel(); var open = Button("S.NativeEditor.Open");
        open.Click += async (_, _) => await OpenAsync(); actions.Children.Add(open);
        _save.IsEnabled = false; _save.Click += (_, _) => SaveAs(); actions.Children.Add(_save);
        _apply.IsEnabled = false; _apply.Click += (_, _) => ApplyEdit(); actions.Children.Add(_apply);
        var revert = Button("S.NativeEditor.RevertEdit"); revert.Click += (_, _) => { _pendingEdit = false; SelectNode(); }; actions.Children.Add(revert);
        controls.Children.Add(actions); _status.Margin = new Thickness(0, 8, 0, 10); controls.Children.Add(_status);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); content.Children.Add(grid);
        _tree.SetResourceReference(Control.BackgroundProperty, "BrushPanel");
        _tree.SetResourceReference(Control.ForegroundProperty, "BrushText");
        _tree.SetResourceReference(Control.BorderBrushProperty, "BrushBorder");
        ApplyTreeTheme(); grid.Children.Add(_tree);
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(splitter, 1); grid.Children.Add(splitter);
        var detail = new DockPanel(); Grid.SetColumn(detail, 2); grid.Children.Add(detail);
        _path.Margin = new Thickness(0, 0, 0, 8); DockPanel.SetDock(_path, Dock.Top); detail.Children.Add(_path); detail.Children.Add(_tabs);
        _structured.Header = Label("S.NativeEditor.ConditionalTab"); _source.Header = Label("S.NativeEditor.RawTab");
        _tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch; _tabs.VerticalContentAlignment = VerticalAlignment.Stretch;
        _tabs.Items.Add(_structured); _tabs.Items.Add(_source); _source.Content = _raw;
        var rowFields = new StackPanel { Margin = new Thickness(8) }; _structured.Content = new ScrollViewer { Content = rowFields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        rowFields.Children.Add(Label("S.NativeEditor.ConditionsAnd")); rowFields.Children.Add(_and);
        rowFields.Children.Add(Label("S.NativeEditor.ConditionsOr")); rowFields.Children.Add(_or);
        var weightRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        var weightLabel = Label("S.NativeEditor.Weight"); weightLabel.VerticalAlignment = VerticalAlignment.Center; weightLabel.Margin = new Thickness(0, 0, 8, 0);
        weightRow.Children.Add(weightLabel); weightRow.Children.Add(_weight); _fallback.Margin = new Thickness(16, 0, 0, 0);
        _fallback.SetResourceReference(ContentControl.ContentProperty, "S.NativeEditor.Fallback"); weightRow.Children.Add(_fallback); rowFields.Children.Add(weightRow);
        rowFields.Children.Add(Label("S.NativeEditor.Value")); rowFields.Children.Add(_value);
        foreach (var textbox in new[] { _raw, _and, _or, _value, _weight }) textbox.TextChanged += (_, _) => TrackPendingEdit();
        _tabs.SelectionChanged += (_, args) =>
        {
            if (_refreshing || !ReferenceEquals(args.OriginalSource, _tabs) || !_pendingEdit || _draftTab is null || ReferenceEquals(_tabs.SelectedItem, _draftTab)) return;
            _refreshing = true;
            try { _tabs.SelectedItem = _draftTab; }
            finally { _refreshing = false; }
            ShowError(LocalizationManager.T("S.NativeEditor.Unapplied"));
        };
        _fallback.Checked += (_, _) => TrackPendingEdit(); _fallback.Unchecked += (_, _) => TrackPendingEdit();
        _tree.SelectedItemChanged += (_, _) => SelectNode();
        Loaded += async (_, _) => { if (_initialPath is not null) await LoadAsync(_initialPath); };
        Closing += (_, args) => { if ((_dirty || _pendingEdit) && !ConfirmDiscard()) args.Cancel = true; };
    }

    private void TrackPendingEdit()
    { if (!_refreshing && _selection is not null) { if (!_pendingEdit) _draftTab = _tabs.SelectedItem as TabItem; _pendingEdit = true; } }

    private void ApplyTreeTheme()
    {
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("BrushText")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
        var selected = new Trigger { Property = TreeViewItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("BrushGoldDim")));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("BrushText")));
        style.Triggers.Add(selected);
        _tree.Resources[typeof(TreeViewItem)] = style;

        // The system TreeViewItem template supplies its own active/inactive selection brushes.
        // Scope those brushes to this tree so selected headers keep the application's dark contrast.
        _tree.Resources[SystemColors.HighlightBrushKey] = FindResource("BrushGoldDim");
        _tree.Resources[SystemColors.HighlightTextBrushKey] = FindResource("BrushText");
        _tree.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = FindResource("BrushHover");
        _tree.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = FindResource("BrushText");
        _tree.Resources[SystemColors.ControlTextBrushKey] = FindResource("BrushText");
        _tree.Resources[SystemColors.WindowTextBrushKey] = FindResource("BrushText");
    }

    private async Task OpenAsync()
    {
        if (_loading || ((_dirty || _pendingEdit) && !ConfirmDiscard())) return;
        var dialog = new OpenFileDialog { Filter = LocalizationManager.T("S.NativeEditor.FileFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await LoadAsync(dialog.FileName);
    }

    private async Task LoadAsync(string path)
    {
        if (_loading) return;
        _loading = true; _save.IsEnabled = false; _apply.IsEnabled = false;
        try
        {
            var document = await Task.Run(() =>
            {
                if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException(LocalizationManager.T("S.NativeEditor.TooLarge"));
                return NativeSchemaDocument.Read(path);
            });
            _document = document; RememberGameRoot(path);
            _selection = null; _dirty = false; _pendingEdit = false;
            RebuildTree(); _save.IsEnabled = true;
            _status.Text = LocalizationManager.T("S.NativeEditor.Loaded", Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { ShowError(ex.Message); _save.IsEnabled = _document is not null; _apply.IsEnabled = _document is not null && _selection is not null; }
        finally { _loading = false; }
    }

    private void RebuildTree()
    {
        _tree.Items.Clear();
        _selectedTreeItem = null;
        if (_document is null) return;
        var root = NodeItem([], _document.Root, Label("S.NativeEditor.Root"));
        _tree.Items.Add(root); root.IsExpanded = true; root.IsSelected = true;
    }

    internal void DebugSelectConditionalParameter()
    {
        if (_document is null || _tree.Items.Count == 0) throw new InvalidOperationException("The native schema has not loaded for verification.");
        string before = _document.ToJson(false);
        var item = (TreeViewItem)_tree.Items[0];
        foreach (string component in new[] { "zoneClasses", "0", "guardedContentValuePerArea", "0" })
        {
            item.IsExpanded = true;
            item = item.Items.OfType<TreeViewItem>().FirstOrDefault(child => child.Tag is Selection selection
                && selection.Pointer.Length > 0 && selection.Pointer[^1] == component)
                ?? throw new InvalidOperationException("The representative native conditional row is unavailable.");
        }
        item.IsSelected = true; item.BringIntoView();
        if (_selection?.Conditional != true || !_structured.IsEnabled || _tabs.SelectedItem != _structured || _value.Text.Length == 0
            || _pendingEdit || _dirty || _document.ToJson(false) != before)
            throw new InvalidOperationException("Selecting a native conditional row did not preserve its source or populate the structured editor.");
    }

    internal void DebugVerifyConditionalParameterEdit()
    {
        var document = _document ?? throw new InvalidOperationException("The native schema was not loaded.");
        var pointer = _selection?.Pointer ?? throw new InvalidOperationException("A conditional row must be selected.");
        var original = Resolve(pointer)?.DeepClone();
        var parent = FindTreeItem(pointer[..^1]);
        string originalStatus = _status.Text;
        try
        {
            _selectedTreeItem!.IsExpanded = true;
            _weight.Text = _weight.Text == "2" ? "1" : "2";
            if (!ApplyEdit() || !ReferenceEquals(_document, document) || _selection?.Pointer.SequenceEqual(pointer) != true
                || !ReferenceEquals(FindTreeItem(pointer[..^1]), parent) || !parent.IsExpanded || !_selectedTreeItem.IsExpanded
                || JsonNode.DeepEquals(Resolve(pointer), original))
                throw new InvalidOperationException("Applying a native row did not retain its selection, expansion or surrounding document.");

            var edited = Resolve(pointer)?.DeepClone();
            _tabs.SelectedItem = _source;
            _raw.Text = """{"value":1,"value":2}""";
            if (ApplyEdit() || !JsonNode.DeepEquals(Resolve(pointer), edited))
                throw new InvalidOperationException("An ambiguous native draft changed the saved row.");

            int excessiveNesting = MaximumSchemaDepth - pointer.Length + 1;
            _raw.Text = string.Concat(Enumerable.Repeat("{\"child\":", excessiveNesting)) + "0" + new string('}', excessiveNesting);
            string depthError = LocalizationManager.T("S.NativeEditor.MaximumDepth", MaximumSchemaDepth);
            if (ApplyEdit() || !JsonNode.DeepEquals(Resolve(pointer), edited)
                || _status.Text != LocalizationManager.T("S.NativeEditor.Error", depthError))
                throw new InvalidOperationException("A native fragment exceeded the schema's overall depth without being rejected.");
        }
        finally
        {
            ApplyReplacement(pointer, document, original, raw: false);
            _dirty = false;
            _status.Text = originalStatus;
        }
    }

    private TreeViewItem NodeItem(string[] pointer, JsonNode? value, object header, string? parentProperty = null)
    {
        bool conditional = value is JsonObject row && row.ContainsKey("value") &&
            (row.ContainsKey("conditionsAnd") || row.ContainsKey("conditionsOr") || row.ContainsKey("fallback")
             || IsConditionalParameter(parentProperty));
        var item = new TreeViewItem { Header = header, Tag = new Selection(pointer, conditional) };
        if (value is JsonObject { Count: > 0 } || value is JsonArray { Count: > 0 })
        {
            item.Items.Add(new TreeViewItem());
            item.Expanded += (_, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, item) || item.Items.Count != 1 || ((TreeViewItem)item.Items[0]).Tag is not null) return;
                item.Items.Clear();
                var current = Resolve(pointer);
                if (current is JsonObject obj)
                    foreach (var property in obj)
                        item.Items.Add(NodeItem([.. pointer, property.Key], property.Value, property.Key, property.Key));
                else if (current is JsonArray array)
                    for (var index = 0; index < array.Count; index++)
                    {
                        var node = array[index];
                        item.Items.Add(NodeItem([.. pointer, index.ToString(CultureInfo.InvariantCulture)], node,
                            ArrayItemCaption(index, node), pointer.LastOrDefault()));
                    }
            };
        }
        return item;
    }

    private void SelectNode()
    {
        if (_restoringSelection || _tree.SelectedItem is not TreeViewItem { Tag: Selection selected } item || _document is null) return;
        if (_pendingEdit && _selection is not null && !ReferenceEquals(_selection, selected))
        {
            ShowError(LocalizationManager.T("S.NativeEditor.Unapplied"));
            // Keep entered text and restore its highlighted target; the right panel cannot edit a different node.
            _restoringSelection = true;
            try { if (_selectedTreeItem is not null) _selectedTreeItem.IsSelected = true; }
            finally { _restoringSelection = false; }
            return;
        }
        _selection = selected; _selectedTreeItem = item; var value = Resolve(selected.Pointer);
        _refreshing = true;
        try
        {
            _path.Text = "/" + string.Join("/", selected.Pointer);
            _raw.Text = Json(value); _structured.IsEnabled = selected.Conditional;
            if (selected.Conditional && value is JsonObject row)
            {
                _and.Text = Json(row["conditionsAnd"] ?? new JsonObject()); _or.Text = Json(row["conditionsOr"] ?? new JsonObject());
                _value.Text = Json(row["value"]); _weight.Text = new NativeConditionalEntry(0, row).Weight.ToString("R", CultureInfo.InvariantCulture);
                _fallback.IsChecked = row["fallback"]?.GetValue<bool>() ?? false; _tabs.SelectedItem = _structured;
            }
            else _tabs.SelectedItem = _source;
            _pendingEdit = false; _draftTab = null; _apply.IsEnabled = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        { _tabs.SelectedItem = _source; _structured.IsEnabled = false; ShowError(ex.Message); }
        finally { _refreshing = false; }
    }

    private bool ApplyEdit()
    {
        if (_document is null || _selection is null || _selectedTreeItem is null) return false;
        try
        {
            var pointer = _selection.Pointer;
            bool raw = _tabs.SelectedItem == _source;
            var candidate = _document;
            JsonNode? replacement;
            if (pointer.Length == 0)
            {
                TemplateJsonReader.GuardBeforeDeserialize(_raw.Text);
                candidate = NativeSchemaDocument.Parse(_raw.Text);
                replacement = candidate.Root;
            }
            else replacement = raw ? ParseDraftJson(_raw.Text) : EditedRow();
            ApplyReplacement(pointer, candidate, replacement, raw);
            _dirty = true;
            _status.Text = LocalizationManager.T("S.NativeEditor.Modified");
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or NotSupportedException)
        { ShowError(ex.Message); return false; }
    }

    private void ReplaceSubtree(string[] pointer, JsonNode? replacement)
    {
        string name = pointer[^1];
        if (pointer.Length == 1 && name is ("zoneClasses" or "mainObjectClasses") && replacement is not JsonArray)
            throw new JsonException(LocalizationManager.T("S.NativeEditor.InvalidTarget"));
        var parent = Resolve(pointer[..^1]);
        if (parent is JsonObject obj) obj[name] = replacement;
        else if (parent is JsonArray array) array[int.Parse(name, CultureInfo.InvariantCulture)] = replacement;
        else throw new JsonException(LocalizationManager.T("S.NativeEditor.InvalidTarget"));
    }

    private void ApplyReplacement(string[] pointer, NativeSchemaDocument document, JsonNode? value, bool raw)
    {
        ValidateDepth(value, MaximumSchemaDepth - pointer.Length);
        var originalDocument = _document!;
        var originalValue = Resolve(pointer);
        var previous = _selectedTreeItem!;
        var previousSelection = _selection;
        var expanded = ExpandedPaths(previous).ToArray();
        var draftTab = _draftTab;
        bool pending = _pendingEdit;
        ItemCollection siblings;
        TreeViewItem replacement;
        if (pointer.Length == 0)
        {
            siblings = _tree.Items;
            replacement = NodeItem([], document.Root, Label("S.NativeEditor.Root"));
        }
        else
        {
            var parentPointer = pointer[..^1];
            var parentItem = FindTreeItem(parentPointer);
            siblings = parentItem.Items;
            if (Resolve(parentPointer) is JsonArray)
                replacement = NodeItem(pointer, value, ArrayItemCaption(int.Parse(pointer[^1], CultureInfo.InvariantCulture), value),
                    parentPointer.LastOrDefault());
            else replacement = NodeItem(pointer, value, pointer[^1], pointer[^1]);
        }
        int index = siblings.IndexOf(previous);
        if (index < 0) throw new JsonException(LocalizationManager.T("S.NativeEditor.InvalidTarget"));
        var previousTab = _tabs.SelectedItem;
        var previousFields = new[] { _raw.Text, _and.Text, _or.Text, _value.Text, _weight.Text };
        var previousFallback = _fallback.IsChecked;
        bool modelChanged = false;
        _restoringSelection = true;
        try
        {
            if (pointer.Length == 0) _document = document;
            else ReplaceSubtree(pointer, value);
            modelChanged = true;
            siblings.RemoveAt(index);
            siblings.Insert(index, replacement);
            RestoreExpansion(replacement, expanded);
            replacement.IsSelected = true;
            _restoringSelection = false;
            _pendingEdit = false;
            _draftTab = null;
            SelectNode();
            if (raw) _tabs.SelectedItem = _source;
            replacement.BringIntoView();
        }
        catch
        {
            if (modelChanged)
            {
                if (pointer.Length == 0) _document = originalDocument;
                else ReplaceSubtree(pointer, originalValue);
            }
            _restoringSelection = _refreshing = true;
            if (siblings.Contains(replacement)) siblings.Remove(replacement);
            if (!siblings.Contains(previous)) siblings.Insert(index, previous);
            previous.IsSelected = true;
            _selectedTreeItem = previous;
            _selection = previousSelection;
            _raw.Text = previousFields[0]; _and.Text = previousFields[1]; _or.Text = previousFields[2];
            _value.Text = previousFields[3]; _weight.Text = previousFields[4]; _fallback.IsChecked = previousFallback;
            _tabs.SelectedItem = previousTab;
            _draftTab = draftTab;
            _pendingEdit = pending;
            throw;
        }
        finally { _restoringSelection = _refreshing = false; }
    }

    private static void ValidateDepth(JsonNode? node, int remaining)
    {
        if (node is not (JsonArray or JsonObject)) return;
        if (remaining <= 0) throw new JsonException(LocalizationManager.T("S.NativeEditor.MaximumDepth", MaximumSchemaDepth));
        if (node is JsonArray array)
            foreach (var child in array) ValidateDepth(child, remaining - 1);
        else if (node is JsonObject obj)
            foreach (var child in obj) ValidateDepth(child.Value, remaining - 1);
    }

    private TreeViewItem FindTreeItem(string[] pointer)
    {
        var item = (TreeViewItem)_tree.Items[0];
        foreach (string part in pointer)
            item = item.Items.OfType<TreeViewItem>().First(child => child.Tag is Selection selected && selected.Pointer[^1] == part);
        return item;
    }

    private static IEnumerable<string[]> ExpandedPaths(TreeViewItem item)
    {
        if (!item.IsExpanded || item.Tag is not Selection selected) yield break;
        yield return selected.Pointer;
        foreach (var child in item.Items.OfType<TreeViewItem>())
        foreach (var path in ExpandedPaths(child))
            yield return path;
    }

    private static void RestoreExpansion(TreeViewItem item, IReadOnlyList<string[]> expanded)
    {
        if (item.Tag is not Selection selected || !expanded.Any(path => path.SequenceEqual(selected.Pointer))) return;
        item.IsExpanded = true;
        foreach (var child in item.Items.OfType<TreeViewItem>())
            RestoreExpansion(child, expanded);
    }

    private static string ArrayItemCaption(int index, JsonNode? node) =>
        node is JsonObject entry && entry["name"] is JsonValue name && name.TryGetValue<string>(out var id)
            ? $"[{index}] {id}" : $"[{index}]";

    private static JsonNode? ParseDraftJson(string text)
    {
        TemplateJsonReader.GuardBeforeDeserialize(text);
        return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = MaximumSchemaDepth });
    }

    private JsonObject EditedRow()
    {
        if (Resolve(_selection!.Pointer) is not JsonObject original) throw new JsonException(LocalizationManager.T("S.NativeEditor.InvalidTarget"));
        var edited = original.DeepClone().AsObject();
        var and = ParseDraftJson(_and.Text) as JsonObject ?? throw new JsonException(LocalizationManager.T("S.NativeEditor.ConditionsObject"));
        var or = ParseDraftJson(_or.Text) as JsonObject ?? throw new JsonException(LocalizationManager.T("S.NativeEditor.ConditionsObject"));
        var value = ParseDraftJson(_value.Text);
        if (!double.TryParse(_weight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || !NativeSchemaWeight.IsRepresentable(weight))
            throw new JsonException(LocalizationManager.T("S.NativeEditor.InvalidWeight"));
        if (!JsonNode.DeepEquals(original["conditionsAnd"] ?? new JsonObject(), and)) edited["conditionsAnd"] = and;
        if (!JsonNode.DeepEquals(original["conditionsOr"] ?? new JsonObject(), or)) edited["conditionsOr"] = or;
        if (!JsonNode.DeepEquals(original["value"], value)) edited["value"] = value;
        if (new NativeConditionalEntry(0, original).Weight != weight) edited["weight"] = weight;
        var fallback = _fallback.IsChecked == true;
        if ((original["fallback"]?.GetValue<bool>() ?? false) != fallback) edited["fallback"] = fallback;
        return edited;
    }

    private void SaveAs()
    {
        if (_document is null || (_pendingEdit && !ApplyEdit())) return;
        try
        {
            var findings = _document.ValidateLocalReferences();
            if (findings.Count > 0) { ShowError(LocalizationManager.T("S.NativeEditor.ReferenceErrors", string.Join("\n", findings.Take(15)))); return; }
            var dialog = new SaveFileDialog { Filter = LocalizationManager.T("S.NativeEditor.FileFilter"),
                DefaultExt = ".mrmg.json", AddExtension = true, FileName = "Custom.mrmg.json", InitialDirectory = Path.GetTempPath() };
            if (dialog.ShowDialog(this) != true) return;
            var destination = Path.GetFullPath(dialog.FileName);
            if (IsGamePath(destination)) { ShowError(LocalizationManager.T("S.NativeEditor.GameFolderBlocked")); return; }
            AtomicFile.WriteAllText(destination, _document.ToJson(), validateDestination: path =>
            {
                if (IsGamePath(path)) throw new InvalidOperationException(LocalizationManager.T("S.NativeEditor.GameFolderBlocked"));
                Services.Export.ExportDestination.EnsureOutsideGame(path);
            });
            _dirty = false;
            _status.Text = LocalizationManager.T("S.NativeEditor.Saved", destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { ShowError(ex.Message); }
    }

    private JsonNode? Resolve(string[] pointer) => _document is null ? null : Resolve(_document.Root, pointer);
    private static JsonNode? Resolve(JsonNode root, string[] pointer)
    {
        JsonNode? current = root;
        foreach (var part in pointer)
            current = current switch { JsonObject obj => obj[part], JsonArray array => array[int.Parse(part, CultureInfo.InvariantCulture)], _ => null };
        return current;
    }
    private bool IsGamePath(string path) => _gameRoots.Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    private void RememberGameRoot(string path)
    {
        var full = Path.GetFullPath(path); var marker = Path.DirectorySeparatorChar + "HeroesOldenEra_Data" + Path.DirectorySeparatorChar;
        var index = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) _gameRoots.Add(full[..index]);
    }
    private bool ConfirmDiscard() => MessageBox.Show(this, LocalizationManager.T("S.NativeEditor.Discard"),
        LocalizationManager.T("S.NativeEditor.Title"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void ShowError(string message) => _status.Text = LocalizationManager.T("S.NativeEditor.Error", message);
    private static string Json(JsonNode? node) => node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true, MaxDepth = 128,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null";
    private static TextBlock Label(string key)
    { var text = new TextBlock { TextWrapping = TextWrapping.Wrap }; text.SetResourceReference(TextBlock.TextProperty, key); return text; }
    private static Button Button(string key)
    { var button = new Button { MinHeight = 30, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) }; button.SetResourceReference(ContentControl.ContentProperty, key); return button; }
    private static TextBox JsonEditor(double minimumHeight = 0) => new()
    { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), FontSize = 12,
        Height = double.NaN, MinHeight = minimumHeight, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(8, 6, 8, 6),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static bool IsConditionalParameter(string? name) => name is not null && new[]
    { "biome", "contentBiome", "metaObjectsBiome", "mainObjectClassMode", "mainObjectClass", "mainObjectCountMode", "mainObjectFaction",
        "layout", "guardedContentPool", "unguardedContentPool", "resourcesContentPool", "contentCountLimits", "guardedContentValue", "guardedContentValuePerArea",
        "unguardedContentValue", "unguardedContentValuePerArea", "resourcesValue", "resourcesValuePerArea", "progressionPoints", "progressionPointsPerArea",
        "zoneGuardMultiplier", "randomHireEnableWeeklyUnitIncrement", "randomHireInitialUnitIncrement", "diplomacyModifier", "guardCutoffValue", "guardMultiplier",
        "guardMultiplierPerProgressionPoint", "guardRandomization", "guardWeeklyIncrement", "guardReactionDistribution", "encounterHolesSettings", "mandatoryContent",
        "enableWeeklyUnitIncrement", "weekOneOnlyModificatorGrowth", "initialUnitIncrement", "guardChance", "guardValue", "buildingsConstructionSid", "buildingsBanSid",
        "portalShuffleWeights", "defaultZoneClass", "centerZoneClass", "primarySpawnZoneClass", "secondarySpawnZoneClass", "centerSpawnZoneClass",
        "perProgressionPointZoneGuardValue", "borderGuardMultiplier", "zoneGuardWeeklyIncrement", "holdCityMainObjectClass" }.Contains(name, StringComparer.Ordinal);
    private sealed record Selection(string[] Pointer, bool Conditional);
}
