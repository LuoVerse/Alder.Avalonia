using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the owner interactions that open an attached tooltip.</summary>
public enum ToolTipTrigger
{
    /// <summary>Opens for pointer hover or keyboard focus.</summary>
    HoverAndFocus,
    /// <summary>Opens for pointer hover.</summary>
    Hover,
    /// <summary>Opens for keyboard focus.</summary>
    Focus
}

/// <summary>Shows a Alder tooltip on pointer hover or keyboard focus without moving focus.</summary>
public sealed class AlderToolTip : AvaloniaObject
{
    /// <summary>Identifies the <see cref="GetTip"/> attached property.</summary>
    public static readonly AttachedProperty<object?> TipProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, object?>("Tip");
    /// <summary>Identifies the <see cref="GetPlacement"/> attached property.</summary>
    public static readonly AttachedProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, PlacementMode>("Placement", PlacementMode.Top);
    /// <summary>Identifies the <see cref="GetShowArrow"/> attached property.</summary>
    public static readonly AttachedProperty<bool> ShowArrowProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, bool>("ShowArrow");
    /// <summary>Identifies the <see cref="GetShowDelay"/> attached property.</summary>
    public static readonly AttachedProperty<int> ShowDelayProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, int>("ShowDelay", 1500, inherits: true);
    /// <summary>Identifies the <see cref="GetCloseDelay"/> attached property.</summary>
    public static readonly AttachedProperty<int> CloseDelayProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, int>("CloseDelay", 500, inherits: true);
    /// <summary>Identifies the <see cref="GetAnimateWhenWarm"/> attached property.</summary>
    public static readonly AttachedProperty<bool> AnimateWhenWarmProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, bool>("AnimateWhenWarm");
    /// <summary>Identifies the <see cref="GetOffset"/> attached property.</summary>
    public static readonly AttachedProperty<double> OffsetProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, double>("Offset", double.NaN);

    /// <summary>Identifies the <see cref="GetTrigger"/> attached property.</summary>
    public static readonly AttachedProperty<ToolTipTrigger> TriggerProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, ToolTipTrigger>("Trigger");
    /// <summary>Identifies the <see cref="GetContentTemplate"/> attached property.</summary>
    public static readonly AttachedProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, IDataTemplate?>("ContentTemplate");
    /// <summary>Identifies the <see cref="GetSurfaceBackground"/> attached property.</summary>
    public static readonly AttachedProperty<IBrush?> SurfaceBackgroundProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, IBrush?>("SurfaceBackground");
    /// <summary>Identifies the <see cref="GetSurfaceCornerRadius"/> attached property.</summary>
    public static readonly AttachedProperty<CornerRadius> SurfaceCornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, CornerRadius>("SurfaceCornerRadius", new CornerRadius(12));
    /// <summary>Identifies the <see cref="GetContentPadding"/> attached property.</summary>
    public static readonly AttachedProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, Thickness>("ContentPadding", new Thickness(8));
    /// <summary>Identifies the <see cref="GetSurfaceBorderBrush"/> attached property.</summary>
    public static readonly AttachedProperty<IBrush?> SurfaceBorderBrushProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, IBrush?>("SurfaceBorderBrush");
    /// <summary>Identifies the <see cref="GetSurfaceBorderThickness"/> attached property.</summary>
    public static readonly AttachedProperty<Thickness> SurfaceBorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, Thickness>("SurfaceBorderThickness");
    /// <summary>Identifies the <see cref="GetSurfaceShadow"/> attached property.</summary>
    public static readonly AttachedProperty<BoxShadows?> SurfaceShadowProperty =
        AvaloniaProperty.RegisterAttached<AlderToolTip, Control, BoxShadows?>("SurfaceShadow");

    private static readonly ConditionalWeakTable<Control, TipSession> Sessions = new();

    static AlderToolTip() => TipProperty.Changed.AddClassHandler<Control>((owner, _) =>
    {
        if (Sessions.TryGetValue(owner, out var previous))
        {
            previous.Dispose();
            Sessions.Remove(owner);
        }
        if (GetTip(owner) is not null) Sessions.Add(owner, new TipSession(owner));
    });

    private AlderToolTip() { }

    /// <summary>Gets the content displayed by the tooltip attached to the owner.</summary>
    public static object? GetTip(Control owner) => owner.GetValue(TipProperty);
    /// <summary>Sets the content displayed by the tooltip attached to the owner.</summary>
    public static void SetTip(Control owner, object? tip) => owner.SetValue(TipProperty, tip);
    /// <summary>Gets the preferred tooltip position relative to the owner.</summary>
    public static PlacementMode GetPlacement(Control owner) => owner.GetValue(PlacementProperty);
    /// <summary>Sets the preferred tooltip position relative to the owner.</summary>
    public static void SetPlacement(Control owner, PlacementMode placement) => owner.SetValue(PlacementProperty, placement);
    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>Whether the surface includes an arrow pointing to its anchor.</remarks>
    public static bool GetShowArrow(Control owner) => owner.GetValue(ShowArrowProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>Whether the surface includes an arrow pointing to its anchor.</remarks>
    public static void SetShowArrow(Control owner, bool value) => owner.SetValue(ShowArrowProperty, value);
    /// <summary>Gets the time in milliseconds from activation to opening.</summary>
    public static int GetShowDelay(Control owner) => owner.GetValue(ShowDelayProperty);
    /// <summary>Sets the time in milliseconds from activation to opening.</summary>
    public static void SetShowDelay(Control owner, int milliseconds) => owner.SetValue(ShowDelayProperty, milliseconds);

    /// <summary>Gets the time in milliseconds from deactivation to closing.</summary>
    public static int GetCloseDelay(Control owner) => owner.GetValue(CloseDelayProperty);
    /// <summary>Sets the time in milliseconds from deactivation to closing.</summary>
    public static void SetCloseDelay(Control owner, int milliseconds) => owner.SetValue(CloseDelayProperty, milliseconds);
    /// <summary>Gets whether the entrance animation plays when switching between recently used tooltips.</summary>
    public static bool GetAnimateWhenWarm(Control owner) => owner.GetValue(AnimateWhenWarmProperty);
    /// <summary>Sets whether the entrance animation plays when switching between recently used tooltips.</summary>
    public static void SetAnimateWhenWarm(Control owner, bool animate) => owner.SetValue(AnimateWhenWarmProperty, animate);
    /// <summary>Gets the gap, in device-independent pixels, between the owner and tooltip surface.</summary>
    public static double GetOffset(Control owner) => owner.GetValue(OffsetProperty);
    /// <summary>Sets the gap, in device-independent pixels, between the owner and tooltip surface.</summary>
    public static void SetOffset(Control owner, double offset) => owner.SetValue(OffsetProperty, offset);

    /// <summary>Gets the interaction that opens the attached tooltip.</summary>
    public static ToolTipTrigger GetTrigger(Control owner) => owner.GetValue(TriggerProperty);
    /// <summary>Sets the interaction that opens the attached tooltip.</summary>
    public static void SetTrigger(Control owner, ToolTipTrigger trigger) => owner.SetValue(TriggerProperty, trigger);
    /// <summary>Gets the data template used when presenting the tooltip content.</summary>
    public static IDataTemplate? GetContentTemplate(Control owner) => owner.GetValue(ContentTemplateProperty);
    /// <summary>Sets the data template used when presenting the tooltip content.</summary>
    public static void SetContentTemplate(Control owner, IDataTemplate? template) => owner.SetValue(ContentTemplateProperty, template);
    /// <summary>Gets the background brush of the anchored surface.</summary>
    public static IBrush? GetSurfaceBackground(Control owner) => owner.GetValue(SurfaceBackgroundProperty);
    /// <summary>Sets the background brush of the anchored surface.</summary>
    public static void SetSurfaceBackground(Control owner, IBrush? brush) => owner.SetValue(SurfaceBackgroundProperty, brush);
    /// <summary>Gets the corner radius of the anchored surface.</summary>
    public static CornerRadius GetSurfaceCornerRadius(Control owner) => owner.GetValue(SurfaceCornerRadiusProperty);
    /// <summary>Sets the corner radius of the anchored surface.</summary>
    public static void SetSurfaceCornerRadius(Control owner, CornerRadius radius) => owner.SetValue(SurfaceCornerRadiusProperty, radius);
    /// <summary>Gets the space between the anchored surface and its content.</summary>
    public static Thickness GetContentPadding(Control owner) => owner.GetValue(ContentPaddingProperty);
    /// <summary>Sets the space between the anchored surface and its content.</summary>
    public static void SetContentPadding(Control owner, Thickness padding) => owner.SetValue(ContentPaddingProperty, padding);
    /// <summary>Gets the brush used to draw the border around the anchored surface.</summary>
    public static IBrush? GetSurfaceBorderBrush(Control owner) => owner.GetValue(SurfaceBorderBrushProperty);
    /// <summary>Sets the brush used to draw the border around the anchored surface.</summary>
    public static void SetSurfaceBorderBrush(Control owner, IBrush? brush) => owner.SetValue(SurfaceBorderBrushProperty, brush);
    /// <summary>Gets the thickness of the border around the anchored surface.</summary>
    public static Thickness GetSurfaceBorderThickness(Control owner) => owner.GetValue(SurfaceBorderThicknessProperty);
    /// <summary>Sets the thickness of the border around the anchored surface.</summary>
    public static void SetSurfaceBorderThickness(Control owner, Thickness thickness) => owner.SetValue(SurfaceBorderThicknessProperty, thickness);
    /// <summary>Gets the shadow drawn around the anchored surface.</summary>
    public static BoxShadows? GetSurfaceShadow(Control owner) => owner.GetValue(SurfaceShadowProperty);
    /// <summary>Sets the shadow drawn around the anchored surface.</summary>
    public static void SetSurfaceShadow(Control owner, BoxShadows? shadow) => owner.SetValue(SurfaceShadowProperty, shadow);

    private sealed class TipSession : IDisposable
    {
        private readonly Control owner;
        private IDisposable? timer;
        private Popup? popup;
        private TopLevel? root;
        private HoverPreviewTiming? timing;
        private bool keyboardFocus;
        private bool entering;
        private bool exiting;
        private bool skipAnimation;

        public TipSession(Control owner)
        {
            this.owner = owner;
            owner.PointerEntered += Schedule;
            owner.PointerExited += PointerLeft;
            owner.AddHandler(InputElement.PointerPressedEvent, PointerDown, RoutingStrategies.Tunnel);
            owner.GotFocus += Focused;
            owner.LostFocus += Blurred;
            owner.PropertyChanged += OwnerPropertyChanged;
            owner.DetachedFromVisualTree += Detached;
        }

        public void Dispose()
        {
            CloseNow();
            owner.PointerEntered -= Schedule;
            owner.PointerExited -= PointerLeft;
            owner.RemoveHandler(InputElement.PointerPressedEvent, PointerDown);
            owner.GotFocus -= Focused;
            owner.LostFocus -= Blurred;
            owner.PropertyChanged -= OwnerPropertyChanged;
            owner.DetachedFromVisualTree -= Detached;
        }

        private void Schedule(object? sender, PointerEventArgs e)
        {
            if (GetTrigger(owner) == ToolTipTrigger.Focus) return;
            if (e.Pointer.Type != PointerType.Mouse && e.Pointer.Type != PointerType.Pen) return;
            CancelTimer();
            if (popup is not null)
            {
                if (exiting) Open();
                else timing?.Resume();
                return;
            }
            int delay = Math.Max(0, GetShowDelay(owner));
            if (delay == 0 || TopLevel.GetTopLevel(owner) is { } top && HoverPreviewTiming.For(top).IsWarm) Open();
            else timer = DispatcherTimer.RunOnce(Open, TimeSpan.FromMilliseconds(delay));
        }

        private void PointerLeft(object? sender, PointerEventArgs e)
        {
            if (!keyboardFocus) ScheduleHide();
        }

        private void PointerDown(object? sender, PointerPressedEventArgs e) => Hide();

        private void Focused(object? sender, FocusChangedEventArgs e)
        {
            if (GetTrigger(owner) == ToolTipTrigger.Hover) return;
            if (e.NavigationMethod is NavigationMethod.Unspecified or NavigationMethod.Pointer) return;
            keyboardFocus = true;
            CancelTimer();
            if (!exiting) timing?.Resume();
            Open();
        }

        private void Blurred(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            keyboardFocus = false;
            if (GetTrigger(owner) == ToolTipTrigger.Focus || !owner.IsPointerOver) Hide();
        }

        private void KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || popup is null) return;
            Hide();
        }

        private void Detached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            keyboardFocus = false;
            CloseNow();
        }

        private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == InputElement.IsEffectivelyEnabledProperty && !owner.IsEffectivelyEnabled)
                CloseNow();
        }

        private void Open()
        {
            if (popup is not null && !exiting || !owner.IsEffectivelyEnabled ||
                TopLevel.GetTopLevel(owner) is not { } top || GetTip(owner) is not { } tip)
                return;

            var windowTiming = HoverPreviewTiming.For(top);
            bool warm = windowTiming.IsWarm;
            bool animateWhenWarm = GetAnimateWhenWarm(owner);
            CloseNow();
            if (windowTiming.Active is { } active)
            {
                var dismiss = animateWhenWarm ? active.BeginExit ?? active.Close : active.Close;
                dismiss();
            }
            timing = windowTiming;
            timing.Active = (CloseNow, animateWhenWarm ? Hide : null);
            timing.Resume();
            root = top;
            root.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
            skipAnimation = AlderMotion.GetReduced(owner) || (warm && !animateWhenWarm);
            Control child = GetContentTemplate(owner) is { } template
                ? new ContentPresenter { Content = tip, ContentTemplate = template }
                : tip as Control ?? new AlderTextBlock
            {
                Text = tip.ToString(), TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 16
            };
            if (child is TextBlock text && tip is not Control)
                text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
            var frame = new AnchoredSurface(tooltip: true)
            {
                Child = child, ShowArrow = GetShowArrow(owner), IsHitTestVisible = false
            };
            frame.ModifyAppearance(GetSurfaceBackground(owner), GetSurfaceCornerRadius(owner), GetContentPadding(owner),
                GetSurfaceBorderBrush(owner), GetSurfaceBorderThickness(owner), GetSurfaceShadow(owner));
            var placement = GetPlacement(owner);
            popup = new AlderPopup
            {
                Child = frame, PlacementTarget = owner, Placement = placement,
                ShouldUseOverlayLayer = true, TakesFocusFromNativeControl = false,
                WindowManagerAddShadowHint = false, IsHitTestVisible = false
            };
            var offset = GetOffset(owner);
            AnchoredSurface.SetOffsets(popup, placement, double.IsFinite(offset) ? offset : frame.ShowArrow ? 7 : 3);
            ((ISetLogicalParent)popup).SetParent(owner);
            popup.Closed += PopupClosed;
            exiting = false;
            entering = true;
            frame.LayoutUpdated += Position;
            popup.Open();
            if (!skipAnimation && ElementComposition.GetElementVisual(frame) is { } visual)
                visual.Opacity = 0;
        }

        private void Position(object? sender, EventArgs e)
        {
            if (popup?.Child is not AnchoredSurface { Bounds.Height: > 0 } frame) return;
            if (frame.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return;
            if (entering && !skipAnimation)
                frame.Play(owner, GetPlacement(owner), entering: true);
            else if (entering || frame.ShowArrow)
                frame.Position(owner, GetPlacement(owner));
            entering = false;
            if (!frame.ShowArrow) frame.LayoutUpdated -= Position;
        }

        private void ScheduleHide()
        {
            if (exiting) return;
            CancelTimer();
            if (popup is null) return;
            var delay = Math.Max(0, GetCloseDelay(owner));
            timing?.CoolDown(delay);
            if (delay == 0) Hide();
            else timer = DispatcherTimer.RunOnce(Hide, TimeSpan.FromMilliseconds(delay));
        }

        private void Hide()
        {
            if (exiting) return;
            CancelTimer();
            timing?.CoolDown(GetCloseDelay(owner));
            if (popup?.Child is not AnchoredSurface frame || entering || AlderMotion.GetReduced(owner))
            {
                CloseNow();
                return;
            }
            exiting = true;
            frame.LayoutUpdated -= Position;
            frame.Play(owner, GetPlacement(owner), entering: false);
            timer = DispatcherTimer.RunOnce(CloseNow, TimeSpan.FromMilliseconds(100), DispatcherPriority.Render);
        }

        private void CloseNow()
        {
            CancelTimer();
            if (popup is not { } current) return;
            popup = null;
            exiting = false;
            root?.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
            root = null;
            if (timing?.Active?.Close == (Action)CloseNow)
            {
                timing.CoolDown(GetCloseDelay(owner));
                timing.Active = null;
            }
            timing = null;
            current.Closed -= PopupClosed;
            if (current.Child is AnchoredSurface frame)
            {
                frame.LayoutUpdated -= Position;
                frame.Child = null;
            }
            current.Close();
            current.Child = null;
            current.PlacementTarget = null;
            ((ISetLogicalParent)current).SetParent(null);
        }

        private void PopupClosed(object? sender, EventArgs e) => CloseNow();

        private void CancelTimer()
        {
            timer?.Dispose();
            timer = null;
        }
    }
}
