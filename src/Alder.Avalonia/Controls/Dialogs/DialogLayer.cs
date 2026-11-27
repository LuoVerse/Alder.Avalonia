using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class DialogLayer : Panel
{
    private static readonly SplineEasing SheetStackEase = new(.32, .72, 0, 1);
    private readonly TaskCompletionSource<DialogResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<DialogLayer, DialogResult> finish;
    private readonly OverlayOptions options;
    private readonly Border backdrop;
    private readonly DialogSurface surface;
    private readonly Control viewport;
    private readonly Border? snapBackdrop;
    private IDisposable? closingTimer;
    private DialogResult? closingResult;
    private IPointer? dragPointer;
    private Point dragStart;
    private double dragDistance;
    private long lastDragTime;
    private double lastDragPosition;
    private double dragVelocity;
    private bool dragEngaged;
    private readonly bool horizontalDrag;
    private readonly int dragSign;
    private bool pressedBackdrop;
    private bool opened;
    private readonly SheetSnap? sheetSnap;
    private double fullSheetExtent;
    private bool coveredBySheet;
    private double SheetInset => options is SheetOptions { IsDetached: true } ? 8 : 0;

    public DialogLayer(object? content, OverlayOptions options, FlowDirection direction,
        IInputElement? previousFocus, Action<DialogLayer, DialogResult> finish, bool nestedSheet = false)
    {
        this.options = options;
        this.finish = finish;
        IsNestedSheet = nestedSheet;
        PreviousFocus = previousFocus;
        FocusReturnMethod = previousFocus is InputElement input && input.Classes.Contains(":focus-visible")
            ? NavigationMethod.Tab : NavigationMethod.Unspecified;
        ClipToBounds = true;
        FlowDirection = FlowDirection.LeftToRight;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        backdrop = new Border { Name = "AlderDialogBackdrop", Background = Brushes.Transparent };
        if (options is SheetOptions { FadeBackdropAfterLastSnap: true, SnapPoints.Count: > 0 })
        {
            snapBackdrop = new Border { IsHitTestVisible = false };
            backdrop.Child = snapBackdrop;
        }
        var backdropPaint = snapBackdrop ?? backdrop;
        if (!IsNestedSheet && options.BackdropBrush is { } brush) backdropPaint.Background = brush;
        else if (Backdrop != DialogBackdrop.Transparent)
            backdropPaint[!Border.BackgroundProperty] = new DynamicResourceExtension(
                options is SheetOptions { Backdrop: DialogBackdrop.Blur } ? "AlderSheetBlurTint" : "AlderDialogBackdrop");
        backdrop.PointerPressed += (_, e) =>
        {
            pressedBackdrop = e.GetCurrentPoint(backdrop).Properties.IsLeftButtonPressed;
            e.Handled = true;
        };
        backdrop.PointerReleased += (_, e) =>
        {
            if (pressedBackdrop && e.InitialPressMouseButton == MouseButton.Left && options.CanLightDismiss)
                RequestClose(DialogResult.Cancel);
            pressedBackdrop = false;
            e.Handled = true;
        };
        backdrop.PointerExited += (_, _) => pressedBackdrop = false;
        backdrop.PointerWheelChanged += (_, e) => e.Handled = true;
        if (options is SheetOptions { SnapPoints.Count: > 0 } sheet)
        {
            if (sheet.SnapPoints.Any(point => !double.IsFinite(point) || point < .05) ||
                sheet.InitialSnapPoint is { } initial && (!double.IsFinite(initial) || initial < .05))
                throw new ArgumentOutOfRangeException(nameof(options), "Sheet snap points must be fractions or positive lengths.");
            sheetSnap = new SheetSnap(this, sheet.SnapPoints, sheet.InitialSnapPoint);
        }

        surface = new DialogSurface(content, options, RequestClose, direction);
        if (options is DrawerOptions drawer)
        {
            surface.CornerRadius = drawer.Placement switch
            {
                DrawerPlacement.Bottom => new CornerRadius(16, 16, 0, 0),
                DrawerPlacement.Top => new CornerRadius(0, 0, 16, 16),
                DrawerPlacement.Left when drawer is SheetOptions => direction == FlowDirection.RightToLeft
                    ? new CornerRadius(16, 0, 0, 16) : new CornerRadius(0, 16, 16, 0),
                DrawerPlacement.Right when drawer is SheetOptions => direction == FlowDirection.RightToLeft
                    ? new CornerRadius(0, 16, 16, 0) : new CornerRadius(16, 0, 0, 16),
                _ => default
            };
            if (SheetInset > 0) surface.CornerRadius = new CornerRadius(16);
            horizontalDrag = drawer.Placement is DrawerPlacement.Left or DrawerPlacement.Right;
            dragSign = drawer.Placement is DrawerPlacement.Top or DrawerPlacement.Left ? -1 : 1;
            if (drawer.CanLightDismiss || sheetSnap is not null)
            {
                foreach (var zone in surface.DragZones)
                {
                    if (drawer is SheetOptions { DragFromHandleOnly: true } && zone != surface.SheetHandle)
                        continue;
                    RegisterDragZone(zone);
                }
                if (sheetSnap is not null && drawer is SheetOptions { DragFromHandleOnly: false } &&
                    surface.SheetBody is { } body)
                    RegisterDragZone(body);
            }
        }
        else if (options is DialogOptions { Size: DialogSize.Full })
        {
            surface.CornerRadius = default;
            surface.BoxShadow = default;
        }

        viewport = surface.CreateViewport();
        Children.Add(backdrop);
        Children.Add(viewport);
        LayoutUpdated += OnFirstLayout;
    }

    public bool IsActive { get; set; }
    public IInputElement? PreviousFocus { get; private set; }
    public NavigationMethod FocusReturnMethod { get; private set; }
    public DialogBackdrop Backdrop => IsNestedSheet ? DialogBackdrop.Transparent : options.Backdrop;
    public bool IsNestedSheet { get; }
    public bool IsSheet => options is SheetOptions;
    public Task<DialogResult> Result => completion.Task;

    public void SetSheetCovered(bool covered)
    {
        if (coveredBySheet == covered) return;
        coveredBySheet = covered;
        var center = new Rect(surface.Bounds.Size).Center;
        OverlayMotion.Scale(surface, covered ? .9 : 1, center, TimeSpan.FromMilliseconds(250), SheetStackEase);
    }

    public void ReplacePreviousFocus(DialogLayer removed)
    {
        if (PreviousFocus is Visual visual && removed.IsVisualAncestorOf(visual))
        {
            PreviousFocus = removed.PreviousFocus;
            FocusReturnMethod = removed.FocusReturnMethod;
        }
    }

    public void FocusInitial()
    {
        if (IsActive) surface.FocusInitial(FocusReturnMethod);
    }

    public void Complete(DialogResult result)
    {
        closingTimer?.Dispose();
        LayoutUpdated -= OnFirstLayout;
        surface.ReleaseContent();
        completion.TrySetResult(closingResult ?? result);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        backdrop.Measure(availableSize);
        var (size, _) = GetSurfaceSlot(availableSize);
        if (sheetSnap is not null && surface.SheetBody is { } body)
        {
            body.VerticalScrollBarVisibility = sheetSnap.IsExpanded ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            body.BringIntoViewOnFocusChange = sheetSnap.IsExpanded;
        }
        viewport.Measure(size);
        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : viewport.DesiredSize.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : viewport.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        backdrop.Arrange(new Rect(finalSize));
        var (slot, placement) = GetSurfaceSlot(finalSize);
        var height = options switch
        {
            DrawerOptions { Placement: DrawerPlacement.Left or DrawerPlacement.Right } => slot.Height,
            DrawerOptions { Extent: not null } => slot.Height,
            SheetOptions when sheetSnap is not null => slot.Height,
            DialogOptions { Size: DialogSize.Cover or DialogSize.Full } => slot.Height,
            _ => Math.Min(slot.Height, viewport.DesiredSize.Height)
        };
        double x = options switch
        {
            DrawerOptions { Placement: DrawerPlacement.Left } => SheetInset,
            DrawerOptions { Placement: DrawerPlacement.Right } => finalSize.Width - slot.Width - SheetInset,
            _ => (finalSize.Width - slot.Width) / 2
        };
        double y = placement switch
        {
            DialogPlacement.Top when options is DialogOptions { TopOffsetRatio: { } ratio } => finalSize.Height * ratio,
            DialogPlacement.Top => (finalSize.Height - slot.Height) / 2,
            DialogPlacement.Bottom => finalSize.Height - height - (finalSize.Height - slot.Height) / 2,
            _ => (finalSize.Height - height) / 2
        };
        if (options is DrawerOptions { Placement: DrawerPlacement.Top }) y = SheetInset;
        if (options is DrawerOptions { Placement: DrawerPlacement.Bottom }) y = finalSize.Height - height - SheetInset;
        viewport.Arrange(new Rect(x, y, slot.Width, height));
        return finalSize;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsActive) return;
        if (sheetSnap is not null && e.Source == surface.SheetHandle &&
            e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            int direction = options is SheetOptions { Placement: DrawerPlacement.Top or DrawerPlacement.Left } ? 1 : -1;
            if (horizontalDrag && e.Key is Key.Left or Key.Right || !horizontalDrag && e.Key is Key.Up or Key.Down)
            {
                sheetSnap.Step(e.Key is Key.Right or Key.Down ? direction : -direction);
                e.Handled = true;
            }
            return;
        }

        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (options.CanEscapeDismiss)
        {
            FocusReturnMethod = NavigationMethod.Tab;
            RequestClose(DialogResult.Cancel);
        }
    }

    private (Size Size, DialogPlacement Placement) GetSurfaceSlot(Size available)
    {
        if (options is DrawerOptions drawer)
        {
            bool horizontal = drawer.Placement is DrawerPlacement.Left or DrawerPlacement.Right;
            double? extent = drawer.Extent is { } value && double.IsFinite(value) && value > 0 ? value : null;
            if (drawer is SheetOptions sheet)
            {
                var area = available.Deflate(new Thickness(SheetInset));
                double axis = horizontal ? area.Width : area.Height;
                double limit = horizontal ? Math.Min(384, axis * .96) : Math.Max(0, axis - (SheetInset == 0 ? 32 : 0));
                double maximum = extent ?? (sheetSnap is null ? limit : axis);
                fullSheetExtent = Math.Min(axis, maximum);
                sheetSnap?.UpdateExtent(fullSheetExtent);
                if (snapBackdrop is not null && sheetSnap is not null) snapBackdrop.Opacity = sheetSnap.BackdropOpacity;
                var show = sheetSnap is null ? fullSheetExtent : fullSheetExtent * sheetSnap.Fraction;
                double width = sheet.MaxWidth is { } maxWidth && double.IsFinite(maxWidth) && maxWidth > 0
                    ? Math.Min(maxWidth, area.Width) : area.Width;
                return horizontal ? (new Size(show, area.Height), DialogPlacement.Center)
                    : (new Size(width, show), drawer.Placement == DrawerPlacement.Top ? DialogPlacement.Top : DialogPlacement.Bottom);
            }

            return horizontal
                ? (new Size(Math.Min(available.Width * .85, extent ?? (available.Width < 640 ? 320 : 384)), available.Height), DialogPlacement.Center)
                : (new Size(available.Width, Math.Min(available.Height * .85, extent ?? double.PositiveInfinity)),
                    drawer.Placement == DrawerPlacement.Top ? DialogPlacement.Top : DialogPlacement.Bottom);
        }

        var dialog = (DialogOptions)options;
        if (dialog.TopOffsetRatio is { } ratio)
        {
            double top = double.IsFinite(available.Height) ? available.Height * ratio : 0;
            var inset = available.Deflate(new Thickness(16, top, 16, 16));
            return (new Size(Math.Min(inset.Width, dialog.MaxPanelWidth), inset.Height), DialogPlacement.Top);
        }

        double padding = dialog.Size == DialogSize.Full ? 0 : available.Width < 640 ? 16 : 40;
        var size = available.Deflate(new Thickness(padding));
        var placement = dialog.Placement == DialogPlacement.Auto
            ? available.Width < 640 ? DialogPlacement.Bottom : DialogPlacement.Center
            : dialog.Placement;
        return (new Size(Math.Min(size.Width, dialog.MaxPanelWidth), size.Height), placement);
    }

    private void OnFirstLayout(object? sender, EventArgs e)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        LayoutUpdated -= OnFirstLayout;
        opened = true;
        var (_, placement) = GetSurfaceSlot(Bounds.Size);
        DialogMotion.Play(viewport, backdrop, options, placement, entering: true);
        FocusInitial();
    }

    private bool RequestClose(DialogResult result)
    {
        if (closingResult.HasValue || !IsActive) return false;
        closingResult = result;
        viewport.IsHitTestVisible = false;
        sheetSnap?.Stop();
        if (!opened || AlderMotion.GetReduced(this))
            finish(this, result);
        else
        {
            var (_, placement) = GetSurfaceSlot(Bounds.Size);
            var duration = DialogMotion.Play(viewport, backdrop, options, placement, entering: false);
            if (duration == TimeSpan.Zero) finish(this, result);
            else closingTimer = DispatcherTimer.RunOnce(() => finish(this, result), duration, DispatcherPriority.Render);
        }
        return true;
    }

    private double DragExtent => horizontalDrag ? surface.Bounds.Width : surface.Bounds.Height;

    private void RegisterDragZone(Control zone)
    {
        zone.PointerPressed += BeginDrag;
        zone.PointerMoved += MoveDrag;
        zone.PointerReleased += EndDrag;
        zone.PointerCaptureLost += LoseDrag;
    }

    private void BeginDrag(object? sender, PointerPressedEventArgs e)
    {
        if (dragPointer is not null || closingResult.HasValue || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (sender == surface.SheetBody && (e.Pointer.Type != PointerType.Touch || sheetSnap?.IsExpanded != false))
            return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors()
                .TakeWhile(visual => visual != sender).OfType<InputElement>().Any(input => input.Focusable))
            return;

        dragPointer = e.Pointer;
        dragStart = e.GetPosition(this);
        lastDragTime = Stopwatch.GetTimestamp();
        lastDragPosition = dragDistance = dragVelocity = 0;
        dragEngaged = false;
        sheetSnap?.BeginDrag();
        e.Pointer.Capture((Control)sender!);
        e.Handled = true;
    }

    private void MoveDrag(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != dragPointer) return;
        var delta = e.GetPosition(this) - dragStart;
        double offset = (horizontalDrag ? delta.X : delta.Y) * dragSign;
        if (!dragEngaged && (sheetSnap is null ? offset : Math.Abs(offset)) < 8) return;

        dragEngaged = true;
        long timestamp = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(lastDragTime, timestamp).TotalMilliseconds;
        if (elapsed > 0) dragVelocity = (offset - lastDragPosition) / elapsed;
        lastDragTime = timestamp;
        lastDragPosition = offset;
        dragDistance = sheetSnap is null ? Math.Clamp(offset, 0, DragExtent) : offset;
        if (sheetSnap is not null && fullSheetExtent > 0)
            sheetSnap.Drag(offset / fullSheetExtent);
        else if (!AlderMotion.GetReduced(this))
            DialogMotion.SetDragOffset(surface, horizontalDrag
                ? new Vector3D(dragSign * dragDistance, 0, 0)
                : new Vector3D(0, dragSign * dragDistance, 0));
        e.Handled = true;
    }

    private void EndDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != dragPointer) return;
        dragPointer = null;
        e.Pointer.Capture(null);
        bool flicked = dragVelocity > .5 && Stopwatch.GetElapsedTime(lastDragTime).TotalMilliseconds < 100;
        if (dragEngaged && sheetSnap is not null && fullSheetExtent > 0)
        {
            double velocity = Stopwatch.GetElapsedTime(lastDragTime).TotalMilliseconds < 100 ? dragVelocity : 0;
            if (sheetSnap.Release(dragDistance / fullSheetExtent, velocity * 1000 / fullSheetExtent, options.CanLightDismiss))
                RequestClose(DialogResult.Cancel);
        }
        else if (dragEngaged && (dragDistance > DragExtent * .3 || (dragDistance >= 8 && flicked)))
            RequestClose(DialogResult.Cancel);
        else if (dragEngaged && !AlderMotion.GetReduced(this))
            DialogMotion.SnapBack(surface);
        e.Handled = true;
    }

    private void LoseDrag(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer != dragPointer) return;
        dragPointer = null;
        if (!dragEngaged || closingResult.HasValue) return;
        if (sheetSnap is not null) sheetSnap.CancelDrag();
        else if (!AlderMotion.GetReduced(this)) DialogMotion.SnapBack(surface);
    }
}
