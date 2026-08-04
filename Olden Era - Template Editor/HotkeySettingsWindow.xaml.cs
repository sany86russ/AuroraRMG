using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Rebinds the zone editor's keyboard shortcuts. Each row shows one command and a button that
    /// listens for the next key combination; conflicts are reported live and block "Save" until the
    /// duplicate is resolved, so the editor can never end up with two commands on one key.
    /// </summary>
    public partial class HotkeySettingsWindow : Window
    {
        private static string L(string key, params object[] args) => LocalizationManager.T(key, args);

        private readonly Dictionary<EditorAction, string> _bindings;
        private readonly Dictionary<EditorAction, Button> _buttons = [];
        private EditorAction? _capturing;

        public HotkeySettingsWindow()
        {
            InitializeComponent();
            _bindings = new Dictionary<EditorAction, string>(EditorHotkeys.Current);
            BuildRows();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void BuildRows()
        {
            Rows.Children.Clear();
            _buttons.Clear();

            foreach (EditorAction action in Enum.GetValues<EditorAction>())
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };

                var capture = new Button
                {
                    Content = _bindings.TryGetValue(action, out string? g) && g.Length > 0 ? g : L("S.HK.Unbound"),
                    Width = 150,
                    Height = 28,
                    Tag = action,
                    Style = (Style)FindResource("ToolbarButton"),
                };
                capture.Click += CaptureButton_Click;
                DockPanel.SetDock(capture, Dock.Right);
                row.Children.Add(capture);
                _buttons[action] = capture;

                row.Children.Add(new TextBlock
                {
                    Text = L(EditorHotkeys.LabelKey(action)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("BrushText"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 0, 10, 0),
                });

                Rows.Children.Add(row);
            }

            RefreshConflicts();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not EditorAction action) return;

            // Cancel a capture already in progress so only one row listens at a time.
            if (_capturing is { } previous) RestoreButton(previous);

            _capturing = action;
            b.Content = L("S.HK.PressKey");
            b.Background = new SolidColorBrush(Color.FromRgb(60, 50, 90));
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_capturing is not { } action) return;

            if (e.Key == Key.Escape)
            {
                RestoreButton(action);
                _capturing = null;
                e.Handled = true;
                return;
            }

            // Backspace clears the binding — a command with no shortcut is a valid choice.
            if (e.Key == Key.Back)
            {
                _bindings[action] = "";
                RestoreButton(action);
                _capturing = null;
                RefreshConflicts();
                e.Handled = true;
                return;
            }

            string gesture = EditorHotkeys.Format(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
            if (gesture.Length == 0) { e.Handled = true; return; } // a bare modifier — keep listening

            _bindings[action] = gesture;
            RestoreButton(action);
            _capturing = null;
            RefreshConflicts();
            e.Handled = true;
        }

        private void RestoreButton(EditorAction action)
        {
            if (!_buttons.TryGetValue(action, out Button? b)) return;
            b.Background = null;
            b.Content = _bindings.TryGetValue(action, out string? g) && g.Length > 0 ? g : L("S.HK.Unbound");
        }

        /// <summary>Lists every key bound to more than one command.</summary>
        private void RefreshConflicts()
        {
            var duplicates = _bindings
                .Where(kv => kv.Value.Length > 0)
                .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key} — {string.Join(", ", g.Select(kv => L(EditorHotkeys.LabelKey(kv.Key))))}")
                .ToList();

            TxtConflict.Text = duplicates.Count == 0 ? "" : L("S.HK.Conflict", string.Join(" · ", duplicates));
        }

        private bool HasConflict() => _bindings
            .Where(kv => kv.Value.Length > 0)
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kv in EditorHotkeys.DefaultsCopy) _bindings[kv.Key] = kv.Value;
            BuildRows();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (HasConflict())
            {
                MessageBox.Show(this, L("S.HK.ConflictBlock"), L("S.HK.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditorHotkeys.Save(_bindings);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
