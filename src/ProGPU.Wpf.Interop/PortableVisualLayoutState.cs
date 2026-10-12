namespace ProGPU.Wpf.Interop;

public interface IPortableVisualLayoutStateSource
{
    bool TryGetPortableVisualLayoutState(out PortableVisualLayoutState state);
}

public sealed class PortableVisualLayoutState
{
    public bool HasRenderSize { get; set; }

    public PortableSize RenderSize { get; set; }

    public bool HasClipToBounds { get; set; }

    public bool ClipToBounds { get; set; }

    public bool HasLayoutClip { get; set; }

    public object? LayoutClip { get; set; }
}
