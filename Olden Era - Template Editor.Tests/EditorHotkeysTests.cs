using Olden_Era___Template_Editor.Localization;
using Olden_Era___Template_Editor.Services;
using System.Windows.Input;

namespace Olden_Era___Template_Editor.Tests;

public class EditorHotkeysTests
{
    [Fact]
    public void DefaultBindings_CoverEveryActionWithoutCollisions()
    {
        var defaults = EditorHotkeys.DefaultsCopy;

        foreach (EditorAction action in Enum.GetValues<EditorAction>())
            Assert.True(defaults.ContainsKey(action), $"{action} has no default binding");

        var collisions = defaults
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(kv => kv.Key))}")
            .ToList();

        Assert.True(collisions.Count == 0, "Two commands share a default key: " + string.Join(" · ", collisions));
    }

    [Theory]
    [InlineData(Key.S, ModifierKeys.Control, "Ctrl+S")]
    [InlineData(Key.Z, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+Z")]
    [InlineData(Key.Delete, ModifierKeys.None, "Delete")]
    [InlineData(Key.F1, ModifierKeys.None, "F1")]
    [InlineData(Key.D0, ModifierKeys.Control, "Ctrl+0")]       // WPF calls the digit 0 "D0"
    [InlineData(Key.NumPad5, ModifierKeys.Alt, "Alt+Num5")]
    public void Format_ProducesTheCanonicalGestureText(Key key, ModifierKeys modifiers, string expected)
        => Assert.Equal(expected, EditorHotkeys.Format(key, modifiers));

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.None)]
    public void Format_IgnoresABareModifier(Key key)
        => Assert.Equal("", EditorHotkeys.Format(key, ModifierKeys.Control));

    [Fact]
    public void EveryActionHasALocalizationKey()
    {
        // The settings window renders each command by its localised name; a missing key would show
        // the raw "S.HK.Act.X" placeholder.
        foreach (EditorAction action in Enum.GetValues<EditorAction>())
        {
            string key = EditorHotkeys.LabelKey(action);
            Assert.True(Strings.Ru.ContainsKey(key), $"RU is missing {key}");
            Assert.True(Strings.En.ContainsKey(key), $"EN is missing {key}");
        }
    }
}
