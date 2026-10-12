using System;

namespace System.Windows;

public readonly struct FontStretch : IFormattable, IEquatable<FontStretch>
{
    private readonly int _stretch;

    internal FontStretch(int stretch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stretch, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stretch, 9);
        _stretch = stretch - 5;
    }

    public static FontStretch UltraCondensed => FontStretches.UltraCondensed;
    public static FontStretch ExtraCondensed => FontStretches.ExtraCondensed;
    public static FontStretch Condensed => FontStretches.Condensed;
    public static FontStretch SemiCondensed => FontStretches.SemiCondensed;
    public static FontStretch Normal => FontStretches.Normal;
    public static FontStretch Medium => FontStretches.Medium;
    public static FontStretch SemiExpanded => FontStretches.SemiExpanded;
    public static FontStretch Expanded => FontStretches.Expanded;
    public static FontStretch ExtraExpanded => FontStretches.ExtraExpanded;
    public static FontStretch UltraExpanded => FontStretches.UltraExpanded;

    public int ToOpenTypeStretch() => _stretch + 5;

    public static int Compare(FontStretch left, FontStretch right) => left._stretch - right._stretch;

    public static bool operator <(FontStretch left, FontStretch right) => Compare(left, right) < 0;

    public static bool operator <=(FontStretch left, FontStretch right) => Compare(left, right) <= 0;

    public static bool operator >(FontStretch left, FontStretch right) => Compare(left, right) > 0;

    public static bool operator >=(FontStretch left, FontStretch right) => Compare(left, right) >= 0;

    public static bool operator ==(FontStretch left, FontStretch right) => Compare(left, right) == 0;

    public static bool operator !=(FontStretch left, FontStretch right) => !(left == right);

    public bool Equals(FontStretch other) => this == other;

    public override bool Equals(object? obj) => obj is FontStretch other && Equals(other);

    public override int GetHashCode() => ToOpenTypeStretch();

    public override string ToString() => FontStretches.FontStretchToString(ToOpenTypeStretch(), out var value)
        ? value
        : ToOpenTypeStretch().ToString();

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();
}
