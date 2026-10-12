namespace ProGPU.Wpf.Interop;

public interface IPortableTransformMatrixSource
{
    bool TryGetPortableTransformMatrix(out PortableMatrix3x2 matrix);
}
