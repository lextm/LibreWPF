using System.Collections.Generic;

namespace ProGPU.Wpf.Interop;

public sealed class PortableRenderDataSnapshot
{
    public PortableRenderDataSnapshot(byte[] renderData, IReadOnlyList<object?> dependentResources)
    {
        ArgumentNullException.ThrowIfNull(renderData);
        ArgumentNullException.ThrowIfNull(dependentResources);

        RenderData = renderData;
        DependentResources = dependentResources;
    }

    public byte[] RenderData { get; }

    public IReadOnlyList<object?> DependentResources { get; }
}

public interface IPortableRenderDataSource
{
    bool TryGetPortableRenderDataSnapshot(out PortableRenderDataSnapshot snapshot);
}
