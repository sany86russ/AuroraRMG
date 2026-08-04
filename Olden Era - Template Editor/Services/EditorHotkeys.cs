using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>Every zone-editor command that can be bound to a key.</summary>
    public enum EditorAction
    {
        AddZone,
        ConnectMode,
        Delete,
        Validate,
        Save,
        Load,
        Undo,
        Redo,
        CopyZone,
        PasteZone,
        Mirror,
        JsonPreview,
        Connections,
        Orientation,
        Help,
        ExportPng,
        FitToView,
        AutoLayout,
        GridSnap,
    }

    /// <summary>
    /// User-rebindable keyboard shortcuts for the visual zone editor.
    /// <para>
    /// The defaults follow the usual Windows conventions (Ctrl+S save, Ctrl+Z undo, Del delete), but
    /// map authors work in the editor for hours and everyone's muscle memory differs — and on some
    /// keyboard layouts a default is simply awkward to reach. Bindings live in
    /// <c>%LOCALAPPDATA%\AuroraRMG\editor-hotkeys.json</c> and every load falls back to the defaults,
    /// so a corrupt or partial file can never leave the editor without shortcuts.
    /// </para>
    /// </summary>
    public static class EditorHotkeys
    {
        private static readonly Dictionary<EditorAction, string> Defaults = new()
        {
            [EditorAction.AddZone]     = "Ctrl+N",
            [EditorAction.ConnectMode] = "Ctrl+L",
            [EditorAction.Delete]      = "Delete",
            [EditorAction.Validate]    = "F5",
            [EditorAction.Save]        = "Ctrl+S",
            [EditorAction.Load]        = "Ctrl+O",
            [EditorAction.Undo]        = "Ctrl+Z",
            [EditorAction.Redo]        = "Ctrl+Y",
            [EditorAction.CopyZone]    = "Ctrl+C",
            [EditorAction.PasteZone]   = "Ctrl+V",
            [EditorAction.Mirror]      = "Ctrl+M",
            [EditorAction.JsonPreview] = "Ctrl+J",
            [EditorAction.Connections] = "Ctrl+K",
            [EditorAction.Orientation] = "Ctrl+R",
            [EditorAction.Help]        = "F1",
            [EditorAction.ExportPng]   = "Ctrl+E",
            [EditorAction.FitToView]   = "Ctrl+0",
            [EditorAction.AutoLayout]  = "Ctrl+G",
            [EditorAction.GridSnap]    = "Ctrl+B",
        };

        private static Dictionary<EditorAction, string>? _current;

        /// <summary>The active bindings (loaded on first use).</summary>
        public static IReadOnlyDictionary<EditorAction, string> Current => _current ??= Load();

        public static string DefaultOf(EditorAction action) => Defaults[action];

        public static IReadOnlyDictionary<EditorAction, string> DefaultsCopy =>
            new Dictionary<EditorAction, string>(Defaults);

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuroraRMG", "editor-hotkeys.json");

        private static Dictionary<EditorAction, string> Load()
        {
            var map = new Dictionary<EditorAction, string>(Defaults);
            try
            {
                if (!File.Exists(FilePath)) return map;
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath));
                if (saved is null) return map;
                foreach (var kv in saved)
                    if (Enum.TryParse(kv.Key, out EditorAction action) && !string.IsNullOrWhiteSpace(kv.Value))
                        map[action] = kv.Value;
            }
            catch
            {
                // A broken file must never cost the user their shortcuts — the defaults stand.
            }
            return map;
        }

        /// <summary>Persists a full binding set and makes it active.</summary>
        public static void Save(IReadOnlyDictionary<EditorAction, string> bindings)
        {
            _current = new Dictionary<EditorAction, string>(bindings);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var payload = bindings.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Keep the in-memory bindings even if the disk write fails.
            }
        }

        /// <summary>Restores the built-in bindings and forgets the saved file.</summary>
        public static void ResetToDefaults()
        {
            _current = new Dictionary<EditorAction, string>(Defaults);
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        /// <summary>Renders a key + modifiers as the canonical text used for storage and display.</summary>
        public static string Format(Key key, ModifierKeys modifiers)
        {
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                    or Key.LeftAlt or Key.RightAlt or Key.System or Key.None)
                return "";

            var parts = new List<string>(4);
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            parts.Add(KeyName(key));
            return string.Join("+", parts);
        }

        /// <summary>Friendly, layout-independent key names ("D0" is what WPF calls the digit 0).</summary>
        private static string KeyName(Key key) => key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + (int)(key - Key.NumPad0),
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            _ => key.ToString(),
        };

        /// <summary>
        /// Finds the action bound to the pressed combination. Returns <c>false</c> when nothing matches.
        /// </summary>
        public static bool TryMatch(Key key, ModifierKeys modifiers, out EditorAction action)
        {
            string gesture = Format(key, modifiers);
            if (gesture.Length > 0)
                foreach (var kv in Current)
                    if (string.Equals(kv.Value, gesture, StringComparison.OrdinalIgnoreCase))
                    {
                        action = kv.Key;
                        return true;
                    }

            action = default;
            return false;
        }

        /// <summary>Localisation key for an action's display name (<c>S.HK.Act.&lt;Action&gt;</c>).</summary>
        public static string LabelKey(EditorAction action) => "S.HK.Act." + action;
    }
}
