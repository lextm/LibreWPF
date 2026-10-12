namespace ProGPU.Wpf.Interop;

public sealed class PortableGeometryHitTestCandidate
{
    public PortableGeometryHitTestCandidate(object visualHit, uint intersectionDetail)
    {
        ArgumentNullException.ThrowIfNull(visualHit);

        VisualHit = visualHit;
        IntersectionDetail = intersectionDetail;
    }

    public object VisualHit { get; }

    public uint IntersectionDetail { get; }
}
