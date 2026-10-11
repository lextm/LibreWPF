using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.ProGPU;
using System.Windows.Media.ProGPU.Composition;
using System.Windows.Media.ProGPU.Composition.Mil;
using ProGPU.Scene;
using ProGPU.Wpf.Interop;
using Xunit;

namespace ProGPU.Wpf.Tests.Composition.Mil;

public sealed partial class WpfVisualTreeRendererTests
{
    [Fact]
    public void DefaultRetainedVisualStateRemainsVisible()
    {
        Assert.True(default(WpfRetainedVisualState).IsVisible);
        Assert.True(new WpfRetainedVisualState(Vector2.Zero, Matrix4x4.Identity, 1, null).IsVisible);
        Assert.False(new WpfRetainedVisualState(Vector2.Zero, Matrix4x4.Identity, 1, null, isVisible: false).IsVisible);
    }

    [Theory]
    [InlineData(PortableVisualVisibility.Hidden)]
    [InlineData(PortableVisualVisibility.Collapsed)]
    public void LocalVisibilityRetiresRetainedMaskedSubtreeAndRestoresSameSource(PortableVisualVisibility hidden)
    {
        var state = new PortableVisualState { HasVisibility = true, HasOpacity = true };
        var root = new FakePortableVisualStateDrawingVisual(null, state);
        var maskedChild = new FakePortableVisualStateDrawingVisual(CreateRenderData(Brushes.Red),
            CreatePortableOpacityMaskState(Brushes.White)) { Bounds = new FakeRect(0, 0, 30, 20) };
        root.Children.Add(maskedChild);
        var sceneRoot = new ProGPU.Scene.ContainerVisual();
        var retainedRoot = new ProGPU.Scene.ContainerVisual();
        var frame = new ProGpuWpfDrawingFrame(sceneRoot, retainedRoot, new ProGPU.Scene.DrawingVisual(), 80, 60,
            hitTestOwnerMap: new WpfGpuHitTestOwnerMap());
        var renderer = new WpfVisualTreeRenderer();
        ProGpuRetainedDrawingVisual owner;
        using (var sink = new ProGpuRetainedCompositionCommandSink(frame, null, null))
        {
            renderer.ReplaySubtree(root, sink);
            owner = Assert.IsType<ProGpuRetainedDrawingVisual>(Assert.Single(sink.RootVisual.Children));
            Assert.True(owner.IsVisible);
            Assert.NotNull(Assert.Single(owner.Children).OpacityMask);
        }

        // Reuse the actual retained owner, as a state-only dirty branch does.
        state.Visibility = hidden;
        state.Opacity = 0;
        using (var sink = new ProGpuRetainedCompositionCommandSink(frame, owner, null, null))
            Assert.True(renderer.TryReplaySubtreeIntoCurrentRetainedVisual(root, sink, null, null, out _));
        Assert.False(owner.IsVisible);
        Assert.NotNull(Assert.Single(owner.Children).OpacityMask);
        using var capture = new GpuRenderCommandHitTestCacheBuilder();
        capture.AddSourceVisual(owner, Matrix4x4.Identity);
        Assert.Empty(capture.BuildIndex().Primitives);

        state.Visibility = PortableVisualVisibility.Visible;
        state.Opacity = 1;
        owner.ClearChildren();
        using (var sink = new ProGpuRetainedCompositionCommandSink(frame, owner, null, null))
            Assert.True(renderer.TryReplaySubtreeIntoCurrentRetainedVisual(root, sink, null, null, out _));
        Assert.True(owner.IsVisible);
        Assert.NotNull(Assert.Single(owner.Children).OpacityMask);
        capture.Clear();
        // Actual source visuals now explicitly preserve geometry through masks.
        // The mask neither contributes a hit rectangle nor clips source input.
        capture.AddSourceVisual(owner, Matrix4x4.Identity);
        var hit = Assert.Single(capture.BuildIndex().Primitives);
        Assert.Equal(global::ProGPU.Vector.GpuHitTestPrimitiveKind.RectangleFill, hit.Kind);
        Assert.Equal(new Vector2(1, 2), hit.BoundsMin);
        Assert.Equal(new Vector2(31, 42), hit.BoundsMax);
    }

    [Theory]
    [InlineData(PortableVisualVisibility.Hidden)]
    [InlineData(PortableVisualVisibility.Collapsed)]
    public void ExplicitCacheRootOmitsVisibilityButDescendantsKeepIt(PortableVisualVisibility hidden)
    {
        var root = new FakePortableVisualStateDrawingVisual(CreateRenderData(Brushes.Red),
            new PortableVisualState { HasVisibility = true, Visibility = hidden });
        root.Children.Add(new FakePortableVisualStateDrawingVisual(CreateRenderData(Brushes.Blue),
            new PortableVisualState { HasVisibility = true, Visibility = hidden }));
        var ordinary = new TestSink();
        new WpfVisualTreeRenderer().ReplaySubtree(root, ordinary);
        Assert.Empty(ordinary.DrawRectangles);
        var cached = new TestSink();
        new WpfVisualTreeRenderer().ReplayBitmapCacheBrushSource(root, cached);
        Assert.Same(Brushes.Red, Assert.Single(cached.DrawRectangles).Brush);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisibleZeroOpacityIsNotSubtreeInvisibility(bool explicitVisibility)
    {
        var root = new FakePortableVisualStateDrawingVisual(CreateRenderData(Brushes.Red),
            new PortableVisualState { HasVisibility = explicitVisibility, HasOpacity = true, Opacity = 0 });
        var sink = new TestSink { AcceptRetainedVisualOwners = true };
        new WpfVisualTreeRenderer().ReplaySubtree(root, sink);
        Assert.True(Assert.Single(sink.RetainedVisualStates).IsVisible);
        Assert.Single(sink.DrawRectangles);
    }
}
