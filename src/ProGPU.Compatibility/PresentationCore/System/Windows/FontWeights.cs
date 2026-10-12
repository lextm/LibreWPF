namespace System.Windows;

public static class FontWeights
{
    public static FontWeight Thin => new(100);
    public static FontWeight ExtraLight => new(200);
    public static FontWeight UltraLight => new(200);
    public static FontWeight Light => new(300);
    public static FontWeight Normal => new(400);
    public static FontWeight Regular => new(400);
    public static FontWeight Medium => new(500);
    public static FontWeight DemiBold => new(600);
    public static FontWeight SemiBold => new(600);
    public static FontWeight Bold => new(700);
    public static FontWeight ExtraBold => new(800);
    public static FontWeight UltraBold => new(800);
    public static FontWeight Black => new(900);
    public static FontWeight Heavy => new(900);
    public static FontWeight ExtraBlack => new(950);
    public static FontWeight UltraBlack => new(950);

    internal static bool FontWeightToString(int weight, out string value)
    {
        switch (weight)
        {
            case 100:
                value = "Thin";
                return true;
            case 200:
                value = "ExtraLight";
                return true;
            case 300:
                value = "Light";
                return true;
            case 400:
                value = "Normal";
                return true;
            case 500:
                value = "Medium";
                return true;
            case 600:
                value = "SemiBold";
                return true;
            case 700:
                value = "Bold";
                return true;
            case 800:
                value = "ExtraBold";
                return true;
            case 900:
                value = "Black";
                return true;
            case 950:
                value = "ExtraBlack";
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }
}
