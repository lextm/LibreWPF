namespace System.Windows;

public static class FontStretches
{
    public static FontStretch UltraCondensed => new(1);
    public static FontStretch ExtraCondensed => new(2);
    public static FontStretch Condensed => new(3);
    public static FontStretch SemiCondensed => new(4);
    public static FontStretch Normal => new(5);
    public static FontStretch Medium => new(5);
    public static FontStretch SemiExpanded => new(6);
    public static FontStretch Expanded => new(7);
    public static FontStretch ExtraExpanded => new(8);
    public static FontStretch UltraExpanded => new(9);

    internal static bool FontStretchToString(int stretch, out string value)
    {
        switch (stretch)
        {
            case 1:
                value = "UltraCondensed";
                return true;
            case 2:
                value = "ExtraCondensed";
                return true;
            case 3:
                value = "Condensed";
                return true;
            case 4:
                value = "SemiCondensed";
                return true;
            case 5:
                value = "Normal";
                return true;
            case 6:
                value = "SemiExpanded";
                return true;
            case 7:
                value = "Expanded";
                return true;
            case 8:
                value = "ExtraExpanded";
                return true;
            case 9:
                value = "UltraExpanded";
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }
}
