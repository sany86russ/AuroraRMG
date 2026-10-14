using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor;

public partial class MainWindow
{
    private void InitializePatchHarness(string[] arguments)
    {
        int index = Array.FindIndex(arguments, a => a is "--shoot-patch" or "--make-patch-kit");
        if (index < 0) return;
        if (index + 1 >= arguments.Length) throw new ArgumentException("The verification command requires an output directory.");
        ShowActivated = false; WindowStartupLocation = WindowStartupLocation.Manual; Left = -6000; Top = 100;
        string command = arguments[index], directory = Path.GetFullPath(arguments[index + 1]);
        ContentRendered += async (_, _) =>
        {
            try
            {
                if (command == "--make-patch-kit")
                {
                    var receipt = await PatchAcceptanceSuite.RunAsync(directory, await CurrentPatchCatalog(true));
                    if (!receipt.OfflinePassed) throw new InvalidOperationException("Offline acceptance did not pass.");
                }
                else await VerifyPatchUiAsync(directory);
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDataPaths.Root, "verification-error.txt"), ex.ToString());
                Application.Current.Shutdown(1);
            }
        };
    }

    private async Task VerifyPatchUiAsync(string directory)
    {
        Services.Export.ExportDestination.EnsureOutsideGame(directory);
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var catalog = await CurrentPatchCatalog(true);
        CmbSimpleType.SelectedIndex = 0; ApplySimplePlayerSelection(5, false);
        Require(CmbSimpleType.SelectedIndex == 1 && SldSimplePlayers.Value == 5, "Duel options silently clamped the new player count.");
        CmbSimpleType.SelectedIndex = 5; ApplySimplePlayerSelection(2, false);
        Require(CmbSimpleType.SelectedIndex == 1 && SldSimplePlayers.Value == 2, "Solo options silently clamped the new player count.");
        checks.Add("Simple: options can change Duel to five players and Solo to two without clamping.");
        var manualOptions = GenerationProfileOptions.CurrentDefaults();
        manualOptions.Alliances = new() { Enabled = true, Preset = "auto", Slots = [new() { Player = 1, Alliance = 1, Locked = true }, new() { Player = 2, Alliance = 2, Locked = false }] };
        var manualDialog = new GenerationOptionsWindow(manualOptions, 2);
        manualDialog.CommitDraft();
        Require(manualDialog.ResultOptions.Alliances.Slots.Count == 2 && manualDialog.ResultOptions.Alliances.Slots[0].Locked && !manualDialog.ResultOptions.Alliances.Slots[1].Locked,
            "Unchanged options lost manual slots or individual alliance locks.");
        manualDialog.Close();
        checks.Add("Options: unchanged manual assignments retain individual alliance locks.");
        var inactivePreset = GenerationProfileOptions.CurrentDefaults(); inactivePreset.Alliances.Preset = "2v2";
        var inactiveDialog = new GenerationOptionsWindow(inactivePreset, 5); inactiveDialog.CommitDraft();
        Require(inactiveDialog.ResultPlayerCount == 5, "Opening options applied an inactive team preset to the player count.");
        inactiveDialog.Close();
        checks.Add("Options: an inactive team preset does not change five players on open/apply.");
        var automaticOptions = GenerationProfileOptions.CurrentDefaults(); automaticOptions.TemplateSeed = 1729; automaticOptions.Alliances.Enabled = true;
        var automaticSlots = Services.Generation.AlliancePlan.Resolve(5, automaticOptions.Alliances, 1729).Slots;
        var automaticDialog = new GenerationOptionsWindow(automaticOptions, 5); automaticDialog.DebugSetAllianceSlot(1, -1); automaticDialog.CommitDraft();
        Require(automaticDialog.ResultOptions.Alliances.Slots.Count == 5 && automaticDialog.ResultOptions.Alliances.Slots[0].Alliance == -1
            && automaticDialog.ResultOptions.Alliances.Slots.Skip(1).All(slot => automaticSlots.Single(original => original.Player == slot.Player).Alliance == slot.Alliance),
            "Editing one automatic slot discarded other players' assignments.");
        automaticDialog.Close();
        checks.Add("Options: editing one automatic assignment retains the other four players.");
        var bonusSettings = new GeneratorSettings { Seed = 1729, ProfileOptions = GenerationProfileOptions.CurrentDefaults(), HeroSettings = new() { HeroCountMin = 1, HeroCountMax = 10, HeroCountIncrement = 1 }, Bonuses = [new() { PresetType = OldenEraTemplateEditor.Models.BonusPresetType.StartingGold, Param = "1500" }] };
        var bonusTemplate = TemplateGenerator.Generate(bonusSettings);
        var bonusSnapshot = SettingsSnapshotMapper.FromGeneratorSettings(bonusSettings);
        Require(JsonSerializer.Serialize(bonusTemplate, JsonExport.Options) == JsonSerializer.Serialize(TemplateGenerator.Generate(Presets.ToGeneratorSettings(bonusSnapshot)), JsonExport.Options),
            "Real WPF bonuses cannot be generated or reproduced in Balanced tempo.");
        checks.Add("WPF bonus: Balanced tempo and restored settings produce identical JSON.");
        _profileOptions = GenerationProfileOptions.CurrentDefaults();
        CmbSimpleType.SelectedIndex = 3; SldSimplePlayers.Value = 5; TxtSimpleSeed.Text = "00ABC123";
        _profileOptions.Alliances = new() { Enabled = true, Preset = "2v2v1", Layout = AllianceLayout.TeamRegions };
        SetMode(false); BtnSimpleGenerate_Click(this, new());
        Require(_generatedTemplate is not null && !_templateOutdated && _generatedSettingsSnapshot is not null, "Simple generation did not finish.");
        Require(_generatedTemplate!.SpawnInfos?.Count == 5, "Odd team assignment was lost in Simple.");
        TxtTemplateName.Text = "Patch UI verification";
        Require(!_templateOutdated && _generatedSettingsSnapshot!.TemplateName == _generatedTemplate.Name && _lastQuickSettings?.TemplateName == _generatedTemplate.Name,
            "Renaming a generated template did not update its reproducible input.");
        var generated = _generatedSettingsSnapshot!;
        var file = SettingsSnapshotMapper.FromGeneratorSettings(generated);
        var regenerated = TemplateGenerator.Generate(Presets.ToGeneratorSettings(file));
        Require(JsonSerializer.Serialize(regenerated, JsonExport.Options) == JsonSerializer.Serialize(_generatedTemplate, JsonExport.Options), "Captured Simple settings do not reproduce the template.");
        checks.Add("Simple: 5 players, 2v2v1 teams, full settings snapshot reproduces JSON.");
        checks.Add("Name: cosmetic renaming updates the template, snapshot and Advanced input.");
        await CaptureMainPatchAsync(directory, "main-simple");
        ChkSimplePortals.IsChecked = !ChkSimplePortals.IsChecked;
        Require(_templateOutdated && !BtnSimpleSave.IsEnabled && !BtnSimpleSaveToGame.IsEnabled && !await CanExportGeneratedTemplate(), "Simple stale export was not blocked.");
        checks.Add("Simple: changing portals marks output stale and blocks both export paths.");
        ApplySettings(file); SetMode(true);
        var restored = BuildSettings();
        Require(JsonSerializer.Serialize(TemplateGenerator.Generate(restored), JsonExport.Options) == JsonSerializer.Serialize(regenerated, JsonExport.Options), "Opening the captured settings changed generation.");
        checks.Add("Advanced: opening captured settings retains the complete generator input.");
        _profileOptions.TemplateSeed = 424242;
        var changedSeedFile = GatherSettings();
        Require(changedSeedFile.Seed == 424242 && SettingsSnapshotMapper.RestoreSnapshot(changedSeedFile)?.Seed == 424242,
            "Advanced saved a different seed in its legacy fields and canonical snapshot.");
        ApplySettings(file);
        BtnPreview_Click(this, new());
        Require(!_templateOutdated && _generatedTemplate is not null && ImgPreview.Source is not null,
            "Advanced generation did not produce a current preview.");
        Require(JsonSerializer.Serialize(_generatedTemplate, JsonExport.Options) == JsonSerializer.Serialize(regenerated, JsonExport.Options),
            "Advanced generation changed the captured Simple input.");
        checks.Add("Advanced: restored inputs generate the same current template and preview.");
        await CaptureMainPatchAsync(directory, "main-advanced");
        await CapturePatchWindowAsync(new GenerationOptionsWindow(generated.ProfileOptions, 5, AppSettings.Current.GameInstallPath, catalog.Source,
            catalog.GetReferenceCatalog().CustomAI), directory, "options");
        var rewardObject = catalog.Search("", GameContentKind.Object, true).Where(entry => entry.RewardVariants.Count > 0)
            .OrderByDescending(entry => entry.Sid.Contains("scroll", StringComparison.OrdinalIgnoreCase)).First();
        await CapturePatchWindowAsync(new GameContentCatalogWindow(catalog), directory, "catalog", window => ((GameContentCatalogWindow)window).DebugSelectRewardObject(rewardObject.Sid));
        checks.Add("Catalog: RMG-only object search selects the actual configured reward variants.");
        await CapturePatchWindowAsync(new NativeSchemaEditorWindow(catalog.Source?.SchemaPath, catalog.Source?.InstallPath), directory, "native",
            window => ((NativeSchemaEditorWindow)window).DebugSelectConditionalParameter());
        checks.Add("Native editor: selecting the actual conditional row fills controls without changing source JSON.");
        await CapturePatchWindowAsync(new TemplatePropertiesWindow(regenerated), directory, "properties");
        await CapturePatchWindowAsync(new TemplateBulkEditWindow(regenerated), directory, "bulk");
        var examplePool = new OldenEraTemplateEditor.Models.GamePool { Name = "UI verification pool", Groups = [new() { Weight = 1.5, Content = [new() { Sid = rewardObject.Sid, Weight = .25, Variant = -1 }] }] };
        await CapturePatchWindowAsync(new ContentPoolCreatorWindow(regenerated, examplePool), directory, "pool-creator");
        await CapturePatchWindowAsync(new TemplateEditorWindow(regenerated), directory, "editor", window =>
        {
            ((TemplateEditorWindow)window).DebugVerifySavedInspectorEditing();
            ((TemplateEditorWindow)window).DebugVerifyJsonFieldDuplicateRejection();
        });
        checks.Add("Editor: committing the saved copy retains live inspector bindings for subsequent edits.");
        checks.Add("Editor: duplicate raw portal JSON is rejected; correcting it clears the save blocker.");
        using (var stream = File.Create(Path.Combine(directory, "teams-preview.png")))
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(TemplatePreviewPngWriter.Render(regenerated, generated.Topology))); encoder.Save(stream);
        }
        checks.Add("WPF: all new dialogs rendered off screen in the active language.");
        File.WriteAllLines(Path.Combine(directory, "ui-verification.txt"), checks);
    }

    private async Task CaptureMainPatchAsync(string directory, string name)
    {
        var root = (FrameworkElement)Content;
        await SettlePatchLayoutAsync(root);
        if (name == "main-advanced")
        {
            var previewContainer = (FrameworkElement)VisualTreeHelper.GetParent(ImgPreview);
            Require(ImgPreview.ActualWidth > 0 && ImgPreview.ActualHeight > 0
                && ImgPreview.ActualWidth <= previewContainer.ActualWidth + 1 && ImgPreview.ActualHeight <= previewContainer.ActualHeight + 1,
                "The Advanced preview exceeds its available area.");
        }
        CapturePatchRoot(root, directory, name);
    }

    private static async Task SettlePatchLayoutAsync(FrameworkElement root)
    {
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        foreach (var element in VisualDescendants(root).OfType<FrameworkElement>().Prepend(root).Where(element => element.IsVisible))
            element.InvalidateVisual();
        root.UpdateLayout();
        await Task.Delay(250);
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        root.UpdateLayout();
    }

    private static void VerifyPatchDialogActions(Window window, FrameworkElement root)
    {
        if (window is not (TemplatePropertiesWindow or GenerationOptionsWindow)) return;
        var actions = VisualDescendants(root).OfType<Button>().Where(button => button.IsCancel || button.IsDefault).ToArray();
        Require(actions.Length == 2, "The dialog action buttons are missing.");
        foreach (var button in actions)
        {
            Require(button.IsVisible && button.ActualWidth > 0 && button.ActualHeight > 0, "A dialog action button is hidden.");
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
            Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1,
                "A dialog action button is outside the visible client area.");
        }
    }

    private static void CapturePatchRoot(FrameworkElement root, string directory, string name)
    {
        FrameworkElement surface = Window.GetWindow(root) is { } window ? window : root;
        Rect bounds = ReferenceEquals(root, surface) ? new Rect(root.RenderSize)
            : root.TransformToAncestor(surface).TransformBounds(new Rect(root.RenderSize));
        int left = (int)Math.Floor(bounds.Left), top = (int)Math.Floor(bounds.Top);
        int right = (int)Math.Ceiling(bounds.Right), bottom = (int)Math.Ceiling(bounds.Bottom);
        Require(left >= 0 && top >= 0 && right > left && bottom > top, "Invalid capture bounds.");
        var full = new RenderTargetBitmap((int)Math.Ceiling(Math.Max(surface.ActualWidth, bounds.Right)),
            (int)Math.Ceiling(Math.Max(surface.ActualHeight, bounds.Bottom)), 96, 96, PixelFormats.Pbgra32);
        // Render the live surface directly; a cyclic VisualBrush can omit off-screen subtrees.
        full.Render(surface);
        var cropped = new CroppedBitmap(full, new Int32Rect(left, top, right - left, bottom - top));
        const int padding = 16;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Window.GetWindow(root)?.Background ?? Brushes.Transparent, null,
                new Rect(0, 0, cropped.PixelWidth + padding * 2, cropped.PixelHeight + padding * 2));
            drawing.DrawImage(cropped, new Rect(padding, padding, cropped.PixelWidth, cropped.PixelHeight));
        }
        var bitmap = new RenderTargetBitmap(cropped.PixelWidth + padding * 2, cropped.PixelHeight + padding * 2, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(directory, name + ".png"))) encoder.Save(stream);
        if (Window.GetWindow(root) is not (TemplatePropertiesWindow or GenerationOptionsWindow)) return;
        foreach (var action in VisualDescendants(root).OfType<Button>().Where(button => button.IsCancel || button.IsDefault))
        {
            Rect actionBounds = action.TransformToAncestor(surface).TransformBounds(new Rect(action.RenderSize));
            int x = padding + (int)Math.Floor(actionBounds.Left) - left, y = padding + (int)Math.Floor(actionBounds.Top) - top;
            var region = new Int32Rect(x, y, (int)Math.Ceiling(actionBounds.Width), (int)Math.Ceiling(actionBounds.Height));
            Require(x >= 0 && y >= 0 && x + region.Width <= bitmap.PixelWidth && y + region.Height <= bitmap.PixelHeight,
                "Dialog action pixels are outside the capture.");
            byte[] pixels = new byte[region.Width * region.Height * 4]; bitmap.CopyPixels(region, pixels, region.Width * 4, 0);
            int visibleTextPixels = 0;
            for (int i = 0; i < pixels.Length; i += 4)
                if (pixels[i] >= 90 && pixels[i + 1] >= 90 && pixels[i + 2] >= 90 && pixels[i + 3] >= 200) visibleTextPixels++;
            Require(visibleTextPixels >= 12, "The dialog action text is absent from the capture: " + name);
        }
    }

    private async Task CapturePatchWindowAsync(Window window, string directory, string name, Action<Window>? verify = null)
    {
        window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -6000; window.Top = 100;
        var rendered = new TaskCompletionSource();
        window.ContentRendered += (_, _) => rendered.TrySetResult();
        window.Show();
        try
        {
            await rendered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Task.Delay(600); await Dispatcher.Yield(DispatcherPriority.Background);
            verify?.Invoke(window);
            await Dispatcher.Yield(DispatcherPriority.Background);
            var root = (FrameworkElement)window.Content;
            await SettlePatchLayoutAsync(root); VerifyPatchDialogActions(window, root);
            CapturePatchRoot(root, directory, name);
            foreach (var tabs in VisualDescendants(root).OfType<TabControl>().Take(1))
            {
                for (int i = 0; i < tabs.Items.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    await SettlePatchLayoutAsync(root); VerifyPatchDialogActions(window, root);
                    CapturePatchRoot(root, directory, name + "-tab-" + i);
                }
            }
            Require(new FileInfo(Path.Combine(directory, name + ".png")).Length > 100, "A dialog capture is empty: " + name);
        }
        finally { if (window is TemplateEditorWindow editor) editor.DebugDiscardAndClose(); else window.Close(); }
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var descendant in VisualDescendants(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
