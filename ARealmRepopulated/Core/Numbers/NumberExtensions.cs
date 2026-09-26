using System.Numerics;

namespace ARealmRepopulated.Core.Numbers;

public static class NumberExtensions {

    /// <summary>
    /// Returns the value while it lies within min and max (both inclusive), otherwise the fallback.
    /// Unlike <see cref="Math.Clamp(int, int, int)"/>, a value out of range is not moved to the nearest bound,
    /// for values where the bound would be just as wrong as the value itself.
    /// </summary>
    public static T? InRangeOrDefault<T>(this T value, T min, T max, T? fallback = null) where T : struct, INumber<T>
        => value >= min && value <= max ? value : fallback;
}
