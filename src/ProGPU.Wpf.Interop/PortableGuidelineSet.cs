using System;

namespace ProGPU.Wpf.Interop;

public interface IPortableGuidelineSetSource
{
    bool TryGetPortableGuidelineSet(out PortableGuidelineSet guidelineSet);
}

public sealed class PortableGuidelineSet
{
    public PortableGuidelineSet(
        bool isFrozen,
        bool isDynamic,
        double[]? guidelinesX,
        double[]? guidelinesY)
    {
        IsFrozen = isFrozen;
        IsDynamic = isDynamic;
        GuidelinesX = guidelinesX ?? Array.Empty<double>();
        GuidelinesY = guidelinesY ?? Array.Empty<double>();
    }

    public bool IsFrozen { get; }

    public bool IsDynamic { get; }

    public double[] GuidelinesX { get; }

    public double[] GuidelinesY { get; }
}
