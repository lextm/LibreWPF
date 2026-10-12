using System.Numerics;
using System.Windows.Media.Composition;

namespace System.Windows.Media;

public abstract class Brush : Freezable, ProGPU.Wpf.Interop.IPortableInvalidationSource
{
    private double _opacity = 1.0;
    private Transform? _relativeTransform;
    private Transform? _transform;
    private uint _changeVersion;

    public event EventHandler? Changed;

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (_opacity.Equals(value))
            {
                return;
            }

            _opacity = value;
            OnChanged();
        }
    }

    public Transform? RelativeTransform
    {
        get => _relativeTransform;
        set
        {
            if (ReferenceEquals(_relativeTransform, value))
            {
                return;
            }

            _relativeTransform = value;
            OnChanged();
        }
    }

    public Transform? Transform
    {
        get => _transform;
        set
        {
            if (ReferenceEquals(_transform, value))
            {
                return;
            }

            _transform = value;
            OnChanged();
        }
    }

    public uint ChangeVersion => _changeVersion;

    public virtual ProGPU.Vector.Brush ToNative()
    {
        return new ProGPU.Vector.SolidColorBrush(Vector4.Zero);
    }

    public virtual ProGPU.Vector.Brush ToNative(Rect targetBounds)
    {
        return ToNative();
    }

    internal virtual DUCE.ResourceHandle AddRefOnChannelCore(DUCE.Channel channel)
    {
        return DUCE.ResourceHandle.Null;
    }

    internal virtual void ReleaseOnChannelCore(DUCE.Channel channel)
    {
    }

    internal virtual DUCE.ResourceHandle GetHandleCore(DUCE.Channel channel)
    {
        return DUCE.ResourceHandle.Null;
    }

    internal virtual int GetChannelCountCore()
    {
        return 0;
    }

    internal virtual DUCE.Channel GetChannelCore(int index)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    protected void OnChanged()
    {
        unchecked
        {
            _changeVersion++;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    bool ProGPU.Wpf.Interop.IPortableInvalidationSource.TrySubscribeInvalidated(EventHandler handler, out IDisposable subscription)
    {
        Changed += handler;
        subscription = new ProGPU.Wpf.Interop.PortableInvalidationSubscription(() => Changed -= handler);
        return true;
    }
}
