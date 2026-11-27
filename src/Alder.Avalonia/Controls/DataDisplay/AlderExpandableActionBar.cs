using System.Diagnostics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Controls the density of an expandable action bar.</summary>
public enum AlderExpandableActionBarSize
{
    /// <summary>Uses the regular toolbar spacing.</summary>
    Medium,
    /// <summary>Uses the compact toolbar spacing.</summary>
    Small
}

/// <summary>An inline toolbar that reveals action labels on hover, focus or an initial touch.</summary>
public sealed class AlderExpandableActionBar : SelectingItemsControl
{
    /// <summary>Identifies the bindable expanded state.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies whether pointer entry and an initial touch reveal labels.</summary>
    public static readonly StyledProperty<bool> ExpandOnHoverProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, bool>(nameof(ExpandOnHover), true);
    /// <summary>Identifies whether keyboard focus reveals labels.</summary>
    public static readonly StyledProperty<bool> ExpandOnFocusProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, bool>(nameof(ExpandOnFocus), true);
    /// <summary>Identifies the delay before automatic collapse.</summary>
    public static readonly StyledProperty<TimeSpan> CollapseDelayProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, TimeSpan>(nameof(CollapseDelay),
            TimeSpan.FromMilliseconds(90), validate: delay => delay >= TimeSpan.Zero);
    /// <summary>Identifies the brush used for the shared action highlight.</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, IBrush?>(nameof(HighlightBrush));
    /// <summary>Identifies the radius of the blur behind the toolbar surface.</summary>
    public static readonly StyledProperty<double> SurfaceBackdropBlurRadiusProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, double>(nameof(SurfaceBackdropBlurRadius),
            validate: radius => double.IsFinite(radius) && radius >= 0);

    /// <summary>Identifies the size preset inherited by actions in this bar.</summary>
    public static readonly StyledProperty<AlderExpandableActionBarSize> SizeProperty =
        AvaloniaProperty.Register<AlderExpandableActionBar, AlderExpandableActionBarSize>(nameof(Size), inherits: true);

    internal static readonly AttachedProperty<double> RevealProperty =
        AvaloniaProperty.RegisterAttached<AlderExpandableActionBar, StyledElement, double>("Reveal", inherits: true);

    /// <summary>Gets or sets whether action text is revealed. The default binding mode is two-way.</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>Gets or sets whether hovering or tapping a collapsed action reveals the labels.</summary>
    public bool ExpandOnHover { get => GetValue(ExpandOnHoverProperty); set => SetValue(ExpandOnHoverProperty, value); }
    /// <summary>Gets or sets whether tabbing into the bar reveals the labels.</summary>
    public bool ExpandOnFocus { get => GetValue(ExpandOnFocusProperty); set => SetValue(ExpandOnFocusProperty, value); }
    /// <summary>Gets or sets the time before the bar collapses when the pointer or focus leaves.</summary>
    public TimeSpan CollapseDelay { get => GetValue(CollapseDelayProperty); set => SetValue(CollapseDelayProperty, value); }
    /// <summary>Gets or sets the brush behind the hovered or selected action.</summary>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    /// <summary>The blur radius in DIPs behind the toolbar surface. Zero disables the effect.</summary>
    public double SurfaceBackdropBlurRadius { get => GetValue(SurfaceBackdropBlurRadiusProperty); set => SetValue(SurfaceBackdropBlurRadiusProperty, value); }

    /// <summary>Gets or sets the size preset used by the track and its actions.</summary>
    public AlderExpandableActionBarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private AlderExpandableActionHighlight? highlight;
    // Keep the last hovered action through the rail gaps and surface padding.
    private AlderExpandableActionBarItem? hovered;
    private AlderExpandableActionBarItem? highlighted;
    private TopLevel? top;
    private IDisposable? collapseTimer;
    private bool mouseOver;
    private bool keyboardWithin;
    private bool touchRevealed;
    private bool framePending;
    private long revealStarted;
    private double revealFrom;

    /// <summary>Initializes a toolbar that selects the most recently invoked action.</summary>
    public AlderExpandableActionBar() => AddHandler(Button.ClickEvent, HandleAction);

    internal AlderExpandableActionBarItem? HighlightedItem => highlighted;

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderExpandableActionBarItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderExpandableActionBarItem();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ActionBarPeer(this);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        highlight = e.NameScope.Find<AlderExpandableActionHighlight>("PART_Highlight");
    }

    internal void PositionHighlight(Rect bounds) => highlight?.Show(bounds, HighlightBrush);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        top = TopLevel.GetTopLevel(this);
        top?.AddHandler(PointerPressedEvent, OnOutsidePress, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        top?.RemoveHandler(PointerPressedEvent, OnOutsidePress);
        top = null;
        CancelCollapse();
        framePending = mouseOver = keyboardWithin = touchRevealed = false;
        SetHovered(null);
        SetValue(RevealProperty, IsExpanded ? 1 : 0);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            if (!IsExpanded) touchRevealed = false;
            StartReveal();
        }
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
        {
            SetValue(RevealProperty, IsExpanded ? 1 : 0);
            ItemsPanelRoot?.InvalidateArrange();
        }
        else if (change.Property == SelectedIndexProperty || change.Property == HighlightBrushProperty)
            ItemsPanelRoot?.InvalidateArrange();
        else if (change.Property == SurfaceBackdropBlurRadiusProperty)
            PseudoClasses.Set(":backdrop-blur", SurfaceBackdropBlurRadius > 0);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch) return;
        mouseOver = true;
        if (ExpandOnHover) Open();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Touch) return;
        mouseOver = false;
        SetHovered(null);
        if (ExpandOnHover && !keyboardWithin && !touchRevealed) ScheduleCollapse();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        keyboardWithin = e.NavigationMethod != NavigationMethod.Pointer;
        if (keyboardWithin && ExpandOnFocus) Open();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // Focus can move between buttons in the same toolbar.
        Dispatcher.UIThread.Post(() =>
        {
            if (top is null || IsKeyboardFocusWithin || !keyboardWithin) return;
            keyboardWithin = false;
            if (ExpandOnFocus && !(ExpandOnHover && mouseOver) && !touchRevealed) ScheduleCollapse();
        });
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsExpanded)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    internal bool RevealFromTouch(AlderExpandableActionBarItem item)
    {
        if (IsExpanded || !ExpandOnHover || touchRevealed) return false;
        Open();
        touchRevealed = true;
        SetHovered(item);
        return true;
    }

    internal void SetHovered(AlderExpandableActionBarItem? item)
    {
        if (hovered == item) return;
        hovered = item;
        ItemsPanelRoot?.InvalidateArrange();
    }

    internal void ClearHovered(AlderExpandableActionBarItem item)
    {
        if (hovered == item) SetHovered(null);
    }

    internal void UpdateHighlight(AlderExpandableActionRail rail)
    {
        if (hovered is { } item && (item.GetVisualParent() != rail || !item.IsVisible || !item.IsEffectivelyEnabled))
            hovered = null;

        var target = hovered ?? ContainerFromIndex(SelectedIndex) as AlderExpandableActionBarItem;
        if (target != highlighted)
        {
            highlighted?.SetHighlighted(false);
            highlighted = target;
            target?.SetHighlighted(true);
        }
    }

    private void HandleAction(object? sender, RoutedEventArgs e)
    {
        if (e.Source is AlderExpandableActionBarItem item && ItemsControlFromItemContainer(item) == this)
            SetCurrentValue(SelectedIndexProperty, IndexFromContainer(item));
    }

    private void OnOutsidePress(object? sender, PointerPressedEventArgs e)
    {
        if (!touchRevealed || e.Pointer.Type != PointerType.Touch ||
            e.Source is Visual visual && (visual == this || this.IsVisualAncestorOf(visual))) return;
        Close();
        e.Handled = true;
    }

    private void Open()
    {
        CancelCollapse();
        SetCurrentValue(IsExpandedProperty, true);
    }

    private void Close()
    {
        CancelCollapse();
        SetHovered(null);
        SetCurrentValue(IsExpandedProperty, false);
    }

    private void ScheduleCollapse()
    {
        CancelCollapse();
        if (CollapseDelay == TimeSpan.Zero) Close();
        else collapseTimer = DispatcherTimer.RunOnce(Close, CollapseDelay);
    }

    private void CancelCollapse()
    {
        collapseTimer?.Dispose();
        collapseTimer = null;
    }

    private void StartReveal()
    {
        var target = IsExpanded ? 1d : 0d;
        revealFrom = GetValue(RevealProperty);
        if (top is null || AlderMotion.GetReduced(this))
        {
            SetValue(RevealProperty, target);
            return;
        }
        if (revealFrom == target) return;

        revealStarted = Stopwatch.GetTimestamp();
        if (framePending) return;
        framePending = true;
        top.RequestAnimationFrame(AnimateReveal);
    }

    private void AnimateReveal(TimeSpan _)
    {
        framePending = false;
        if (top is null) return;

        double elapsed = Stopwatch.GetElapsedTime(revealStarted).TotalSeconds;
        double target = IsExpanded ? 1d : 0d;
        if (elapsed >= .36 || AlderMotion.GetReduced(this))
        {
            SetValue(RevealProperty, target);
            return;
        }

        // Unit step of the label spring (mass .7, damping 32, stiffness 380).
        const double decay = 32 / 1.4;
        const double frequency = 4.5175395145;
        double progress = 1 - Math.Exp(-decay * elapsed) *
            (Math.Cos(frequency * elapsed) + decay / frequency * Math.Sin(frequency * elapsed));
        SetValue(RevealProperty, Math.Clamp(revealFrom + (target - revealFrom) * progress, 0, 1));
        framePending = true;
        top.RequestAnimationFrame(AnimateReveal);
    }

    private sealed class ActionBarPeer(AlderExpandableActionBar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}
