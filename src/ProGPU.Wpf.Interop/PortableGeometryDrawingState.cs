namespace ProGPU.Wpf.Interop;

public interface IPortableGeometryDrawingStateSource
{
    bool TryGetPortableGeometryDrawingState(out PortableGeometryDrawingState state);
}

public sealed class PortableGeometryDrawingState
{
    public bool HasGeometry { get; set; }

    public object? Geometry { get; set; }

    public bool HasBrush { get; set; }

    public object? Brush { get; set; }

    public bool HasPen { get; set; }

    public object? Pen { get; set; }
}
