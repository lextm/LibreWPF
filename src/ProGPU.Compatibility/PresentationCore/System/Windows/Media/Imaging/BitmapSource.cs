using ProGPU.Backend;

namespace System.Windows.Media.Imaging;

public abstract class BitmapSource : ImageSource, IProGpuTextureSource
{
    public abstract int PixelWidth { get; }
    public abstract int PixelHeight { get; }
    public abstract GpuTexture GpuTexture { get; }

    public bool TryGetGpuTexture(out GpuTexture texture)
    {
        texture = GpuTexture;
        return texture != null && !texture.IsDisposed;
    }
}
