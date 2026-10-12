using System;
using System.Globalization;

namespace System.Windows;

public readonly struct FontWeight : IFormattable, IEquatable<FontWeight>
{
    private readonly int _weight;

    internal FontWeight(int weight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, 999);
        _weight = weight - 400;
    }

    public static FontWeight Thin => FontWeights.Thin;
    public static FontWeight ExtraLight => FontWeights.ExtraLight;
    public static FontWeight UltraLight => FontWeights.UltraLight;
    public static FontWeight Light => FontWeights.Light;
    public static FontWeight Normal => FontWeights.Normal;
    public static FontWeight Regular => FontWeights.Regular;
    public static FontWeight Medium => FontWeights.Medium;
    public static FontWeight DemiBold => FontWeights.DemiBold;
    public static FontWeight SemiBold => FontWeights.SemiBold;
    public static FontWeight Bold => FontWeights.Bold;
    public static FontWeight ExtraBold => FontWeights.ExtraBold;
    public static FontWeight UltraBold => FontWeights.UltraBold;
    public static FontWeight Black => FontWeights.Black;
    public static FontWeight Heavy => FontWeights.Heavy;
    public static FontWeight ExtraBlack => FontWeights.ExtraBlack;
    public static FontWeight UltraBlack => FontWeights.UltraBlack;

    public static FontWeight FromOpenTypeWeight(int weightValue) => new(weightValue);

    public int ToOpenTypeWeight() => _weight + 400;

    internal bool IsBold => ToOpenTypeWeight() >= 600;

    public static int Compare(FontWeight left, FontWeight right) => left._weight - right._weight;

    public static bool operator <(FontWeight left, FontWeight right) => Compare(left, right) < 0;

    public static bool operator <=(FontWeight left, FontWeight right) => Compare(left, right) <= 0;

    public static bool operator >(FontWeight left, FontWeight right) => Compare(left, right) > 0;

    public static bool operator >=(FontWeight left, FontWeight right) => Compare(left, right) >= 0;

    public static bool operator ==(FontWeight left, FontWeight right) => Compare(left, right) == 0;

    public static bool operator !=(FontWeight left, FontWeight right) => !(left == right);

    public bool Equals(FontWeight other) => this == other;

    public override bool Equals(object? obj) => obj is FontWeight other && Equals(other);

    public override int GetHashCode() => ToOpenTypeWeight();

    public override string ToString() => ConvertToString(null, CultureInfo.CurrentCulture);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ConvertToString(format, formatProvider);

    private string ConvertToString(string? format, IFormatProvider? formatProvider)
    {
        return FontWeights.FontWeightToString(ToOpenTypeWeight(), out var value)
            ? value
            : ToOpenTypeWeight().ToString(format, formatProvider);
    }
}
