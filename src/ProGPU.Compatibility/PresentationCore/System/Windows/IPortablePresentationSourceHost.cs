using System;

namespace System.Windows;

public delegate bool PortableHitTestAllBufferOverride(double x, double y, Span<object?> results, out int resultCount);

public delegate bool PortableGeometryHitTestBufferOverride(
    double minX,
    double minY,
    double maxX,
    double maxY,
    Span<object?> results,
    out int resultCount);

public interface IPortablePresentationSourceHost : IDisposable
{
    event EventHandler? RenderRequested;

    event EventHandler? CursorRequested;

    object? RootVisual { get; set; }

    object? CompositionTarget { get; }

    IntPtr Handle { get; }

    object? RequestedCursor { get; }

    string? RequestedCursorName { get; }

    Func<double, double, object?>? HitTestOverride { get; set; }

    Func<double, double, object?[]?>? HitTestAllOverride { get; set; }

    PortableHitTestAllBufferOverride? HitTestAllBufferOverride { get; set; }

    Func<double, double, double, double, object?[]?>? HitTestBoundsOverride { get; set; }

    PortableGeometryHitTestBufferOverride? HitTestBoundsBufferOverride { get; set; }

    Func<double, double, double, double, object?[]?>? HitTestEllipseBoundsOverride { get; set; }

    PortableGeometryHitTestBufferOverride? HitTestEllipseBoundsBufferOverride { get; set; }

    void SetDeviceScale(double dpiScaleX, double dpiScaleY);

    void SetClientSize(double width, double height);

    /// <summary>
    /// Sets this source's client origin in absolute screen-device coordinates.
    /// Main presentation sources use the native owner's client-screen origin; composited popup
    /// sources use their absolute popup-screen position so client/screen conversions remain
    /// correct after owner moves and for nested popups. A compositor that renders a popup into
    /// its owner surface subtracts the owner's client-screen origin from the popup position.
    /// </summary>
    void SetClientOrigin(double x, double y)
    {
    }

    bool TryUpdateRootVisualClientSize(out double width, out double height);

    /// <summary>
    /// Queries this source's ordinary input hit test in root coordinates,
    /// without dispatching input or changing mouse capture. A successful
    /// query can report a miss; an unavailable query must not imply a hit.
    /// </summary>
    bool TryHitTestInput(double x, double y, out bool isHit)
    {
        isHit = false;
        return false;
    }

    bool DispatchHwndSourceHook(int message, IntPtr wParam, IntPtr lParam, out IntPtr result, out bool handled);
}
