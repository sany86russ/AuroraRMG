namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>
/// Native 0.81.04 content budget formula. Eligible cells are counted by the game after layout:
/// Free cells within five neighborhood expansion passes from AlwaysFree cells, in the same zone.
/// A schematic zone weight or total tile count is not this quantity.
/// </summary>
public static class NativeAreaBudget
{
    public static int Calculate(int absoluteBudget, int perAreaCoefficient, int eligibleFreeCells)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(perAreaCoefficient);
        ArgumentOutOfRangeException.ThrowIfNegative(eligibleFreeCells);
        // Native code multiplies integers before conversion to float and uses banker's rounding.
        // Reject overflow rather than reproduce a wrapped negative content budget.
        var product = checked(perAreaCoefficient * eligibleFreeCells);
        var areaBudget = checked((int)MathF.Round(product * 0.1f, MidpointRounding.ToEven));
        return checked(absoluteBudget + areaBudget);
    }
}
