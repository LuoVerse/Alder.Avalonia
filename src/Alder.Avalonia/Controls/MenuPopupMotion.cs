using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class MenuPopupMotion
{
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(100);
    private static readonly SplineEasing Ease = new(.25, .1, .25, 1);
    private static readonly Vector3D FullScale = new(1, 1, 1);
    private Popup? popup;
    private Control? presenter;
    private PixelPoint? pointerAnchor;
    private PlacementMode? preferredPlacement;
    private CompositionVisual? visual;
    private Control? motionTarget;
    private IInputElement? focusTarget;
    private bool openedFromKeyboard;
    private IDisposable? closeTimer;
    private bool waitingForPlacement;
    private bool finishingClose;
    private bool previousHitTest;

    public bool IsClosing => closeTimer is not null;

    public void Opened(Popup popup, Control presenter, Control target, PixelPoint? pointerAnchor = null,
        IInputElement? previousFocus = null, PlacementMode? preferredPlacement = null)
    {
        this.popup = popup;
        this.presenter = presenter;
        this.pointerAnchor = pointerAnchor;
        this.preferredPlacement = preferredPlacement;
        motionTarget = target;
        focusTarget = previousFocus ?? target;
        openedFromKeyboard = pointerAnchor is null && focusTarget is InputElement { } input && input.Classes.Contains(":focus-visible");
        target.PropertyChanged += OnTargetPropertyChanged;
        visual = ElementComposition.GetElementVisual(presenter);
        if (visual is not null && !AlderMotion.GetReduced(target))
        {
            if (TryStartEntrance()) return;

            // Overlay positioning can still flip the menu after Opened.
            visual.Opacity = 0;
            waitingForPlacement = true;
            presenter.LayoutUpdated += OnPresenterPositioned;
            presenter.InvalidateArrange();
        }
    }

    public void Closing(CancelEventArgs args)
    {
        if (finishingClose || args.Cancel) return;
        if (closeTimer is not null)
        {
            args.Cancel = true;
            return;
        }
        if (motionTarget is not { } target || !target.IsAttachedToVisualTree() || presenter is not { } content) return;

        if (focusTarget is { } anchor && TopLevel.GetTopLevel(content)?.FocusManager?.GetFocusedElement() is InputElement focused &&
            (focused == content || content.IsVisualAncestorOf(focused)) &&
            (openedFromKeyboard || focused.Classes.Contains(":focus-visible")))
            anchor.Focus(NavigationMethod.Tab);

        if (waitingForPlacement || popup is not { } host || visual is not { } surface || AlderMotion.GetReduced(target)) return;

        args.Cancel = true;
        previousHitTest = host.IsHitTestVisible;
        host.SetCurrentValue(InputElement.IsHitTestVisibleProperty, false);
        Play(surface, content, target, entering: false);
        closeTimer = DispatcherTimer.RunOnce(FinishClose, ExitDuration, DispatcherPriority.Render);

        Dispatcher.UIThread.Post(() =>
        {
            if (closeTimer is null || motionTarget != target || !target.IsAttachedToVisualTree()) return;
            if (TopLevel.GetTopLevel(content)?.FocusManager?.GetFocusedElement() is Visual focused &&
                (focused == content || content.IsVisualAncestorOf(focused)))
                focusTarget?.Focus();
        }, DispatcherPriority.Input);
    }

    public void Closed()
    {
        CancelPendingEntrance();
        if (closeTimer is not null)
        {
            closeTimer.Dispose();
            closeTimer = null;
            popup?.SetCurrentValue(InputElement.IsHitTestVisibleProperty, previousHitTest);
        }
        ResetVisual();
        visual = null;
        if (motionTarget is not null)
            motionTarget.PropertyChanged -= OnTargetPropertyChanged;
        motionTarget = null;
        focusTarget = null;
        openedFromKeyboard = false;
        popup = null;
        presenter = null;
        pointerAnchor = null;
        preferredPlacement = null;
    }

    public void FinishClose()
    {
        // A native menu item can already have written Popup.IsOpen = false.
        // Close() finishes the popup even when that property value is unchanged.
        finishingClose = true;
        try { popup?.Close(); }
        finally { finishingClose = false; }

        // A ContextMenu.Closing handler can veto the second native close.
        if (closeTimer is not null)
        {
            closeTimer.Dispose();
            closeTimer = null;
            popup?.SetCurrentValue(InputElement.IsHitTestVisibleProperty, previousHitTest);
            ResetVisual();
        }
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != AlderMotion.ReducedProperty || !change.GetNewValue<bool>()) return;
        CancelPendingEntrance();
        if (closeTimer is not null) FinishClose();
        else ResetVisual();
    }

    private void OnPresenterPositioned(object? sender, EventArgs e) => TryStartEntrance();

    private bool TryStartEntrance()
    {
        if (presenter is not { IsArrangeValid: true, Bounds.Width: > 0, Bounds.Height: > 0 } content ||
            visual is not { } surface || motionTarget is not { } target) return false;
        // The first arrange of an overlay host may run before its positioner sets Canvas.Top.
        if (content.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return false;

        CancelPendingEntrance();
        Play(surface, content, target, entering: true);
        return true;
    }

    private void CancelPendingEntrance()
    {
        if (!waitingForPlacement) return;
        waitingForPlacement = false;
        if (presenter is not null)
            presenter.LayoutUpdated -= OnPresenterPositioned;
    }

    private void ResetVisual()
    {
        if (visual is not { } surface) return;
        surface.StopAnimation(nameof(CompositionVisual.Opacity));
        surface.StopAnimation(nameof(CompositionVisual.Scale));
        surface.StopAnimation(nameof(CompositionVisual.Translation));
        surface.Opacity = 1;
        surface.Scale = FullScale;
        surface.Translation = default;
    }

    private void Play(CompositionVisual surface, Control presenter, Control target, bool entering)
    {
        var (origin, offset) = GetMotionPlacement(presenter, target);
        surface.CenterPoint = origin;
        var duration = entering ? EnterDuration : ExitDuration;
        var opacity = surface.Compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = duration;
        if (entering) opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(1, entering ? 1 : 0, Ease);

        // With no explicit exit start keyframe, the compositor continues from the
        // current value even when dismissal interrupts the entrance.
        surface.StartAnimation(nameof(CompositionVisual.Opacity), opacity);
        AnimateVector(surface, nameof(CompositionVisual.Scale), duration,
            entering ? new Vector3D(.9, .9, 1) : null, entering ? FullScale : new Vector3D(.95, .95, 1));
        AnimateVector(surface, nameof(CompositionVisual.Translation), duration, entering ? offset : null, default);
    }

    private (Vector3D Origin, Vector3D Offset) GetMotionPlacement(Control presenter, Control target)
    {
        var size = presenter.Bounds.Size;
        var targetCenter = new Rect(target.Bounds.Size).Center;
        var anchor = target.TranslatePoint(targetCenter, presenter)
            ?? presenter.PointToClient(target.PointToScreen(targetCenter));

        if (pointerAnchor is { } point)
        {
            anchor = presenter.PointToClient(point);
            var origin = new Vector3D(Math.Clamp(anchor.X, 0, size.Width), Math.Clamp(anchor.Y, 0, size.Height), 0);
            return (origin, new Vector3D(0, anchor.Y > size.Height / 2 ? 4 : -4, 0));
        }

        var placement = preferredPlacement ?? popup?.Placement;
        if (placement == PlacementMode.Center)
            return (new Vector3D(size.Width / 2, size.Height / 2, 0), default);

        if (placement is PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom
            or PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom)
        {
            bool opensLeft = anchor.X >= size.Width / 2;
            double y = placement switch
            {
                PlacementMode.LeftEdgeAlignedTop or PlacementMode.RightEdgeAlignedTop => 0,
                PlacementMode.LeftEdgeAlignedBottom or PlacementMode.RightEdgeAlignedBottom => size.Height,
                _ => Math.Clamp(anchor.Y, 0, size.Height)
            };
            return (new Vector3D(opensLeft ? size.Width : 0, y, 0), new Vector3D(opensLeft ? 4 : -4, 0, 0));
        }

        bool opensUp = anchor.Y >= size.Height / 2;
        double x = placement switch
        {
            PlacementMode.TopEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedLeft => 0,
            PlacementMode.TopEdgeAlignedRight or PlacementMode.BottomEdgeAlignedRight => size.Width,
            _ => Math.Clamp(anchor.X, 0, size.Width)
        };
        if (target.FlowDirection == FlowDirection.RightToLeft && placement is
            PlacementMode.TopEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedLeft or
            PlacementMode.TopEdgeAlignedRight or PlacementMode.BottomEdgeAlignedRight)
            x = size.Width - x;
        return (new Vector3D(x, opensUp ? size.Height : 0, 0), new Vector3D(0, opensUp ? 4 : -4, 0));
    }

    private static void AnimateVector(CompositionVisual visual, string property, TimeSpan duration,
        Vector3D? from, Vector3D to)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        if (from is { } start) animation.InsertKeyFrame(0, start);
        animation.InsertKeyFrame(1, to, Ease);
        visual.StartAnimation(property, animation);
    }
}
