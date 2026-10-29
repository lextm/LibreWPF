// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Windows.Input;
using System.Windows.Media;
using LibreWinForms.Platform;
using ProGPU.Wpf.Interop;

using SD = System.Drawing;
using SWF = System.Windows.Forms;

namespace System.Windows.Forms.Integration
{
    // The canonical WinForms of this fork (LibreWinForms) gives every control other than a Form a
    // logical handle: there is no child HWND to parent, on any OS. The host therefore draws the control
    // tree into its own DrawingContext and forwards pointer input to it, instead of building a window.
    public partial class WindowsFormsHost
    {
        private bool _visualHostInitialized;

        /// <summary>
        ///     The hosted control has no window of its own (LibreWinForms), so the host owns its visual
        ///     content under a native HwndSource as well as under a portable one.
        /// </summary>
        protected override bool UsesPortableVisualHosting => true;

        private void EnsureVisualHost()
        {
            if (_visualHostInitialized)
            {
                return;
            }

            _visualHostInitialized = true;
            _hostContainerInternal.CreateControl();
            _hostContainerInternal.Invalidated += (_, _) => Dispatcher.BeginInvoke(new Action(InvalidateVisual));
            Loaded += ApplyAllProperties;
            _propertyMap.ApplyAll();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            EnsureVisualHost();
        }

        // WinForms pixels per WPF device-independent unit, the factor Convert applies to layout sizes.
        private double VisualPixelsPerUnitX => Math.Max(_currentScale.X, 1e-6) * GetDpi().DpiScaleX;

        private double VisualPixelsPerUnitY => Math.Max(_currentScale.Y, 1e-6) * GetDpi().DpiScaleY;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (Child == null || !TryGetNativeDrawingContext(drawingContext, out ProGPU.Scene.DrawingContext nativeContext, out Matrix4x4 outerTransform))
            {
                return;
            }

            SD.Size size = _hostContainerInternal.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            // Draw in WinForms pixels, scaled down into WPF units.
            Matrix4x4 transform = Matrix4x4.CreateScale((float)(1d / VisualPixelsPerUnitX), (float)(1d / VisualPixelsPerUnitY), 1f) * outerTransform;
            using SD.Graphics graphics = SD.Graphics.FromProGpuDrawingContext(nativeContext, transform);
            LibreControlHosting.Paint(_hostContainerInternal, graphics, new SD.Rectangle(SD.Point.Empty, size));
        }

        private static bool TryGetNativeDrawingContext(
            DrawingContext drawingContext,
            out ProGPU.Scene.DrawingContext nativeContext,
            out Matrix4x4 outerTransform)
        {
            nativeContext = null;
            outerTransform = Matrix4x4.Identity;
            if (drawingContext is IPortableNativeDrawingContextStateSource stateSource
                && stateSource.TryGetPortableNativeDrawingContextState(out var portableState)
                && ProGPU.Scene.ProGpuDrawingContextState.TryCreate(
                    portableState.NativeDrawingContext,
                    portableState.Transform,
                    out ProGPU.Scene.ProGpuDrawingContextState state))
            {
                nativeContext = state.DrawingContext;
                outerTransform = state.OuterTransform;
                return true;
            }

            if (drawingContext is IPortableNativeDrawingContextSource source
                && source.TryGetPortableNativeDrawingContext(out object resolved)
                && resolved is ProGPU.Scene.DrawingContext context)
            {
                nativeContext = context;
                return true;
            }

            return false;
        }

        private LibrePoint ToWinFormsPoint(System.Windows.Input.MouseEventArgs e)
        {
            Point position = e.GetPosition(this);
            return new LibrePoint(
                Convert.ToBoundedInt(position.X * VisualPixelsPerUnitX),
                Convert.ToBoundedInt(position.Y * VisualPixelsPerUnitY));
        }

        private static LibrePointerButton ToLibreButton(MouseButton button) => button switch
        {
            MouseButton.Left => LibrePointerButton.Primary,
            MouseButton.Right => LibrePointerButton.Secondary,
            MouseButton.Middle => LibrePointerButton.Middle,
            MouseButton.XButton1 => LibrePointerButton.XButton1,
            MouseButton.XButton2 => LibrePointerButton.XButton2,
            _ => LibrePointerButton.None,
        };

        private static LibreInputModifiers CurrentModifiers()
        {
            ModifierKeys keys = Keyboard.Modifiers;
            LibreInputModifiers modifiers = LibreInputModifiers.None;
            if ((keys & ModifierKeys.Shift) != 0) modifiers |= LibreInputModifiers.Shift;
            if ((keys & ModifierKeys.Control) != 0) modifiers |= LibreInputModifiers.Control;
            if ((keys & ModifierKeys.Alt) != 0) modifiers |= LibreInputModifiers.Alt;
            if ((keys & ModifierKeys.Windows) != 0) modifiers |= LibreInputModifiers.Meta;
            return modifiers;
        }

        private void ForwardPointer(LibreInputEventKind kind, System.Windows.Input.MouseEventArgs e, LibrePointerButton button)
        {
            if (Child == null)
            {
                return;
            }

            LibreControlHosting.DispatchInput(
                _hostContainerInternal,
                new LibreInputEvent(kind, Environment.TickCount64, CurrentModifiers(), default, null, ToWinFormsPoint(e), default, button));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            ForwardPointer(LibreInputEventKind.PointerDown, e, ToLibreButton(e.ChangedButton));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ForwardPointer(LibreInputEventKind.PointerUp, e, ToLibreButton(e.ChangedButton));
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ForwardPointer(LibreInputEventKind.PointerMove, e, LibrePointerButton.None);
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            ForwardPointer(LibreInputEventKind.PointerLeave, e, LibrePointerButton.None);
        }
    }
}
