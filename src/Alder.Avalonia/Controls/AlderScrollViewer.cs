using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A scroll viewport with Alder scrollbars, retaining native scrolling and input support.</summary>
public class AlderScrollViewer : ScrollViewer
{
    private double thumbGrabOffset;

    /// <summary>Initializes a scroll viewport with native scrolling behavior.</summary>
    public AlderScrollViewer() { }

    /// <summary>Optionally rechecks the end of a variable-height extent after the vertical thumb is released there.</summary>
    protected AlderScrollViewer(bool resolveVerticalThumbEnd)
    {
        if (!resolveVerticalThumbEnd) return;
        AddHandler(Thumb.DragStartedEvent, OnThumbDragStarted, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnThumbPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnThumbDragStarted(object? sender, VectorEventArgs e)
    {
        if (GetVerticalTrack(e.Source as Thumb) is not null)
            thumbGrabOffset = e.Vector.Y;
    }

    private void OnThumbPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) return;
        var thumb = (e.Source as Visual)?.FindAncestorOfType<Thumb>(includeSelf: true);
        if (thumb is null || GetVerticalTrack(thumb) is not { } track ||
            e.GetPosition(track).Y + Math.Max(0, thumb.Bounds.Height - thumbGrabOffset) < track.Bounds.Height - 1)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (this.IsAttachedToVisualTree()) ScrollToVerticalThumbEnd();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Scrolls to the end after the vertical thumb drag completes. A derived viewport can realize its last item first.</summary>
    protected virtual void ScrollToVerticalThumbEnd() => SetCurrentValue(OffsetProperty, Offset.WithY(double.PositiveInfinity));

    private Track? GetVerticalTrack(Thumb? thumb) =>
        thumb?.TemplatedParent is ScrollBar { Orientation: Orientation.Vertical } bar &&
        ReferenceEquals(bar.TemplatedParent, this) ? thumb.GetVisualParent() as Track : null;
}
