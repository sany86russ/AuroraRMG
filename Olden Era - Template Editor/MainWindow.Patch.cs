using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Export;
using Olden_Era___Template_Editor.Services.GameData;
using KnownValues = OldenEraTemplateEditor.Models.KnownValues;

namespace Olden_Era___Template_Editor;

public partial class MainWindow
{
    private GenerationProfileOptions _simpleProfileOptions = GenerationProfileOptions.CurrentDefaults();
    private GenerationProfileOptions _advancedProfileOptions = new();
    private GenerationProfileOptions ActiveProfileOptions => IsSimpleMode ? _simpleProfileOptions : _advancedProfileOptions;
    private GameCatalog? _patchCatalog;
    private bool _patchHooksReady;
    private bool _patchOperationRunning;
    private GeneratorSettings? _generatedSettingsSnapshot;
    private GeneratorSettings? _loadedGeneratorSnapshot;
    private GeneratorSettings? _loadedSnapshotUiBaseline;
    private Dictionary<string, System.Text.Json.JsonElement>? _loadedSettingsMetadata;

    private SettingsFile RetainSettingsMetadata(SettingsFile file)
    {
        file.Extra = _loadedSettingsMetadata?.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal);
        return file;
    }

    private void InitializePatchControls()
    {
        var menu = new ContextMenu();
        void Add(string key, Func<Task> action)
        {
            var item = new MenuItem(); item.SetResourceReference(HeaderedItemsControl.HeaderProperty, key);
            item.Click += async (_, _) => await RunPatchOperation(action); menu.Items.Add(item);
        }
        Add("S.Patch.Options", OpenGenerationOptions);
        Add("S.Patch.CatalogTitle", OpenGameCatalog);
        Add("S.Patch.Package", ExportTemplatePackage);
        Add("S.Patch.Batch", GenerateTemplateBatch);
        Add("S.Patch.BatchCurrentPresets", () => GenerateTemplateBatch("presets"));
        Add("S.Patch.BatchTeamPresets", () => GenerateTemplateBatch("teams"));
        Add("S.Patch.Diagnostics", ExportDiagnostics);
        Add("S.Patch.Native", OpenNativeSchema);
        Add("S.Patch.NativeImport", ImportNativeConfiguration);
        Add("S.Patch.BalanceTitle", ShowDetailedBalance);
        var button = new Button { Margin = new Thickness(6, 0, 0, 0), ContextMenu = menu, Style = (Style)FindResource("ToolbarButton") };
        button.SetResourceReference(ContentControl.ContentProperty, "S.Patch.Menu");
        button.Click += (_, _) => { menu.PlacementTarget = button; menu.IsOpen = true; };
        if (BtnGitHub.Parent is Panel parent) parent.Children.Add(button);
        foreach (var view in new FrameworkElement[] { SimpleView, AdvancedView })
        {
            view.AddHandler(RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((_, e) => InvalidatePatchResult(e)));
            view.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, e) => { if (e.OriginalSource is not TabControl) InvalidatePatchResult(e); }));
            view.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, e) => InvalidatePatchResult(e)));
            view.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, e) => InvalidatePatchResult(e)));
            view.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, e) => { if (!ReferenceEquals(e.OriginalSource, TxtTemplateName)) InvalidatePatchResult(e); }));
        }
        ChkSimplePortals.SetResourceReference(ToolTipProperty, "S.Patch.PortalsHelp");
        ChkRandomPortals.SetResourceReference(ToolTipProperty, "S.Patch.PortalsHelp");
        Loaded += (_, _) => _patchHooksReady = true;
        CmbSimpleType.SelectionChanged += (_, _) => UpdateSimplePlayerLimits();
        UpdateSimplePlayerLimits();
    }

    private void UpdateSimplePlayerLimits()
    {
        bool solo = CmbSimpleType.SelectedIndex == 5, duel = CmbSimpleType.SelectedIndex == 0;
        SldSimplePlayers.Maximum = solo ? 1 : duel ? 2 : 8;
        SldSimplePlayers.Minimum = solo ? 1 : 2;
        SldSimplePlayers.IsEnabled = !solo && !duel;
    }

    private void InvalidatePatchResult(RoutedEventArgs e)
    {
        if (!_patchHooksReady || _refreshingLists || _isRefreshingMapSizes) return;
        if (ReferenceEquals(e.OriginalSource, ChkSavePreviewImage) || ReferenceEquals(e.OriginalSource, ChkUseGameAssets)) return;
        MarkDirty();
    }

    private bool ValidateSimpleSelections(bool validateSeed = true)
    {
        if (SimpleSelectionError(validateSeed) is not { } error) return true;
        MessageBox.Show(this, error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private string? SimpleSelectionError(bool validateSeed = true)
    {
        if (new[] { CmbSimpleType, CmbSimpleScale, CmbSimpleLength, CmbSimpleChaos, CmbSimpleGuards, CmbSimpleCastleFaction, CmbSimpleVictory }.Any(x => x.SelectedIndex < 0))
            return L.Get("S.Patch.SelectOptions");
        var text = TxtSimpleSeed.Text.Trim();
        if (validateSeed && !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) && !int.TryParse(text, out _))
            return L.Get("S.Patch.InvalidSeed");
        string victory = KnownValues.VictoryConditionIds[CmbSimpleVictory.SelectedIndex];
        if (victory is "win_condition_4" or "win_condition_6" && (int)SldSimplePlayers.Value != 2)
            return L.Get("S.Patch.TwoPlayers");
        return null;
    }

    private async Task<GameCatalog> CurrentPatchCatalog(bool refresh = false)
        => _patchCatalog = await GameCatalogService.Instance.GetCatalogAsync(forceRefresh: refresh);

    private async Task<bool> CanExportGeneratedTemplate()
    {
        if (CurrentGeneratedTemplate is null) return false;
        _patchCatalog = await CurrentPatchCatalog();
        if (CurrentGeneratedTemplate is not { } template) return false;
        var issues = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.StrictExport, _patchCatalog.GetReferenceCatalog());
        var errors = issues.Where(x => x.Severity == TemplateIssueSeverity.Error).ToArray();
        if (errors.Length == 0) return true;
        MessageBox.Show(L.Get("S.Patch.ValidationFailed", string.Join("\n", errors.Select(x => $"{x.Path}: {x.Message}"))),
            L.Get("S.Patch.ValidationTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private async Task RunPatchOperation(Func<Task> action)
    {
        if (_patchOperationRunning) return;
        _patchOperationRunning = true;
        try { await action(); }
        catch (Exception ex) { MessageBox.Show(L.Get("S.Patch.OperationFailed", ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _patchOperationRunning = false; }
    }

    private async Task OpenGenerationOptions()
    {
        var catalog = await CurrentPatchCatalog();
        var source = catalog.Source;
        var projected = ActiveProfileOptions.Clone();
        if (SimpleView.Visibility == Visibility.Visible)
        {
            var text = TxtSimpleSeed.Text.Trim();
            if (uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var seed)) projected.TemplateSeed = unchecked((int)seed);
            else if (int.TryParse(text, out var signed)) projected.TemplateSeed = signed;
        }
        if (SimpleView.Visibility == Visibility.Visible && CmbSimpleType.SelectedIndex == 3) projected.Alliances.Enabled = true;
        var window = new GenerationOptionsWindow(projected, SimpleView.Visibility == Visibility.Visible ? (int)SldSimplePlayers.Value : (int)SldPlayers.Value,
            AppSettings.Current.GameInstallPath, source, catalog.GetReferenceCatalog().CustomAI,
            availableSkills: catalog.Search(kind: GameContentKind.Skill).ToArray()) { Owner = this };
        if (window.ShowDialog() != true) return;
        AppSettings.Current.GameInstallPath = window.ResultGameInstallPath;
        GameInstallationService.Instance.ManualInstallPath = window.ResultGameInstallPath;
        if (SimpleView.Visibility == Visibility.Visible)
        {
            _simpleProfileOptions = window.ResultOptions.Clone();
            ApplySimplePlayerSelection(window.ResultPlayerCount, _simpleProfileOptions.Alliances.Enabled);
            if (_simpleProfileOptions.TemplateSeed is int seed) TxtSimpleSeed.Text = unchecked((uint)seed).ToString("X8");
            SaveSimpleState();
        }
        else
        {
            _advancedProfileOptions = window.ResultOptions.Clone();
            SldPlayers.Value = window.ResultPlayerCount;
            AppSettings.Current.Save();
        }
        MarkDirty(); _patchCatalog = null;
    }

    private void ApplySimplePlayerSelection(int players, bool alliances)
    {
        if (players == 1) CmbSimpleType.SelectedIndex = 5;
        else if (alliances) CmbSimpleType.SelectedIndex = 3;
        else if (CmbSimpleType.SelectedIndex is 3 or 5 || CmbSimpleType.SelectedIndex == 0 && players != 2)
            CmbSimpleType.SelectedIndex = 1;
        UpdateSimplePlayerLimits();
        SldSimplePlayers.Value = players;
    }

    private async Task OpenGameCatalog()
    {
        var window = new GameContentCatalogWindow(await CurrentPatchCatalog()) { Owner = this };
        window.ShowDialog();
    }

    private async Task OpenNativeSchema()
    {
        var source = (await CurrentPatchCatalog()).Source;
        var window = new NativeSchemaEditorWindow(source?.SchemaPath, source?.InstallPath) { Owner = this };
        window.ShowDialog();
    }

    private async Task ExportTemplatePackage()
    {
        if (CurrentGeneratedTemplate is null) { MessageBox.Show(this, L.Get("S.Patch.NeedsGenerated"), Title); return; }
        if (!await CanExportGeneratedTemplate()) return;
        var dialog = new SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = CurrentGeneratedTemplate!.Name + ".zip" };
        if (dialog.ShowDialog(this) != true) return;
        var catalog = await CurrentPatchCatalog();
        if (CurrentGeneratedTemplate is not { } template || _generatedSettingsSnapshot is null) { MessageBox.Show(L.Get("S.Patch.NeedsGenerated"), Title); return; }
        var templateSnapshot = Services.TemplateRefactor.CreateEditingCopy(template);
        var inputSnapshot = Services.SettingsSnapshotMapper.Clone(_generatedSettingsSnapshot);
        byte[]? preview = null;
        bool includeSettings = MessageBox.Show(this, L.Get("S.Patch.OptionalSettings"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (MessageBox.Show(this, L.Get("S.Patch.OptionalPreview"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            preview = TemplatePreviewPngWriter.Encode(GetGeneratedPreview(template));
        var request = new TemplatePackageRequest(templateSnapshot, Services.SettingsSnapshotMapper.FromGeneratorSettings(inputSnapshot))
        { TemplateSeed = inputSnapshot.Seed, GameSeed = inputSnapshot.ProfileOptions.GameSeed, AlgorithmVersion = inputSnapshot.ProfileOptions.AlgorithmVersion,
            Profile = catalog.Source?.Profile, ValidationCatalog = catalog.GetReferenceCatalog(), PreviewPng = preview, IncludeSettings = includeSettings };
        var receipt = await TemplatePackageService.CreateAsync(dialog.FileName, request);
        MessageBox.Show(L.Get("S.Patch.Saved", receipt.Path), Title);
    }

    private async Task ExportDiagnostics()
    {
        var dialog = new SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = "AuroraRMG-diagnostics.zip" };
        if (dialog.ShowDialog(this) != true) return;
        var catalog = await CurrentPatchCatalog();
        string[] logs = [];
        if (MessageBox.Show(this, L.Get("S.Patch.OptionalLogs"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var selected = new OpenFileDialog { Multiselect = true, Filter = "Logs (*.log;*.txt;*.jsonl)|*.log;*.txt;*.jsonl" };
            if (selected.ShowDialog(this) == true) logs = selected.FileNames;
        }
        var template = CurrentGeneratedTemplate;
        var input = template is null ? null : _generatedSettingsSnapshot;
        var request = new DiagnosticBundleRequest { Template = template, Settings = input is null ? GatherSettings() : Services.SettingsSnapshotMapper.FromGeneratorSettings(input), Installation = catalog.Source,
            TemplateSeed = input?.Seed ?? ActiveProfileOptions.TemplateSeed, AlgorithmVersion = input?.ProfileOptions.AlgorithmVersion ?? ActiveProfileOptions.AlgorithmVersion,
            GameSeed = input is null ? ActiveProfileOptions.GameSeed : input.ProfileOptions.GameSeed,
            SelectedLogPaths = logs,
            ValidationIssues = template is null ? [] : TemplateCompatibilityValidator.Validate(template, catalog: catalog.GetReferenceCatalog()) };
        var receipt = await DiagnosticBundleService.CreateAsync(dialog.FileName, request);
        MessageBox.Show(L.Get("S.Patch.Saved", receipt.Path) + "\n" + L.Get("S.Patch.DiagnosticReceipt", receipt.Redactions,
            receipt.Logs.Count(l => l.Truncated), receipt.Logs.Count(l => l.Error is not null))
            + (receipt.Omissions.Count == 0 ? "" : "\n" + L.Get("S.Patch.DiagnosticOmissions", receipt.Omissions.Count)) + "\n"
            + string.Join("\n", receipt.Logs.Where(l => l.Error is not null).Select(l => l.Error)), Title);
    }

    private Task GenerateTemplateBatch() => GenerateTemplateBatch("current");

    private async Task GenerateTemplateBatch(string source)
    {
        var value = AskPatchText("S.Patch.Batch", source == "presets" ? "1, 2" : "1, 2, 3, 4");
        if (value is null) return;
        var seeds = value.Split([',', ';', ' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var folder = new OpenFolderDialog { Title = L.Get("S.Patch.Batch") };
        if (folder.ShowDialog(this) != true) return;
        QuickGenerateOptions? quickOptions = null;
        if (source == "current" && SimpleView.Visibility == Visibility.Visible)
        {
            if (!ValidateSimpleSelections(validateSeed: false)) return;
            quickOptions = BuildQuickOptions(seeds.FirstOrDefault());
        }
        GeneratorSettings[] configurations = source switch
        {
            "presets" => PatchPresets.Current.Select(p => Presets.ToGeneratorSettings(p.Settings)).ToArray(),
            "teams" => PatchPresets.Cooperative.Select(p => Presets.ToGeneratorSettings(p.Settings)).ToArray(),
            _ => quickOptions is null ? [BuildSettings()] : []
        };
        int total = checked(seeds.Length * (quickOptions is null ? configurations.Length : 1));
        if (total > TemplateBatchService.MaximumCases) throw new ArgumentException(L.Get("S.Patch.BatchLimit", TemplateBatchService.MaximumCases));
        var catalog = (await CurrentPatchCatalog()).GetReferenceCatalog();
        using var cancellation = new CancellationTokenSource();
        var progressWindow = new Window { Owner = this, Title = L.Get("S.Patch.Batch"), Width = 520, Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        progressWindow.SetResourceReference(BackgroundProperty, "BrushBackground"); progressWindow.SetResourceReference(ForegroundProperty, "BrushText");
        var progressPanel = new StackPanel { Margin = new Thickness(20) };
        var caption = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Minimum = 0, Maximum = total, Height = 18, Margin = new Thickness(0, 14, 0, 12) };
        var cancel = new Button { Content = L.Get("S.Patch.Cancel"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
        cancel.Click += (_, _) => { cancellation.Cancel(); cancel.IsEnabled = false; };
        progressWindow.Closing += (_, _) => cancellation.Cancel();
        progressPanel.Children.Add(caption); progressPanel.Children.Add(bar); progressPanel.Children.Add(cancel); progressWindow.Content = progressPanel;
        progressWindow.Show(); IsEnabled = false;
        var cases = new List<TemplateBatchCaseResult>();
        bool cancelled = false;
        try
        {
            if (quickOptions is not null)
            {
                var progress = new Progress<TemplateBatchProgress>(p =>
                {
                    bar.Value = p.Completed;
                    caption.Text = L.Get("S.Patch.BatchProgress", p.Completed, total, p.Case.Seed);
                });
                var part = await TemplateBatchService.GenerateQuickAsync(new(quickOptions, seeds, folder.FolderName)
                {
                    ValidationCatalog = catalog
                }, cancellation.Token, progress);
                cases.AddRange(part.Cases);
                cancelled = part.Cancelled;
            }
            foreach (var settings in configurations)
            {
                if (cancellation.IsCancellationRequested) { cancelled = true; break; }
                int completedBefore = cases.Count;
                var progress = new Progress<TemplateBatchProgress>(p => { bar.Value = completedBefore + p.Completed; caption.Text = L.Get("S.Patch.BatchProgress", completedBefore + p.Completed, total, p.Case.Seed); });
                var part = await TemplateBatchService.GenerateAsync(new(settings, seeds, folder.FolderName) { ValidationCatalog = catalog }, cancellation.Token, progress);
                cases.AddRange(part.Cases); if (part.Cancelled) { cancelled = true; break; }
            }
        }
        finally { IsEnabled = true; progressWindow.Close(); }
        var receipt = new TemplateBatchReceipt(cases, cancelled);
        MessageBox.Show(L.Get("S.Patch.BatchResult", receipt.SuccessfulCount, receipt.FailedCount, receipt.Cancelled)
            + "\n" + string.Join("\n", receipt.Cases.Where(x => !x.Success).Select(x => x.Error)), L.Get("S.Patch.Batch"));
    }

    private Task ImportNativeConfiguration()
    {
        var dialog = new OpenFileDialog { Filter = "Game settings (*.json;*.txt)|*.json;*.txt", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return Task.CompletedTask;
        if (new FileInfo(dialog.FileName).Length > 1024 * 1024) throw new InvalidDataException("The configuration exceeds 1 MiB.");
        var text = File.ReadAllText(dialog.FileName).Trim();
        var configuration = text.StartsWith('{') ? Services.NativeSchema.NativeConfigurationCodec.ParseJson(text) : Services.NativeSchema.NativeConfigurationCodec.Decode(text);
        if (IsSimpleMode && !ValidateSimpleSelections()) return Task.CompletedTask;
        var quick = IsSimpleMode ? BuildQuickOptions() : null;
        var input = quick is null ? BuildSettings() : Services.Generation.RandomTemplateBuilder.Build(quick);
        var imported = Services.NativeSchema.NativeConfigurationImporter.Import(configuration, input, quick);
        imported.Settings.ProfileOptions.ImportedNativeConfiguration = imported.RawConfiguration;
        imported.Settings.ProfileOptions.ImportedNativeSchema = imported.RawSchema is { } schema
            ? JsonSerializer.SerializeToElement(schema) : null;
        if (!ResolveUnsavedSettings(simple: false)) return Task.CompletedTask;
        ReplaceSettings(Services.SettingsSnapshotMapper.FromGeneratorSettings(imported.Settings), path: null, dirty: true);
        MessageBox.Show(this, L.Get("S.Patch.ImportWarning", string.Join("\n", imported.Warnings.Select(x => x.Message))), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    private Task ShowDetailedBalance()
    {
        if (CurrentGeneratedTemplate is not { } template) { MessageBox.Show(L.Get("S.Patch.NeedsGenerated"), Title); return Task.CompletedTask; }
        var report = TemplateBalanceReport.Analyze(template);
        var text = FormatAnalysis(template) + "\n\n" + string.Join("\n", report.Players.Select(p =>
            L.Get("S.Patch.BalancePlayer", p.SpawnId, p.AllianceKey, p.StartingCities, p.StartingResourceValue, p.StartingAreaCoefficient, p.NearestOpponentHops, p.ClosestCastleGuardBudget?.ToString() ?? "—")
            + "\n" + L.Get("S.Patch.BalanceTreasure", p.TreasureHops?.ToString() ?? "—", p.ClosestTreasureGuardBudget?.ToString() ?? "—")))
            + "\n\n" + string.Join("\n", report.Teams.Select(t => L.Get("S.Patch.BalanceTeam", t.AllianceKey, string.Join(", ", t.Players), t.StartingCities, t.StartingResources, t.StartingAreaCoefficient, t.NearestOpponentHops)))
            + "\n\n" + L.Get("S.Patch.BalancePortalHelp") + "\n" + string.Join("\n", report.Portals.Select(p =>
                L.Get("S.Patch.BalancePortal", p.Name, p.From, p.To, p.FromEnabled.HasValue ? L.Get(p.FromEnabled.Value ? "S.Patch.Yes" : "S.Patch.No") : L.Get("S.Patch.Default"),
                    p.ToEnabled.HasValue ? L.Get(p.ToEnabled.Value ? "S.Patch.Yes" : "S.Patch.No") : L.Get("S.Patch.Default"), p.GuardBudget)));
        var window = new Window { Owner = this, Title = L.Get("S.Patch.BalanceTitle"), Width = 900, Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
            Content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(18) } };
        window.SetResourceReference(BackgroundProperty, "BrushBackground"); window.SetResourceReference(ForegroundProperty, "BrushText");
        window.ShowDialog(); return Task.CompletedTask;
    }

    private string? AskPatchText(string titleKey, string initial)
    {
        var window = new Window { Owner = this, Title = L.Get(titleKey), Width = 580, Height = 270, MinWidth = 440, MinHeight = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        window.SetResourceReference(BackgroundProperty, "BrushBackground"); window.SetResourceReference(ForegroundProperty, "BrushText");
        var panel = new DockPanel { Margin = new Thickness(16) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = L.Get("S.Patch.Cancel"), IsCancel = true, MinWidth = 90 };
        var ok = new Button { Content = L.Get("S.Patch.OK"), IsDefault = true, MinWidth = 90, Margin = new Thickness(12, 0, 0, 0) };
        ok.Click += (_, _) => window.DialogResult = true; footer.Children.Add(cancel); footer.Children.Add(ok);
        DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        var input = new TextBox { Text = initial, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(input); window.Content = panel;
        return window.ShowDialog() == true ? input.Text : null;
    }
}
