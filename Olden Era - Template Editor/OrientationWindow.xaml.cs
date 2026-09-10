using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System;
using System.Text.Json;
using Olden_Era___Template_Editor.Services;
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
            void RefreshOptions()
            {
                foreach (var combo in new[] { CmbMode, CmbWaterType })
                {
                    var text = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
                    text.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new GameTokenConverter() });
                    combo.ItemTemplate = new DataTemplate { VisualTree = text };
                    combo.ToolTip = GameLabels.Token(combo.Text);
                }
            }
            Loaded += (_, _) => RefreshOptions();
            LocalizationManager.Observe(this, RefreshOptions);

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
            // Parse every field before changing the document, preserving unknown engine fields.
            try
            {
                var o = _variant.Orientation is null ? new ModelOrientation()
                    : JsonSerializer.Deserialize<ModelOrientation>(JsonSerializer.Serialize(_variant.Orientation, JsonExport.Options), JsonExport.Options)!;
                o.Mode = Str(CmbMode);
                o.ZeroAngleZone = Str(CmbZeroZone);
                o.BaseAngleMin = ParseD(TxtBaseMin);
                o.BaseAngleMax = ParseD(TxtBaseMax);
                o.RandomAngleAmplitude = ParseD(TxtRandAmp);
                o.RandomAngleStep = ParseD(TxtRandStep);
                bool oEmpty = o.Mode is null && o.ZeroAngleZone is null && o.BaseAngleMin is null
                              && o.BaseAngleMax is null && o.RandomAngleAmplitude is null && o.RandomAngleStep is null;
                var b = _variant.Border is null ? new ModelBorder()
                    : JsonSerializer.Deserialize<ModelBorder>(JsonSerializer.Serialize(_variant.Border, JsonExport.Options), JsonExport.Options)!;
                b.CornerRadius = ParseD(TxtCorner);
                b.ObstaclesWidth = ParseI(TxtObstWidth);
                b.WaterWidth = ParseI(TxtWaterWidth);
                b.WaterType = Str(CmbWaterType);
                // ObstaclesNoise / WaterNoise are intentionally preserved.
                bool bEmpty = b.CornerRadius is null && b.ObstaclesWidth is null && b.WaterWidth is null
                              && b.WaterType is null
                              && (b.ObstaclesNoise is null || b.ObstaclesNoise.Count == 0)
                              && (b.WaterNoise is null || b.WaterNoise.Count == 0);
                if (o.BaseAngleMin > o.BaseAngleMax || o.RandomAngleAmplitude < 0 || o.RandomAngleStep < 0
                    || b.CornerRadius < 0 || b.ObstaclesWidth < 0 || b.WaterWidth < 0)
                    throw new FormatException(L("S.OR.InvalidRange"));
                if (o.ZeroAngleZone is not null && !CmbZeroZone.Items.Contains(o.ZeroAngleZone))
                    throw new FormatException(L("S.OR.InvalidZone"));
                _variant.Orientation = oEmpty && !(o.Extra?.Count > 0) ? null : o;
                _variant.Border = bEmpty && !(b.Extra?.Count > 0) ? null : b;

                DialogResult = true;
                Close();
            }
            catch (FormatException ex)
            {
                MessageBox.Show(this, ex.Message, L("S.OR.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static string Fmt(double? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";

        private static string? Str(ComboBox c)
        {
            var s = c.Text?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static double? ParseD(TextBox t)
        {
            if (NumericInput.TryOptionalDouble(t.Text, out var value)) return value;
            t.Focus(); t.SelectAll();
            throw new FormatException(L("S.OR.InvalidNumber", t.Text));
        }

        private static int? ParseI(TextBox t)
        {
            if (NumericInput.TryOptionalInt(t.Text, out var value)) return value;
            t.Focus(); t.SelectAll();
            throw new FormatException(L("S.OR.InvalidInteger", t.Text));
        }
    }
}
