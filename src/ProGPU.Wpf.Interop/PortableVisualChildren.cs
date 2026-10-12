namespace ProGPU.Wpf.Interop;

public interface IPortableVisualChildrenSource
{
    bool TryGetPortableVisualChildCount(out int count);

    bool TryGetPortableVisualChild(int index, out object? child);
}
