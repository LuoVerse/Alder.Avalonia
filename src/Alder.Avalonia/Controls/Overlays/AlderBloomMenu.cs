using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>An action button that expands from its center into a three-column menu.</summary>
public sealed class AlderBloomMenu : ItemsControl
{
    /// <summary>Identifies the two-way expanded state.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AlderBloomMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies the caption shared by the trigger and panel heading.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<AlderBloomMenu, string>(nameof(Header), "Create");

    /// <summary>Identifies the trigger icon geometry.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderBloomMenu, object?>(nameof(Icon));

    /// <summary>Identifies the accessible name of the close button.</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<AlderBloomMenu, string>(nameof(CloseLabel), "Close menu");

    /// <summary>Identifies the maximum width of the expanded surface, in DIPs.</summary>
    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<AlderBloomMenu, double>(nameof(ExpandedWidth), 420,
            validate: width => double.IsFinite(width) && width > 0);

    /// <summary>Identifies the shadow used by the trigger and the expanded surface.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<AlderBloomMenu>();

    /// <summary>Gets or sets whether the menu is open. The default binding mode is two-way.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Gets or sets the text shown on the trigger and in the panel heading.</summary>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Gets or sets the trigger icon. The default theme supplies a plus icon. Set to null to hide it.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Gets or sets the accessible label of the icon-only close button.</summary>
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    /// <summary>Gets or sets the preferred expanded width. The available viewport can reduce this width.</summary>
    public double ExpandedWidth { get => GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }

    /// <summary>Gets or sets the shadow of the morphing surface.</summary>
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    private Button? trigger;
    private Control? triggerCaption;
    private Popup? popup;
    private AlderBloomMenuSurface? surface;
    private Button? close;
    private TopLevel? top;
    private AlderBloomMenuItem? activeItem;
    private NavigationMethod openNavigation = NavigationMethod.Tab;
    private NavigationMethod menuClickNavigation = NavigationMethod.Tab;
    private Rect? anchorAtOpen;

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderBloomMenuItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderBloomMenuItem();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BloomMenuPeer(this);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (trigger is not null)
        {
            trigger.LayoutUpdated -= TriggerArranged;
            trigger.Click -= TriggerClicked;
            trigger.RemoveHandler(KeyDownEvent, TriggerKeyDown);
            trigger.RemoveHandler(PointerPressedEvent, TriggerPointerPressed);
        }
        if (surface is not null)
        {
            surface.RemoveHandler(Button.ClickEvent, OnMenuClick);
            surface.RemoveHandler(KeyDownEvent, MenuKeyDown);
            surface.RemoveHandler(PointerPressedEvent, MenuPointerPressed);
            surface.GotFocus -= MenuGotFocus;
            surface.LostFocus -= MenuLostFocus;
        }
        if (popup is not null)
        {
            StopTracking();
            popup.Opened -= PopupOpened;
            popup.Closed -= PopupClosed;
            popup.IsOpen = false;
        }

        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_Trigger");
        triggerCaption = e.NameScope.Find<Control>("PART_TriggerCaption");
        popup = e.NameScope.Find<Popup>("PART_Popup");
        surface = e.NameScope.Find<AlderBloomMenuSurface>("PART_Surface");
        close = e.NameScope.Find<Button>("PART_Close");
        if (trigger is null || popup is null || surface is null || close is null) return;

        trigger.Click += TriggerClicked;
        trigger.AddHandler(KeyDownEvent, TriggerKeyDown, RoutingStrategies.Tunnel);
        trigger.AddHandler(PointerPressedEvent, TriggerPointerPressed, RoutingStrategies.Tunnel);
        surface.AddHandler(Button.ClickEvent, OnMenuClick);
        surface.AddHandler(KeyDownEvent, MenuKeyDown, RoutingStrategies.Tunnel);
        surface.AddHandler(PointerPressedEvent, MenuPointerPressed, RoutingStrategies.Tunnel);
        surface.GotFocus += MenuGotFocus;
        surface.LostFocus += MenuLostFocus;
        popup.PlacementTarget = trigger;
        popup.PlacementConstraintAdjustment =
            PopupPositionerConstraintAdjustment.SlideX |
            PopupPositionerConstraintAdjustment.SlideY;
        popup.Opened += PopupOpened;
        popup.Closed += PopupClosed;
        UpdateOpenState();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        top = TopLevel.GetTopLevel(this);
        UpdateOpenState();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (trigger is not null) trigger.LayoutUpdated -= TriggerArranged;
        StopTracking();
        top = null;
        if (popup is { IsOpen: true }) popup.IsOpen = false;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) UpdateOpenState();
        else if (change.Property == ExpandedWidthProperty) surface?.InvalidateMeasure();
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this)) surface?.FinishMotion();
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) Dismiss(false);
    }

    private void TriggerClicked(object? sender, RoutedEventArgs e) => SetCurrentValue(IsOpenProperty, true);

    private void TriggerPointerPressed(object? sender, PointerPressedEventArgs e) =>
        openNavigation = NavigationMethod.Pointer;

    private void TriggerKeyDown(object? sender, KeyEventArgs e)
    {
        // Observe activation before Button handles Enter or Space. The previous
        // focus ring can still be visible when a pointer clicks a focused trigger.
        if (e.Key is Key.Enter or Key.Space) openNavigation = NavigationMethod.Tab;
        if (e.Key is not (Key.Down or Key.Up)) return;
        openNavigation = NavigationMethod.Directional;
        SetCurrentValue(IsOpenProperty, true);
        e.Handled = true;
    }

    private void UpdateOpenState()
    {
        if (top is null || trigger is null || surface is null || popup is null || close is null) return;
        trigger.LayoutUpdated -= TriggerArranged;
        KeyboardNavigation.SetIsTabStop(trigger, !IsOpen);
        trigger.IsHitTestVisible = !IsOpen;
        if (IsOpen)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
                return;
            }

            // A bound initial state can arrive before the trigger has a position.
            if (!trigger.IsArrangeValid)
            {
                trigger.LayoutUpdated += TriggerArranged;
                return;
            }

            if (popup.IsOpen)
                ShowSurface();
            else
            {
                surface.Reset();
                surface.SetViewport(top.ClientSize);
                popup.IsOpen = true;
            }
        }
        else if (popup.IsOpen)
        {
            if (surface.IsKeyboardFocusWithin) trigger.Focus(NavigationMethod.Tab);
            surface.Transition(false);
        }
    }

    private void TriggerArranged(object? sender, EventArgs e)
    {
        if (trigger is not { IsArrangeValid: true } button) return;
        button.LayoutUpdated -= TriggerArranged;
        Dispatcher.UIThread.Post(UpdateOpenState, DispatcherPriority.Loaded);
    }

    internal Size TriggerSize => trigger is { Bounds.Width: > 0, Bounds.Height: > 0 } button
        ? button.Bounds.Size : new Size(144, 44);

    private void PopupOpened(object? sender, EventArgs e)
    {
        StartTracking();
        ShowSurface();
    }

    private void ShowSurface()
    {
        surface?.Transition(true);
        FocusOnOpen();
    }

    private void FocusOnOpen()
    {
        // Window activation establishes the focus scope and can clear earlier focus.
        if (top is Window { IsActive: false }) return;
        menuClickNavigation = openNavigation;
        if (!FocusItem(0, 1, openNavigation)) close?.Focus(openNavigation);
        openNavigation = NavigationMethod.Tab;
    }

    internal Point TriggerCenterIn(Visual relativeTo, Point fallback) =>
        trigger?.TranslatePoint(new Rect(TriggerSize).Center, relativeTo) ?? fallback;

    internal Rect TriggerCaptionBoundsIn(Visual relativeTo, Rect fallback) =>
        triggerCaption?.TranslatePoint(default, relativeTo) is { } point ? new Rect(point, triggerCaption.Bounds.Size) : fallback;

    internal void HideTrigger()
    {
        if (trigger is not null) trigger.Opacity = 0;
    }

    internal void FinishClose()
    {
        if (popup is { IsOpen: true } && !IsOpen) popup.IsOpen = false;
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        StopTracking();
        if (activeItem is not null) KeyboardNavigation.SetIsTabStop(activeItem, false);
        activeItem = null;
        if (trigger is not null)
        {
            trigger.ClearValue(OpacityProperty);
            trigger.IsHitTestVisible = true;
            KeyboardNavigation.SetIsTabStop(trigger, true);
        }
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }

    private void StartTracking()
    {
        if (top is not { } host) return;
        anchorAtOpen = GetAnchorInWindow(host);
        host.AddHandler(PointerPressedEvent, OutsidePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        host.LayoutUpdated += HostLayoutUpdated;
        if (host is Window window)
        {
            window.Activated += HostActivated;
            window.Deactivated += HostDeactivated;
        }
    }

    private void StopTracking()
    {
        if (top is not { } host) return;
        host.RemoveHandler(PointerPressedEvent, OutsidePressed);
        host.LayoutUpdated -= HostLayoutUpdated;
        if (host is Window window)
        {
            window.Activated -= HostActivated;
            window.Deactivated -= HostDeactivated;
        }
        anchorAtOpen = null;
    }

    private Rect? GetAnchorInWindow(TopLevel host) =>
        trigger?.TranslatePoint(default, host) is { } origin ? new Rect(origin, TriggerSize) : null;

    private void HostLayoutUpdated(object? sender, EventArgs e)
    {
        if (top is not { } host || surface is null) return;
        surface.SetViewport(host.ClientSize);
        if (GetAnchorInWindow(host) is not { } anchor || anchorAtOpen is not { } previous) return;

        // Popup positioning follows the trigger's own layout. An ancestor scroll can move
        // it without changing its bounds, so dismiss when the anchor changes in the viewport.
        if (IsOpen && (Math.Abs(anchor.X - previous.X) > 1 || Math.Abs(anchor.Y - previous.Y) > 1))
            Dismiss(false);
    }

    private void HostActivated(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && top == sender && surface?.IsKeyboardFocusWithin == false) FocusOnOpen();
        });

    private void HostDeactivated(object? sender, EventArgs e) => Dismiss(false);

    private void OutsidePressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && surface is { } panel &&
            (e.Source is not Visual visual || (visual != panel && !panel.IsVisualAncestorOf(visual))))
            Dismiss(false);
    }

    private void MenuLostFocus(object? sender, FocusChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && surface?.IsKeyboardFocusWithin == false)
                Dismiss(false);
        });

    private void MenuGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not AlderBloomMenuItem item || IndexFromContainer(item) < 0) return;
        if (activeItem is not null) KeyboardNavigation.SetIsTabStop(activeItem, false);
        activeItem = item;
        KeyboardNavigation.SetIsTabStop(item, true);
    }

    private void MenuPointerPressed(object? sender, PointerPressedEventArgs e) =>
        menuClickNavigation = NavigationMethod.Pointer;

    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source == close || e.Source is AlderBloomMenuItem item && IndexFromContainer(item) >= 0)
            Dismiss(true, menuClickNavigation);
    }

    private void Dismiss(bool restoreFocus, NavigationMethod navigation = NavigationMethod.Tab)
    {
        if (!IsOpen) return;
        if (surface?.IsKeyboardFocusWithin == true)
        {
            if (restoreFocus) trigger?.Focus(navigation);
            else top?.FocusManager?.Focus(null);
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    private void MenuKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen) return;
        if (e.Key is Key.Enter or Key.Space) menuClickNavigation = NavigationMethod.Tab;
        if (e.Key == Key.Escape)
        {
            Dismiss(true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool previous = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            bool stayedInMenu = previous && e.Source is AlderBloomMenuItem && close?.Focus(NavigationMethod.Tab) == true
                || !previous && e.Source == close && FocusItem(0, 1, NavigationMethod.Tab, activeItem);
            if (!stayedInMenu)
            {
                Dismiss(true);
                top?.FocusManager?.TryMoveFocus(previous ? NavigationDirection.Previous : NavigationDirection.Next);
            }
            e.Handled = true;
            return;
        }

        if (e.Source is not AlderBloomMenuItem current) return;
        int index = IndexFromContainer(current);
        int step = e.Key switch
        {
            Key.Left => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right => FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            Key.Up => -AlderBloomMenuGrid.ColumnCount,
            Key.Down => AlderBloomMenuGrid.ColumnCount,
            Key.Home => 1,
            Key.End => -1,
            _ => 0
        };
        if (step == 0) return;
        int target = e.Key switch { Key.Home => 0, Key.End => ItemCount - 1, _ => index + step };
        FocusItem(target, step, NavigationMethod.Directional);
        e.Handled = true;
    }

    private bool FocusItem(int index, int step, NavigationMethod method,
        AlderBloomMenuItem? preferred = null)
    {
        if (preferred is { IsEffectivelyEnabled: true } && preferred.Focus(method)) return true;
        for (; index >= 0 && index < ItemCount; index += step)
            if (ContainerFromIndex(index) is AlderBloomMenuItem { IsEffectivelyEnabled: true } item && item.Focus(method))
            {
                item.BringIntoView();
                return true;
            }
        return false;
    }

    private sealed class BloomMenuPeer(AlderBloomMenu owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState =>
            owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public bool ShowsMenu => true;
        public void Expand() => owner.SetCurrentValue(IsOpenProperty, true);
        public void Collapse() => owner.SetCurrentValue(IsOpenProperty, false);
        protected override string? GetNameCore() => AutomationProperties.GetName(owner) ?? owner.Header;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

internal sealed class AlderBloomMenuTrigger : Button
{
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<AlderBloomMenuTrigger>();

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }
}

internal sealed class AlderBloomMenuCaption : ContentControl
{
    public static readonly StyledProperty<object?> IconProperty = AlderBloomMenu.IconProperty.AddOwner<AlderBloomMenuCaption>();

    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public static readonly DirectProperty<AlderBloomMenuCaption, object?> MirroredIconProperty =
        AvaloniaProperty.RegisterDirect<AlderBloomMenuCaption, object?>(nameof(MirroredIcon), caption => caption.MirroredIcon);

    public object? MirroredIcon { get => field; private set => SetAndRaise(MirroredIconProperty, ref field, value); }

    private AlderIconPresenter? iconSlot;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        iconSlot = e.NameScope.Find<AlderIconPresenter>("PART_Icon");
        UpdateMirror();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IconProperty) UpdateMirror();
    }

    private void UpdateMirror()
    {
        // The morph and trigger coexist. A control icon has one visual parent.
        MirroredIcon = Icon is Control
            ? iconSlot is null ? null : new Border
            {
                Width = 12, Height = 12, Background = new VisualBrush(iconSlot)
            }
            : Icon;
    }
}
