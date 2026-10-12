namespace System.Windows;

public static class PortablePresentationSourceHost
{
    public static IPortablePresentationSourceHost Create(double dpiScaleX = 1.0, double dpiScaleY = 1.0)
    {
        throw new PlatformNotSupportedException("Portable presentation sources are provided by the WPF transport assembly at runtime.");
    }
}
