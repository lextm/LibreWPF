using System;

namespace System.Windows;

public readonly struct FontStyle : IFormattable, IEquatable<FontStyle>
{
    private readonly int _style;

    internal FontStyle(int style)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(style, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(style, 2);
        _style = style;
    }

    public static FontStyle Normal => FontStyles.Normal;
    public static FontStyle Oblique => FontStyles.Oblique;
    public static FontStyle Italic => FontStyles.Italic;

    internal bool IsSlanted => _style != 0;

    public static bool operator ==(FontStyle left, FontStyle right) => left._style == right._style;

    public static bool operator !=(FontStyle left, FontStyle right) => !(left == right);

    public bool Equals(FontStyle other) => this == other;

    public override bool Equals(object? obj) => obj is FontStyle other && Equals(other);

    public override int GetHashCode() => _style;

    public override string ToString() => _style switch
    {
        1 => "Oblique",
        2 => "Italic",
        _ => "Normal"
    };

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();
}
