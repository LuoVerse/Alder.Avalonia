using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderColorPickerFlyout : PopupFlyoutBase
{
    private static readonly SplineEasing Ease = new(.25, .1, .25, 1);
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(100);
    private readonly Control presenter;
    private IDisposable? exit;
    private bool awaitingPlacement;

    public AlderColorPickerFlyout(Control presenter)
    {
        this.presenter = presenter;
        Popup.ShouldUseOverlayLayer = true;
        Popup.Child = presenter;
        OverlayDismissEventPassThrough = true;
    }

    public bool IsClosing => exit is not null;

    public void CloseImmediately() => HideCore(false);

    public void AlignToRightEdge(bool preferBelow)
    {
        Placement = PlacementMode.Custom;
        HorizontalOffset = 0;
        VerticalOffset = 0;
        CustomPopupPlacementCallback = placement =>
        {
            placement.Anchor = (preferBelow ? PopupAnchor.Bottom : PopupAnchor.Top) | PopupAnchor.Right;
            placement.Gravity = (preferBelow ? PopupGravity.Bottom : PopupGravity.Top) | PopupGravity.Left;
            placement.AnchorRectangle = placement.AnchorRectangle.Inflate(new Thickness(0, 8));
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY |
                PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY |
                PopupPositionerConstraintAdjustment.ResizeX | PopupPositionerConstraintAdjustment.ResizeY;
        };
    }

    protected override Control CreatePresenter() => presenter;

    protected override void OnOpened()
    {
        base.OnOpened();
        if (Target is not { } anchor || AlderMotion.GetReduced(anchor)) return;

        if (ElementComposition.GetElementVisual(presenter) is { } visual)
            visual.Opacity = 0;
        awaitingPlacement = true;
        presenter.LayoutUpdated += OnPlaced;
        presenter.InvalidateArrange();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);
        if (args.Cancel) return;
        if (exit is not null)
        {
            args.Cancel = true;
            return;
        }
        if (awaitingPlacement || Target is not { } anchor || AlderMotion.GetReduced(anchor)) return;

        args.Cancel = true;
        presenter.IsHitTestVisible = false;
        var (pivot, _) = GetPlacement(anchor);
        OverlayMotion.Animate(presenter, false, default, .95, ExitDuration, ExitDuration, pivot, Ease);
        exit = DispatcherTimer.RunOnce(CloseImmediately, ExitDuration, DispatcherPriority.Render);
    }

    protected override void OnClosed()
    {
        StopListeningForPlacement();
        exit?.Dispose();
        exit = null;
        presenter.IsHitTestVisible = true;
        OverlayMotion.Reset(presenter);
        base.OnClosed();
    }

    private void OnPlaced(object? sender, EventArgs e)
    {
        if (Target is not { } anchor || presenter.Bounds.Height <= 0 ||
            presenter.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return;
        StopListeningForPlacement();
        var (pivot, offset) = GetPlacement(anchor);
        OverlayMotion.Animate(presenter, true, offset, .95, EnterDuration, EnterDuration, pivot, Ease);
    }

    private void StopListeningForPlacement()
    {
        awaitingPlacement = false;
        presenter.LayoutUpdated -= OnPlaced;
    }

    private (Point Pivot, Vector3D Offset) GetPlacement(Control anchor)
    {
        var center = new Rect(anchor.Bounds.Size).Center;
        var origin = anchor.TranslatePoint(center, presenter) ?? presenter.PointToClient(anchor.PointToScreen(center));
        double width = presenter.Bounds.Width, height = presenter.Bounds.Height;
        bool side = Placement is PlacementMode.Left or PlacementMode.Right or
            PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom or
            PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom;
        if (side)
        {
            bool onLeft = origin.X > width / 2;
            return (new Point(onLeft ? width : 0, Math.Clamp(origin.Y, 0, height)),
                new Vector3D(onLeft ? 4 : -4, 0, 0));
        }
        bool above = origin.Y > height / 2;
        return (new Point(Math.Clamp(origin.X, 0, width), above ? height : 0),
            new Vector3D(0, above ? 4 : -4, 0));
    }
}
