using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

internal static class AlderWindowResize
{
    public static void Attach(Window window, Panel grips)
    {
        if (!OperatingSystem.IsMacOS()) return;

        // The macOS backend handles TitleBar during non-client hit testing but does not resize
        // an undecorated window from the Resize* roles used by the other desktop backends.
        foreach (var grip in grips.Children)
        {
            var (_, cursor) = GetEdge(grip);
            grip.Cursor = new Cursor(cursor);
        }

        ResizeDrag? drag = null;
        grips.PointerPressed += (_, e) =>
        {
            if (drag is not null || e.Source is not Control grip || !window.CanResize ||
                window.WindowState != WindowState.Normal || !e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                return;

            var (direction, _) = GetEdge(grip);
            if (direction == default) return;
            drag = new ResizeDrag(e.Pointer, direction, window.PointToScreen(e.GetPosition(window)),
                window.Position, window.ClientSize, window.DesktopScaling);
            e.Pointer.Capture(grips);
            e.Handled = true;
        };
        grips.PointerMoved += (_, e) =>
        {
            if (drag is not { } current || e.Pointer != current.Pointer) return;

            // Keep the last sample in screen coordinates while the window moves under the pointer.
            current.PendingPosition = window.PointToScreen(e.GetPosition(window));
            if (!current.FrameQueued)
            {
                current.FrameQueued = true;
                window.RequestAnimationFrame(_ => ApplyPendingResize(current));
            }
            e.Handled = true;
        };
        grips.PointerReleased += (_, e) =>
        {
            if (drag is not { } current || e.Pointer != current.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
            current.PendingPosition = window.PointToScreen(e.GetPosition(window));
            ApplyPendingResize(current);
            drag = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        };
        grips.PointerCaptureLost += (_, e) =>
        {
            if (drag?.Pointer == e.Pointer) drag = null;
        };

        void ApplyPendingResize(ResizeDrag current)
        {
            current.FrameQueued = false;
            if (!ReferenceEquals(drag, current) || current.PendingPosition is not { } pointer ||
                !window.IsVisible || !window.CanResize || window.WindowState != WindowState.Normal) return;

            current.PendingPosition = null;
            var delta = pointer - current.PointerPosition;
            double width = Math.Clamp(current.Size.Width + current.Direction.X * delta.X / current.Scale, window.MinWidth, window.MaxWidth);
            double height = Math.Clamp(current.Size.Height + current.Direction.Y * delta.Y / current.Scale, window.MinHeight, window.MaxHeight);
            var clientSize = window.ClientSize;
            var requestedWidth = double.IsNaN(window.Width) ? clientSize.Width : window.Width;
            var requestedHeight = double.IsNaN(window.Height) ? clientSize.Height : window.Height;
            if (width == requestedWidth && height == requestedHeight) return;

            // Update both dimensions before layout and keep the opposite edge fixed on north/west drags.
            window.SetCurrentValue(Window.WidthProperty, width);
            window.SetCurrentValue(Window.HeightProperty, height);
            int x = current.Position.X;
            int y = current.Position.Y;
            if (current.Direction.X < 0) x += (int)Math.Round((current.Size.Width - width) * current.Scale);
            if (current.Direction.Y < 0) y += (int)Math.Round((current.Size.Height - height) * current.Scale);
            if (current.Direction.X < 0 || current.Direction.Y < 0)
                window.Position = new PixelPoint(x, y);
        }
    }

    private static (Vector Direction, StandardCursorType Cursor) GetEdge(Control grip) =>
        WindowDecorationProperties.GetElementRole(grip) switch
        {
            WindowDecorationsElementRole.ResizeN => (new Vector(0, -1), StandardCursorType.SizeNorthSouth),
            WindowDecorationsElementRole.ResizeS => (new Vector(0, 1), StandardCursorType.SizeNorthSouth),
            WindowDecorationsElementRole.ResizeE => (new Vector(1, 0), StandardCursorType.SizeWestEast),
            WindowDecorationsElementRole.ResizeW => (new Vector(-1, 0), StandardCursorType.SizeWestEast),
            WindowDecorationsElementRole.ResizeNE => (new Vector(1, -1), StandardCursorType.TopRightCorner),
            WindowDecorationsElementRole.ResizeNW => (new Vector(-1, -1), StandardCursorType.TopLeftCorner),
            WindowDecorationsElementRole.ResizeSE => (new Vector(1, 1), StandardCursorType.BottomRightCorner),
            WindowDecorationsElementRole.ResizeSW => (new Vector(-1, 1), StandardCursorType.BottomLeftCorner),
            _ => (default, StandardCursorType.Arrow)
        };

    private sealed record ResizeDrag(IPointer Pointer, Vector Direction, PixelPoint PointerPosition,
        PixelPoint Position, Size Size, double Scale)
    {
        public PixelPoint? PendingPosition { get; set; }
        public bool FrameQueued { get; set; }
    }
}
