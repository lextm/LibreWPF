namespace ProGPU.Wpf.Interop;

public interface IPortableGlyphRunDrawingStateSource
{
    bool TryGetPortableGlyphRunDrawingState(out PortableGlyphRunDrawingState state);
}

public sealed class PortableGlyphRunDrawingState
{
    public bool HasGlyphRun { get; set; }

    public object? GlyphRun { get; set; }

    public bool HasForegroundBrush { get; set; }

    public object? ForegroundBrush { get; set; }
}
