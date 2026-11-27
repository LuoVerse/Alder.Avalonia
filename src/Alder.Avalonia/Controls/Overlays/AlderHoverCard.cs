using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>An interactive anchored preview that can open on hover, keyboard focus or a touch hold.</summary>
public sealed class AlderHoverCard : TemplatedControl
{
    /// <summary>Identifies the <see cref="Trigger"/> Avalonia property.</summary>
    public static readonly StyledProperty<Control?> TriggerProperty = AvaloniaProperty.Register<AlderHoverCard, Control?>(nameof(Trigger));
    /// <summary>Identifies the <see cref="Content"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ContentProperty = ContentControl.ContentProperty.AddOwner<AlderHoverCard>();
    /// <summary>Identifies the <see cref="ContentTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty = ContentControl.ContentTemplateProperty.AddOwner<AlderHoverCard>();
    /// <summary>Identifies the <see cref="IsOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<AlderHoverCard, bool>(nameof(IsOpen),
        defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="OpenDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> OpenDelayProperty = AvaloniaProperty.Register<AlderHoverCard, int>(nameof(OpenDelay), 700,
        validate: value => value >= 0);
    /// <summary>Identifies the <see cref="CloseDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> CloseDelayProperty = AvaloniaProperty.Register<AlderHoverCard, int>(nameof(CloseDelay), 300,
        validate: value => value >= 0);
    /// <summary>Identifies the <see cref="Placement"/> Avalonia property.</summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<AlderHoverCard, PlacementMode>(nameof(Placement), PlacementMode.Top);
    /// <summary>Identifies the <see cref="Offset"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> OffsetProperty = AvaloniaProperty.Register<AlderHoverCard, double>(nameof(Offset), 8,
        validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="ShowArrow"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowArrowProperty = AvaloniaProperty.Register<AlderHoverCard, bool>(nameof(ShowArrow));
    /// <summary>Identifies the <see cref="SurfaceBackdropBlurRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SurfaceBackdropBlurRadiusProperty =
        AlderPopover.SurfaceBackdropBlurRadiusProperty.AddOwner<AlderHoverCard>();
    /// <summary>Identifies the <see cref="MaxContentWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxContentWidthProperty = AvaloniaProperty.Register<AlderHoverCard, double>(nameof(MaxContentWidth), 320,
        validate: value => value > 0);
    /// <summary>Identifies the <see cref="PreviewName"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> PreviewNameProperty = AvaloniaProperty.Register<AlderHoverCard, string?>(nameof(PreviewName));
    /// <summary>Identifies the <see cref="KeepShadowInsideViewport"/> property.</summary>
    public static readonly StyledProperty<bool> KeepShadowInsideViewportProperty =
        AvaloniaProperty.Register<AlderHoverCard, bool>(nameof(KeepShadowInsideViewport));

    /// <summary>The control whose pointer and focus state drive the preview.</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }
    /// <summary>The content presented in the hover preview.</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    /// <summary>The data template used to present the content.</summary>
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    /// <summary>Whether the hover preview is currently open.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>Pointer hover delay in milliseconds.</summary>
    public int OpenDelay { get => GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }
    /// <summary>Dismissal delay in milliseconds after interaction leaves the trigger and preview.</summary>
    public int CloseDelay { get => GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }
    /// <summary>The preferred position of the preview relative to its trigger.</summary>
    public PlacementMode Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>The gap between the trigger and the preview surface.</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
    /// <summary>Whether the surface includes an arrow pointing to its anchor.</summary>
    public bool ShowArrow { get => GetValue(ShowArrowProperty); set => SetValue(ShowArrowProperty, value); }
    /// <summary>The blur radius in DIPs behind an arrowless preview. Zero disables the effect.</summary>
    public double SurfaceBackdropBlurRadius { get => GetValue(SurfaceBackdropBlurRadiusProperty); set => SetValue(SurfaceBackdropBlurRadiusProperty, value); }
    /// <summary>The maximum width of the content in device-independent pixels.</summary>
    public double MaxContentWidth { get => GetValue(MaxContentWidthProperty); set => SetValue(MaxContentWidthProperty, value); }
    /// <summary>An accessible name identifying the preview surface.</summary>
    public string? PreviewName { get => GetValue(PreviewNameProperty); set => SetValue(PreviewNameProperty, value); }
    /// <summary>Reserves horizontal space for the shadow on previews aligned to the trigger's left edge.</summary>
    public bool KeepShadowInsideViewport { get => GetValue(KeepShadowInsideViewportProperty); set => SetValue(KeepShadowInsideViewportProperty, value); }

    private ContentPresenter? anchor;
    private ContentPresenter? body;
    private AnchoredSurface? surface;
    private Popup? popup;
    private TopLevel? root;
    private HoverPreviewTiming? timing;
    private IDisposable? intentTimer;
    private IDisposable? exitTimer;
    private InputElement? keyboardTrigger;
    private Point? bridgeOrigin;
    private Point? lastBridgePoint;
    private bool isPositioning;
    private bool animateOpening;
    private bool focusPreview;
    private bool restoringFocus;

    private static readonly FuncDataTemplate<string> TextTemplate = new((text, _) => new AlderTextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 20
    });

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClosePopup();
        if (anchor is { } old) WireTrigger(old, attach: false);
        base.OnApplyTemplate(e);
        anchor = e.NameScope.Find<ContentPresenter>("PART_Trigger");
        if (anchor is { } next) WireTrigger(next, attach: true);
        if (IsOpen) OpenPopup();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsOpen) OpenPopup();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        keyboardTrigger = null;
        Dismiss();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (IsOpen) OpenPopup();
            else BeginClose();
        }
        else if ((change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) ||
                 (change.Property == IsVisibleProperty && !IsVisible) ||
                 (change.Property == TriggerProperty && Trigger is null))
            Dismiss();
        else if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            if (Content is null) Dismiss();
            else if (body is { } presenter) SetContent(presenter);
            else if (IsOpen) OpenPopup();
        }
        else if (change.Property == TriggerProperty && IsOpen && popup is null)
            OpenPopup();
        else if ((change.Property == ForegroundProperty || change.Property == FontSizeProperty) && body is { } text)
        {
            text.Foreground = Foreground;
            text.FontSize = FontSize;
        }
        else if (change.Property == PlacementProperty || change.Property == OffsetProperty || change.Property == ShowArrowProperty ||
                 change.Property == MaxContentWidthProperty || change.Property == PreviewNameProperty || change.Property == KeepShadowInsideViewportProperty ||
                 change.Property == SurfaceBackdropBlurRadiusProperty || change.Property == BackgroundProperty || change.Property == BorderBrushProperty ||
                 change.Property == BorderThicknessProperty || change.Property == CornerRadiusProperty || change.Property == PaddingProperty)
            ApplyAppearance();
    }

    private void WireTrigger(ContentPresenter trigger, bool attach)
    {
        if (attach)
        {
            trigger.PointerEntered += TriggerEntered;
            trigger.PointerExited += TriggerExited;
            trigger.AttachedToVisualTree += TriggerAttached;
            trigger.GotFocus += TriggerFocused;
            trigger.LostFocus += FocusLeft;
            trigger.AddHandler(HoldingEvent, TriggerHeld);
        }
        else
        {
            trigger.PointerEntered -= TriggerEntered;
            trigger.PointerExited -= TriggerExited;
            trigger.AttachedToVisualTree -= TriggerAttached;
            trigger.GotFocus -= TriggerFocused;
            trigger.LostFocus -= FocusLeft;
            trigger.RemoveHandler(HoldingEvent, TriggerHeld);
        }
    }

    private void TriggerAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (IsOpen) OpenPopup();
    }

    private void TriggerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || !IsEffectivelyEnabled) return;
        bridgeOrigin = lastBridgePoint = null;
        CancelIntent();
        if (IsOpen) return;
        var warm = TopLevel.GetTopLevel(this) is { } window && HoverPreviewTiming.For(window).IsWarm;
        if (warm || OpenDelay == 0) Open();
        else intentTimer = DispatcherTimer.RunOnce(() =>
        {
            if (anchor?.IsPointerOver == true) Open();
        }, TimeSpan.FromMilliseconds(OpenDelay));
    }

    private void TriggerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch) return;
        bridgeOrigin = null;
        lastBridgePoint = null;
        if (root is { } window && anchor is { } trigger && surface is { IsPointerOver: false } frame &&
            GetBounds(trigger, window) is { } triggerBounds && GetBounds(frame, window) is { } previewBounds)
        {
            var pointer = e.GetPosition(window);
            var edge = FacingEdge(triggerBounds, previewBounds, pointer);
            if (edge is { } origin && (previewBounds.Contains(pointer) || IsInBridge(origin, pointer, previewBounds)))
                bridgeOrigin = origin;
        }
        ScheduleClose(bridgeOrigin is not null);
    }

    private void TriggerFocused(object? sender, FocusChangedEventArgs e)
    {
        if (restoringFocus || e.NavigationMethod == NavigationMethod.Pointer) return;
        keyboardTrigger = e.Source as InputElement;
        Open();
    }

    private void TriggerHeld(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || e.PointerType == PointerType.Mouse || !CanOpen) return;
        Open();
        FocusPreview();
        e.Handled = true;
    }

    private void FocusLeft(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (anchor?.IsKeyboardFocusWithin != true) keyboardTrigger = null;
            ScheduleClose();
        }, DispatcherPriority.Input);

    private void PreviewEntered(object? sender, PointerEventArgs e)
    {
        bridgeOrigin = lastBridgePoint = null;
        CancelIntent();
    }

    private void PreviewExited(object? sender, PointerEventArgs e) => ScheduleClose();

    private void PreviewFocused(object? sender, FocusChangedEventArgs e) => CancelIntent();

    private bool HasInteraction => anchor?.IsPointerOver == true || surface?.IsPointerOver == true ||
        (keyboardTrigger is not null && anchor?.IsKeyboardFocusWithin == true) || surface?.IsKeyboardFocusWithin == true;

    private void ScheduleClose(bool crossingGap = false)
    {
        CancelIntent();
        if (!IsOpen || HasInteraction) return;
        var delay = crossingGap ? Math.Max(CloseDelay, 140) : CloseDelay;
        if (delay == 0) SetCurrentValue(IsOpenProperty, false);
        else intentTimer = DispatcherTimer.RunOnce(() =>
        {
            if (!HasInteraction) SetCurrentValue(IsOpenProperty, false);
        }, TimeSpan.FromMilliseconds(delay));
    }

    private void Open()
    {
        CancelIntent();
        if (CanOpen) SetCurrentValue(IsOpenProperty, true);
    }

    private bool CanOpen => IsEffectivelyEnabled && IsEffectivelyVisible && Trigger is not null && Content is not null &&
        anchor?.IsAttachedToVisualTree() == true;

    private void OpenPopup()
    {
        if (anchor is not { } placementTarget || !CanOpen || TopLevel.GetTopLevel(this) is not { } window) return;
        if (popup is not null && exitTimer is null) return;

        var coordinated = HoverPreviewTiming.For(window);
        var warm = coordinated.IsWarm;
        ClosePopup();
        coordinated.Active?.Close();
        timing = coordinated;
        timing.Active = (Dismiss, null);
        timing.Resume();
        root = window;
        root.SizeChanged += OnPreviewViewportChanged;
        root.AddHandler(KeyDownEvent, OnPreviewKey, RoutingStrategies.Tunnel);
        root.AddHandler(PointerPressedEvent, OnOutsidePress, RoutingStrategies.Tunnel);
        root.AddHandler(PointerMovedEvent, OnBridgeMove, RoutingStrategies.Tunnel);

        body = new ContentPresenter { FontSize = FontSize, Foreground = Foreground };
        SetContent(body);
        surface = new AnchoredSurface(tooltip: false) { Name = "AlderHoverCardSurface", Child = body, Focusable = true, FocusAdorner = null };
        AutomationProperties.SetControlTypeOverride(surface, AutomationControlType.Pane);
        KeyboardNavigation.SetIsTabStop(surface, false);
        surface.LayoutUpdated += PositionPreview;
        surface.AddHandler(KeyDownEvent, OnPreviewKey, RoutingStrategies.Tunnel);
        surface.PointerEntered += PreviewEntered;
        surface.PointerExited += PreviewExited;
        surface.GotFocus += PreviewFocused;
        surface.LostFocus += FocusLeft;
        popup = new AlderPopup
        {
            Child = surface, PlacementTarget = placementTarget, ShouldUseOverlayLayer = true,
            IsLightDismissEnabled = false, TakesFocusFromNativeControl = false, WindowManagerAddShadowHint = false
        };
        ApplyAppearance();
        ((ISetLogicalParent)popup).SetParent(this);
        popup.Closed += PopupClosed;
        isPositioning = true;
        animateOpening = !warm && !AlderMotion.GetReduced(this);
        popup.Open();
        if (animateOpening && ElementComposition.GetElementVisual(surface) is { } visual)
            visual.Opacity = 0;
    }

    private void SetContent(ContentPresenter presenter)
    {
        presenter.ContentTemplate = ContentTemplate ?? (Content is string ? TextTemplate : null);
        presenter.Content = Content;
    }

    private void ApplyAppearance()
    {
        if (surface is not { } frame || popup is not { } current) return;
        frame.MaxWidth = MaxContentWidth;
        frame.ShowArrow = ShowArrow;
        frame.ModifyAppearance(Background, CornerRadius, Padding, BorderBrush, BorderThickness, null,
            ShowArrow ? 0 : SurfaceBackdropBlurRadius);
        if (PreviewName is null) frame.ClearValue(AutomationProperties.NameProperty);
        else AutomationProperties.SetName(frame, PreviewName);
        current.Placement = Placement;
        double horizontalOffset = 0;
        if (KeepShadowInsideViewport &&
            Placement is PlacementMode.BottomEdgeAlignedLeft or PlacementMode.TopEdgeAlignedLeft &&
            root is { ClientSize.Width: > 0 } owner && anchor?.TranslatePoint(default, owner) is { } origin)
        {
            double inset = Math.Min(AnchoredSurface.ShadowMargin.Left, owner.ClientSize.Width / 4);
            frame.MaxWidth = Math.Min(MaxContentWidth, owner.ClientSize.Width - inset * 2);
            double rightmost = Math.Max(inset, owner.ClientSize.Width - frame.MaxWidth - inset);
            horizontalOffset = Math.Clamp(origin.X, inset, rightmost) - origin.X;
        }
        AnchoredSurface.SetOffsets(current, Placement, Offset, horizontal: horizontalOffset);
    }

    private void OnPreviewViewportChanged(object? sender, SizeChangedEventArgs args)
    {
        if (KeepShadowInsideViewport) ApplyAppearance();
    }

    private void PositionPreview(object? sender, EventArgs e)
    {
        if (!IsEffectivelyVisible)
        {
            Dismiss();
            return;
        }
        if (anchor is not { } origin || surface is not { Bounds.Height: > 0 } frame ||
            frame.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return;
        if (isPositioning && animateOpening) frame.Play(origin, Placement, entering: true);
        else if (isPositioning || ShowArrow) frame.Position(origin, Placement);
        isPositioning = false;
        if (focusPreview) FocusPreview();
    }

    private void FocusPreview()
    {
        if (surface is not { } frame || isPositioning)
        {
            focusPreview = true;
            return;
        }
        focusPreview = false;
        (FirstTabStop(frame) ?? frame).Focus(NavigationMethod.Tab);
    }

    private static InputElement? FirstTabStop(Visual parent) => parent.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element =>
        element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible && KeyboardNavigation.GetIsTabStop(element));

    private void OnPreviewKey(object? sender, KeyEventArgs e)
    {
        if (!IsOpen || anchor is not { } origin) return;
        if (e.Key == Key.Escape)
        {
            if (surface?.IsKeyboardFocusWithin == true) RestoreTriggerFocus();
            SetCurrentValue(IsOpenProperty, false);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && origin.IsKeyboardFocusWithin)
        {
            FocusPreview();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift &&
                 surface is { } frame && (frame.IsFocused || FirstTabStop(frame)?.IsFocused == true))
        {
            RestoreTriggerFocus();
            e.Handled = true;
        }
    }

    private void RestoreTriggerFocus()
    {
        if (anchor is not { } origin) return;
        restoringFocus = true;
        keyboardTrigger = Trigger is { Focusable: true } control ? control : FirstTabStop(origin);
        keyboardTrigger?.Focus(NavigationMethod.Tab);
        restoringFocus = false;
    }

    private void OnOutsidePress(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && e.Source is Visual source && !(anchor?.IsVisualAncestorOf(source) == true || ReferenceEquals(anchor, source)) &&
            !(surface?.IsVisualAncestorOf(source) == true || ReferenceEquals(surface, source)))
            SetCurrentValue(IsOpenProperty, false);
    }

    private void OnBridgeMove(object? sender, PointerEventArgs e)
    {
        if (!IsOpen || e.Pointer.Type == PointerType.Touch || root is not { } window || bridgeOrigin is not { } exit ||
            surface is not { } frame || GetBounds(frame, window) is not { } preview || HasInteraction) return;
        var point = e.GetPosition(window);
        if (lastBridgePoint == point) return;
        lastBridgePoint = point;
        if (IsInBridge(exit, point, preview)) ScheduleClose(crossingGap: true);
        else
        {
            bridgeOrigin = null;
            ScheduleClose();
        }
    }

    private static Rect? GetBounds(Control control, Visual relativeTo) =>
        control.TranslatePoint(default, relativeTo) is { } origin ? new Rect(origin, control.Bounds.Size) : null;

    private static Point? FacingEdge(Rect trigger, Rect preview, Point pointer)
    {
        double x = Math.Clamp(pointer.X, trigger.Left, trigger.Right);
        double y = Math.Clamp(pointer.Y, trigger.Top, trigger.Bottom);
        if (preview.Bottom <= trigger.Top && pointer.Y <= trigger.Top) return new Point(x, trigger.Top);
        if (preview.Top >= trigger.Bottom && pointer.Y >= trigger.Bottom) return new Point(x, trigger.Bottom);
        if (preview.Right <= trigger.Left && pointer.X <= trigger.Left) return new Point(trigger.Left, y);
        if (preview.Left >= trigger.Right && pointer.X >= trigger.Right) return new Point(trigger.Right, y);
        return null;
    }

    private static bool IsInBridge(Point origin, Point point, Rect preview)
    {
        Point a;
        Point b;
        if (origin.Y < preview.Top || origin.Y > preview.Bottom)
        {
            var y = origin.Y < preview.Top ? preview.Top : preview.Bottom;
            a = new Point(preview.Left - 8, y);
            b = new Point(preview.Right + 8, y);
        }
        else
        {
            var x = origin.X < preview.Left ? preview.Left : preview.Right;
            a = new Point(x, preview.Top - 8);
            b = new Point(x, preview.Bottom + 8);
        }
        static double Cross(Vector x, Vector y) => x.X * y.Y - x.Y * y.X;
        var sign = Math.Sign(Cross(a - origin, b - origin));
        return sign != 0 && Cross(a - origin, point - origin) * sign >= 0 &&
            Cross(b - a, point - a) * sign >= 0 && Cross(origin - b, point - b) * sign >= 0;
    }

    private void BeginClose()
    {
        CancelIntent();
        if (isPositioning || AlderMotion.GetReduced(this) || anchor is not { } origin || surface is not { } frame)
        {
            ClosePopup();
            return;
        }
        timing?.CoolDown(CloseDelay);
        frame.IsHitTestVisible = false;
        frame.Play(origin, Placement, entering: false);
        exitTimer = DispatcherTimer.RunOnce(ClosePopup, TimeSpan.FromMilliseconds(100), DispatcherPriority.Render);
    }

    private void Dismiss()
    {
        ClosePopup();
        SetCurrentValue(IsOpenProperty, false);
    }

    private void PopupClosed(object? sender, EventArgs e) => Dismiss();

    private void ClosePopup()
    {
        CancelIntent();
        exitTimer?.Dispose();
        exitTimer = null;
        bridgeOrigin = lastBridgePoint = null;
        focusPreview = false;
        if (popup is not { } current) return;
        popup = null;
        isPositioning = false;
        if (root is not null) root.SizeChanged -= OnPreviewViewportChanged;
        root?.RemoveHandler(KeyDownEvent, OnPreviewKey);
        root?.RemoveHandler(PointerPressedEvent, OnOutsidePress);
        root?.RemoveHandler(PointerMovedEvent, OnBridgeMove);
        root = null;
        if (timing?.Active?.Close == (Action)Dismiss)
        {
            timing.CoolDown(CloseDelay);
            timing.Active = null;
        }
        timing = null;
        if (surface is { } frame)
        {
            frame.LayoutUpdated -= PositionPreview;
            frame.RemoveHandler(KeyDownEvent, OnPreviewKey);
            frame.PointerEntered -= PreviewEntered;
            frame.PointerExited -= PreviewExited;
            frame.GotFocus -= PreviewFocused;
            frame.LostFocus -= FocusLeft;
        }
        current.Closed -= PopupClosed;
        current.Close();
        if (body is { } presenter)
        {
            presenter.Content = null;
            presenter.UpdateChild();
        }
        if (surface is { } closedSurface) closedSurface.Child = null;
        current.Child = null;
        current.PlacementTarget = null;
        ((ISetLogicalParent)current).SetParent(null);
        surface = null;
        body = null;
    }

    private void CancelIntent()
    {
        intentTimer?.Dispose();
        intentTimer = null;
    }
}
