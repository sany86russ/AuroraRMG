using System.Globalization;

namespace Olden_Era___Template_Editor.Services;

/// <summary>UI numbers accept a decimal comma or point, never implicit thousands separators.</summary>
public static class NumericInput
{
    public static bool TryDouble(string? text, out double value)
    {
        bool valid = TryOptionalDouble(text, out var parsed) && parsed.HasValue;
        value = parsed.GetValueOrDefault();
        return valid;
    }
    public static bool TryOptionalDouble(string? text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number)) return false;
        value = number;
        return true;
    }

    public static bool TryOptionalInt(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return false;
        value = number;
        return true;
    }
}
