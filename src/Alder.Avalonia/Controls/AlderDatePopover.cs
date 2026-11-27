using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Native light dismissal and shared calendar popover motion.</summary>
internal sealed class AlderDatePopover : PopupFlyoutBase
{
    private static readonly TimeSpan EnterTime = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ExitTime = TimeSpan.FromMilliseconds(100);
    private static readonly Thickness ShadowInsets = new(24, 20, 24, 36);
    private readonly AlderCalendarBase calendar;
    private IDisposable? closeTimer;
    private CompositionVisual? surface;
    private Control? focusReturn;
    private NavigationMethod openingMethod;
    private bool restoreFocus;
    private bool waitingForPlacement;

    public AlderDatePopover(AlderCalendarBase calendar)
    {
        this.calendar = calendar;
        Placement = PlacementMode.BottomEdgeAlignedLeft;
        VerticalOffset = 8;
        OverlayDismissEventPassThrough = true;
    }

    public void Open(Control anchor, Control trigger, NavigationMethod method)
    {
        focusReturn = trigger;
        openingMethod = method;
        restoreFocus = false;
        OverlayInputPassThroughElement = trigger;
        ShowAt(anchor);
    }

    public void Dismiss()
    {
        restoreFocus = true;
        Hide();
    }

    protected override void OnOpening(CancelEventArgs args)
    {
        base.OnOpening(args);
        if (Popup.Child is { } child && Target is { } target && TopLevel.GetTopLevel(target) is { } root)
            child.MaxWidth = Math.Max(276, root.ClientSize.Width - 16);
    }

    protected override Control CreatePresenter()
    {
        var frame = new Border
        {
            CornerRadius = new CornerRadius(22),
            // Avalonia includes the margin in the host size and excludes it from placement.
            Margin = ShadowInsets,
            Padding = new Thickness(8),
            Child = new AlderScrollViewer
            {
                Content = new Border { Padding = new Thickness(4), Child = calendar },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 400,
                IsScrollChainingEnabled = false
            }
        };
        frame[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderSurface");
        frame[!Border.BoxShadowProperty] = new DynamicResourceExtension("AlderToolbarShadow");
        frame.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Handled || e.Key != Key.Escape || calendar.IsYearPickerOpen) return;
            e.Handled = true;
            openingMethod = NavigationMethod.Tab;
            Dismiss();
        }, RoutingStrategies.Tunnel);
        return frame;
    }

    protected override void OnOpened()
    {
        base.OnOpened();
        if (Popup.Child is not { } child || Target is not { } target) return;
        surface = ElementComposition.GetElementVisual(child);
        if (surface is not null && !AlderMotion.GetReduced(target))
        {
            waitingForPlacement = true;
            surface.Opacity = 0;
            child.LayoutUpdated += OnPresenterPlaced;
            child.InvalidateArrange();
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && closeTimer is null) calendar.FocusDate(openingMethod);
        }, DispatcherPriority.Loaded);
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);
        if (args.Cancel) return;
        if (closeTimer is not null)
        {
            args.Cancel = true;
            return;
        }
        if (restoreFocus && focusReturn is { } trigger && trigger.IsAttachedToVisualTree())
        {
            var focused = TopLevel.GetTopLevel(trigger)?.FocusManager?.GetFocusedElement() as InputElement;
            trigger.Focus(focused?.Classes.Contains(":focus-visible") == true ? NavigationMethod.Tab : openingMethod);
        }
        if (surface is null || waitingForPlacement || Target is not { } target || Popup.Child is not { } child || AlderMotion.GetReduced(target)) return;
        args.Cancel = true;
        child.IsHitTestVisible = false;
        Play(enter: false, child, target);
        closeTimer = DispatcherTimer.RunOnce(() => HideCore(false), ExitTime, DispatcherPriority.Render);
    }

    protected override void OnClosed()
    {
        CancelPendingEntrance();
        closeTimer?.Dispose();
        closeTimer = null;
        if (Popup.Child is { } child) child.IsHitTestVisible = true;
        if (surface is { } visual)
        {
            visual.StopAnimation(nameof(CompositionVisual.Opacity));
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.Opacity = 1;
            visual.Scale = new Vector3D(1, 1, 1);
        }
        surface = null;
        restoreFocus = false;
        focusReturn = null;
        base.OnClosed();
    }

    private void OnPresenterPlaced(object? sender, EventArgs e)
    {
        if (Popup.Child is not { } child || Target is not { } target || child.Bounds.Height <= 0) return;
        if (child.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return;
        CancelPendingEntrance();
        Play(enter: true, child, target);
    }

    private void CancelPendingEntrance()
    {
        if (!waitingForPlacement) return;
        waitingForPlacement = false;
        if (Popup.Child is { } child) child.LayoutUpdated -= OnPresenterPlaced;
    }

    private void Play(bool enter, Control child, Control target)
    {
        if (surface is not { } visual) return;
        var center = new Rect(target.Bounds.Size).Center;
        var anchor = target.TranslatePoint(center, child) ?? child.PointToClient(target.PointToScreen(center));
        bool opensAbove = anchor.Y > child.Bounds.Height / 2;
        visual.CenterPoint = new Vector3D(0, opensAbove ? child.Bounds.Height : 0, 0);
        var scale = visual.Compositor.CreateVector3DKeyFrameAnimation();
        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        scale.Duration = opacity.Duration = enter ? EnterTime : ExitTime;
        if (enter)
        {
            scale.InsertKeyFrame(0, new Vector3D(.95, .95, 1));
            opacity.InsertKeyFrame(0, 0);
        }
        scale.InsertKeyFrame(1, enter ? new Vector3D(1, 1, 1) : new Vector3D(.95, .95, 1), new SplineEasing(.25, .1, .25, 1));
        opacity.InsertKeyFrame(1, enter ? 1 : 0, new SplineEasing(.25, .1, .25, 1));
        visual.StartAnimation(nameof(CompositionVisual.Scale), scale);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), opacity);
    }
}
