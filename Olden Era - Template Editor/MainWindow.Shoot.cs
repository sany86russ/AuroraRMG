using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Documentation screenshot mode (enabled with <c>--shoot &lt;dir&gt;</c>).
    ///
    /// Renders each main tab to a PNG using <see cref="RenderTargetBitmap"/>, which
    /// rasterises the live WPF visual tree directly — no PrintWindow, no need to put
    /// the window on-screen, and therefore no risk of stealing focus from a fullscreen
    /// game. Each tab is selected, laid out, given a couple of render passes to settle,
    /// then captured. The app exits when done.
    /// </summary>
    public partial class MainWindow
    {
        private static readonly (int Index, string File)[] ShootTabs =
        [
            (0, "ui-rules"),
            (1, "ui-map-zones"),
            (2, "ui-extra-content"),   // tab order: Rules / Zones / Content / Bans
            (3, "ui-bonuses-bans"),
        ];

        /// <summary>Renders the current window content to <paramref name="dir"/>/<paramref name="file"/>.png.</summary>
        private void CaptureRoot(FrameworkElement root, string dir, string file)
        {
            int w = (int)Math.Ceiling(root.ActualWidth);
            int h = (int)Math.Ceiling(root.ActualHeight);
            if (w <= 0 || h <= 0) return;

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = new FileStream(Path.Combine(dir, file + ".png"), FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }

        /// <summary>
        /// Headless ReadyMaps regeneration (<c>--gen-readymaps &lt;dir&gt;</c>): writes every built-in
        /// preset to "[Gen] &lt;TemplateName&gt;.rmg.json" via the shared export options (literal UTF-8,
        /// no BOM), then exits. Refreshes the repo ReadyMaps after preset/name changes.
        /// </summary>
        private void GenerateReadyMaps(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                foreach (var preset in Presets.All)
                {
                    var settings = Presets.ToGeneratorSettings(preset.Settings);
                    var template = TemplateGenerator.Generate(settings);
                    string json = System.Text.Json.JsonSerializer.Serialize(template, JsonExport.Options);
                    string file = $"[Gen] {preset.Settings.TemplateName}.rmg.json";
                    File.WriteAllText(Path.Combine(dir, file), json);
                }
            }
            catch
            {
                // Best-effort dev tool; never hard-crash.
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        private async Task ShootTabsAsync(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var root = (FrameworkElement)Content;

                // Let the first layout pass complete.
                await Dispatcher.Yield(DispatcherPriority.Background);
                root.UpdateLayout();
                await Task.Delay(400);

                // ── Simple Mode (the default landing view) ──
                SetMode(advanced: false, persist: false);
                try { BtnSimpleGenerate_Click(this, new RoutedEventArgs()); } catch { /* preview is best-effort */ }
                // Reveal the (default-hidden) preview so the docs screenshot shows the full feature.
                try { if (SimplePreviewBox != null) { SimplePreviewBox.Visibility = Visibility.Visible; UpdateSimplePreviewToggle(); } } catch { }
                await Dispatcher.Yield(DispatcherPriority.Background);
                root.UpdateLayout();
                await Task.Delay(550);
                await Dispatcher.Yield(DispatcherPriority.Background);
                CaptureRoot(root, dir, "ui-simple");

                // ── Lanes showcase (v1.4.0 headline): pick the new game type, 4 players, generate ──
                try
                {
                    int lanesIdx = Array.IndexOf(SimpleTypeKeys, "S.Simple.Type.Lanes");
                    if (CmbSimpleType != null && lanesIdx >= 0) CmbSimpleType.SelectedIndex = lanesIdx;
                    if (SldSimplePlayers != null) SldSimplePlayers.Value = 4;
                    try { BtnSimpleGenerate_Click(this, new RoutedEventArgs()); } catch { /* preview is best-effort */ }
                    try { if (SimplePreviewBox != null) { SimplePreviewBox.Visibility = Visibility.Visible; UpdateSimplePreviewToggle(); } } catch { }
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    root.UpdateLayout();
                    await Task.Delay(550);
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    CaptureRoot(root, dir, "ui-simple-lanes");
                }
                catch { /* showcase shot is best-effort */ }

                // ── Advanced Mode tabs ──
                SetMode(advanced: true, persist: false);
                await Dispatcher.Yield(DispatcherPriority.Background);
                root.UpdateLayout();
                await Task.Delay(300);

                foreach (var (index, file) in ShootTabs)
                {
                    if (index >= MainTabs.Items.Count) continue;
                    MainTabs.SelectedIndex = index;

                    // Give WPF time to realise + lay out + paint the newly selected tab.
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    root.UpdateLayout();
                    await Task.Delay(450);
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    CaptureRoot(root, dir, file);
                }
            }
            catch
            {
                // Screenshot mode is best-effort; never crash the app over it.
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Documentation screenshot for the visual zone editor (<c>--shoot-editor &lt;dir&gt;</c>):
        /// generates a representative hub-and-spoke template, opens the editor off-screen, lets it
        /// lay out, captures it to PNG, then exits. Never activates / never pops over a game.
        /// </summary>
        private async Task ShootEditorAsync(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);

                var settings = new GeneratorSettings
                {
                    TemplateName = "Editor Demo",
                    PlayerCount  = 4,
                    MapSize      = 160,
                    Topology     = MapTopology.HubAndSpoke,
                    ZoneCfg = new ZoneConfiguration
                    {
                        HubZoneCastles = 0,
                        Advanced = new AdvancedSettings { Enabled = true, NeutralMediumNoCastleCount = 4 },
                    },
                };
                var template = TemplateGenerator.Generate(settings);

                var editor = new TemplateEditorWindow(template, settings.Topology)
                {
                    ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -6000, Top = 120, Width = 1160, Height = 800,
                };

                var done = new TaskCompletionSource();
                editor.ContentRendered += async (_, _) =>
                {
                    try
                    {
                        var root = (FrameworkElement)editor.Content;
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        root.UpdateLayout();
                        await Task.Delay(800); // let Loaded → ComputePositions/FitToView settle
                        editor.DebugSelectZone("Hub"); // populate the inspector for the screenshot
                        root.UpdateLayout();
                        await Task.Delay(150);
                        await Dispatcher.Yield(DispatcherPriority.Background);

                        int w = (int)System.Math.Ceiling(root.ActualWidth);
                        int h = (int)System.Math.Ceiling(root.ActualHeight);
                        if (w > 0 && h > 0)
                        {
                            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                            rtb.Render(root);
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(rtb));
                            using var fs = new FileStream(Path.Combine(dir, "ui-editor.png"), FileMode.Create, FileAccess.Write);
                            encoder.Save(fs);
                        }
                    }
                    catch { /* best effort */ }
                    finally { done.TrySetResult(); }
                };

                editor.Show();
                await done.Task;
            }
            catch { /* best effort */ }
            finally
            {
                Application.Current.Shutdown();
            }
        }
    }
}
