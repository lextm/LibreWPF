namespace ProGPU.Wpf.Interop;

public interface IPortableDrawingContentSource
{
    bool TryGetPortableDrawingContent(out object? content);
}
