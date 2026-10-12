namespace ProGPU.Wpf.Interop;

public interface IPortableVisualBoundsSource
{
    bool TryGetPortableVisualBounds(out PortableVisualBounds bounds);
}

public sealed class PortableVisualBounds
{
    public bool HasContentBounds { get; set; }

    public PortableRect ContentBounds { get; set; } = PortableRect.Empty;

    public bool HasDescendantBounds { get; set; }

    public PortableRect DescendantBounds { get; set; } = PortableRect.Empty;
}
