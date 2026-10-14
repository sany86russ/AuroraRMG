using System.Text.Json;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

internal static class NativeSchemaWeight
{
    internal static bool IsRepresentable(double weight) => double.IsFinite(weight) && weight >= 0
        && weight <= float.MaxValue && (weight == 0 || (float)weight > 0);

    internal static bool HasUsableSum(IEnumerable<double> weights)
    {
        double total = 0;
        foreach (double weight in weights)
        {
            if (!IsRepresentable(weight)) return false;
            total += (float)weight;
            if (total > float.MaxValue) return false;
        }
        return total > 0;
    }

    internal static void RequireUsableSum(IEnumerable<double> weights)
    {
        if (!HasUsableSum(weights)) throw new JsonException("Conditional selection needs positive weights whose native float sum stays finite.");
    }
}
