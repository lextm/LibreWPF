namespace ProGPU.Wpf.Interop;

public interface IPortableImageDrawingStateSource
{
    bool TryGetPortableImageDrawingState(out PortableImageDrawingState state);
}

public sealed class PortableImageDrawingState
{
    public bool HasImageSource { get; set; }

    public object? ImageSource { get; set; }

    public bool HasRect { get; set; }

    public PortableRect Rect { get; set; } = PortableRect.Empty;
}
