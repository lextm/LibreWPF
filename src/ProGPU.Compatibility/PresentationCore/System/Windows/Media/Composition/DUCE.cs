namespace System.Windows.Media.Composition;

internal partial class DUCE
{
    internal sealed partial class Channel
    {
    }

    internal readonly struct ResourceHandle
    {
        private readonly uint _handle;

        public ResourceHandle(uint handle)
        {
            _handle = handle;
        }

        public static ResourceHandle Null { get; } = new(0);

        public bool IsNull => _handle == 0;
    }
}
