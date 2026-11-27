using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Controls how the navigation bar is placed relative to the content it accompanies.</summary>
public enum AlderNavbarPosition
{
    /// <summary>Keeps the header at the top of its region during scrolling.</summary>
    Sticky,
    /// <summary>Uses the normal layout position.</summary>
    Static,
    /// <summary>Places the header in a floating layer.</summary>
    Floating
}

/// <summary>A responsive navigation bar that can track a separate scroll surface.</summary>
public sealed class AlderNavbar : TemplatedControl
{
    /// <summary>Identifies the <see cref="Brand"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> BrandProperty = AvaloniaProperty.Register<AlderNavbar, object?>(nameof(Brand));
    /// <summary>Identifies the <see cref="Navigation"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> NavigationProperty = AvaloniaProperty.Register<AlderNavbar, object?>(nameof(Navigation));
    /// <summary>Identifies the <see cref="Actions"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<AlderNavbar, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="Menu"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> MenuProperty = AvaloniaProperty.Register<AlderNavbar, object?>(nameof(Menu));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AvaloniaProperty.Register<AlderNavbar, AlderSize>(nameof(Size), AlderSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="Position"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderNavbarPosition> PositionProperty =
        AvaloniaProperty.Register<AlderNavbar, AlderNavbarPosition>(nameof(Position), AlderNavbarPosition.Sticky);
    /// <summary>Identifies the <see cref="HeaderHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<AlderNavbar, double>(nameof(HeaderHeight), 64,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="HeaderMaxWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> HeaderMaxWidthProperty =
        AvaloniaProperty.Register<AlderNavbar, double>(nameof(HeaderMaxWidth), 1024, validate: Positive);
    /// <summary>Identifies the <see cref="CollapseWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> CollapseWidthProperty =
        AvaloniaProperty.Register<AlderNavbar, double>(nameof(CollapseWidth), 768,
            validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="AlwaysShowMenuToggle"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AlwaysShowMenuToggleProperty =
        AvaloniaProperty.Register<AlderNavbar, bool>(nameof(AlwaysShowMenuToggle));
    /// <summary>Identifies the <see cref="IsMenuOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsMenuOpenProperty =
        AvaloniaProperty.Register<AlderNavbar, bool>(nameof(IsMenuOpen), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="HideOnScroll"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> HideOnScrollProperty =
        AvaloniaProperty.Register<AlderNavbar, bool>(nameof(HideOnScroll));
    /// <summary>Identifies the <see cref="ShouldBlockScroll"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShouldBlockScrollProperty =
        AvaloniaProperty.Register<AlderNavbar, bool>(nameof(ShouldBlockScroll), true);
    /// <summary>Identifies the <see cref="ScrollHost"/> Avalonia property.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollHostProperty =
        AvaloniaProperty.Register<AlderNavbar, ScrollViewer?>(nameof(ScrollHost));
    /// <summary>Identifies the <see cref="NavigateCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<AlderNavbar, ICommand?>(nameof(NavigateCommand));
    /// <summary>Identifies the <see cref="CurrentRoute"/> Avalonia property.</summary>
    public static readonly AttachedProperty<string?> CurrentRouteProperty =
        AvaloniaProperty.RegisterAttached<AlderNavbar, StyledElement, string?>(nameof(CurrentRoute), inherits: true,
            defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsCompact"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderNavbar, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AlderNavbar, bool>(nameof(IsCompact), bar => bar.IsCompact);
    /// <summary>Identifies the <see cref="IsScrollHidden"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderNavbar, bool> IsScrollHiddenProperty =
        AvaloniaProperty.RegisterDirect<AlderNavbar, bool>(nameof(IsScrollHidden), bar => bar.IsScrollHidden);
    internal static readonly DirectProperty<AlderNavbar, bool> IsMenuToggleVisibleProperty =
        AvaloniaProperty.RegisterDirect<AlderNavbar, bool>(nameof(IsMenuToggleVisible), bar => bar.IsMenuToggleVisible);
    internal static readonly DirectProperty<AlderNavbar, bool> IsNavigationVisibleProperty =
        AvaloniaProperty.RegisterDirect<AlderNavbar, bool>(nameof(IsNavigationVisible), bar => bar.IsNavigationVisible);

    private Border? header;
    private Button? toggle;
    private Popup? popup;
    private ScrollViewer? menuScroll;
    private ScrollViewer? scroll;
    private TopLevel? menuRoot;
    private bool revealMenu;
    private bool hasArranged;

    /// <summary>The branding content shown in the header.</summary>
    public object? Brand { get => GetValue(BrandProperty); set => SetValue(BrandProperty, value); }
    /// <summary>The primary navigation content presented at full header width.</summary>
    public object? Navigation { get => GetValue(NavigationProperty); set => SetValue(NavigationProperty, value); }
    /// <summary>The auxiliary actions presented in the header.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>The content shown when the compact menu is open.</summary>
    public object? Menu { get => GetValue(MenuProperty); set => SetValue(MenuProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Sticky and floating bars stay at the top of an enclosing scroll viewer. Floating adds inset chrome.</summary>
    public AlderNavbarPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    /// <summary>The height reserved for the navigation header.</summary>
    public double HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }
    /// <summary>The maximum width of the inner header region.</summary>
    public double HeaderMaxWidth { get => GetValue(HeaderMaxWidthProperty); set => SetValue(HeaderMaxWidthProperty, value); }
    /// <summary>The width threshold below which compact navigation is used.</summary>
    public double CollapseWidth { get => GetValue(CollapseWidthProperty); set => SetValue(CollapseWidthProperty, value); }
    /// <summary>Whether to keep the menu toggle available at full width.</summary>
    public bool AlwaysShowMenuToggle { get => GetValue(AlwaysShowMenuToggleProperty); set => SetValue(AlwaysShowMenuToggleProperty, value); }
    /// <summary>Whether the navigation menu is requested open.</summary>
    public bool IsMenuOpen { get => GetValue(IsMenuOpenProperty); set => SetValue(IsMenuOpenProperty, value); }
    /// <summary>Whether downward scrolling can hide the header.</summary>
    public bool HideOnScroll { get => GetValue(HideOnScrollProperty); set => SetValue(HideOnScrollProperty, value); }
    /// <summary>Whether an open compact menu prevents scrolling the accompanying content.</summary>
    public bool ShouldBlockScroll { get => GetValue(ShouldBlockScrollProperty); set => SetValue(ShouldBlockScrollProperty, value); }
    /// <summary>The scroll viewer monitored for header visibility changes.</summary>
    public ScrollViewer? ScrollHost { get => GetValue(ScrollHostProperty); set => SetValue(ScrollHostProperty, value); }
    /// <summary>The command notified of navigation from a navbar item.</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
    /// <summary>The route shared with items to determine their current state.</summary>
    public string? CurrentRoute { get => GetValue(CurrentRouteProperty); set => SetValue(CurrentRouteProperty, value); }
    /// <summary>Whether the navbar currently uses the compact layout.</summary>
    public bool IsCompact { get => field; private set => SetAndRaise(IsCompactProperty, ref field, value); }
    /// <summary>Whether the header is currently hidden because of scrolling.</summary>
    public bool IsScrollHidden { get => field; private set => SetAndRaise(IsScrollHiddenProperty, ref field, value); }
    internal bool IsMenuToggleVisible { get => field; private set => SetAndRaise(IsMenuToggleVisibleProperty, ref field, value); }
    internal bool IsNavigationVisible { get => field; private set => SetAndRaise(IsNavigationVisibleProperty, ref field, value); } = true;

    /// <summary>Initializes a new instance of <see cref="AlderNavbar"/>.</summary>
    public AlderNavbar()
    {
        AddHandler(Button.ClickEvent, OnItemClick, RoutingStrategies.Bubble, handledEventsToo: true);
        LayoutUpdated += (_, _) =>
        {
            RefreshHeader();
            FitMenuBelowHeader();
        };
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        SetMenuRoot(null);
        if (toggle is not null) toggle.Click -= OnToggleClick;
        if (menuScroll is not null) menuScroll.RemoveHandler(RequestBringIntoViewEvent, OnMenuBringIntoView);
        if (popup is not null)
        {
            popup.Opened -= OnPopupOpened;
            popup.Closed -= OnPopupClosed;
        }
        base.OnApplyTemplate(e);
        header = e.NameScope.Find<Border>("PART_Header");
        toggle = e.NameScope.Find<Button>("PART_Toggle");
        popup = e.NameScope.Find<Popup>("PART_Menu");
        menuScroll = e.NameScope.Find<ScrollViewer>("PART_MenuScroll");
        if (toggle is not null) toggle.Click += OnToggleClick;
        if (menuScroll is not null) menuScroll.AddHandler(RequestBringIntoViewEvent, OnMenuBringIntoView);
        if (popup is not null)
        {
            popup.Opened += OnPopupOpened;
            popup.Closed += OnPopupClosed;
        }
        RefreshHeader();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
            UpdateCompact(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateCompact(finalSize.Width);
        hasArranged = true;
        return base.ArrangeOverride(finalSize);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SetScrollHost(ScrollHost ?? this.FindAncestorOfType<ScrollViewer>());
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SetMenuRoot(null);
        SetScrollHost(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CollapseWidthProperty)
            InvalidateMeasure();
        if (change.Property == MenuProperty || change.Property == AlwaysShowMenuToggleProperty)
            UpdateToggle();
        if (change.Property == ScrollHostProperty && this.IsAttachedToVisualTree())
            SetScrollHost(ScrollHost ?? this.FindAncestorOfType<ScrollViewer>());
        if (change.Property == HideOnScrollProperty && !HideOnScroll)
            SetScrollHidden(false);
        if (change.Property == IsMenuOpenProperty && IsMenuOpen)
        {
            if (hasArranged && !IsMenuToggleVisible)
                SetCurrentValue(IsMenuOpenProperty, false);
            else
                SetScrollHidden(false);
        }
        if (change.Property == HeaderHeightProperty || change.Property == PositionProperty)
            RefreshHeader();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && IsMenuOpen)
        {
            e.Handled = true;
            CloseMenuAndFocusToggle();
        }
        base.OnKeyDown(e);
    }

    private void UpdateCompact(double width)
    {
        IsCompact = width < CollapseWidth;
        PseudoClasses.Set(":compact", IsCompact);
        UpdateToggle();
    }

    private void UpdateToggle()
    {
        IsNavigationVisible = !IsCompact || Menu is null;
        IsMenuToggleVisible = Menu is not null && (IsCompact || AlwaysShowMenuToggle);
        if (hasArranged && !IsMenuToggleVisible && IsMenuOpen)
            SetCurrentValue(IsMenuOpenProperty, false);
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsMenuOpenProperty, !IsMenuOpen);

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not AlderNavbarItem item || !item.IsEffectivelyEnabled) return;
        if (item.Command is null && item.Route is { Length: > 0 } route)
        {
            if (NavigateCommand is { } command)
            {
                if (!command.CanExecute(route)) return;
                command.Execute(route);
            }
            SetCurrentValue(CurrentRouteProperty, route);
        }
        if (item.IsMenuItem && IsMenuOpen) CloseMenuAndFocusToggle();
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        SetMenuRoot(TopLevel.GetTopLevel(this));
        revealMenu = true;
        FitMenuBelowHeader();
        if (!FocusMenu())
            Dispatcher.UIThread.Post(() => FocusMenu(), DispatcherPriority.Input);
    }

    private void FitMenuBelowHeader()
    {
        if (menuRoot is null || menuScroll is null || header is null || !IsMenuOpen ||
            header.TranslatePoint(new Point(0, header.Bounds.Height), menuRoot) is not { } anchor)
            return;

        var safeArea = menuRoot.InsetsManager?.SafeAreaPadding ?? default;
        double viewportBottom = menuRoot.ClientSize.Height - safeArea.Bottom;
        var presenter = this.FindAncestorOfType<ScrollContentPresenter>();
        if (presenter?.TranslatePoint(new Point(0, presenter.Bounds.Height), menuRoot) is { } viewport)
            viewportBottom = Math.Min(viewportBottom, viewport.Y);

        const double menuHeightLimit = 440;
        double availableHeight = Math.Max(0, viewportBottom - anchor.Y);
        menuScroll.MaxHeight = Math.Min(menuHeightLimit, availableHeight);

        if (!revealMenu || menuScroll.Extent.Height <= 0) return;
        revealMenu = false;
        double menuHeight = Math.Min(menuHeightLimit, menuScroll.Extent.Height);
        if (menuHeight > availableHeight && presenter?.FindAncestorOfType<ScrollViewer>() is { } outerScroll &&
            presenter.TranslatePoint(default, menuRoot) is { } viewportTop &&
            header.TranslatePoint(default, menuRoot) is { } headerTop)
        {
            // Reveal the menu using the enclosing viewport while keeping it attached below the header.
            double delta = Math.Min(menuHeight - availableHeight, Math.Max(0, headerTop.Y - Math.Max(viewportTop.Y, safeArea.Top)));
            outerScroll.SetCurrentValue(ScrollViewer.OffsetProperty, outerScroll.Offset + new Vector(0, delta));
        }
    }

    private static void OnMenuBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // The menu's own scroll presenter can bring focused items into view.
        e.Handled = true;
    }

    private bool FocusMenu()
    {
        if (popup is not { IsOpen: true, Child: { } menu }) return false;
        return menu.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
            control is not ScrollViewer && control.IsEffectivelyEnabled && control.IsEffectivelyVisible &&
            control.Focusable && KeyboardNavigation.GetIsTabStop(control))?.Focus() == true;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        SetMenuRoot(null);
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null)
            toggle?.Focus();
    }

    private void CloseMenuAndFocusToggle()
    {
        SetCurrentValue(IsMenuOpenProperty, false);
        toggle?.Focus();
    }

    private void SetScrollHost(ScrollViewer? host)
    {
        if (scroll == host) return;
        if (scroll is not null)
        {
            scroll.ScrollChanged -= OnScrollChanged;
            scroll.RemoveHandler(PointerWheelChangedEvent, OnScrollWheel);
        }
        scroll = host;
        SetScrollHidden(false);
        if (scroll is not null)
        {
            scroll.ScrollChanged += OnScrollChanged;
            scroll.AddHandler(PointerWheelChangedEvent, OnScrollWheel, RoutingStrategies.Tunnel);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source != scroll || e.OffsetDelta.Y == 0) return;
        SetScrollHidden(HideOnScroll && !IsMenuOpen && scroll!.Offset.Y > HeaderHeight && e.OffsetDelta.Y > 0);
    }

    private void OnScrollWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ShouldBlockScroll && IsMenuOpen)
            e.Handled = true;
    }

    private void SetMenuRoot(TopLevel? root)
    {
        if (menuRoot is not null)
            menuRoot.PointerWheelChanged -= OnMenuRootWheel;
        menuRoot = root;
        if (menuRoot is not null)
            menuRoot.PointerWheelChanged += OnMenuRootWheel;
    }

    private void OnMenuRootWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ShouldBlockScroll || !IsMenuOpen || scroll is null || e.Source is not Visual source ||
            source == scroll || scroll.IsVisualAncestorOf(source) || this.IsVisualAncestorOf(source) ||
            source.FindAncestorOfType<OverlayPopupHost>(includeSelf: true) is not null)
            return;
        if (scroll.Presenter is not ScrollContentPresenter presenter) return;
        var point = e.GetCurrentPoint(presenter);
        if (!new Rect(presenter.Bounds.Size).Contains(point.Position)) return;

        // Forward wheel input intercepted by the popup's light dismiss layer.
        var forwarded = new PointerWheelEventArgs(presenter, e.Pointer, presenter, point.Position,
            e.Timestamp, point.Properties, e.KeyModifiers, e.Delta);
        presenter.RaiseEvent(forwarded);
        e.Handled = forwarded.Handled;
    }

    private void SetScrollHidden(bool value)
    {
        IsScrollHidden = value;
        PseudoClasses.Set(":scroll-hidden", value);
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        if (header?.RenderTransform is TranslateTransform translation)
        {
            double stickyOffset = Position != AlderNavbarPosition.Static && scroll is not null &&
                                  scroll.IsVisualAncestorOf(this) && this.TranslatePoint(default, scroll) is { } position
                ? Math.Max(0, (Position == AlderNavbarPosition.Floating ? Margin.Top : 0) - position.Y)
                : 0;
            translation.Y = stickyOffset + (IsScrollHidden ? -HeaderHeight - 16 : 0);
        }
        if (header is not null)
            KeyboardNavigation.SetTabNavigation(header, IsScrollHidden ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
    }

    private static bool Positive(double value) => value > 0 && !double.IsNaN(value);
}

/// <summary>A navigation action or link that can participate in an AlderNavbar's routing.</summary>
public sealed class AlderNavbarItem : HyperlinkButton
{
    /// <summary>Identifies the <see cref="IsCurrent"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty = AvaloniaProperty.Register<AlderNavbarItem, bool>(nameof(IsCurrent));
    /// <summary>Identifies the <see cref="IsMenuItem"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsMenuItemProperty = AvaloniaProperty.Register<AlderNavbarItem, bool>(nameof(IsMenuItem));
    /// <summary>Identifies the <see cref="Route"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> RouteProperty = AvaloniaProperty.Register<AlderNavbarItem, string?>(nameof(Route));
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IconProperty = AvaloniaProperty.Register<AlderNavbarItem, object?>(nameof(Icon));

    internal static readonly StyledProperty<AlderSize> SizeProperty = AlderNavbar.SizeProperty.AddOwner<AlderNavbarItem>();

    internal AlderSize Size => GetValue(SizeProperty);

    /// <summary>Whether this item matches the current route.</summary>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    /// <summary>Whether this item uses the compact menu presentation.</summary>
    public bool IsMenuItem { get => GetValue(IsMenuItemProperty); set => SetValue(IsMenuItemProperty, value); }
    /// <summary>The route associated with this navigation item.</summary>
    public string? Route { get => GetValue(RouteProperty); set => SetValue(RouteProperty, value); }
    /// <summary>An optional geometry or icon control displayed with the content.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty || change.Property == RouteProperty || change.Property == AlderNavbar.CurrentRouteProperty)
            PseudoClasses.Set(":current", IsCurrent || Route is { Length: > 0 } route && route == GetValue(AlderNavbar.CurrentRouteProperty));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NavbarItemPeer(this);

    private sealed class NavbarItemPeer(AlderNavbarItem item) : ButtonAutomationPeer(item)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            item.NavigateUri is not null || item.Route is not null ? AutomationControlType.Hyperlink : AutomationControlType.Button;
    }
}
