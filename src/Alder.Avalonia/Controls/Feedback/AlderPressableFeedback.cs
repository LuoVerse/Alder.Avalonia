using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Feedback layers applied to a pressable control.</summary>
[Flags]
public enum PressableFeedbackEffects
{
    /// <summary>No extra feedback layers.</summary>
    None = 0,
    /// <summary>A tint on hover and press.</summary>
    Highlight = 1,
    /// <summary>A radial press reveal.</summary>
    Ripple = 2,
    /// <summary>A sweep that completes when the button is held.</summary>
    HoldConfirm = 4,
    /// <summary>A sweep triggered by one click.</summary>
    ProgressFeedback = 8,
    /// <summary>A spring-driven scale on press that returns to rest on release or cancellation.</summary>
    SpringScale = 16
}

/// <summary>Specifies the direction in which the feedback overlay is revealed.</summary>
public enum PressableSweep
{
    /// <summary>From left to right.</summary>
    Right,
    /// <summary>From right to left.</summary>
    Left,
    /// <summary>From top to bottom.</summary>
    Down,
    /// <summary>From bottom to top.</summary>
    Up
}

/// <summary>The current state of the animated feedback.</summary>
public enum PressableFeedbackState
{
    /// <summary>No hold or progress is active.</summary>
    Idle,
    /// <summary>The button is being held.</summary>
    Holding,
    /// <summary>A click started an automatic sweep.</summary>
    Progressing,
    /// <summary>The sweep completed.</summary>
    Complete,
    /// <summary>The sweep is returning to its idle state.</summary>
    Resetting
}

/// <summary>Provides hover, ripple, hold-confirm and click-to-progress feedback for Alder controls.</summary>
public sealed partial class AlderPressableFeedback : StyledElement
{
    internal static void OnOwnerPropertyChanged(Control owner, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != AlderButton.FeedbackProperty) return;
        var (previous, current) = change.GetOldAndNewValue<AlderPressableFeedback?>();
        if (previous?.target == owner) previous.Detach();
        RefreshDefaultRipple(owner);
        current?.Attach(owner);
    }

    private static readonly ConditionalWeakTable<Control, AlderPressableFeedback> DefaultRipples = new();

    internal static void RefreshDefaultRipple(Control control)
    {
        bool enabled = control switch
        {
            AlderButton button => button.IsRippleEnabled,
            AlderCheckboxButtonGroupItem checkbox => checkbox.IsRippleEnabled,
            AlderRadioButtonGroupItem radio => radio.IsRippleEnabled,
            _ => false
        };
        if (!enabled || control.GetValue(AlderButton.FeedbackProperty) is not null)
        {
            if (DefaultRipples.TryGetValue(control, out var old)) old.Detach();
            return;
        }
        DefaultRipples.GetValue(control, static _ => new AlderPressableFeedback { HoverOpacity = 0, PressedOpacity = .24 })
            .Attach(control, legacyRipple: true);
    }

    /// <summary>Controls the enabled feedback layers. Hold confirmation takes priority over click-to-progress.</summary>
    [GeneratedStyledProperty(DefaultValue = PressableFeedbackEffects.Ripple)]
    public partial PressableFeedbackEffects Effects { get; set; }
    /// <summary>The scale reached while the spring press is held.</summary>
    [GeneratedStyledProperty(DefaultValue = .98, ValidateMethodName = nameof(IsValidScale))]
    public partial double PressScale { get; set; }
    /// <summary>The visual surface to scale. Defaults to the attached control.</summary>
    [GeneratedStyledProperty]
    public partial Visual? ScaleTarget { get; set; }
    /// <summary>The sweep direction.</summary>
    [GeneratedStyledProperty]
    public partial PressableSweep Sweep { get; set; }
    /// <summary>The duration of a hold or a click-to-progress sweep.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDuration))]
    public partial TimeSpan? Duration { get; set; }
    /// <summary>The duration of a returning sweep.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDuration))]
    public partial TimeSpan? ReleaseDuration { get; set; }
    /// <summary>Delay before a completed click sweep resets.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDuration))]
    public partial TimeSpan? ResetDelay { get; set; }
    /// <summary>Whether a completed sweep returns automatically.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool AutoReset { get; set; }
    /// <summary>The growth duration of a ripple.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDuration))]
    public partial TimeSpan? RippleDuration { get; set; }
    /// <summary>The minimum time a ripple remains visible.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidDuration))]
    public partial TimeSpan? MinimumPressDuration { get; set; }
    /// <summary>The opacity of the feedback ink while hovering.</summary>
    [GeneratedStyledProperty(DefaultValue = .08, ValidateMethodName = nameof(IsValidOpacity))]
    public partial double HoverOpacity { get; set; }
    /// <summary>The opacity of the feedback ink during a press.</summary>
    [GeneratedStyledProperty(DefaultValue = .12, ValidateMethodName = nameof(IsValidOpacity))]
    public partial double PressedOpacity { get; set; }
    /// <summary>Overrides the brush used for the hover and press effects.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? FeedbackBrush { get; set; }
    /// <summary>The sweep's surface brush.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? OverlayBackground { get; set; }
    /// <summary>The sweep's content brush.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? OverlayForeground { get; set; }
    /// <summary>Content displayed inside the revealed portion of the sweep.</summary>
    [GeneratedStyledProperty]
    public partial object? OverlayContent { get; set; }
    /// <summary>A template for the sweep content.</summary>
    [GeneratedStyledProperty]
    public partial IDataTemplate? OverlayContentTemplate { get; set; }
    /// <summary>A command executed once when a sweep completes.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? CompletionCommand { get; set; }
    /// <summary>The parameter supplied to the completion command.</summary>
    [GeneratedStyledProperty]
    public partial object? CompletionCommandParameter { get; set; }

    /// <summary>Identifies the read-only feedback state.</summary>
    public static readonly DirectProperty<AlderPressableFeedback, PressableFeedbackState> StateProperty =
        AvaloniaProperty.RegisterDirect<AlderPressableFeedback, PressableFeedbackState>(nameof(State), control => control.State);
    /// <summary>Identifies the read-only progress value.</summary>
    public static readonly DirectProperty<AlderPressableFeedback, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<AlderPressableFeedback, double>(nameof(Progress), control => control.Progress);

    /// <summary>The current feedback state.</summary>
    public PressableFeedbackState State { get => field; private set => SetAndRaise(StateProperty, ref field, value); }
    /// <summary>The currently visible fraction of the sweep, between zero and one.</summary>
    public double Progress { get => field; private set => SetAndRaise(ProgressProperty, ref field, value); }

    /// <summary>Raised once when the hold or click sweep reaches completion.</summary>
    public event EventHandler? Completed;
    /// <summary>Raised when a completed or active sweep returns to idle.</summary>
    public event EventHandler? ResetCompleted;

    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RippleFade = TimeSpan.FromMilliseconds(375);
    private static readonly TimeSpan LegacyRippleDuration = TimeSpan.FromMilliseconds(420);
    private static readonly SplineEasing SweepReleaseEasing = new(0, 0, .58, 1);
    private DispatcherTimer? timer;
    private Control? target;
    private bool usesLegacyRipple;
    private bool confirming;
    private bool appliedHoldPress;
    private ButtonTint? tint;
    private Grid? adorner;
    private Panel? layerHost;
    private Border? rippleClip;
    private Border? focusRing;
    private Border? sweepClip;
    private Border? sweepSurface;
    private ContentPresenter? overlay;
    private StackPanel? overlayLayout;
    private PathIcon? overlayIcon;
    private Ink? ink;
    private Key? holdKey;
    private Point rippleOrigin;
    private long rippleStarted;
    private long rippleReleased;
    private long stateStarted;
    private long resetAt;
    private double initialProgress;
    private bool Holds => Effects.HasFlag(PressableFeedbackEffects.HoldConfirm);
    private bool Progresses => !Holds && Effects.HasFlag(PressableFeedbackEffects.ProgressFeedback);

    private static bool IsValidDuration(TimeSpan? value) => value is null || value.Value >= TimeSpan.Zero;
    private static bool IsValidOpacity(double value) => value is >= 0 and <= 1;
    private static bool IsValidScale(double value) => double.IsFinite(value) && value > 0;

    private bool CanInteract => target is { IsEffectivelyEnabled: true } && target switch
    {
        AlderButton button => !button.IsPending,
        AlderCheckboxButtonGroupItem checkbox => checkbox.CanActivate,
        AlderRadioButtonGroupItem radio => radio.CanActivate,
        _ => true
    };
    private IBrush? Foreground => target?.GetValue(TemplatedControl.ForegroundProperty);
    private IBrush? FeedbackInk => FeedbackBrush ?? Foreground ?? ResolveBrush("AlderInk");
    private Rect Area => target is { } owner ? new Rect(owner.Bounds.Size) : default;

    internal void Attach(Control control, bool legacyRipple = false)
    {
        if (target == control) return;
        Detach();
        target = control;
        usesLegacyRipple = legacyRipple;
        SetButtonSpringClass(HasSpringScale);
        ((ISetLogicalParent)this).SetParent(control);
        control.PropertyChanged += TargetChanged;
        control.AttachedToVisualTree += TargetAttached;
        control.DetachedFromVisualTree += TargetDetached;
        if (control is TemplatedControl templated) templated.TemplateApplied += TemplateChanged;
        control.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerMovedEvent, PointerMoved, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerReleasedEvent, PointerReleased, RoutingStrategies.Tunnel);
        control.PointerCaptureLost += CaptureLost;
        control.PointerExited += PointerExited;
        control.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.KeyUpEvent, KeyUp, RoutingStrategies.Tunnel);
        control.LostFocus += LostFocus;
        if (control is Button button) button.AddHandler(Button.ClickEvent, Click, RoutingStrategies.Bubble);
        if (control.IsAttachedToVisualTree()) SetupLayers();
    }

    internal void Detach()
    {
        if (target is not { } control) return;
        Clear();
        RemoveLayers();
        SetButtonSpringClass(false);
        control.PropertyChanged -= TargetChanged;
        control.AttachedToVisualTree -= TargetAttached;
        control.DetachedFromVisualTree -= TargetDetached;
        if (control is TemplatedControl templated) templated.TemplateApplied -= TemplateChanged;
        control.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
        control.RemoveHandler(InputElement.PointerMovedEvent, PointerMoved);
        control.RemoveHandler(InputElement.PointerReleasedEvent, PointerReleased);
        control.PointerCaptureLost -= CaptureLost;
        control.PointerExited -= PointerExited;
        control.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
        control.RemoveHandler(InputElement.KeyUpEvent, KeyUp);
        control.LostFocus -= LostFocus;
        if (control is Button button) button.RemoveHandler(Button.ClickEvent, Click);
        ((ISetLogicalParent)this).SetParent(null);
        target = null;
    }

    private void TargetAttached(object? sender, VisualTreeAttachmentEventArgs e) => SetupLayers();
    private void TargetDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Clear();
        RemoveLayers();
    }
    private void TemplateChanged(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender == target) SetupLayers();
    }

    private void SetupLayers()
    {
        if (target is not { } owner) return;
        RemoveLayers();
        if ((Effects & ~PressableFeedbackEffects.SpringScale) == PressableFeedbackEffects.None)
        {
            RefreshSpringScale();
            return;
        }
        var parts = owner.GetVisualDescendants().OfType<Control>().Where(control => control.TemplatedParent == owner);
        rippleClip = parts.OfType<Border>().FirstOrDefault(part => part.Name == "RippleClip");
        bool ripple = Effects.HasFlag(PressableFeedbackEffects.Ripple);
        bool highlight = Effects.HasFlag(PressableFeedbackEffects.Highlight);
        if (ripple) ink = new Ink(this);
        if (usesLegacyRipple) return;

        if (rippleClip?.GetVisualParent() is Panel layer)
        {
            layerHost = layer;
            focusRing = parts.OfType<Border>().FirstOrDefault(part => part.Name == "FocusRing");
            if (ripple || highlight)
            {
                tint = new ButtonTint { IsHitTestVisible = false, FeedbackFade = true, ZIndex = 1, Opacity = 0 };
                layer.Children.Insert(layer.Children.IndexOf(rippleClip), tint);
            }
            rippleClip.ZIndex = 1;
            if (focusRing is not null) focusRing.ZIndex = 2;
        }
        else if (AdornerLayer.GetAdornerLayer(owner) is { } adorners)
        {
            adorner = new Grid { IsHitTestVisible = false };
            AdornerLayer.SetAdornedElement(adorner, owner);
            AdornerLayer.SetIsClipEnabled(adorner, true);
            adorners.Children.Add(adorner);
            layerHost = adorner;
            if (ripple || highlight)
            {
                tint = new ButtonTint { IsHitTestVisible = false, FeedbackFade = true, Opacity = 0 };
                adorner.Children.Add(tint);
            }
            if (ripple)
            {
                rippleClip = new Border { IsHitTestVisible = false, ClipToBounds = true };
                adorner.Children.Add(rippleClip);
            }
        }
        if (layerHost is null) return;
        if (!Holds && !Progresses)
        {
            RefreshLayers();
            return;
        }
        overlay = new ContentPresenter { HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(overlay, AccessibilityView.Raw);
        overlayIcon = new AlderPathIcon { Width = 16, Height = 16, Margin = new Thickness(-2, 0) };
        AutomationProperties.SetAccessibilityView(overlayIcon, AccessibilityView.Raw);
        overlayLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children = { overlayIcon, overlay } };
        sweepSurface = new Border { Child = overlayLayout };
        sweepClip = new Border { IsHitTestVisible = false, ClipToBounds = true, Child = sweepSurface, ZIndex = 1 };
        layerHost.Children.Add(sweepClip);
        RefreshLayers();
    }

    private void RemoveLayers()
    {
        ReleaseSpringVisual();
        if (rippleClip?.Child == ink && rippleClip is not null) rippleClip.Child = null;
        rippleClip?.ClearValue(Visual.ZIndexProperty);
        if (tint is not null) layerHost?.Children.Remove(tint);
        focusRing?.ClearValue(Visual.ZIndexProperty);
        if (sweepClip is not null) layerHost?.Children.Remove(sweepClip);
        if (adorner?.GetVisualParent() is Panel layer) layer.Children.Remove(adorner);
        if (adorner is not null) AdornerLayer.SetAdornedElement(adorner, null);
        layerHost = null;
        adorner = null;
        tint = null;
        focusRing = null;
        rippleClip = null;
        sweepClip = null;
        sweepSurface = null;
        overlay = null;
        overlayLayout = null;
        overlayIcon = null;
        ink = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (target is null || change.Property == ProgressProperty || change.Property == StateProperty) return;
        if (change.Property == EffectsProperty)
        {
            Clear();
            SetButtonSpringClass(HasSpringScale);
            if (target.IsAttachedToVisualTree()) SetupLayers();
        }
        if (change.Property == ScaleTargetProperty) ReleaseSpringVisual();
        RefreshLayers();
    }

    private void TargetChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if ((change.Property == InputElement.IsEffectivelyEnabledProperty || change.Property == AlderButton.IsPendingProperty) && !CanInteract)
            Clear();
        if (change.Property == AlderMotion.ReducedProperty && target is { } owner && AlderMotion.GetReduced(owner))
        {
            rippleStarted = rippleReleased = 0;
            if (rippleClip?.Child == ink && rippleClip is not null) rippleClip.Child = null;
        }
        RefreshLayers();
    }

    private void RefreshLayers()
    {
        if (target is not { } owner) return;
        RefreshSpringScale();
        if (owner is Button buttonHost && (Holds || appliedHoldPress))
        {
            appliedHoldPress = Holds && (contact is not null || holdKey is not null);
            ((IPseudoClasses)buttonHost.Classes).Set(":pressed", appliedHoldPress || buttonHost.IsPressed);
        }
        var radius = owner is TemplatedControl templated ? templated.CornerRadius : default;
        if (tint is not null)
        {
            tint.CornerRadius = radius;
            tint.Background = FeedbackInk;
            bool highlight = Effects.HasFlag(PressableFeedbackEffects.Highlight);
            bool pressed = contact is not null ||
                           State == PressableFeedbackState.Holding || owner is Button { IsPressed: true } || holdKey is not null;
            tint.Opacity = !CanInteract ? 0 : highlight && pressed ? PressedOpacity :
                owner.IsPointerOver && (highlight || !AlderMotion.GetReduced(owner)) ? HoverOpacity : 0;
        }
        if (rippleClip is not null)
        {
            if (adorner is not null) rippleClip.CornerRadius = radius;
            if (rippleStarted != 0 && rippleClip.Child is null) rippleClip.Child = ink;
            if (rippleStarted == 0 && rippleClip.Child == ink) rippleClip.Child = null;
        }
        ink?.InvalidateVisual();
        if (sweepClip is null || sweepSurface is null || overlay is null || overlayLayout is null || overlayIcon is null) return;
        sweepClip.CornerRadius = radius;
        sweepClip.IsVisible = Progress > 0;
        sweepSurface.Background = OverlayBackground ?? ResolveBrush("AlderAccent");
        overlay.Content = OverlayContent ?? ((owner as ContentControl)?.Content is string text ? text : null);
        overlay.ContentTemplate = OverlayContentTemplate;
        var foreground = OverlayForeground ?? ResolveBrush("AlderOnAccent") ?? Foreground;
        overlay.SetCurrentValue(TemplatedControl.ForegroundProperty, foreground);
        overlay.IsVisible = overlay.Content is not null;
        overlayIcon.Data = owner is AlderButton button && OverlayContentTemplate is null &&
            (OverlayContent is null or string) ? button.Icon : null;
        overlayIcon.IsVisible = overlayIcon.Data is not null;
        overlayIcon.Foreground = foreground;
        if (owner is ContentControl content)
        {
            overlayLayout.Margin = content.Padding;
            overlayLayout.HorizontalAlignment = content.HorizontalContentAlignment;
            overlayLayout.VerticalAlignment = content.VerticalContentAlignment;
        }
        var w = Area.Width;
        var h = Area.Height;
        var rect = Sweep switch
        {
            PressableSweep.Left => new Rect(w * (1 - Progress), 0, w * Progress, h),
            PressableSweep.Down => new Rect(0, 0, w, h * Progress),
            PressableSweep.Up => new Rect(0, h * (1 - Progress), w, h * Progress),
            _ => new Rect(0, 0, w * Progress, h)
        };
        // A new geometry publishes the changed clip to the compositor.
        if (sweepSurface.Clip is not RectangleGeometry clip || clip.Rect != rect)
            sweepSurface.Clip = new RectangleGeometry(rect);
    }

    private IBrush? ResolveBrush(string key) =>
        target is { } owner && owner.TryFindResource(key, owner.ActualThemeVariant, out var value) ? value as IBrush : null;

    /// <summary>Cancels active feedback or resets a completed sweep.</summary>
    public void Reset()
    {
        ReleaseContact();
        holdKey = null;
        springPointer = null;
        springKey = null;
        if (State != PressableFeedbackState.Idle) StartReset();
        RefreshLayers();
    }

    private IPointer? contact;
    private bool OwnsInput => Holds || target is not Button && (Effects & ~PressableFeedbackEffects.SpringScale) != 0;

    private bool FromNestedButton(RoutedEventArgs e) =>
        e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is { } button &&
        button != target && target?.IsVisualAncestorOf(button) == true;

    private void PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (target is not { } owner || e.Handled || !CanInteract || FromNestedButton(e) ||
            (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(owner).Properties.IsLeftButtonPressed)) return;
        if (OwnsInput)
        {
            if (contact is not null || holdKey is not null || Holds && State == PressableFeedbackState.Complete) return;
            owner.Focus(NavigationMethod.Pointer);
            contact = e.Pointer;
            e.Pointer.Capture(owner);
            e.PreventGestureRecognition();
            if (Holds) BeginSweep(PressableFeedbackState.Holding);
            e.Handled = true;
        }
        if (HasSpringScale && springPointer is null)
        {
            springPointer = e.Pointer;
            springPointerInside = true;
        }
        BeginRipple(e.GetPosition(owner));
        RefreshLayers();
    }

    private void PointerMoved(object? sender, PointerEventArgs e)
    {
        if (target is not { } owner || contact != e.Pointer && springPointer != e.Pointer) return;
        var inside = Area.Contains(e.GetPosition(owner));
        if (contact == e.Pointer && State == PressableFeedbackState.Holding && !inside)
            CancelHold();
        if (springPointer == e.Pointer && springPointerInside != inside)
        {
            springPointerInside = inside;
            RefreshSpringScale();
        }
    }

    private void PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (springPointer == e.Pointer) springPointer = null;
        if (contact == e.Pointer)
        {
            bool activate = !Holds && CanInteract && target is { } owner && Area.Contains(e.GetPosition(owner));
            CancelHold();
            if (activate) StartClickSweep();
            e.Handled = true;
        }
        EndRipple();
        RefreshLayers();
    }

    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (contact == e.Pointer) CancelHold();
        if (springPointer == e.Pointer) springPointer = null;
        EndRipple();
        RefreshLayers();
    }

    private void PointerExited(object? sender, PointerEventArgs e)
    {
        if (springPointer != e.Pointer || e.Pointer.Captured is Visual captured &&
            (captured == target || target?.IsVisualAncestorOf(captured) == true)) return;
        springPointer = null;
        RefreshSpringScale();
    }

    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !CanInteract || FromNestedButton(e)) return;
        if (e.Key == Key.Escape && State is PressableFeedbackState.Holding or PressableFeedbackState.Progressing)
        {
            Reset();
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Enter or Key.Space) || target?.IsFocused != true) return;
        BeginSpringKey(e.Key);
        if (OwnsInput)
        {
            if (holdKey is null && contact is null && State != PressableFeedbackState.Complete)
            {
                holdKey = e.Key;
                if (Holds) BeginSweep(PressableFeedbackState.Holding);
                BeginRipple(Area.Center);
            }
            e.Handled = true;
        }
        else if (rippleStarted == 0) BeginRipple(Area.Center);
        RefreshLayers();
    }

    private void KeyUp(object? sender, KeyEventArgs e)
    {
        EndSpringKey(e.Key);
        if (e.Key == holdKey)
        {
            CancelHold();
            if (!Holds && CanInteract) StartClickSweep();
            e.Handled = true;
        }
        if (e.Key is Key.Enter or Key.Space) EndRipple();
        RefreshLayers();
    }

    private void LostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (holdKey is not null) CancelHold();
        CancelSpringKey();
        EndRipple();
        RefreshLayers();
    }

    private void Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source != target || confirming || e.Handled) return;
        if (Holds || Progresses && !StartClickSweep()) e.Handled = true;
    }

    private bool StartClickSweep()
    {
        if (!Progresses || State is PressableFeedbackState.Progressing or PressableFeedbackState.Complete) return false;
        BeginSweep(PressableFeedbackState.Progressing);
        return true;
    }

    private void ReleaseContact()
    {
        var pointer = contact;
        contact = null;
        if (pointer is not null && target is not null && pointer.Captured == target) pointer.Capture(null);
    }

    private void InvokeConfirmedButton()
    {
        if (target is not Button button) return;
        confirming = true;
        try
        {
            if (button is AlderButton alderButton) alderButton.InvokeConfirmedClick();
            else
            {
                button.Flyout?.ShowAt(button);
                var click = new RoutedEventArgs(Button.ClickEvent);
                button.RaiseEvent(click);
                if (!click.Handled && button.Command is { } command && command.CanExecute(button.CommandParameter))
                    command.Execute(button.CommandParameter);
            }
        }
        finally { confirming = false; }
    }

    private void CancelHold()
    {
        ReleaseContact();
        holdKey = null;
        if (State == PressableFeedbackState.Holding || (State == PressableFeedbackState.Complete && AutoReset))
            StartReset();
    }

    private void BeginSweep(PressableFeedbackState state)
    {
        initialProgress = Progress;
        stateStarted = Stopwatch.GetTimestamp();
        resetAt = 0;
        State = state;
        StartTimer();
    }

    private void StartReset()
    {
        if (State == PressableFeedbackState.Resetting) return;
        BeginSweep(PressableFeedbackState.Resetting);
        if (target is { } motionTarget && AlderMotion.GetReduced(motionTarget) || Progress <= 0) FinishReset();
    }

    private void FinishReset()
    {
        Progress = 0;
        State = PressableFeedbackState.Idle;
        RefreshLayers();
        ResetCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteSweep(bool held)
    {
        Progress = 1;
        State = PressableFeedbackState.Complete;
        RefreshLayers();
        if (held) InvokeConfirmedButton();
        if (CompletionCommand is { } command && command.CanExecute(CompletionCommandParameter))
            command.Execute(CompletionCommandParameter);
        Completed?.Invoke(this, EventArgs.Empty);
        if (State == PressableFeedbackState.Complete && AutoReset && !held)
            resetAt = Stopwatch.GetTimestamp();
    }

    private void StartTimer()
    {
        if (timer is null)
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += UpdateMotion;
        }
        timer.Start();
    }

    private void UpdateMotion(object? sender, EventArgs e)
    {
        var state = State;
        if (state is PressableFeedbackState.Holding or PressableFeedbackState.Progressing)
        {
            var t = Fraction(Stopwatch.GetElapsedTime(stateStarted), Duration ?? DefaultDuration);
            if (t >= 1) CompleteSweep(state == PressableFeedbackState.Holding);
            else Progress = initialProgress + (1 - initialProgress) * t;
        }
        else if (state == PressableFeedbackState.Resetting)
        {
            // A CSS reveal reverses over the distance already travelled.
            var duration = (ReleaseDuration ?? TimeSpan.FromMilliseconds(Holds ? 200 : 300)) * initialProgress;
            var t = Fraction(Stopwatch.GetElapsedTime(stateStarted), duration);
            if (t >= 1) FinishReset();
            else Progress = initialProgress * (1 - SweepReleaseEasing.Ease(t));
        }
        else if (resetAt != 0 && Stopwatch.GetElapsedTime(resetAt) >= (ResetDelay ?? TimeSpan.FromSeconds(1.5)))
            StartReset();

        if (rippleStarted != 0 && RippleOpacity() <= 0) rippleStarted = 0;
        RefreshLayers();
        if (rippleStarted == 0 && State is PressableFeedbackState.Idle or PressableFeedbackState.Complete && resetAt == 0)
            timer?.Stop();
    }

    private static double Fraction(TimeSpan elapsed, TimeSpan duration) =>
        duration == TimeSpan.Zero ? 1 : Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);

    private void BeginRipple(Point position)
    {
        if (!CanInteract || !Effects.HasFlag(PressableFeedbackEffects.Ripple) ||
            target is { } motionTarget && AlderMotion.GetReduced(motionTarget)) return;
        rippleOrigin = position;
        rippleStarted = Stopwatch.GetTimestamp();
        rippleReleased = 0;
        StartTimer();
        RefreshLayers();
    }

    private void EndRipple()
    {
        if (!usesLegacyRipple && rippleStarted != 0 && rippleReleased == 0) rippleReleased = Stopwatch.GetTimestamp();
    }

    private double RippleOpacity()
    {
        if (rippleStarted == 0 || !CanInteract || target is { } motionTarget && AlderMotion.GetReduced(motionTarget)) return 0;
        double sinceStart = Stopwatch.GetElapsedTime(rippleStarted).TotalMilliseconds;
        if (usesLegacyRipple) return PressedOpacity * Math.Pow(1 - Fraction(TimeSpan.FromMilliseconds(sinceStart), LegacyRippleDuration), 3);
        double opacity = PressedOpacity * ButtonEase.Ease(Math.Clamp(sinceStart / 75, 0, 1));
        if (rippleReleased == 0) return opacity;
        double minimum = (MinimumPressDuration ?? TimeSpan.FromMilliseconds(225)).TotalMilliseconds;
        double fadeElapsed = Math.Min(sinceStart - minimum, Stopwatch.GetElapsedTime(rippleReleased).TotalMilliseconds);
        return opacity * (1 - ButtonEase.Ease(Math.Clamp(fadeElapsed / RippleFade.TotalMilliseconds, 0, 1)));
    }

    private void Clear()
    {
        ReleaseContact();
        holdKey = null;
        springPointer = null;
        springKey = null;
        resetAt = rippleStarted = rippleReleased = 0;
        timer?.Stop();
        State = PressableFeedbackState.Idle;
        Progress = 0;
        RefreshLayers();
    }

    private sealed class Ink(AlderPressableFeedback owner) : Control
    {
        private static readonly RadialGradientBrush SoftEdge = new()
        {
            RadiusX = RelativeScalar.Middle, RadiusY = RelativeScalar.Middle,
            GradientStops = [new(Colors.White, .65), new(Colors.Transparent, 1)]
        };

        public override void Render(DrawingContext context)
        {
            if (!owner.CanInteract || owner.FeedbackInk is not { } brush) return;
            var area = new Rect(Bounds.Size);
            var opacity = owner.RippleOpacity();
            if (opacity <= 0) return;
            var p = owner.rippleOrigin;
            var elapsed = Stopwatch.GetElapsedTime(owner.rippleStarted);
            using var alpha = context.PushOpacity(opacity);
            if (owner.usesLegacyRipple)
            {
                var x = Math.Max(p.X, area.Width - p.X);
                var y = Math.Max(p.Y, area.Height - p.Y);
                var legacyFraction = Fraction(elapsed, LegacyRippleDuration);
                var legacyRadius = Math.Sqrt(x * x + y * y) * (1 - .95 * Math.Pow(1 - legacyFraction, 3));
                context.DrawEllipse(brush, null, p, legacyRadius, legacyRadius);
                return;
            }

            var t = Fraction(elapsed, owner.RippleDuration ?? TimeSpan.FromMilliseconds(150));
            var startRadius = Math.Max(.5, Math.Floor(Math.Max(area.Width, area.Height) * .2) / 2);
            var endRadius = (Math.Sqrt(area.Width * area.Width + area.Height * area.Height) + 87) / 2;
            var start = p + new Vector(startRadius, startRadius);
            var center = start + (area.Center - start) * t;
            var radius = startRadius + (endRadius - startRadius) * t;
            var circle = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using var mask = context.PushOpacityMask(SoftEdge, circle);
            context.FillRectangle(brush, circle);
        }
    }
}
