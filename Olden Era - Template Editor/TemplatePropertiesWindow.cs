using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Localization;
using OldenEraTemplateEditor.Models;
using Orientation = System.Windows.Controls.Orientation;

namespace Olden_Era___Template_Editor;

/// <summary>Transactional root properties, alliance metadata, rules, description blocks and variant rivers.</summary>
public sealed class TemplatePropertiesWindow : Window
{
    private readonly TemplatePropertiesDraft _draft;
    private readonly JsonObject _initial;
    private readonly List<Action<JsonObject>> _commit = [];
    private readonly List<AllianceRow> _allianceRows = [];
    private readonly StackPanel _alliancePanel = new();
    private readonly CheckBox _useAlliances = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.OrangeRed };
    public RmgTemplate? ResultTemplate { get; private set; }
    public IReadOnlyList<TemplateValidationIssue> ValidationIssues { get; private set; } = [];

    public TemplatePropertiesWindow(RmgTemplate source)
    {
        _draft = new(source); _initial = _draft.Snapshot();
        Width = 820; Height = 750; MinWidth = 600; MinHeight = 420; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(TitleProperty, "S.TemplateProperties.Title");
        SetResourceReference(BackgroundProperty, "BrushBackground"); SetResourceReference(ForegroundProperty, "BrushText");
        var root = new DockPanel { Margin = new Thickness(16) }; Content = root;
        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("S.Patch.Cancel"); cancel.IsCancel = true; actions.Children.Add(cancel);
        var apply = Button("S.Patch.OK"); apply.IsDefault = true; apply.Click += (_, _) => Apply(); actions.Children.Add(apply); footer.Children.Add(actions);
        var tabs = new TabControl(); root.Children.Add(tabs);
        var general = Tab(tabs, "S.TemplateProperties.General");
        AddFields<RmgTemplate>(general, [], ["name", "gameMode", "description", "displayWinCondition", "sizeX", "sizeZ", "maxAlliancesCount"]);
        var teams = Tab(tabs, "S.Patch.Teams"); Help(teams, "S.Patch.TeamHelp");
        _useAlliances.SetResourceReference(ContentControl.ContentProperty, "S.TemplateProperties.UseAlliances");
        _useAlliances.IsChecked = _initial["spawnInfos"] is JsonArray; _useAlliances.Margin = new Thickness(0, 8, 0, 8); teams.Children.Add(_useAlliances);
        var originalTeams = _initial["spawnInfos"] as JsonArray;
        if (originalTeams?.Any(node => node is not JsonObject) == true)
        {
            // Malformed/unknown rows remain inspectable and must never disappear because a typed row cannot show them.
            AddRaw(teams, [], "spawnInfos", "S.TemplateProperties.SpawnJson");
            _useAlliances.IsEnabled = false;
        }
        else
        {
            teams.Children.Add(_alliancePanel);
            foreach (var row in originalTeams?.OfType<JsonObject>() ?? []) AddAlliance(row.DeepClone().AsObject());
            var add = Button("S.TemplateProperties.AddPlayer"); add.Click += (_, _) =>
            {
                var taken = _allianceRows.Select(row => row.Spawn.Text).ToHashSet(StringComparer.Ordinal);
                var next = KnownValues.SpawnPlayers.FirstOrDefault(spawn => !taken.Contains(spawn));
                if (next is not null) { AddAlliance(new JsonObject { ["spawn"] = next, ["allianceIndex"] = -1 }); _useAlliances.IsChecked = true; }
            }; teams.Children.Add(add);
            _commit.Add(candidate =>
            {
                if (_useAlliances.IsChecked != true) { Set(candidate, [], "spawnInfos", null); return; }
                var rows = new JsonArray();
                foreach (var row in _allianceRows)
                {
                    var value = row.Original.DeepClone().AsObject();
                    value["spawn"] = row.Spawn.Text;
                    if (string.IsNullOrWhiteSpace(row.Alliance.Text)) value.Remove("allianceIndex");
                    else
                    {
                        int alliance = Integer(row.Alliance.Text, "allianceIndex");
                        if (alliance is < -1 or > 8 || alliance == 0) throw ValueError("allianceIndex");
                        value["allianceIndex"] = alliance;
                    }
                    if (row.Locked.IsChecked.HasValue) value["isAllianceLocked"] = row.Locked.IsChecked.Value;
                    else value.Remove("isAllianceLocked");
                    rows.Add(value);
                }
                Set(candidate, [], "spawnInfos", rows);
            });
        }
        var rules = Tab(tabs, "S.TemplateProperties.Rules"); Help(rules, "S.TemplateProperties.NullHelp");
        AddFields<GameRules>(rules, ["gameRules"]); AddRaw(rules, ["gameRules"], "bonuses", "S.TemplateProperties.Bonuses");
        var victory = Tab(tabs, "S.TemplateProperties.Victory"); Help(victory, "S.TemplateProperties.CalendarHelp");
        AddFields<WinConditions>(victory, ["gameRules", "winConditions"]);
        AddRaw(victory, ["gameRules", "winConditions"], "tournamentAnnounceDays", "S.TemplateProperties.Announcements");
        AddRaw(victory, ["gameRules", "winConditions"], "tournamentDays", "S.TemplateProperties.Durations");
        var card = Tab(tabs, "S.TemplateProperties.Card"); Help(card, "S.TemplateProperties.JsonHelp");
        AddRaw(card, [], "descBlocks", "S.TemplateProperties.CardJson");
        var rivers = Tab(tabs, "S.TemplateProperties.Rivers"); Help(rivers, "S.TemplateProperties.NullHelp");
        var variants = _initial["variants"] as JsonArray;
        for (var index = 0; index < (variants?.Count ?? 0); index++)
        {
            var caption = Label("S.TemplateProperties.Variant"); caption.FontWeight = FontWeights.Bold; caption.Margin = new Thickness(0, 10, 0, 6);
            caption.Text = LocalizationManager.T("S.TemplateProperties.Variant", index + 1); rivers.Children.Add(caption);
            AddFields<RiverSettings>(rivers, ["variants", index.ToString(CultureInfo.InvariantCulture), "river"]);
        }
        Loaded += (_, _) => { Width = Math.Min(Width, SystemParameters.WorkArea.Width); Height = Math.Min(Height, SystemParameters.WorkArea.Height); };
    }

    private void AddFields<T>(StackPanel panel, string[] path, string[]? only = null)
    {
        foreach (var property in typeof(T).GetProperties())
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (name is null || only is not null && !only.Contains(name, StringComparer.Ordinal)) continue;
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type != typeof(string) && type != typeof(bool) && type != typeof(int) && type != typeof(double)) continue;
            var original = Resolve(_initial, path)?[name];
            FrameworkElement editor;
            if (type == typeof(bool))
            {
                var box = new CheckBox { IsThreeState = true, IsChecked = original?.GetValue<bool>(), VerticalAlignment = VerticalAlignment.Center };
                box.SetResourceReference(ToolTipProperty, "S.TemplateProperties.NullHelp"); editor = box;
                _commit.Add(candidate => Set(candidate, path, name, box.IsChecked.HasValue ? JsonValue.Create(box.IsChecked.Value) : null));
            }
            else
            {
                var box = new TextBox { Text = original is null ? "" : type == typeof(string) ? original.GetValue<string>() : original.ToJsonString(), MinHeight = 28 };
                if (name == "description") { box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.MinHeight = 70; }
                editor = box;
                bool optional = Nullable.GetUnderlyingType(property.PropertyType) is not null || type == typeof(string) && name != "name";
                string initialText = box.Text;
                _commit.Add(candidate => TemplatePropertiesDraft.ApplyTextEdit(candidate, path, name, initialText, box.Text, currentText =>
                {
                    // An unchanged blank may represent an explicit empty string, a null or an absent field.
                    // Keep the original node rather than normalizing all three states to removal.
                    JsonNode? value;
                    if (currentText.Length == 0 && optional) value = null;
                    else if (type == typeof(string))
                    {
                        if (name == "name" && string.IsNullOrWhiteSpace(currentText)) throw ValueError(name);
                        value = JsonValue.Create(currentText);
                    }
                    else if (type == typeof(int)) value = JsonValue.Create(Integer(currentText, name));
                    else
                    {
                        if (!double.TryParse(currentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw ValueError(name);
                        value = JsonValue.Create(number);
                    }
                    return value;
                }));
            }
            Row(panel, "S.TemplateProperties.Field." + name, editor);
        }
    }

    private void AddRaw(StackPanel panel, string[] path, string property, string caption)
    {
        panel.Children.Add(Label(caption));
        var box = new TextBox { Text = Resolve(_initial, path)?[property]?.ToJsonString(JsonExport.Options) ?? "null", FontFamily = new FontFamily("Consolas"), FontSize = 12,
            AcceptsReturn = true, AcceptsTab = true, MinHeight = 130, TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 5, 0, 12) };
        panel.Children.Add(box);
        _commit.Add(candidate =>
        {
            TemplateJsonReader.GuardBeforeDeserialize(box.Text);
            Set(candidate, path, property, JsonNode.Parse(box.Text));
        });
    }

    private void AddAlliance(JsonObject original)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        foreach (var width in new[] { 150, 120, 100, 85 }) grid.ColumnDefinitions.Add(new() { Width = new GridLength(width) });
        var spawn = new TextBox { Text = original["spawn"]?.GetValue<string>() ?? "", MinHeight = 28 };
        var alliance = new TextBox { Text = original["allianceIndex"]?.ToJsonString() ?? "", MinHeight = 28, Margin = new Thickness(8, 0, 8, 0) };
        var locked = new CheckBox { IsThreeState = true, IsChecked = original["isAllianceLocked"]?.GetValue<bool>(), VerticalAlignment = VerticalAlignment.Center };
        locked.SetResourceReference(ContentControl.ContentProperty, "S.TemplateProperties.Locked");
        var remove = Button("S.TemplateProperties.Remove"); remove.MinWidth = 60;
        var row = new AllianceRow(original, spawn, alliance, locked); _allianceRows.Add(row);
        remove.Click += (_, _) => { _allianceRows.Remove(row); _alliancePanel.Children.Remove(grid); };
        grid.Children.Add(spawn); Grid.SetColumn(alliance, 1); grid.Children.Add(alliance); Grid.SetColumn(locked, 2); grid.Children.Add(locked); Grid.SetColumn(remove, 3); grid.Children.Add(remove);
        _alliancePanel.Children.Add(grid);
    }

    private void Apply()
    {
        try
        {
            var candidate = _draft.Snapshot(); foreach (var commit in _commit) commit(candidate);
            ResultTemplate = _draft.Commit(candidate, out var findings); ValidationIssues = findings;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        { _error.Text = LocalizationManager.T("S.TemplateProperties.Error", ex.Message); }
    }

    private void Set(JsonObject candidate, string[] path, string property, JsonNode? value)
    {
        if (JsonNode.DeepEquals(Resolve(_initial, path)?[property], value)) return;
        JsonNode current = candidate;
        foreach (string part in path)
        {
            if (current is JsonObject obj) { obj[part] ??= new JsonObject(); current = obj[part]!; }
            else if (current is JsonArray array) current = array[int.Parse(part, CultureInfo.InvariantCulture)] ?? throw ValueError(property);
            else throw ValueError(property);
        }
        if (current is not JsonObject target) throw ValueError(property);
        if (value is null) target.Remove(property); else target[property] = value;
    }

    private static JsonObject? Resolve(JsonObject root, string[] path)
    {
        JsonNode? current = root;
        foreach (string part in path)
            current = current switch { JsonObject obj => obj[part], JsonArray array => array[int.Parse(part, CultureInfo.InvariantCulture)], _ => null };
        return current as JsonObject;
    }
    private static int Integer(string text, string property) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : throw ValueError(property);
    private static Exception ValueError(string property) => new ArgumentException(LocalizationManager.T("S.TemplateProperties.Invalid", property));
    private static StackPanel Tab(TabControl tabs, string key)
    { var panel = new StackPanel { Margin = new Thickness(12) }; tabs.Items.Add(new TabItem { Header = Label(key), Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }); return panel; }
    private static TextBlock Label(string key)
    { var text = new TextBlock { TextWrapping = TextWrapping.Wrap }; text.SetResourceReference(TextBlock.TextProperty, key); return text; }
    private static void Help(StackPanel panel, string key)
    { var text = Label(key); text.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(text); }
    private static void Row(StackPanel panel, string key, FrameworkElement editor)
    { var row = new Grid { Margin = new Thickness(0, 4, 0, 4) }; row.ColumnDefinitions.Add(new() { Width = new GridLength(280) }); row.ColumnDefinitions.Add(new());
      var caption = Label(key); caption.VerticalAlignment = VerticalAlignment.Center; caption.Margin = new Thickness(0, 0, 12, 0); row.Children.Add(caption); Grid.SetColumn(editor, 1); row.Children.Add(editor); panel.Children.Add(row); }
    private static Button Button(string key)
    { var button = new Button { MinHeight = 30, MinWidth = 105, Margin = new Thickness(6, 0, 0, 0) }; button.SetResourceReference(ContentControl.ContentProperty, key); return button; }
    private sealed record AllianceRow(JsonObject Original, TextBox Spawn, TextBox Alliance, CheckBox Locked);
}
