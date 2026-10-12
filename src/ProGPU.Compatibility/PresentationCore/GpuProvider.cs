using ProGPU.Backend;
using ProGPU.Scene;
using Silk.NET.WebGPU;

namespace System.Windows.Media;

internal static class GpuProvider
{
    private static readonly object s_compositorCacheScope = new();
    private static WgpuContext? _context;

    public static WgpuContext Context
    {
        get
        {
            var current = WgpuContext.Current;
            if (current != null && !current.IsDisposed)
            {
                return current;
            }

            if (WgpuContext.TryGetFirstActiveContext(out var active))
            {
                return active;
            }
            if (_context != null && !_context.IsDisposed)
            {
                return _context;
            }
            if (_context != null)
            {
                try { _context.Dispose(); } catch {}
            }
            _context = new WgpuContext();
            _context.Initialize(null);
            return _context;
        }
    }

    public static Compositor Compositor
    {
        get
        {
            var ctx = Context;
            return SharedCompositorCache.GetOrCreate(ctx, TextureFormat.Rgba8Unorm, s_compositorCacheScope);
        }
    }
}
