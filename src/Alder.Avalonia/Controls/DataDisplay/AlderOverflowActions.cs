using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A connected toolbar with always-visible actions and an inline expandable group.</summary>
public sealed class AlderOverflowActions : TemplatedControl
{
    /// <summary>Identifies the bindable expansion state.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AlderOverflowActions, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the policy for collapsing after an action runs.</summary>
    public static readonly StyledProperty<bool> CollapseOnActionProperty =
        AvaloniaProperty.Register<AlderOverflowActions, bool>(nameof(CollapseOnAction));
    /// <summary>Identifies the layout density, inherited by the action buttons.</summary>
    public static readonly AttachedProperty<AlderSize> SizeProperty =
        AvaloniaProperty.RegisterAttached<AlderOverflowActions, StyledElement, AlderSize>(nameof(Size), AlderSize.Medium, inherits: true);
    /// <summary>Identifies the accessible name for the collapsed toggle.</summary>
    public static readonly StyledProperty<string?> ExpandLabelProperty =
        AvaloniaProperty.Register<AlderOverflowActions, string?>(nameof(ExpandLabel));
    /// <summary>Identifies the accessible name for the expanded toggle.</summary>
    public static readonly StyledProperty<string?> CollapseLabelProperty =
        AvaloniaProperty.Register<AlderOverflowActions, string?>(nameof(CollapseLabel));

    /// <summary>Gets the buttons that are always shown.</summary>
    public AvaloniaList<AlderOverflowAction> PrimaryActions { get; } = [];
    /// <summary>Gets the buttons revealed inline when the bar expands.</summary>
    public AvaloniaList<AlderOverflowAction> OverflowActions { get; } = [];
    /// <summary>Gets or sets whether overflow actions are revealed. The default binding mode is two-way.</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    /// <summary>Gets or sets whether the bar collapses after a primary or overflow action runs.</summary>
    public bool CollapseOnAction { get => GetValue(CollapseOnActionProperty); set => SetValue(CollapseOnActionProperty, value); }
    /// <summary>Gets or sets the action density. Small uses the compact rail.</summary>
    public AlderSize Size { get => GetSize(this); set => SetSize(this, value); }
    /// <summary>Gets or sets the text read by assistive technology when the toggle opens the rail.</summary>
    public string? ExpandLabel { get => GetValue(ExpandLabelProperty); set => SetValue(ExpandLabelProperty, value); }
    /// <summary>Gets or sets the text read by assistive technology when the toggle closes the rail.</summary>
    public string? CollapseLabel { get => GetValue(CollapseLabelProperty); set => SetValue(CollapseLabelProperty, value); }

    /// <summary>Gets the inherited overflow action density on an element.</summary>
    public static AlderSize GetSize(StyledElement element) => element.GetValue(SizeProperty);
    /// <summary>Sets the overflow action density on an element.</summary>
    public static void SetSize(StyledElement element, AlderSize value) => element.SetValue(SizeProperty, value);

    /// <summary>Occurs after an action's click and command have run.</summary>
    public event EventHandler<AlderOverflowActionInvokedEventArgs>? ActionInvoked;

    private static readonly SplineEasing IconEase = new(.16, 1, .3, 1);
    private Border? surface;
    private AlderOverflowActionsPanel? rail;
    private AlderOverflowActionsToggle? toggle;
    private TopLevel? top;
    private NavigationMethod returnFocusMethod = NavigationMethod.Pointer;
    private int generation;
    private double reveal, opacity, iconMix;

    /// <summary>Initializes an overflow actions toolbar.</summary>
    public AlderOverflowActions()
    {
        PrimaryActions.CollectionChanged += (_, _) => UpdateAvailability();
        OverflowActions.CollectionChanged += (_, _) => UpdateAvailability();
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);

        // A click on an already-focused action may not raise another focus event.
        AddHandler(PointerPressedEvent, (_, _) => returnFocusMethod = NavigationMethod.Pointer, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, HandleActivationKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, HandleActivationKey, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarPeer(this);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        generation++;
        base.OnApplyTemplate(e);
        surface = e.NameScope.Find<Border>("PART_Surface");
        rail = e.NameScope.Find<AlderOverflowActionsPanel>("PART_Rail");
        toggle = e.NameScope.Find<AlderOverflowActionsToggle>("PART_Toggle");
        UpdateAvailability();
        UpdateToggleName();
        Snap();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        top = TopLevel.GetTopLevel(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        top = null;
        Snap();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            if (!IsExpanded && rail?.OverflowContainsFocus == true)
                toggle?.Focus(returnFocusMethod);
            UpdateToggleName();
            toggle?.NotifyExpansion(IsExpanded);
            Animate();
        }
        else if (change.Property == ExpandLabelProperty || change.Property == CollapseLabelProperty)
            UpdateToggleName();
        else if (change.Property == AlderMotion.ReducedProperty)
            Snap();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        returnFocusMethod = e.NavigationMethod;
    }

    private void HandleActivationKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
            returnFocusMethod = NavigationMethod.Tab;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.Escape && IsExpanded && OverflowActions.Count > 0)
        {
            toggle?.Focus(NavigationMethod.Tab);
            SetCurrentValue(IsExpandedProperty, false);
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End) || e.Source is not Button current) return;

        IEnumerable<Button> buttons = PrimaryActions;
        if (IsExpanded) buttons = buttons.Concat(OverflowActions);
        if (toggle is not null) buttons = buttons.Append(toggle);
        var available = buttons.Where(button => button.IsEffectivelyEnabled && button.IsEffectivelyVisible && button.Focusable).ToArray();
        int index = Array.IndexOf(available, current);
        if (index < 0) return;
        int direction = (e.Key == Key.Left ? -1 : 1) * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1);
        int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? available.Length - 1
            : (index + direction + available.Length) % available.Length;
        e.Handled = available[next].Focus(NavigationMethod.Directional);
        if (e.Handled) available[next].BringIntoView();
    }

    internal void Invoke(AlderOverflowAction action)
    {
        ActionInvoked?.Invoke(this, new AlderOverflowActionInvokedEventArgs(action));
        if (CollapseOnAction) SetCurrentValue(IsExpandedProperty, false);
    }

    private void UpdateAvailability()
    {
        if (surface is not null) surface.IsVisible = PrimaryActions.Count > 0 || OverflowActions.Count > 0;
        if (toggle is not null) toggle.IsVisible = OverflowActions.Count > 0;
        if (OverflowActions.Count == 0) Snap();
        else if (IsExpanded && reveal == 0) Snap();
    }

    private void UpdateToggleName()
    {
        if (toggle is null) return;
        var label = IsExpanded ? CollapseLabel : ExpandLabel;
        AutomationProperties.SetName(toggle, label);
        ToolTip.SetTip(toggle, label);
    }

    private void Animate()
    {
        if (top is not { } root || rail is null || OverflowActions.Count == 0)
        {
            Snap();
            return;
        }
        int run = ++generation;
        long started = Stopwatch.GetTimestamp();
        double initialReveal = reveal;
        double initialOpacity = opacity;
        double initialIconMix = iconMix;
        if (AlderMotion.GetReduced(this)) reveal = IsExpanded ? 1 : 0;
        Present();
        root.RequestAnimationFrame(Advance);

        void Advance(TimeSpan _)
        {
            if (run != generation) return;
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            bool reduced = AlderMotion.GetReduced(this);
            if (elapsed >= (reduced ? .18 : .64))
            {
                Snap();
                return;
            }

            double target = IsExpanded ? 1 : 0;
            double crossfade = IconEase.Ease(Math.Clamp(elapsed / .18, 0, 1));
            double spring = reduced ? crossfade : Spring(elapsed);
            reveal = reduced ? target : Math.Max(0, Mix(initialReveal, target, spring));
            opacity = Math.Clamp(Mix(initialOpacity, target, spring), 0, 1);
            iconMix = Mix(initialIconMix, target, crossfade);
            Present();
            root.RequestAnimationFrame(Advance);
        }
    }

    private void Snap()
    {
        generation++;
        reveal = opacity = iconMix = IsExpanded && OverflowActions.Count > 0 ? 1 : 0;
        Present();
    }

    private void Present()
    {
        bool reduced = AlderMotion.GetReduced(this);
        rail?.Present(IsExpanded && OverflowActions.Count > 0, reveal, opacity, reduced);
        toggle?.PresentIcons(iconMix, reduced);
    }

    // Unit-step response of the underdamped Web rail spring (220, 17, .85).
    private static double Spring(double seconds)
    {
        const double decay = 10;
        const double frequency = 12.6025207563;
        return 1 - Math.Exp(-decay * seconds) *
            (Math.Cos(frequency * seconds) + decay / frequency * Math.Sin(frequency * seconds));
    }

    private static double Mix(double from, double to, double amount) => from + (to - from) * amount;

    private sealed class ToolbarPeer(AlderOverflowActions owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}

/// <summary>A native command button used in either group of an overflow actions toolbar.</summary>
public sealed class AlderOverflowAction : AlderButton
{
    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsPending || !IsEffectivelyEnabled) return;
        var bar = this.FindAncestorOfType<AlderOverflowActions>();
        if (bar is { IsExpanded: false } && bar.OverflowActions.Contains(this)) return;
        base.OnClick();
        bar?.Invoke(this);
    }
}

/// <summary>Identifies the action that was invoked from a toolbar.</summary>
public sealed class AlderOverflowActionInvokedEventArgs(AlderOverflowAction action) : EventArgs
{
    /// <summary>Gets the native button that ran the action.</summary>
    public AlderOverflowAction Action { get; } = action;
}

internal sealed class AlderOverflowActionsToggle : Button
{
    private readonly BlurEffect openBlur = new();
    private readonly BlurEffect closeBlur = new();
    private AlderOverflowActions? Owner => TemplatedParent as AlderOverflowActions;
    internal bool Expanded => Owner?.IsExpanded == true;

    protected override void OnClick()
    {
        base.OnClick();
        SetExpanded(!Expanded);
    }

    internal void SetExpanded(bool value)
    {
        if (IsEffectivelyEnabled && IsEffectivelyVisible)
            Owner?.SetCurrentValue(AlderOverflowActions.IsExpandedProperty, value);
    }

    internal void PresentIcons(double mix, bool reduced)
    {
        if (Content is not Panel { Children: [var open, var close] }) return;
        open.Opacity = 1 - mix;
        close.Opacity = mix;
        AlderOverflowActionsPanel.ApplyBlur(open, openBlur, reduced ? 0 : 3 * mix);
        AlderOverflowActionsPanel.ApplyBlur(close, closeBlur, reduced ? 0 : 3 * (1 - mix));
    }

    internal void NotifyExpansion(bool expanded)
    {
        ControlAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, State(!expanded), State(expanded));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandCollapsePeer(this);

    private static ExpandCollapseState State(bool expanded) =>
        expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    private sealed class ExpandCollapsePeer(AlderOverflowActionsToggle owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => State(owner.Expanded);
        public bool ShowsMenu => false;
        public void Expand() => owner.SetExpanded(true);
        public void Collapse() => owner.SetExpanded(false);
    }
}
