using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies what happens to the sidebar when the available space is reduced.</summary>
public enum AppLayoutSidebarCollapse
{
    /// <summary>Collapses the sidebar to an icon rail.</summary>
    Icon,
    /// <summary>Moves the sidebar outside the main layout.</summary>
    OffCanvas,
    /// <summary>Keeps the expanded sidebar presentation.</summary>
    None
}
/// <summary>Places the sidebar at the logical start or end of the shell.</summary>
public enum AppLayoutSidebarPlacement
{
    /// <summary>Places the sidebar at the logical leading edge.</summary>
    Start,
    /// <summary>Places the sidebar at the logical trailing edge.</summary>
    End
}
/// <summary>Selects the sidebar's relationship to the surrounding shell surface.</summary>
public enum AppLayoutSidebarVariant
{
    /// <summary>Uses the standard sidebar surface.</summary>
    Standard,
    /// <summary>Insets the neighboring content relative to the sidebar.</summary>
    Inset,
    /// <summary>Presents the sidebar as a floating surface.</summary>
    Floating
}
/// <summary>Controls how the aside is presented at compact widths.</summary>
public enum AppLayoutCompactAside
{
    /// <summary>Hides the aside in the compact layout.</summary>
    Hidden,
    /// <summary>Allows the aside to open over the compact layout.</summary>
    Overlay
}
/// <summary>Chooses the element that owns scrolling in the application shell.</summary>
public enum AppLayoutScrollMode
{
    /// <summary>Scrolls the content region independently.</summary>
    Content,
    /// <summary>Lets the surrounding host manage scrolling.</summary>
    Host
}
/// <summary>Identifies which shell panel currently occupies the overlay layer.</summary>
public enum AppLayoutOverlay
{
    /// <summary>Indicates that neither panel occupies the overlay layer.</summary>
    None,
    /// <summary>Indicates that the sidebar occupies the overlay layer.</summary>
    Sidebar,
    /// <summary>Indicates that the aside occupies the overlay layer.</summary>
    Aside
}

/// <summary>
/// A responsive shell with navigation, main content, and contextual details.
/// Panel widths and desktop open states can be bound independently of the transient compact overlays.
/// </summary>
public class AlderAppLayout : ContentControl
{
    /// <summary>Identifies the <see cref="Sidebar"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SidebarProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(Sidebar));
    /// <summary>Identifies the <see cref="SidebarTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> SidebarTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(SidebarTemplate));
    /// <summary>Identifies the <see cref="MobileSidebar"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> MobileSidebarProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(MobileSidebar));
    /// <summary>Identifies the <see cref="MobileSidebarTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> MobileSidebarTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(MobileSidebarTemplate));
    /// <summary>Identifies the <see cref="Navbar"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> NavbarProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(Navbar));
    /// <summary>Identifies the <see cref="NavbarTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> NavbarTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(NavbarTemplate));
    /// <summary>Identifies the <see cref="Toolbar"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ToolbarProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(Toolbar));
    /// <summary>Identifies the <see cref="ToolbarTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ToolbarTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(ToolbarTemplate));
    /// <summary>Identifies the <see cref="Footer"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="Aside"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> AsideProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(Aside));
    /// <summary>Identifies the <see cref="AsideTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> AsideTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(AsideTemplate));
    /// <summary>Identifies the <see cref="MobileAside"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> MobileAsideProperty = AvaloniaProperty.Register<AlderAppLayout, object?>(nameof(MobileAside));
    /// <summary>Identifies the <see cref="MobileAsideTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> MobileAsideTemplateProperty = AvaloniaProperty.Register<AlderAppLayout, IDataTemplate?>(nameof(MobileAsideTemplate));

    /// <summary>Identifies the <see cref="IsSidebarOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSidebarOpenProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(IsSidebarOpen), true, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsAsideOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAsideOpenProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(IsAsideOpen), true, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="SidebarCollapse"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutSidebarCollapse> SidebarCollapseProperty =
        AvaloniaProperty.Register<AlderAppLayout, AppLayoutSidebarCollapse>(nameof(SidebarCollapse));
    /// <summary>Identifies the <see cref="SidebarPlacement"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutSidebarPlacement> SidebarPlacementProperty =
        AvaloniaProperty.Register<AlderAppLayout, AppLayoutSidebarPlacement>(nameof(SidebarPlacement));
    /// <summary>Identifies the <see cref="SidebarVariant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutSidebarVariant> SidebarVariantProperty =
        AvaloniaProperty.Register<AlderAppLayout, AppLayoutSidebarVariant>(nameof(SidebarVariant));
    /// <summary>Identifies the <see cref="CompactAside"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutCompactAside> CompactAsideProperty =
        AvaloniaProperty.Register<AlderAppLayout, AppLayoutCompactAside>(nameof(CompactAside));
    /// <summary>Identifies the <see cref="ScrollMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutScrollMode> ScrollModeProperty =
        AvaloniaProperty.Register<AlderAppLayout, AppLayoutScrollMode>(nameof(ScrollMode));
    /// <summary>Identifies the <see cref="ShowToggleButtons"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowToggleButtonsProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(ShowToggleButtons), true);
    /// <summary>Identifies the <see cref="ShowOverlayCloseButtons"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowOverlayCloseButtonsProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(ShowOverlayCloseButtons), true);
    /// <summary>Identifies the <see cref="IsSidebarResizable"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSidebarResizableProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(IsSidebarResizable));
    /// <summary>Identifies the <see cref="IsAsideResizable"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAsideResizableProperty =
        AvaloniaProperty.Register<AlderAppLayout, bool>(nameof(IsAsideResizable));

    /// <summary>Identifies the <see cref="SidebarBreakpoint"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SidebarBreakpointProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(SidebarBreakpoint), 768, validate: IsSize);
    /// <summary>Identifies the <see cref="AsideBreakpoint"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> AsideBreakpointProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(AsideBreakpoint), 1024, validate: IsSize);
    /// <summary>Identifies the <see cref="SidebarWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SidebarWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(SidebarWidth), 240, defaultBindingMode: BindingMode.TwoWay, validate: IsSize);
    /// <summary>Identifies the <see cref="CollapsedSidebarWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> CollapsedSidebarWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(CollapsedSidebarWidth), 48, validate: IsSize);
    /// <summary>Identifies the <see cref="AsideWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> AsideWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(AsideWidth), 320, defaultBindingMode: BindingMode.TwoWay, validate: IsSize);
    /// <summary>Identifies the <see cref="MinimumContentWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinimumContentWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(MinimumContentWidth), 280, validate: IsSize);
    /// <summary>Identifies the <see cref="MinimumSidebarWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinimumSidebarWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(MinimumSidebarWidth), 144, validate: IsSize);
    /// <summary>Identifies the <see cref="MinimumAsideWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinimumAsideWidthProperty =
        AvaloniaProperty.Register<AlderAppLayout, double>(nameof(MinimumAsideWidth), 176, validate: IsSize);
    /// <summary>Identifies the <see cref="SidebarToggleGesture"/> Avalonia property.</summary>
    public static readonly StyledProperty<KeyGesture?> SidebarToggleGestureProperty =
        AvaloniaProperty.Register<AlderAppLayout, KeyGesture?>(nameof(SidebarToggleGesture),
            new KeyGesture(Key.B, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control));
    /// <summary>Identifies the <see cref="AsideToggleGesture"/> Avalonia property.</summary>
    public static readonly StyledProperty<KeyGesture?> AsideToggleGestureProperty =
        AvaloniaProperty.Register<AlderAppLayout, KeyGesture?>(nameof(AsideToggleGesture));

    /// <summary>Identifies the <see cref="IsCompact"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderAppLayout, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AlderAppLayout, bool>(nameof(IsCompact), layout => layout.IsCompact);
    /// <summary>Identifies the <see cref="IsSidebarCollapsed"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderAppLayout, bool> IsSidebarCollapsedProperty =
        AvaloniaProperty.RegisterDirect<AlderAppLayout, bool>(nameof(IsSidebarCollapsed), layout => layout.IsSidebarCollapsed);
    /// <summary>Identifies the <see cref="ActiveOverlay"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderAppLayout, AppLayoutOverlay> ActiveOverlayProperty =
        AvaloniaProperty.RegisterDirect<AlderAppLayout, AppLayoutOverlay>(nameof(ActiveOverlay), layout => layout.ActiveOverlay);

    /// <summary>The sidebar content or its view model. An <see cref="AlderSidebar"/> supplied here or via SidebarTemplate receives the layout's icon-rail state.</summary>
    public object? Sidebar { get => GetValue(SidebarProperty); set => SetValue(SidebarProperty, value); }
    /// <summary>The data template used to present the sidebar.</summary>
    public IDataTemplate? SidebarTemplate { get => GetValue(SidebarTemplateProperty); set => SetValue(SidebarTemplateProperty, value); }
    /// <summary>Optional sidebar content for the compact overlay.</summary>
    public object? MobileSidebar { get => GetValue(MobileSidebarProperty); set => SetValue(MobileSidebarProperty, value); }
    /// <summary>The data template used to present the mobile sidebar.</summary>
    public IDataTemplate? MobileSidebarTemplate { get => GetValue(MobileSidebarTemplateProperty); set => SetValue(MobileSidebarTemplateProperty, value); }
    /// <summary>The navigation content displayed at the top of the shell.</summary>
    public object? Navbar { get => GetValue(NavbarProperty); set => SetValue(NavbarProperty, value); }
    /// <summary>The data template used to present the navbar.</summary>
    public IDataTemplate? NavbarTemplate { get => GetValue(NavbarTemplateProperty); set => SetValue(NavbarTemplateProperty, value); }
    /// <summary>The actions or controls displayed in the shell's toolbar slot.</summary>
    public object? Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
    /// <summary>The data template used to present the toolbar.</summary>
    public IDataTemplate? ToolbarTemplate { get => GetValue(ToolbarTemplateProperty); set => SetValue(ToolbarTemplateProperty, value); }
    /// <summary>The optional content displayed in the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>The data template used to present the footer.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>The content displayed in the secondary side panel.</summary>
    public object? Aside { get => GetValue(AsideProperty); set => SetValue(AsideProperty, value); }
    /// <summary>The data template used to present the aside.</summary>
    public IDataTemplate? AsideTemplate { get => GetValue(AsideTemplateProperty); set => SetValue(AsideTemplateProperty, value); }
    /// <summary>Optional aside content for the compact overlay.</summary>
    public object? MobileAside { get => GetValue(MobileAsideProperty); set => SetValue(MobileAsideProperty, value); }
    /// <summary>The data template used to present the mobile aside.</summary>
    public IDataTemplate? MobileAsideTemplate { get => GetValue(MobileAsideTemplateProperty); set => SetValue(MobileAsideTemplateProperty, value); }
    /// <summary>Whether the sidebar is requested open for the current layout.</summary>
    public bool IsSidebarOpen { get => GetValue(IsSidebarOpenProperty); set => SetValue(IsSidebarOpenProperty, value); }
    /// <summary>Whether the aside is requested open for the current layout.</summary>
    public bool IsAsideOpen { get => GetValue(IsAsideOpenProperty); set => SetValue(IsAsideOpenProperty, value); }
    /// <summary>How the sidebar is presented when collapsed.</summary>
    public AppLayoutSidebarCollapse SidebarCollapse { get => GetValue(SidebarCollapseProperty); set => SetValue(SidebarCollapseProperty, value); }
    /// <summary>The logical edge used to position the sidebar.</summary>
    public AppLayoutSidebarPlacement SidebarPlacement { get => GetValue(SidebarPlacementProperty); set => SetValue(SidebarPlacementProperty, value); }
    /// <summary>The surface treatment used for the desktop sidebar.</summary>
    public AppLayoutSidebarVariant SidebarVariant { get => GetValue(SidebarVariantProperty); set => SetValue(SidebarVariantProperty, value); }
    /// <summary>How the aside is presented when the layout is compact.</summary>
    public AppLayoutCompactAside CompactAside { get => GetValue(CompactAsideProperty); set => SetValue(CompactAsideProperty, value); }
    /// <summary>Which part of the shell owns scrolling.</summary>
    public AppLayoutScrollMode ScrollMode { get => GetValue(ScrollModeProperty); set => SetValue(ScrollModeProperty, value); }
    /// <summary>Whether to show the built-in shell panel toggles.</summary>
    public bool ShowToggleButtons { get => GetValue(ShowToggleButtonsProperty); set => SetValue(ShowToggleButtonsProperty, value); }
    /// <summary>Whether compact panel overlays include close buttons.</summary>
    public bool ShowOverlayCloseButtons { get => GetValue(ShowOverlayCloseButtonsProperty); set => SetValue(ShowOverlayCloseButtonsProperty, value); }
    /// <summary>Whether the expanded sidebar has a resize handle.</summary>
    public bool IsSidebarResizable { get => GetValue(IsSidebarResizableProperty); set => SetValue(IsSidebarResizableProperty, value); }
    /// <summary>Whether the open aside has a resize handle.</summary>
    public bool IsAsideResizable { get => GetValue(IsAsideResizableProperty); set => SetValue(IsAsideResizableProperty, value); }
    /// <summary>The width threshold for presenting the sidebar in compact mode.</summary>
    public double SidebarBreakpoint { get => GetValue(SidebarBreakpointProperty); set => SetValue(SidebarBreakpointProperty, value); }
    /// <summary>The width threshold for changing the aside to its compact behavior.</summary>
    public double AsideBreakpoint { get => GetValue(AsideBreakpointProperty); set => SetValue(AsideBreakpointProperty, value); }
    /// <summary>The preferred width of the expanded sidebar in device-independent pixels.</summary>
    public double SidebarWidth { get => GetValue(SidebarWidthProperty); set => SetValue(SidebarWidthProperty, value); }
    /// <summary>The width reserved for the collapsed sidebar rail.</summary>
    public double CollapsedSidebarWidth { get => GetValue(CollapsedSidebarWidthProperty); set => SetValue(CollapsedSidebarWidthProperty, value); }
    /// <summary>The preferred width of the aside in device-independent pixels.</summary>
    public double AsideWidth { get => GetValue(AsideWidthProperty); set => SetValue(AsideWidthProperty, value); }
    /// <summary>The minimum space reserved for the central content when resizing panels.</summary>
    public double MinimumContentWidth { get => GetValue(MinimumContentWidthProperty); set => SetValue(MinimumContentWidthProperty, value); }
    /// <summary>The smallest expanded width allowed for the sidebar.</summary>
    public double MinimumSidebarWidth { get => GetValue(MinimumSidebarWidthProperty); set => SetValue(MinimumSidebarWidthProperty, value); }
    /// <summary>The smallest expanded width allowed for the aside.</summary>
    public double MinimumAsideWidth { get => GetValue(MinimumAsideWidthProperty); set => SetValue(MinimumAsideWidthProperty, value); }
    /// <summary>The optional keyboard shortcut that toggles the sidebar.</summary>
    public KeyGesture? SidebarToggleGesture { get => GetValue(SidebarToggleGestureProperty); set => SetValue(SidebarToggleGestureProperty, value); }
    /// <summary>The optional keyboard shortcut that toggles the aside.</summary>
    public KeyGesture? AsideToggleGesture { get => GetValue(AsideToggleGestureProperty); set => SetValue(AsideToggleGestureProperty, value); }
    /// <summary>Whether the shell is currently below its sidebar breakpoint.</summary>
    public bool IsCompact => isCompact;
    /// <summary>Whether the sidebar is currently presented in its collapsed state.</summary>
    public bool IsSidebarCollapsed => isSidebarCollapsed;
    /// <summary>The panel currently displayed in the compact overlay layer.</summary>
    public AppLayoutOverlay ActiveOverlay => activeOverlay;

    private const double HandleWidth = 8;
    private const double FloatingSidebarGutter = 16;
    private static readonly TimeSpan PaneDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan DrawerEnterDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DrawerExitDuration = TimeSpan.FromMilliseconds(200);
    private static readonly SplineEasing PaneEasing = new(.25, .1, .25, 1);
    private static readonly SplineEasing DrawerEasing = new(.32, .72, 0, 1);
    private Grid? shell;
    private Border? sidebar, aside, scrim;
    private ContentPresenter? sidebarContent, mobileSidebarContent, asideContent, mobileAsideContent;
    private ScrollViewer? sidebarScroll, asideScroll;
    private bool sidebarOwnsScroll;
    private ScrollBarVisibility previousSidebarScrollVisibility;
    private Control? navbar;
    private ToggleButton? sidebarToggle, asideToggle;
    private Button? closeSidebar, closeAside;
    private GridSplitter? sidebarHandle, asideHandle;
    private bool isCompact, isSidebarCollapsed;
    private AppLayoutOverlay activeOverlay;
    private double layoutWidth = double.PositiveInfinity;
    private Control? returnFocus, lastSidebarFocus, lastAsideFocus;
    private int focusRequest;
    private DispatcherTimer? paneTimer;
    private PaneTransition sidebarTransition, asideTransition;
    private AppLayoutOverlay closingOverlay;
    private IDisposable? drawerExit;
    private bool IsAsideCompact => layoutWidth < AsideBreakpoint;
    private bool CanToggleSidebar => IsCompact ? Sidebar is not null || MobileSidebar is not null :
        Sidebar is not null && SidebarCollapse != AppLayoutSidebarCollapse.None;
    private bool CanToggleAside => IsAsideCompact ? CompactAside == AppLayoutCompactAside.Overlay && (Aside is not null || MobileAside is not null) :
        Aside is not null;

    /// <summary>Toggles the sidebar using the current responsive layout.</summary>
    public void ToggleSidebar()
    {
        if (!CanToggleSidebar || !IsEffectivelyEnabled) return;
        if (IsCompact) ToggleOverlay(AppLayoutOverlay.Sidebar);
        else SetCurrentValue(IsSidebarOpenProperty, !IsSidebarOpen);
    }

    /// <summary>Toggles the aside using the current responsive layout.</summary>
    public void ToggleAside()
    {
        if (!CanToggleAside || !IsEffectivelyEnabled) return;
        if (!IsAsideCompact) SetCurrentValue(IsAsideOpenProperty, !IsAsideOpen);
        else ToggleOverlay(AppLayoutOverlay.Aside);
    }

    /// <summary>Closes the sidebar or aside currently displayed in the overlay layer.</summary>
    public void CloseOverlay()
    {
        if (ActiveOverlay == AppLayoutOverlay.None) return;
        var toggle = ToggleFor(ActiveOverlay);
        var target = returnFocus ?? toggle;
        returnFocus = null;
        if (!AlderMotion.GetReduced(this) && PaneFor(ActiveOverlay) is { } pane)
        {
            closingOverlay = ActiveOverlay;
            OverlayMotion.Translate(pane, null, DrawerOffset(pane), DrawerExitDuration, DrawerEasing);
            drawerExit = DispatcherTimer.RunOnce(() =>
            {
                CancelDrawerExit();
                ApplyLayoutState();
            }, DrawerExitDuration);
        }
        SetAndRaise(ActiveOverlayProperty, ref activeOverlay, AppLayoutOverlay.None);
        ApplyLayoutState();
        QueueFocus(target, toggle ?? this);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        WireParts(attach: false);
        CancelDrawerExit();
        base.OnApplyTemplate(e);
        shell = e.NameScope.Find<Grid>("PART_Shell");
        sidebar = e.NameScope.Find<Border>("PART_Sidebar");
        aside = e.NameScope.Find<Border>("PART_Aside");
        sidebarContent = e.NameScope.Find<ContentPresenter>("PART_SidebarContent");
        mobileSidebarContent = e.NameScope.Find<ContentPresenter>("PART_MobileSidebarContent");
        asideContent = e.NameScope.Find<ContentPresenter>("PART_AsideContent");
        mobileAsideContent = e.NameScope.Find<ContentPresenter>("PART_MobileAsideContent");
        sidebarScroll = e.NameScope.Find<ScrollViewer>("PART_SidebarScroll");
        sidebarOwnsScroll = false;
        asideScroll = e.NameScope.Find<ScrollViewer>("PART_AsideScroll");
        scrim = e.NameScope.Find<Border>("PART_Scrim");
        navbar = e.NameScope.Find<Control>("PART_Navbar");
        sidebarToggle = e.NameScope.Find<ToggleButton>("PART_SidebarToggle");
        asideToggle = e.NameScope.Find<ToggleButton>("PART_AsideToggle");
        closeSidebar = e.NameScope.Find<Button>("PART_CloseSidebar");
        closeAside = e.NameScope.Find<Button>("PART_CloseAside");
        sidebarHandle = e.NameScope.Find<GridSplitter>("PART_SidebarHandle");
        asideHandle = e.NameScope.Find<GridSplitter>("PART_AsideHandle");
        WireParts(attach: true);
        ApplyLayoutState();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width)) SetLayoutWidth(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        SetLayoutWidth(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyLayoutState();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        paneTimer?.Stop();
        sidebarTransition = asideTransition = default;
        CancelDrawerExit();
        focusRequest++;
        returnFocus = lastSidebarFocus = lastAsideFocus = null;
        SetAndRaise(ActiveOverlayProperty, ref activeOverlay, AppLayoutOverlay.None);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (shell is not null && double.IsFinite(layoutWidth) && this.IsAttachedToVisualTree() && !AlderMotion.GetReduced(this))
        {
            if (change.Property == IsAsideOpenProperty && !IsAsideCompact && Aside is not null && !IsAsideResizable)
                StartPaneTransition(ref asideTransition, IsAsideOpen);
            if (change.Property == IsSidebarOpenProperty && !IsCompact && CanToggleSidebar &&
                (!IsSidebarResizable || SidebarCollapse == AppLayoutSidebarCollapse.Icon))
                StartPaneTransition(ref sidebarTransition, IsSidebarOpen);
        }
        if (shell is not null && (change.Property == SidebarProperty || change.Property == AsideProperty ||
            change.Property == MobileSidebarProperty || change.Property == MobileAsideProperty || change.Property == AlderMotion.ReducedProperty ||
            change.Property == NavbarProperty || change.Property == IsSidebarOpenProperty ||
            change.Property == IsAsideOpenProperty || change.Property == SidebarCollapseProperty ||
            change.Property == SidebarPlacementProperty || change.Property == SidebarVariantProperty || change.Property == CompactAsideProperty ||
            change.Property == SidebarBreakpointProperty || change.Property == AsideBreakpointProperty ||
            change.Property == SidebarWidthProperty || change.Property == CollapsedSidebarWidthProperty ||
            change.Property == AsideWidthProperty || change.Property == MinimumContentWidthProperty ||
            change.Property == MinimumSidebarWidthProperty || change.Property == MinimumAsideWidthProperty ||
            change.Property == IsSidebarResizableProperty || change.Property == IsAsideResizableProperty ||
            change.Property == ShowToggleButtonsProperty || change.Property == ShowOverlayCloseButtonsProperty ||
            change.Property == IsEffectivelyEnabledProperty))
            ApplyLayoutState();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Key.Escape && ActiveOverlay != AppLayoutOverlay.None)
            CloseOverlay();
        else if (CanToggleSidebar && SidebarToggleGesture?.Matches(e) == true)
            ToggleSidebar();
        else if (CanToggleAside && AsideToggleGesture?.Matches(e) == true)
            ToggleAside();
        else
            return;
        e.Handled = true;
    }

    private void SetLayoutWidth(double width)
    {
        if (layoutWidth == width) return;
        layoutWidth = width;
        ApplyLayoutState();
    }

    private void ApplyLayoutState()
    {
        if (shell is not { ColumnDefinitions.Count: >= 5 }) return;
        bool wasSidebarVisible = sidebar?.IsVisible == true;
        bool wasAsideVisible = aside?.IsVisible == true;
        var focused = FocusedControl();
        // Capture the focus presentation before hiding a pane can clear its focused element.
        var focusMethod = focused?.Classes.Contains(":focus-visible") == true
            ? NavigationMethod.Tab : NavigationMethod.Unspecified;
        bool compact = layoutWidth < SidebarBreakpoint;
        bool compactAside = IsAsideCompact;
        SetAndRaise(IsCompactProperty, ref isCompact, compact);
        PseudoClasses.Set(":compact", compact);
        bool reduceMotion = AlderMotion.GetReduced(this);
        if (reduceMotion && ActiveOverlay != AppLayoutOverlay.None && PaneFor(ActiveOverlay) is { } activePane)
            OverlayMotion.Reset(activePane);
        if (compact || Sidebar is null || SidebarCollapse == AppLayoutSidebarCollapse.None || reduceMotion ||
            IsSidebarResizable && SidebarCollapse != AppLayoutSidebarCollapse.Icon)
            sidebarTransition = default;
        if (compactAside || Aside is null || IsAsideResizable || reduceMotion)
            asideTransition = default;
        if (closingOverlay != AppLayoutOverlay.None && (reduceMotion || !CanShowDrawer(closingOverlay)))
            CancelDrawerExit();

        if (ActiveOverlay != AppLayoutOverlay.None && !CanShowDrawer(ActiveOverlay))
        {
            returnFocus = null;
            SetAndRaise(ActiveOverlayProperty, ref activeOverlay, AppLayoutOverlay.None);
        }

        bool sidebarOverlay = ActiveOverlay == AppLayoutOverlay.Sidebar;
        bool asideOverlay = ActiveOverlay == AppLayoutOverlay.Aside;
        bool isOverlay = sidebarOverlay || asideOverlay;
        bool showSidebarDrawer = sidebarOverlay || closingOverlay == AppLayoutOverlay.Sidebar;
        bool showAsideDrawer = asideOverlay || closingOverlay == AppLayoutOverlay.Aside;
        PseudoClasses.Set(":drawer-open", isOverlay);
        PseudoClasses.Set(":drawer-visible", isOverlay || closingOverlay != AppLayoutOverlay.None);
        bool collapsed = Sidebar is not null && !compact && !IsSidebarOpen && SidebarCollapse == AppLayoutSidebarCollapse.Icon;
        SetAndRaise(IsSidebarCollapsedProperty, ref isSidebarCollapsed, collapsed);

        bool sidebarInline = Sidebar is not null && !compact &&
                             (IsSidebarOpen || SidebarCollapse != AppLayoutSidebarCollapse.OffCanvas);
        bool asideInline = Aside is not null && !compactAside && IsAsideOpen;
        bool sidebarAvailable = Sidebar is not null && !compact;
        var now = Stopwatch.GetTimestamp();
        double sidebarReveal = sidebarTransition.Value(IsSidebarOpen, now);
        double asideReveal = asideTransition.Value(asideInline, now);
        bool resizeSidebar = sidebarInline && !collapsed && IsSidebarResizable && SidebarCollapse != AppLayoutSidebarCollapse.Icon;
        bool resizeAside = asideInline && IsAsideResizable;
        bool sidebarAtStart = SidebarPlacement == AppLayoutSidebarPlacement.Start;
        int navColumn = sidebarAtStart ? 0 : 4;
        int detailColumn = sidebarAtStart ? 4 : 0;
        double handleSpace = (resizeSidebar ? HandleWidth : 0) + (resizeAside ? HandleWidth : 0);
        double sideRoom = Math.Max(0, layoutWidth - MinimumContentWidth - handleSpace);

        double preferredSidebar = Math.Max(SidebarWidth, MinimumSidebarWidth);
        double preferredAside = Math.Max(AsideWidth, MinimumAsideWidth);
        double sidebarGutter = SidebarVariant == AppLayoutSidebarVariant.Floating ? FloatingSidebarGutter : 0;
        double sidebarClosedWidth = SidebarCollapse == AppLayoutSidebarCollapse.Icon ? CollapsedSidebarWidth + sidebarGutter : 0;
        double sidebarOpenWidth = preferredSidebar + sidebarGutter;
        double requestedSidebar = sidebarAvailable
            ? SidebarCollapse == AppLayoutSidebarCollapse.None ? sidebarOpenWidth :
                sidebarClosedWidth + (sidebarOpenWidth - sidebarClosedWidth) * sidebarReveal
            : 0;
        double requestedAside = preferredAside * asideReveal;
        // Keep the primary content usable if the caller supplies oversized panel widths.
        double sidebarMin = Math.Min(requestedSidebar, Math.Min(MinimumSidebarWidth + sidebarGutter, sideRoom));
        double asideMin = Math.Min(requestedAside, Math.Min(MinimumAsideWidth, sideRoom - sidebarMin));
        double sidebarSize = Math.Min(requestedSidebar, sideRoom - asideMin);
        double asideSize = Math.Min(requestedAside, sideRoom - sidebarSize);
        ConfigureColumn(navColumn, sidebarSize, sidebarMin);
        ConfigureColumn(detailColumn, asideSize, asideMin);
        ConfigureColumn(navColumn == 0 ? 1 : 3, resizeSidebar ? HandleWidth : 0);
        ConfigureColumn(detailColumn == 0 ? 1 : 3, resizeAside ? HandleWidth : 0);
        shell.ColumnDefinitions[2].MinWidth = Math.Min(MinimumContentWidth, layoutWidth);

        SetPane(sidebar, navColumn, sidebarInline || requestedSidebar > 0 || showSidebarDrawer, showSidebarDrawer, resizeSidebar, Math.Min(layoutWidth * 0.8, 500));
        SetPane(aside, detailColumn, asideInline || asideReveal > 0 || showAsideDrawer, showAsideDrawer, resizeAside, preferredAside);
        SetPaneInteraction(sidebar, sidebarInline, sidebarOverlay, focused, sidebarToggle, focusMethod, ref lastSidebarFocus);
        SetPaneInteraction(aside, asideInline, asideOverlay, focused, asideToggle, focusMethod, ref lastAsideFocus);
        SetSlidingContent(sidebarScroll, navColumn, sidebarTransition.IsRunning && SidebarCollapse == AppLayoutSidebarCollapse.OffCanvas, preferredSidebar);
        SetSlidingContent(asideScroll, detailColumn, asideTransition.IsRunning, preferredAside);
        bool showMobileSidebar = showSidebarDrawer && MobileSidebar is not null;
        bool showMobileAside = showAsideDrawer && MobileAside is not null;
        SetVisible(sidebarContent, !showMobileSidebar);
        SetVisible(mobileSidebarContent, showMobileSidebar);
        UpdateSidebarScrollOwnership(showMobileSidebar);
        SetVisible(asideContent, !showMobileAside);
        SetVisible(mobileAsideContent, showMobileAside);
        SetHandle(sidebarHandle, navColumn, resizeSidebar);
        SetHandle(asideHandle, detailColumn, resizeAside);
        SetVisible(scrim, isOverlay || closingOverlay != AppLayoutOverlay.None);
        if (scrim is not null) scrim.IsHitTestVisible = isOverlay;
        SetVisible(closeSidebar, showSidebarDrawer && ShowOverlayCloseButtons);
        SetVisible(closeAside, showAsideDrawer && ShowOverlayCloseButtons);
        bool showSidebarToggle = ShowToggleButtons && CanToggleSidebar;
        bool showAsideToggle = ShowToggleButtons && CanToggleAside;
        SetVisible(navbar, Navbar is not null || showSidebarToggle || showAsideToggle);
        SetToggle(sidebarToggle, showSidebarToggle, sidebarOverlay || sidebarInline && !collapsed);
        SetToggle(asideToggle, showAsideToggle, asideOverlay || asideInline);
        if (sidebarToggle is not null)
            Grid.SetColumn(sidebarToggle, sidebarAtStart ? 0 : 2);
        if (asideToggle is not null)
            Grid.SetColumn(asideToggle, sidebarAtStart ? 2 : 0);

        TrackPaneFocus(sidebar, wasSidebarVisible, focused, sidebarToggle, focusMethod, ref lastSidebarFocus);
        TrackPaneFocus(aside, wasAsideVisible, focused, asideToggle, focusMethod, ref lastAsideFocus);
        if (!isOverlay && focused is { IsEffectivelyVisible: false })
        {
            if (focused == sidebarHandle || focused == sidebarToggle)
                QueueFocus(showSidebarToggle ? sidebarToggle : FirstFocusable(navbar), this, focusMethod);
            else if (focused == asideHandle || focused == asideToggle)
                QueueFocus(showAsideToggle ? asideToggle : FirstFocusable(navbar), this, focusMethod);
        }
    }

    private void ConfigureColumn(int index, double width, double minimum = 0)
    {
        var column = shell!.ColumnDefinitions[index];
        column.MinWidth = minimum;
        column.Width = new GridLength(width);
    }

    private void SetPane(Border? pane, int column, bool visible, bool overlay, bool resizable, double width)
    {
        if (pane is null) return;
        if (!overlay && pane.Classes.Contains("overlay"))
            OverlayMotion.Reset(pane);
        pane.IsVisible = visible;
        pane.Classes.Set("overlay", overlay);
        pane.Classes.Set("resizable", resizable);
        Grid.SetColumn(pane, overlay ? 0 : column);
        Grid.SetColumnSpan(pane, overlay ? 5 : 1);
        pane.ZIndex = overlay ? 2 : 0;
        pane.Width = overlay ? Math.Min(width, Math.Max(0, layoutWidth - 24)) : double.NaN;
        pane.HorizontalAlignment = overlay ? column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right : HorizontalAlignment.Stretch;
    }

    private void SetPaneInteraction(Border? pane, bool inline, bool overlay, Control? focused, Control? toggle,
        NavigationMethod focusMethod, ref Control? lastFocus)
    {
        if (pane is null) return;
        pane.IsHitTestVisible = inline || overlay;
        KeyboardNavigation.SetTabNavigation(pane, overlay ? KeyboardNavigationMode.Cycle :
            inline ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        if (pane.IsVisible && !pane.IsHitTestVisible && ActiveOverlay == AppLayoutOverlay.None && IsInside(pane, focused))
        {
            lastFocus = focused;
            QueueFocus(toggle, this, focusMethod);
        }
    }

    private static void SetSlidingContent(ScrollViewer? content, int column, bool sliding, double width)
    {
        if (content is null) return;
        content.Width = sliding ? width : double.NaN;
        content.HorizontalAlignment = sliding ? column == 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left : HorizontalAlignment.Stretch;
    }

    private void StartPaneTransition(ref PaneTransition transition, bool open)
    {
        transition.Begin(open);
        paneTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            ApplyLayoutState();
            if (!sidebarTransition.IsRunning && !asideTransition.IsRunning)
                paneTimer?.Stop();
        });
        paneTimer.Start();
    }

    private struct PaneTransition
    {
        private long start;
        private double from;
        public readonly bool IsRunning => start != 0;

        public void Begin(bool open)
        {
            var now = Stopwatch.GetTimestamp();
            from = Value(!open, now);
            start = now;
        }

        public double Value(bool open, long now)
        {
            double target = open ? 1 : 0;
            if (!IsRunning) return target;
            double progress = Stopwatch.GetElapsedTime(start, now) / PaneDuration;
            if (progress >= 1)
            {
                start = 0;
                return target;
            }
            return from + (target - from) * PaneEasing.Ease(progress);
        }
    }

    private static void SetHandle(GridSplitter? handle, int column, bool visible)
    {
        if (handle is null) return;
        handle.IsVisible = visible;
        Grid.SetColumn(handle, column == 0 ? 1 : 3);
    }

    private static void SetToggle(ToggleButton? button, bool visible, bool expanded)
    {
        if (button is null) return;
        button.IsVisible = visible;
        button.SetCurrentValue(ToggleButton.IsCheckedProperty, expanded);
    }

    private static void SetVisible(Control? control, bool visible)
    {
        if (control is not null) control.IsVisible = visible;
    }

    private void ToggleOverlay(AppLayoutOverlay overlay)
    {
        if (ActiveOverlay == overlay)
        {
            CloseOverlay();
            return;
        }

        if (ActiveOverlay == AppLayoutOverlay.None) returnFocus = FocusedControl();
        bool reopening = closingOverlay == overlay;
        CancelDrawerExit(reset: !reopening);
        SetAndRaise(ActiveOverlayProperty, ref activeOverlay, overlay);
        ApplyLayoutState();
        var pane = PaneFor(overlay);
        if (pane is not null && !AlderMotion.GetReduced(this))
            OverlayMotion.Translate(pane, reopening ? null : DrawerOffset(pane), default, DrawerEnterDuration, DrawerEasing);
        var remembered = overlay == AppLayoutOverlay.Sidebar ? lastSidebarFocus : lastAsideFocus;
        QueueFocus(IsInside(pane, remembered) && remembered?.IsEffectivelyVisible == true ? remembered : FirstFocusable(pane), pane);
    }

    private Border? PaneFor(AppLayoutOverlay overlay) => overlay == AppLayoutOverlay.Sidebar ? sidebar : aside;

    private bool CanShowDrawer(AppLayoutOverlay overlay) => IsEffectivelyEnabled &&
        (overlay == AppLayoutOverlay.Sidebar ? IsCompact && CanToggleSidebar : IsAsideCompact && CanToggleAside);

    private Vector3D DrawerOffset(Border pane)
    {
        bool left = pane.HorizontalAlignment == HorizontalAlignment.Left;
        bool leftToRight = FlowDirection == global::Avalonia.Media.FlowDirection.LeftToRight;
        return new Vector3D(left == leftToRight ? -pane.Width : pane.Width, 0, 0);
    }

    private void CancelDrawerExit(bool reset = true)
    {
        if (reset && closingOverlay != AppLayoutOverlay.None && PaneFor(closingOverlay) is { } pane)
            OverlayMotion.Reset(pane);
        drawerExit?.Dispose();
        drawerExit = null;
        closingOverlay = AppLayoutOverlay.None;
    }

    private Control? ToggleFor(AppLayoutOverlay overlay) => overlay == AppLayoutOverlay.Sidebar ? sidebarToggle : asideToggle;

    private void TrackPaneFocus(Control? pane, bool wasVisible, Control? focused, Control? toggle,
        NavigationMethod focusMethod, ref Control? lastFocused)
    {
        if (pane is null) return;
        if (!pane.IsVisible && wasVisible && IsInside(pane, focused))
        {
            lastFocused = focused;
            if (ActiveOverlay == AppLayoutOverlay.None)
                QueueFocus(toggle, FirstFocusable(navbar) ?? this, focusMethod);
        }
        else if (pane.IsVisible && !wasVisible && ActiveOverlay == AppLayoutOverlay.None &&
                 (focused == toggle || focused == this) && IsInside(pane, lastFocused))
            QueueFocus(lastFocused, pane, focusMethod);
    }

    private Control? FocusedControl() => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

    private static bool IsInside(Control? parent, Control? child) =>
        parent is not null && child is not null && (parent == child || parent.IsVisualAncestorOf(child));

    private static Control? FirstFocusable(Control? parent) => parent?.GetVisualDescendants().OfType<Control>()
        .FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && KeyboardNavigation.GetIsTabStop(c));

    private void QueueFocus(Control? target, Control? fallback, NavigationMethod method = NavigationMethod.Tab)
    {
        var request = ++focusRequest;
        Dispatcher.UIThread.Post(() =>
        {
            if (focusRequest != request || !this.IsAttachedToVisualTree()) return;
            if (target is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } ||
                !target.IsAttachedToVisualTree() || !target.Focus(method))
                fallback?.Focus(method);
        }, DispatcherPriority.Loaded);
    }

    private void WireParts(bool attach)
    {
        WireSidebarPresenter(sidebarContent);
        WireSidebarPresenter(mobileSidebarContent);
        WireClick(sidebarToggle, OnSidebarToggle);
        WireClick(asideToggle, OnAsideToggle);
        WireClick(closeSidebar, OnCloseClick);
        WireClick(closeAside, OnCloseClick);
        WireHandle(sidebarHandle);
        WireHandle(asideHandle);
        if (scrim is null) return;
        if (attach) scrim.PointerPressed += OnScrimPressed;
        else scrim.PointerPressed -= OnScrimPressed;

        void WireClick(Button? button, EventHandler<RoutedEventArgs> handler)
        {
            if (button is null) return;
            if (attach) button.Click += handler;
            else button.Click -= handler;
        }

        void WireHandle(GridSplitter? handle)
        {
            if (handle is null) return;
            if (attach)
            {
                handle.AddHandler(Thumb.DragCompletedEvent, OnResizeCompleted, handledEventsToo: true);
                handle.AddHandler(KeyDownEvent, OnResizeKey, handledEventsToo: true);
            }
            else
            {
                handle.RemoveHandler(Thumb.DragCompletedEvent, OnResizeCompleted);
                handle.RemoveHandler(KeyDownEvent, OnResizeKey);
            }
        }

        void WireSidebarPresenter(ContentPresenter? presenter)
        {
            if (presenter is null) return;
            if (attach) presenter.PropertyChanged += OnSidebarPresenterChanged;
            else presenter.PropertyChanged -= OnSidebarPresenterChanged;
        }
    }

    private void OnSidebarPresenterChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ChildProperty)
            UpdateSidebarScrollOwnership(mobileSidebarContent?.IsVisible == true);
    }

    private void UpdateSidebarScrollOwnership(bool mobile)
    {
        if (sidebarScroll is null) return;
        var content = mobile ? MobileSidebar : Sidebar;
        var presenter = mobile ? mobileSidebarContent : sidebarContent;
        bool ownsScroll = content is AlderSidebar || presenter?.Child is AlderSidebar or ContentControl { Content: AlderSidebar };
        if (ownsScroll == sidebarOwnsScroll) return;
        if (ownsScroll) previousSidebarScrollVisibility = sidebarScroll.VerticalScrollBarVisibility;
        sidebarOwnsScroll = ownsScroll;
        sidebarScroll.SetCurrentValue(ScrollViewer.VerticalScrollBarVisibilityProperty,
            ownsScroll ? ScrollBarVisibility.Disabled : previousSidebarScrollVisibility);
    }

    private void OnSidebarToggle(object? sender, RoutedEventArgs e) => ToggleSidebar();
    private void OnAsideToggle(object? sender, RoutedEventArgs e) => ToggleAside();
    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseOverlay();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        CloseOverlay();
        e.Handled = true;
    }

    private void OnResizeCompleted(object? sender, VectorEventArgs e) => SaveWidth(sender as GridSplitter);
    private void OnResizeKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right && e.Handled) SaveWidth(sender as GridSplitter);
    }

    private void SaveWidth(GridSplitter? handle)
    {
        if (shell is null || handle is not { IsVisible: true }) return;
        int column = Grid.GetColumn(handle) == 1 ? 0 : 4;
        var width = shell.ColumnDefinitions[column].Width;
        if (!width.IsAbsolute) return;
        if (handle == sidebarHandle)
        {
            double gutter = SidebarVariant == AppLayoutSidebarVariant.Floating ? FloatingSidebarGutter : 0;
            SetCurrentValue(SidebarWidthProperty, Math.Max(0, width.Value - gutter));
        }
        else SetCurrentValue(AsideWidthProperty, width.Value);
    }

    private static bool IsSize(double value) => double.IsFinite(value) && value >= 0;
}
