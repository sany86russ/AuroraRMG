using System;
using System.Text.Json;
using System.Windows;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Shows the current <see cref="RmgTemplate"/> serialized as JSON and lets the user edit it
    /// directly. "Apply" parses the text back into a template; on success <see cref="Result"/> holds
    /// the parsed model and <see cref="Window.DialogResult"/> is <c>true</c>. Parse errors are shown
    /// inline and never mutate the editor's model.
    /// </summary>
    public partial class JsonPreviewWindow : Window
    {
        // Display copy of the save options with indentation on — same converters/encoder, so a
        // round-trip is faithful; the editor still SAVES with the original compact options.
        private readonly JsonSerializerOptions _display;
        private string? _statusKey;
        private object[] _statusArgs = [];
        private void Status(string key, params object[] args) { _statusKey = key; _statusArgs = args; TxtStatus.Text = L(key, args); }

        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        /// <summary>The parsed template after a successful "Apply"; null otherwise.</summary>
        public RmgTemplate? Result { get; private set; }

        public JsonPreviewWindow(RmgTemplate template, JsonSerializerOptions options)
        {
            InitializeComponent();
            _display = new JsonSerializerOptions(options) { WriteIndented = true };
            TxtJson.Text = JsonSerializer.Serialize(template, _display);
            LocalizationManager.Observe(this, () => { if (_statusKey is not null) TxtStatus.Text = L(_statusKey, _statusArgs); });
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (TryParse(out var parsed))
            {
                Result = parsed;
                DialogResult = true;
                Close();
            }
        }

        private void BtnReformat_Click(object sender, RoutedEventArgs e)
        {
            if (TryParse(out var parsed))
            {
                TxtJson.Text = JsonSerializer.Serialize(parsed, _display);
                Status("S.JP.Reformatted");
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtJson.Text);
                Status("S.JP.Copied");
            }
            catch (Exception ex) { Status("S.Label.CopyFailed", ex.Message); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Parses the editor text; shows the error inline and returns false on failure.</summary>
        private bool TryParse(out RmgTemplate parsed)
        {
            parsed = new RmgTemplate();
            try
            {
                var t = JsonSerializer.Deserialize<RmgTemplate>(TxtJson.Text, _display);
                if (t is null) { Status("S.JP.ParseError", "null"); return false; }
                parsed = t;
                return true;
            }
            catch (Exception ex)
            {
                if (ex is JsonException json) Status("S.Label.JsonPosition", (json.LineNumber ?? 0) + 1, (json.BytePositionInLine ?? 0) + 1, json.Path ?? "$");
                else Status("S.JP.ParseError", ex.Message);
                return false;
            }
        }
    }
}
