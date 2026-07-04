using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;
using ModelOrientation = OldenEraTemplateEditor.Models.Orientation;
using ModelBorder = OldenEraTemplateEditor.Models.Border;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Editor for a <see cref="Variant"/>'s <c>orientation</c> and <c>border</c> blocks. Scalar
    /// fields only; the border noise curves (<c>obstaclesNoise</c>/<c>waterNoise</c>) are preserved
    /// untouched. Empty inputs serialize as null (omitted), and a fully-empty block becomes null so
    /// the engine falls back to its defaults. "Apply" writes back and closes with DialogResult=true.
    /// </summary>
    public partial class OrientationWindow : Window
    {
        private readonly Variant _variant;

        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        public OrientationWindow(Variant variant, IEnumerable<string> zoneNames)
        {
            InitializeComponent();
            _variant = variant;

            CmbMode.ItemsSource = KnownValues.OrientationModes;
            CmbWaterType.ItemsSource = KnownValues.WaterTypes;
            CmbZeroZone.ItemsSource = zoneNames.ToList();

            var o = variant.Orientation;
            CmbMode.Text = o?.Mode ?? "";
            CmbZeroZone.Text = o?.ZeroAngleZone ?? "";
            TxtBaseMin.Text = Fmt(o?.BaseAngleMin);
            TxtBaseMax.Text = Fmt(o?.BaseAngleMax);
            TxtRandAmp.Text = Fmt(o?.RandomAngleAmplitude);
            TxtRandStep.Text = Fmt(o?.RandomAngleStep);

            var b = variant.Border;
            TxtCorner.Text = Fmt(b?.CornerRadius);
            TxtObstWidth.Text = b?.ObstaclesWidth?.ToString(CultureInfo.InvariantCulture) ?? "";
            TxtWaterWidth.Text = b?.WaterWidth?.ToString(CultureInfo.InvariantCulture) ?? "";
            CmbWaterType.Text = b?.WaterType ?? "";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var o = _variant.Orientation ?? new ModelOrientation();
            o.Mode = Str(CmbMode);
            o.ZeroAngleZone = Str(CmbZeroZone);
            o.BaseAngleMin = ParseD(TxtBaseMin);
            o.BaseAngleMax = ParseD(TxtBaseMax);
            o.RandomAngleAmplitude = ParseD(TxtRandAmp);
            o.RandomAngleStep = ParseD(TxtRandStep);
            bool oEmpty = o.Mode is null && o.ZeroAngleZone is null && o.BaseAngleMin is null
                          && o.BaseAngleMax is null && o.RandomAngleAmplitude is null && o.RandomAngleStep is null;
            _variant.Orientation = oEmpty ? null : o;

            var b = _variant.Border ?? new ModelBorder();
            b.CornerRadius = ParseD(TxtCorner);
            b.ObstaclesWidth = ParseI(TxtObstWidth);
            b.WaterWidth = ParseI(TxtWaterWidth);
            b.WaterType = Str(CmbWaterType);
            // ObstaclesNoise / WaterNoise are intentionally preserved.
            bool bEmpty = b.CornerRadius is null && b.ObstaclesWidth is null && b.WaterWidth is null
                          && b.WaterType is null
                          && (b.ObstaclesNoise is null || b.ObstaclesNoise.Count == 0)
                          && (b.WaterNoise is null || b.WaterNoise.Count == 0);
            _variant.Border = bEmpty ? null : b;

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static string Fmt(double? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";

        private static string? Str(ComboBox c)
        {
            var s = c.Text?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static double? ParseD(TextBox t) =>
            double.TryParse(t.Text?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (double?)null;

        private static int? ParseI(TextBox t) =>
            int.TryParse(t.Text?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;
    }
}
