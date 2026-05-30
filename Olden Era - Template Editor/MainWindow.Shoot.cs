using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
            (2, "ui-bonuses-bans"),
            (3, "ui-extra-content"),
        ];

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

                foreach (var (index, file) in ShootTabs)
                {
                    if (index >= MainTabs.Items.Count) continue;
                    MainTabs.SelectedIndex = index;

                    // Give WPF time to realise + lay out + paint the newly selected tab.
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    root.UpdateLayout();
                    await Task.Delay(450);
                    await Dispatcher.Yield(DispatcherPriority.Background);

                    int w = (int)Math.Ceiling(root.ActualWidth);
                    int h = (int)Math.Ceiling(root.ActualHeight);
                    if (w <= 0 || h <= 0) continue;

                    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(root);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    string path = Path.Combine(dir, file + ".png");
                    using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                        encoder.Save(fs);
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
    }
}
