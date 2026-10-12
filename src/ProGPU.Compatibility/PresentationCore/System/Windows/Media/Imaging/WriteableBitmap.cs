using ProGPU.Backend;
using ProGPU.Scene;
using System;

namespace System.Windows
{
    public struct Int32Rect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Int32Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}

namespace System.Windows.Media.Imaging
{
    public readonly struct PixelFormat : IEquatable<PixelFormat>
    {
        private readonly PixelDataFormat _format;

        internal PixelFormat(PixelDataFormat format)
        {
            _format = format;
        }

        internal PixelDataFormat ProGpuFormat => _format;

        public bool Equals(PixelFormat other)
        {
            return _format == other._format;
        }

        public override bool Equals(object? obj)
        {
            return obj is PixelFormat other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)_format;
        }

        public override string ToString()
        {
            return _format.ToString();
        }

        public static bool operator ==(PixelFormat left, PixelFormat right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PixelFormat left, PixelFormat right)
        {
            return !left.Equals(right);
        }
    }

    public static class PixelFormats
    {
        public static PixelFormat Bgr32 { get; } = new PixelFormat(PixelDataFormat.Bgr32);
        public static PixelFormat Pbgra32 { get; } = new PixelFormat(PixelDataFormat.Pbgra32);
    }

    public class BitmapPalette
    {
    }

    public class WriteableBitmap : BitmapSource
    {
        private readonly GpuTexture _texture;
        private readonly PixelFormat _pixelFormat;

        public override int PixelWidth => (int)_texture.Width;
        public override int PixelHeight => (int)_texture.Height;
        public override GpuTexture GpuTexture => _texture;
        public PixelFormat Format => _pixelFormat;

        public WriteableBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat, BitmapPalette? palette)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "WriteableBitmap pixel width must be positive.");
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "WriteableBitmap pixel height must be positive.");
            }

            _pixelFormat = pixelFormat;
            _texture = new GpuTexture(
                GpuProvider.Context,
                (uint)pixelWidth,
                (uint)pixelHeight,
                Silk.NET.WebGPU.TextureFormat.Bgra8Unorm,
                Silk.NET.WebGPU.TextureUsage.RenderAttachment | Silk.NET.WebGPU.TextureUsage.CopySrc | Silk.NET.WebGPU.TextureUsage.CopyDst | Silk.NET.WebGPU.TextureUsage.TextureBinding,
                "WPF WriteableBitmap Backing Texture",
                alphaMode: GpuTextureAlphaMode.Premultiplied
            );
        }

        public void WritePixels(Int32Rect sourceRect, IntPtr buffer, int bufferSize, int stride)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);
            if (buffer == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            unsafe
            {
                var pixels = new ReadOnlySpan<byte>((void*)buffer, bufferSize).ToArray();
                WritePbgra32Pixels(sourceRect, ConvertWritePixelsBuffer(sourceRect, _pixelFormat, pixels, stride));
            }
        }

        public void WritePbgra32Pixels(Int32Rect sourceRect, Pbgra32PixelBuffer pixels)
        {
            _texture.WritePbgra32SubRect(pixels, (uint)sourceRect.X, (uint)sourceRect.Y);
        }

        private static Pbgra32PixelBuffer ConvertWritePixelsBuffer(
            Int32Rect sourceRect,
            PixelFormat pixelFormat,
            byte[] pixels,
            int stride)
        {
            ArgumentNullException.ThrowIfNull(pixels);
            if (sourceRect.X < 0 || sourceRect.Y < 0 || sourceRect.Width <= 0 || sourceRect.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceRect), "WriteableBitmap source rectangle must be non-empty and non-negative.");
            }

            var format = pixelFormat.ProGpuFormat;
            if (!PixelDataConverter.TryGetSourceByteLength(sourceRect.Width, sourceRect.Height, stride, format, out var requiredByteLength)
                || pixels.Length < requiredByteLength)
            {
                throw new ArgumentException("WriteableBitmap pixel buffer does not contain the requested source rectangle rows.", nameof(pixels));
            }

            if (format == PixelDataFormat.Pbgra32)
            {
                return new Pbgra32PixelBuffer(sourceRect.Width, sourceRect.Height, stride, pixels);
            }

            var source = new PixelDataBuffer(sourceRect.Width, sourceRect.Height, stride, format, pixels);
            return source.ConvertToPbgra32();
        }
    }
}
