namespace System.Windows.Media;

public static class PortableRenderDataDrawingContextSinkProvider
{
    private static readonly object SyncRoot = new();
    private static SinkFactoryScope? s_currentObjectScope;
    private static DrawingContextFactoryScope? s_currentDrawingContextScope;

    public static Func<object?, IPortableRenderDataDrawingContextSink?>? ObjectSinkFactory { get; private set; }

    public static Func<object?, DrawingContext?>? DrawingContextFactory { get; private set; }

    public static IDisposable PushObjectSinkFactory(Func<object?, IPortableRenderDataDrawingContextSink?> sinkFactory)
    {
        ArgumentNullException.ThrowIfNull(sinkFactory);

        lock (SyncRoot)
        {
            var scope = new SinkFactoryScope(s_currentObjectScope, sinkFactory);
            s_currentObjectScope = scope;
            ObjectSinkFactory = sinkFactory;
            return scope;
        }
    }

    public static IDisposable PushDrawingContextFactory(Func<object?, DrawingContext?> drawingContextFactory)
    {
        ArgumentNullException.ThrowIfNull(drawingContextFactory);

        lock (SyncRoot)
        {
            var scope = new DrawingContextFactoryScope(s_currentDrawingContextScope, drawingContextFactory);
            s_currentDrawingContextScope = scope;
            DrawingContextFactory = drawingContextFactory;
            return scope;
        }
    }

    private static void RestoreSinkFactory(SinkFactoryScope scope)
    {
        lock (SyncRoot)
        {
            if (scope.IsDisposed)
            {
                return;
            }

            scope.IsDisposed = true;

            if (!ReferenceEquals(s_currentObjectScope, scope))
            {
                return;
            }

            do
            {
                s_currentObjectScope = s_currentObjectScope.PreviousScope;
            }
            while (s_currentObjectScope != null && s_currentObjectScope.IsDisposed);

            ObjectSinkFactory = s_currentObjectScope?.SinkFactory;
        }
    }

    private static void RestoreDrawingContextFactory(DrawingContextFactoryScope scope)
    {
        lock (SyncRoot)
        {
            if (scope.IsDisposed)
            {
                return;
            }

            scope.IsDisposed = true;

            if (!ReferenceEquals(s_currentDrawingContextScope, scope))
            {
                return;
            }

            do
            {
                s_currentDrawingContextScope = s_currentDrawingContextScope.PreviousScope;
            }
            while (s_currentDrawingContextScope != null && s_currentDrawingContextScope.IsDisposed);

            DrawingContextFactory = s_currentDrawingContextScope?.DrawingContextFactory;
        }
    }

    private sealed class SinkFactoryScope : IDisposable
    {
        internal readonly SinkFactoryScope? PreviousScope;
        internal readonly Func<object?, IPortableRenderDataDrawingContextSink?> SinkFactory;
        internal bool IsDisposed;

        internal SinkFactoryScope(
            SinkFactoryScope? previousScope,
            Func<object?, IPortableRenderDataDrawingContextSink?> sinkFactory)
        {
            PreviousScope = previousScope;
            SinkFactory = sinkFactory;
        }

        public void Dispose()
        {
            RestoreSinkFactory(this);
        }
    }

    private sealed class DrawingContextFactoryScope : IDisposable
    {
        internal readonly DrawingContextFactoryScope? PreviousScope;
        internal readonly Func<object?, DrawingContext?> DrawingContextFactory;
        internal bool IsDisposed;

        internal DrawingContextFactoryScope(
            DrawingContextFactoryScope? previousScope,
            Func<object?, DrawingContext?> drawingContextFactory)
        {
            PreviousScope = previousScope;
            DrawingContextFactory = drawingContextFactory;
        }

        public void Dispose()
        {
            RestoreDrawingContextFactory(this);
        }
    }
}
