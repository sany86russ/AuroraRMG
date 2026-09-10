using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Reads a hand-drawn map sketch and turns it into a zone graph. The picture is analysed live as
    /// the two tolerances are adjusted, so the author can see how many zones and links the sketch
    /// yields before committing to the import.
    /// </summary>
    public partial class ImageImportWindow : Window
    {
        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        /// <summary>Long-side cap for the analysis buffer: enough detail for blobs, instant to process.</summary>
        private const int MaxAnalysisSide = 400;

        private byte[]? _pixels;
        private int _width, _height;
        private int _analysisVersion;
        private readonly SemaphoreSlim _analysisGate = new(1, 1);
        private bool _closed;

        /// <summary>The accepted import, or <c>null</c> when the dialog was cancelled.</summary>
        public SketchImportResult? Result { get; private set; }

        public ImageImportWindow()
        {
            InitializeComponent();
            TxtSummary.Text = L("S.IM.SummaryEmpty");
            LocalizationManager.Observe(this, () => { if (_pixels is null) TxtSummary.Text = L("S.IM.SummaryEmpty"); else Analyse(); });
            Closed += (_, _) => { _closed = true; _analysisVersion++; };
        }

        private void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = L("S.IM.Title"), Filter = L("S.IM.Filter") };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var source = new BitmapImage();
                source.BeginInit();
                source.UriSource = new Uri(dlg.FileName);
                source.CacheOption = BitmapCacheOption.OnLoad;   // don't hold the file open
                using (var stream = File.OpenRead(dlg.FileName))
                {
                    var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    if (Math.Max(frame.PixelWidth, frame.PixelHeight) > 800)
                    {
                        if (frame.PixelWidth >= frame.PixelHeight) source.DecodePixelWidth = 800;
                        else source.DecodePixelHeight = 800;
                    }
                }
                source.EndInit();
                source.Freeze();

                ImgPreview.Source = source;
                TxtNoImage.Visibility = Visibility.Collapsed;
                LoadPixels(source);
                Analyse();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.IM.LoadFailed", ex.Message), L("S.IM.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Converts the image to a down-scaled Bgra32 buffer for the analyser.</summary>
        private void LoadPixels(BitmapSource source)
        {
            double scale = Math.Min(1.0, MaxAnalysisSide / (double)Math.Max(source.PixelWidth, source.PixelHeight));
            BitmapSource working = scale < 1.0
                ? new TransformedBitmap(source, new ScaleTransform(scale, scale))
                : source;

            var converted = new FormatConvertedBitmap(working, PixelFormats.Bgra32, null, 0);
            _width = converted.PixelWidth;
            _height = converted.PixelHeight;
            _pixels = new byte[_width * _height * 4];
            converted.CopyPixels(_pixels, _width * 4, 0);
        }

        private void Param_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            TxtMinArea.Text = $"{SldMinArea.Value / 10.0:0.0}%";
            TxtLinkRadius.Text = ((int)SldLinkRadius.Value).ToString();
            Analyse();
        }

        private async void Analyse()
        {
            if (_pixels is null) return;
            int version = ++_analysisVersion;
            byte[] pixels = _pixels;
            int width = _width, height = _height;
            double area = SldMinArea.Value / 10.0;
            int radius = (int)SldLinkRadius.Value;
            Result = null;
            BtnImport.IsEnabled = false;
            TxtSummary.Text = L("S.IM.Analysing");
            await Task.Delay(180); // coalesce rapid slider moves
            if (_closed || version != _analysisVersion) return;
            await _analysisGate.WaitAsync();
            try
            {
                if (_closed || version != _analysisVersion) return;
                var result = await Task.Run(() => MapSketchImporter.Analyse(pixels, width, height, area, radius));
                if (_closed || version != _analysisVersion) return;
                Result = result;

                var variant = Result.Template.Variants![0];
                var zones = variant.Zones ?? [];
                var connections = variant.Connections ?? [];
                int spawns = zones.SelectMany(z => z.MainObjects ?? []).Count(o => o.Type == "Spawn");

                string notes = Result.Warnings.Count == 0
                    ? L("S.IM.NoNotes")
                    : "• " + string.Join("\n• ", Result.Warnings.Take(5));

                TxtSummary.Text = L("S.IM.Summary", zones.Count, connections.Count, spawns) + "\n\n" + notes;
                BtnImport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                if (_closed || version != _analysisVersion) return;
                Result = null;
                BtnImport.IsEnabled = false;
                TxtSummary.Text = ex.Message;
            }
            finally { _analysisGate.Release(); }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (Result is null) return;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }
    }
}
