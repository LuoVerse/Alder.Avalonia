using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public class AppLayoutInteractions
{
    [AvaloniaFact]
    public void TemplatedSidebarHeaderShowsOnlyItsIconInTheDesktopRail()
    {
        var icon = Geometry.Parse("M0,0 H8 V8 H0 Z");
        var layout = new AlderAppLayout
        {
            Sidebar = "Workspace", SidebarCollapse = AppLayoutSidebarCollapse.Icon,
            SidebarTemplate = new FuncDataTemplate<string>((title, _) => new AlderSidebar { Header = title, HeaderIcon = icon })
        };
        AlderMotion.SetReduced(layout, true);
        using var view = new LayoutWindow(layout, 1000);
        var sidebar = Assert.Single(layout.GetVisualDescendants().OfType<AlderSidebar>());
        var header = Find<ContentPresenter>(sidebar, "PART_HeaderPresenter");
        var glyph = Assert.IsType<AlderPathIcon>(Find<ContentPresenter>(sidebar, "PART_HeaderIcon").Child);
        Assert.Equal("Workspace", header.Content);
        Assert.True(IsDisplayed(header));

        layout.ToggleSidebar();
        view.Pump();
        Assert.True(sidebar.IsRail);
        Assert.True(IsDisplayed(glyph));
        Assert.False(IsDisplayed(header));

        view.Resize(500);
        layout.ToggleSidebar();
        view.Pump();
        Assert.Equal(AppLayoutOverlay.Sidebar, layout.ActiveOverlay);
        Assert.True(IsDisplayed(glyph));
        Assert.True(IsDisplayed(header));
    }

    [AvaloniaFact]
    public void TemplatedSidebarSearchInTheDesktopRailRevealsAndSelectsTheMatchingChild()
    {
        var home = new AlderSidebarItem { Header = "Home", IsCurrent = true };
        var details = new AlderSidebarItem { Header = "Details" };
        var reports = new AlderSidebarItem { Header = "Reports", Items = { details } };
        var menu = new AlderSidebarMenu { Items = { home, reports } };
        var layout = new AlderAppLayout
        {
            Sidebar = "Workspace", SidebarCollapse = AppLayoutSidebarCollapse.Icon,
            SidebarTemplate = new FuncDataTemplate<string>((title, _) => new AlderSidebar { Header = title, Content = menu })
        };
        AlderMotion.SetReduced(layout, true);
        using var view = new LayoutWindow(layout, 1000);
        var sidebar = Assert.Single(layout.GetVisualDescendants().OfType<AlderSidebar>());
        var trigger = Find<AlderButton>(sidebar, "PART_Search");
        Assert.False(IsDisplayed(trigger));

        sidebar.IsSearchEnabled = true;
        layout.ToggleSidebar();
        view.Pump();
        Assert.True(IsDisplayed(trigger));
        Assert.True(trigger.IsIconOnly);
        Assert.Same(home, sidebar.SelectedItem);

        var point = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, view.Window)!.Value;
        view.Window.MouseDown(point, MouseButton.Left);
        view.Window.MouseUp(point, MouseButton.Left);
        view.Pump();
        var search = Assert.IsType<AlderSearchField>(view.Window.FocusManager!.GetFocusedElement());
        var palette = search.FindAncestorOfType<AlderCommand>()!;
        view.Window.KeyTextInput("Details");
        view.Pump();
        Assert.Equal("Details", palette.ActiveItem?.Label);

        view.Press(PhysicalKey.Enter);
        Assert.True(layout.IsSidebarOpen);
        Assert.True(reports.IsExpanded);
        Assert.Same(details, sidebar.SelectedItem);
        Assert.Same(details, Assert.Single(menu.SelectedItems));
    }

    [AvaloniaFact]
    public void CanvasAndPanelEdgesFollowThemeAndSidebarVariant()
    {
        var layout = new AlderAppLayout { Sidebar = new TextBlock { Text = "Navigation" }, Aside = new TextBlock { Text = "Details" } };
        using var view = new LayoutWindow(layout, 1120);
        var sidebar = Find<Border>(layout, "PART_Sidebar");
        var aside = Find<Border>(layout, "PART_Aside");

        view.Window.RequestedThemeVariant = ThemeVariant.Light;
        view.Pump();
        Assert.Equal(Color.Parse("#F5F5F5"), ((ISolidColorBrush)layout.Background!).Color);
        Assert.Equal(Color.Parse("#14000000"), ((ISolidColorBrush)sidebar.BorderBrush!).Color);
        Assert.Equal(Color.Parse("#DDDEE0"), ((ISolidColorBrush)aside.BorderBrush!).Color);

        view.Window.RequestedThemeVariant = ThemeVariant.Dark;
        view.Pump();
        Assert.Equal(Color.Parse("#060607"), ((ISolidColorBrush)layout.Background!).Color);
        Assert.Equal(Color.Parse("#1AFFFFFF"), ((ISolidColorBrush)sidebar.BorderBrush!).Color);
        Assert.Equal(Color.Parse("#28282C"), ((ISolidColorBrush)aside.BorderBrush!).Color);

        layout.SidebarVariant = AppLayoutSidebarVariant.Inset;
        view.Pump();
        Assert.Equal(default, sidebar.BorderThickness);
        layout.SidebarVariant = AppLayoutSidebarVariant.Floating;
        view.Pump();
        Assert.Equal(new Thickness(1), sidebar.BorderThickness);
        Assert.Equal(Color.Parse("#28282C"), ((ISolidColorBrush)sidebar.BorderBrush!).Color);
        Assert.Equal(Color.Parse("#18181B"), ((ISolidColorBrush)sidebar.Background!).Color);
    }

    [AvaloniaFact]
    public void ResponsiveLayoutKeepsNavigationSelectionContentAndScrollOffset()
    {
        var item = new ListBoxItem { Content = "Members" };
        var nav = new ListBox { Items = { new ListBoxItem { Content = "Workspace" }, item }, SelectedIndex = 1 };
        var content = new TextBlock { Text = "Members", Height = 1200 };
        var layout = new AlderAppLayout
        {
            Sidebar = nav,
            Navbar = new TextBlock { Text = "Workspace settings", Height = 28 },
            Toolbar = new TextBlock { Text = "Actions", Height = 24 },
            Content = content,
            Aside = new Button { Content = "Details" },
            Footer = new TextBlock { Text = "Status", Height = 28 }
        };
        using var view = new LayoutWindow(layout, 1180);
        var scroller = Find<ScrollViewer>(layout, "PART_ContentScroll");
        var footer = layout.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Status");
        Assert.True(Find<Border>(layout, "PART_Aside").IsVisible);
        Assert.False(layout.IsCompact);

        scroller.Offset = new Vector(0, 180);
        view.Pump();
        Assert.True(item.Focus());
        double footerY = Position(footer, view.Window).Y;

        view.Resize(620);
        Assert.True(layout.IsCompact);
        Assert.False(nav.IsEffectivelyVisible);
        Assert.False(Find<Border>(layout, "PART_Aside").IsVisible);
        Assert.False(Find<ToggleButton>(layout, "PART_AsideToggle").IsVisible);
        Assert.True(Find<ToggleButton>(layout, "PART_SidebarToggle").IsFocused);
        Assert.Same(item, nav.SelectedItem);
        Assert.Same(content, layout.Content);
        Assert.Equal(180, scroller.Offset.Y);
        Assert.Equal(footerY, Position(footer, view.Window).Y);

        view.Resize(1180);
        Assert.True(item.IsFocused);
        Assert.True(nav.IsEffectivelyVisible);
        Assert.Equal(180, scroller.Offset.Y);
        Assert.True(layout.IsSidebarOpen);
        Assert.True(layout.IsAsideOpen);

        view.Resize(768);
        Assert.False(layout.IsCompact);
        Assert.True(nav.IsEffectivelyVisible);
        view.Resize(1024);
        Assert.True(Find<Border>(layout, "PART_Aside").IsVisible);
    }

    [AvaloniaFact]
    public void CompactPanelsAreMutuallyExclusiveAndDismissWithoutChangingDesktopState()
    {
        var nav = new Button { Content = "Workspace" };
        var details = new Button { Content = "Details" };
        var layout = new AlderAppLayout
        {
            Sidebar = nav, Aside = details, Content = new TextBlock { Text = "Main" },
            IsSidebarOpen = false, IsAsideOpen = false,
            CompactAside = AppLayoutCompactAside.Overlay,
            SidebarToggleGesture = new KeyGesture(Key.F8)
        };
        using var view = new LayoutWindow(layout, 600);
        var toggle = Find<ToggleButton>(layout, "PART_SidebarToggle");
        Assert.True(toggle.Focus());
        view.Press(PhysicalKey.F8);

        Assert.Equal(AppLayoutOverlay.Sidebar, layout.ActiveOverlay);
        Assert.True(nav.IsEffectivelyVisible);
        Assert.True(Find<Border>(layout, "PART_Sidebar").IsKeyboardFocusWithin);
        Assert.True(toggle.IsChecked);
        Assert.False(Find<Border>(layout, "PART_Aside").IsVisible);
        view.Press(PhysicalKey.Tab);
        Assert.True(Find<Border>(layout, "PART_Sidebar").IsKeyboardFocusWithin);

        layout.ToggleAside();
        view.Pump();
        Assert.Equal(AppLayoutOverlay.Aside, layout.ActiveOverlay);
        Assert.False(nav.IsEffectivelyVisible);
        Assert.True(details.IsEffectivelyVisible);
        Assert.True(Find<Border>(layout, "PART_Aside").IsKeyboardFocusWithin);

        view.Press(PhysicalKey.Escape);
        Assert.Equal(AppLayoutOverlay.None, layout.ActiveOverlay);
        Assert.True(toggle.IsFocused);
        Assert.False(layout.IsSidebarOpen);
        Assert.False(layout.IsAsideOpen);

        layout.ToggleAside();
        view.Pump();
        view.Window.MouseDown(new Point(12, 255), MouseButton.Left);
        view.Window.MouseUp(new Point(12, 255), MouseButton.Left);
        view.Pump();
        Assert.Equal(AppLayoutOverlay.None, layout.ActiveOverlay);

        layout.ToggleSidebar();
        view.Pump();
        view.Resize(1100);
        Assert.Equal(AppLayoutOverlay.None, layout.ActiveOverlay);
        Assert.True(layout.IsSidebarCollapsed);
        Assert.False(layout.IsSidebarOpen);
        Assert.False(layout.IsAsideOpen);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NavbarBreadcrumbsAlignWithTheSidebarToggle(bool sidebarHost)
    {
        var breadcrumb = new AlderBreadcrumbItem { Content = "Home" };
        var breadcrumbs = new AlderBreadcrumbs { Items = { breadcrumb, new AlderBreadcrumbItem { Content = "Settings" } } };
        var account = new AlderUserMenu { Title = "Account", Height = 40 };
        Grid.SetColumn(account, 1);
        var navbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { breadcrumbs, account } };
        AlderAppLayout layout = sidebarHost ? new AlderSidebarHost() : new AlderAppLayout();
        layout.Sidebar = new AlderSidebar { Content = "Navigation" };
        layout.Navbar = navbar;
        using var view = new LayoutWindow(layout, 1080);
        VerifyAlignment();
        layout.ToggleSidebar();
        view.Pump();
        VerifyAlignment();

        void VerifyAlignment()
        {
            var toggle = Find<AlderToggleButton>(layout, "PART_SidebarToggle");
            var toggleCenter = toggle.TranslatePoint(new Rect(toggle.Bounds.Size).Center, layout)!.Value;
            var breadcrumbCenter = breadcrumb.TranslatePoint(new Rect(breadcrumb.Bounds.Size).Center, layout)!.Value;
            Assert.Equal(toggleCenter.Y, breadcrumbCenter.Y, precision: 1);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerCollapseAndReopenDoNotShowKeyboardFocusOnTheToggleOrRail(bool sidebarHost)
    {
        var sidebar = new AlderSidebar { ShowRail = true, Content = "Navigation" };
        AlderAppLayout layout = sidebarHost ? new AlderSidebarHost() : new AlderAppLayout();
        layout.Sidebar = sidebar;
        layout.SidebarCollapse = AppLayoutSidebarCollapse.OffCanvas;
        AlderMotion.SetReduced(layout, true);
        using var view = new LayoutWindow(layout, 1080);
        var rail = Find<Button>(sidebar, "PART_Rail");
        var edge = Find<Border>(rail, "Edge");
        var toggle = Find<AlderToggleButton>(layout, "PART_SidebarToggle");
        var focusRing = Find<Border>(toggle, "FocusRing");

        Click(rail);
        Assert.False(layout.IsSidebarOpen);
        Assert.True(toggle.IsFocused);
        Assert.False(focusRing.IsVisible);

        Click(toggle);
        Assert.True(layout.IsSidebarOpen);
        view.Window.MouseMove(rail.TranslatePoint(new Rect(rail.Bounds.Size).Center, view.Window)!.Value);
        view.Pump();
        Assert.True(rail.IsPointerOver);
        Assert.False(edge.IsVisible);

        Assert.True(toggle.Focus(NavigationMethod.Tab));
        view.Pump();
        Assert.True(focusRing.IsVisible);
        Assert.True(focusRing.Margin.Left >= 0);

        void Click(Control control)
        {
            var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, view.Window)!.Value;
            view.Window.MouseMove(point);
            view.Window.MouseDown(point, MouseButton.Left);
            view.Window.MouseUp(point, MouseButton.Left);
            view.Pump();
        }
    }

    [AvaloniaFact]
    public void SplittersUpdatePaneWidthsAndRetainThemAcrossBreakpoints()
    {
        var layout = new AlderAppLayout
        {
            Sidebar = new TextBlock { Text = "Navigation" },
            Aside = new TextBlock { Text = "Details" },
            Content = new TextBlock { Text = "Main" },
            SidebarCollapse = AppLayoutSidebarCollapse.OffCanvas,
            IsSidebarResizable = true,
            IsAsideResizable = true
        };
        using var view = new LayoutWindow(layout, 1180);
        var sidebar = Find<Border>(layout, "PART_Sidebar");
        var aside = Find<Border>(layout, "PART_Aside");
        var sidebarHandle = Find<GridSplitter>(layout, "PART_SidebarHandle");
        var asideHandle = Find<GridSplitter>(layout, "PART_AsideHandle");

        foreach (var handle in new[] { sidebarHandle, asideHandle })
        {
            var line = Find<Border>(handle, "HandleLine");
            var brush = line.Background;
            view.Window.MouseMove(handle.TranslatePoint(new Rect(handle.Bounds.Size).Center, view.Window)!.Value);
            view.Pump();
            Assert.True(handle.IsPointerOver);
            Assert.Equal(1, line.Width);
            Assert.Same(brush, line.Background);
        }

        Assert.True(sidebarHandle.Focus());
        view.Press(PhysicalKey.ArrowRight);
        Assert.Equal(252, layout.SidebarWidth);
        Assert.Equal(layout.SidebarWidth, sidebar.Bounds.Width);

        var handleCenter = asideHandle.TranslatePoint(new Rect(asideHandle.Bounds.Size).Center, view.Window)!.Value;
        view.Window.MouseDown(handleCenter, MouseButton.Left);
        view.Window.MouseMove(handleCenter - new Vector(24, 0));
        view.Window.MouseUp(handleCenter - new Vector(24, 0), MouseButton.Left);
        view.Pump();
        Assert.Equal(344, layout.AsideWidth);
        Assert.Equal(layout.AsideWidth, aside.Bounds.Width);

        Assert.True(sidebarHandle.Focus());
        view.Resize(620);
        Assert.True(Find<ToggleButton>(layout, "PART_SidebarToggle").IsFocused);
        Assert.False(sidebarHandle.IsVisible);
        Assert.False(asideHandle.IsVisible);
        view.Resize(1180);
        Assert.Equal(252, sidebar.Bounds.Width);
        Assert.Equal(344, aside.Bounds.Width);

        layout.ToggleSidebar();
        view.Pump();
        Assert.False(sidebar.IsVisible);
        Assert.False(sidebarHandle.IsVisible);
        layout.ToggleSidebar();
        view.Pump();
        Assert.Equal(252, sidebar.Bounds.Width);
    }

    [AvaloniaFact]
    public void IconRailAndLogicalPlacementWorkInBothDirections()
    {
        var layout = new AlderAppLayout
        {
            Sidebar = new TextBlock { Text = "Navigation" },
            Aside = new TextBlock { Text = "Details" },
            SidebarPlacement = AppLayoutSidebarPlacement.End,
            IsSidebarOpen = false,
            ScrollMode = AppLayoutScrollMode.Host
        };
        using var view = new LayoutWindow(layout, 1180);
        var sidebar = Find<Border>(layout, "PART_Sidebar");
        var aside = Find<Border>(layout, "PART_Aside");
        Assert.True(layout.IsSidebarCollapsed);
        Assert.Equal(48, sidebar.Bounds.Width);
        Assert.True(Position(sidebar, view.Window).X > Position(aside, view.Window).X);
        Assert.Equal(ScrollBarVisibility.Disabled, Find<ScrollViewer>(layout, "PART_ContentScroll").VerticalScrollBarVisibility);

        layout.FlowDirection = FlowDirection.RightToLeft;
        view.Pump();
        Assert.True(Position(sidebar, view.Window).X < Position(aside, view.Window).X);

        AlderMotion.SetReduced(layout, true);
        layout.ToggleSidebar();
        view.Pump();
        Assert.False(layout.IsSidebarCollapsed);
        Assert.Equal(240, sidebar.Bounds.Width);
    }

    [AvaloniaFact]
    public void CompactSlotsReplaceDesktopContentOnlyInTheDrawers()
    {
        var sidebar = new TextBlock { Text = "Navigation" };
        var mobileSidebar = new Button { Content = "Mobile navigation" };
        var aside = new TextBlock { Text = "Details" };
        var mobileAside = new Button { Content = "Mobile details" };
        var layout = new AlderAppLayout
        {
            Sidebar = sidebar, MobileSidebar = mobileSidebar, Aside = aside, MobileAside = mobileAside,
            CompactAside = AppLayoutCompactAside.Overlay
        };
        using var view = new LayoutWindow(layout, 1024);
        var toggle = Find<AlderToggleButton>(layout, "PART_SidebarToggle");
        var desktopIcon = toggle.Icon;
        Assert.True(sidebar.IsEffectivelyVisible);
        Assert.True(aside.IsEffectivelyVisible);
        Assert.False(IsDisplayed(mobileSidebar));
        Assert.False(IsDisplayed(mobileAside));
        Assert.Equal(64, Find<Border>(layout, "PART_Navbar").Bounds.Height);

        view.Resize(767);
        Assert.NotSame(desktopIcon, toggle.Icon);
        layout.ToggleSidebar();
        view.Pump();
        Assert.True(IsDisplayed(mobileSidebar));
        Assert.Equal(500, Find<Border>(layout, "PART_Sidebar").Bounds.Width);
        Assert.False(sidebar.IsEffectivelyVisible);
        view.Resize(390);
        Assert.Equal(312, Find<Border>(layout, "PART_Sidebar").Bounds.Width);

        layout.ToggleAside();
        view.Pump();
        Assert.True(mobileAside.IsEffectivelyVisible);
        Assert.False(aside.IsEffectivelyVisible);
        Assert.False(mobileSidebar.IsEffectivelyVisible);

        view.Resize(1024);
        Assert.Equal(AppLayoutOverlay.None, layout.ActiveOverlay);
        Assert.True(aside.IsEffectivelyVisible);
        Assert.True(sidebar.IsEffectivelyVisible);
        Assert.Same(desktopIcon, toggle.Icon);
    }

    [AvaloniaFact]
    public void AsideTransitionCanReverseAndReducedMotionClosesImmediately()
    {
        var layout = new AlderAppLayout { Aside = new Button { Content = "Details" } };
        using var view = new LayoutWindow(layout, 1120);
        var aside = Find<Border>(layout, "PART_Aside");
        Assert.Equal(320, aside.Bounds.Width);

        layout.ToggleAside();
        Assert.False(layout.IsAsideOpen);
        Assert.True(aside.IsVisible);
        Assert.False(aside.IsHitTestVisible);
        layout.ToggleAside();
        view.Pump();
        Assert.True(layout.IsAsideOpen);
        Assert.True(aside.IsHitTestVisible);

        layout.ToggleAside();
        using (var animation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
            Dispatcher.UIThread.MainLoop(animation.Token);
        view.Pump();
        Assert.False(aside.IsVisible);

        AlderMotion.SetReduced(layout, true);
        layout.ToggleAside();
        view.Pump();
        Assert.True(aside.IsVisible);
        layout.ToggleAside();
        view.Pump();
        Assert.False(aside.IsVisible);
    }

    [AvaloniaFact]
    public void OffCanvasTransitionCanBeInterruptedAndFinishesHidden()
    {
        var layout = new AlderAppLayout
        {
            Sidebar = new Button { Content = "Navigation" },
            SidebarCollapse = AppLayoutSidebarCollapse.OffCanvas
        };
        using var view = new LayoutWindow(layout, 940);
        var sidebar = Find<Border>(layout, "PART_Sidebar");
        layout.ToggleSidebar();
        Assert.False(layout.IsSidebarOpen);
        Assert.True(sidebar.IsVisible);
        Assert.False(sidebar.IsHitTestVisible);

        layout.ToggleSidebar();
        SettleMotion(view);
        Assert.Equal(240, sidebar.Bounds.Width);
        layout.ToggleSidebar();
        SettleMotion(view);
        Assert.False(sidebar.IsVisible);
    }

    [AvaloniaFact]
    public void ClosingDrawerImmediatelyReleasesInputAndRetainsSurfaceForExit()
    {
        var layout = new AlderAppLayout { Sidebar = new Button { Content = "Navigation" } };
        using var view = new LayoutWindow(layout, 390);
        var sidebar = Find<Border>(layout, "PART_Sidebar");
        var scrim = Find<Border>(layout, "PART_Scrim");
        var toggle = Find<ToggleButton>(layout, "PART_SidebarToggle");
        Assert.True(toggle.Focus());
        layout.ToggleSidebar();
        view.Pump();
        layout.CloseOverlay();
        view.Pump();
        Assert.Equal(AppLayoutOverlay.None, layout.ActiveOverlay);
        Assert.True(sidebar.IsVisible);
        Assert.False(sidebar.IsHitTestVisible);
        Assert.False(scrim.IsHitTestVisible);
        Assert.True(toggle.IsFocused);

        layout.ToggleSidebar();
        Assert.Equal(AppLayoutOverlay.Sidebar, layout.ActiveOverlay);
        SettleMotion(view);
        Assert.True(sidebar.IsVisible);
        Assert.True(sidebar.IsHitTestVisible);
        layout.CloseOverlay();
        SettleMotion(view);
        Assert.False(sidebar.IsVisible);
        Assert.False(scrim.IsVisible);
    }

    private static void SettleMotion(LayoutWindow view)
    {
        using var animation = new CancellationTokenSource(TimeSpan.FromMilliseconds(270));
        Dispatcher.UIThread.MainLoop(animation.Token);
        view.Pump();
    }

    private static T Find<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(element => element.Name == name);

    private static bool IsDisplayed(Control control) => control.IsAttachedToVisualTree() && control.IsEffectivelyVisible;

    private static Point Position(Control control, Window window) => control.TranslatePoint(new Point(), window)!.Value;

    private sealed class LayoutWindow : IDisposable
    {
        public LayoutWindow(AlderAppLayout content, double width)
        {
            Window = new Window { Width = width, Height = 520 };
            Window.Content = content;
            Window.Show();
            Pump();
        }

        public Window Window { get; }

        public void Resize(double width)
        {
            Window.Width = width;
            Pump();
        }

        public void Press(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }
}
