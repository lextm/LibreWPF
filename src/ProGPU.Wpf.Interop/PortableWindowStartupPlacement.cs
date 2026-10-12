namespace ProGPU.Wpf.Interop;

/// <summary>
/// Desktop-coordinate placement math for source-owned top-level windows.
/// The caller selects a monitor and supplies native host dimensions in the same
/// coordinate space; framebuffer DPI is not applied to desktop origins.
/// </summary>
public static class PortableWindowStartupPlacement
{
    public static bool TryCenterScreen(
        PortableRect workArea,
        double windowWidth,
        double windowHeight,
        out double left,
        out double top)
    {
        left = top = 0;
        if (!IsValidWorkArea(workArea) || !IsValidSize(windowWidth, windowHeight))
            return false;

        left = workArea.X + (workArea.Width - windowWidth) / 2;
        top = workArea.Y + (workArea.Height - windowHeight) / 2;
        return double.IsFinite(left) && double.IsFinite(top);
    }

    public static bool TryCenterOwner(
        PortableRect ownerBounds,
        PortableRect workArea,
        double windowWidth,
        double windowHeight,
        out double left,
        out double top)
    {
        left = top = 0;
        if (!PortablePopupMonitorSelection.IsFiniteRectangle(ownerBounds) ||
            !IsValidWorkArea(workArea) || !IsValidSize(windowWidth, windowHeight))
            return false;

        double centeredLeft = ownerBounds.X + (ownerBounds.Width - windowWidth) / 2;
        double centeredTop = ownerBounds.Y + (ownerBounds.Height - windowHeight) / 2;
        left = Math.Max(workArea.X, Math.Min(centeredLeft, workArea.X + workArea.Width - windowWidth));
        top = Math.Max(workArea.Y, Math.Min(centeredTop, workArea.Y + workArea.Height - windowHeight));
        return double.IsFinite(left) && double.IsFinite(top);
    }

    private static bool IsValidWorkArea(PortableRect workArea) =>
        PortablePopupMonitorSelection.IsFiniteRectangle(workArea);

    private static bool IsValidSize(double width, double height) =>
        double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0;
}
