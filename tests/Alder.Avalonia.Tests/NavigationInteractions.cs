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

public class NavigationInteractions
{
    [AvaloniaFact]
    public void SegmentTracksKeysAndSkipsDisabledOptionsFromTheKeyboard()
    {
        var first = new AlderSegmentItem { Key = "a", Content = "Alpha" };
        var disabled = new AlderSegmentItem { Key = "b", Content = "Beta", IsEnabled = false };
        var last = new AlderSegmentItem { Key = "c", Content = "Gamma" };
        var segment = new AlderSegment { SelectedKey = "a", Items = { first, disabled, last } };
        using var view = new NavigationWindow(segment);
        Assert.Same(first, segment.SelectedItem);
        Assert.True(first.Focus());

        view.Press(PhysicalKey.ArrowRight);
        Assert.Same(last, segment.SelectedItem);
        Assert.Equal("c", segment.SelectedKey);
        segment.SelectedKey = "a";
        view.Pump();
        Assert.True(first.IsSelected);
        Assert.False(last.IsSelected);

        segment.SelectedKey = null;
        Assert.Null(segment.SelectedItem);
    }

    [AvaloniaFact]
    public void SegmentRetainsItsSelectionKeyAcrossSourceReplacementAndDelayedItems()
    {
        var segment = new AlderSegment
        {
            ItemsSource = new[] { "Alpha", "Beta", "Gamma" },
            SelectedKey = "Beta"
        };
        using var view = new NavigationWindow(segment);
        segment.ItemsSource = new[] { "Delta", "Gamma", "Beta" };
        view.Pump();
        Assert.Equal("Beta", segment.SelectedKey);
        Assert.Equal(2, segment.SelectedIndex);

        segment.SelectedKey = "Later";
        view.Pump();
        Assert.Null(segment.SelectedItem);
        Assert.False(Find<Border>(segment, "Indicator").IsVisible);
        segment.ItemsSource = new[] { "Delta", "Later" };
        view.Pump();
        Assert.Equal("Later", segment.SelectedItem);
        Assert.True(Find<Border>(segment, "Indicator").IsVisible);
    }

    [AvaloniaFact]
    public void SegmentUsesDarkIndicatorAndKeepsTextOnlyOptionsVisibleWhenExpandingIcons()
    {
        var first = new AlderSegmentItem { Key = "a", Content = "Icon", Icon = Geometry.Parse("M0,0 H8 V8 H0 Z") };
        var second = new AlderSegmentItem { Key = "b", Content = "Text only" };
        var segment = new AlderSegment { SelectedKey = "a", ExpandSelectedItem = true, Items = { first, second } };
        AlderMotion.SetReduced(segment, true);
        using var view = new NavigationWindow(segment);
        view.Window.RequestedThemeVariant = ThemeVariant.Dark;
        view.Pump();
        Assert.True(Find<ContentPresenter>(second, "PART_ContentPresenter").IsVisible);
        var indicator = Find<Border>(segment, "Indicator");
        Assert.Equal(Color.Parse("#46464C"), ((ISolidColorBrush)indicator.Background!).Color);
        Assert.Equal(16, Find<Border>(second, "Separator").Bounds.Height);
        Assert.True(first.Focus(NavigationMethod.Tab));
        view.Pump();
        var focusRing = Find<Border>(first, "FocusRing");
        Assert.Equal(1, focusRing.Opacity);
        Assert.Equal(new Thickness(-4), focusRing.Margin);

        segment.SelectedKey = "b";
        view.Pump();
        Assert.False(Find<ContentPresenter>(first, "PART_ContentPresenter").IsVisible);
    }

    [AvaloniaFact]
    public void SegmentCentersAnIconWithinEachSelectedSlot()
    {
        var icon = Geometry.Parse("M0,0 H16 V16 H0 Z");
        var segment = new AlderSegment
        {
            Size = AlderSegmentSize.Small,
            SelectedIndex = 0,
            Items =
            {
                new AlderSegmentItem { Icon = icon },
                new AlderSegmentItem { Icon = icon },
                new AlderSegmentItem { Icon = icon }
            }
        };
        AlderMotion.SetReduced(segment, true);
        using var view = new NavigationWindow(segment);
        var indicator = Find<Border>(segment, "Indicator");
        Assert.Equal(new Size(112, 32), segment.Bounds.Size);

        for (var index = 0; index < segment.ItemCount; index++)
        {
            segment.SelectedIndex = index;
            view.Pump();
            var item = Assert.IsType<AlderSegmentItem>(segment.SelectedItem);
            var glyph = Find<PathIcon>(item, "Icon");
            Assert.Equal(new Size(36, 28), indicator.Bounds.Size);
            Assert.Equal(new Point(2 + 36 * index, 2), indicator.TranslatePoint(default, segment));
            Assert.Equal(new Size(16, 16), glyph.Bounds.Size);
            Assert.Equal(new Point(20 + 36 * index, 16), glyph.TranslatePoint(new Rect(glyph.Bounds.Size).Center, segment));
        }
    }

    [AvaloniaFact]
    public void SegmentIndicatorAndSeparatorsFollowSelectedBoundsInRtl()
    {
        var segment = new AlderSegment
        {
            FlowDirection = FlowDirection.RightToLeft, Size = AlderSegmentSize.Small,
            ShowSeparators = true, SelectedKey = "b",
            Items =
            {
                new AlderSegmentItem { Key = "a", Content = "Alpha" },
                new AlderSegmentItem { Key = "b", Content = "Beta with a longer label" },
                new AlderSegmentItem { Key = "c", Content = "Gamma" },
                new AlderSegmentItem { Key = "d", Content = "Delta" }
            }
        };
        AlderMotion.SetReduced(segment, true);
        using var view = new NavigationWindow(segment);
        var indicator = Find<Border>(segment, "Indicator");
        var selected = Assert.IsType<AlderSegmentItem>(segment.SelectedItem);
        Assert.Equal(selected.Bounds.Size, indicator.Bounds.Size);
        Assert.Equal(selected.TranslatePoint(default, view.Window), indicator.TranslatePoint(default, view.Window));
        Assert.Equal(28, selected.Bounds.Height);
        Assert.Equal(0, Find<Border>(selected, "Separator").Opacity);
        Assert.Equal(0, Find<Border>((AlderSegmentItem)segment.Items[2]!, "Separator").Opacity);
        Assert.Equal(1, Find<Border>((AlderSegmentItem)segment.Items[3]!, "Separator").Opacity);
        ((AlderSegmentItem)segment.Items[3]!).ShowSeparator = false;
        view.Pump();
        Assert.Equal(0, Find<Border>((AlderSegmentItem)segment.Items[3]!, "Separator").Opacity);

        segment.SelectedKey = "d";
        view.Pump();
        selected = Assert.IsType<AlderSegmentItem>(segment.SelectedItem);
        Assert.Equal(selected.TranslatePoint(default, view.Window), indicator.TranslatePoint(default, view.Window));
    }

    [AvaloniaFact]
    public void SidebarHostPreservesMenuStateAndClosesMobileOnLeafInvocation()
    {
        var child = new AlderSidebarItem { Header = "Child" };
        var branch = new AlderSidebarItem
        {
            Header = "Branch", Icon = Geometry.Parse("M0,0 L8,0 0,8 Z"), Items = { child }, IsExpanded = true
        };
        var menu = new AlderSidebarMenu { Items = { branch } };
        var sidebar = new AlderSidebar { Header = "Header", Footer = "Footer", Content = menu };
        var host = new AlderSidebarHost { Sidebar = sidebar, Content = "Page" };
        AlderMotion.SetReduced(host, true);
        using var view = new NavigationWindow(host, 1000);

        Assert.Equal(240, Find<Border>(host, "PART_Sidebar").Bounds.Width);
        Assert.Equal(ScrollBarVisibility.Disabled, Find<ScrollViewer>(host, "PART_SidebarScroll").VerticalScrollBarVisibility);
        var footer = Find<ContentPresenter>(sidebar, "PART_Footer");
        Assert.Equal(host.Bounds.Bottom, footer.TranslatePoint(new Point(0, footer.Bounds.Height), host)!.Value.Y);
        host.ToggleSidebar();
        view.Pump();
        Assert.True(sidebar.IsRail);
        Assert.Equal(48, Find<Border>(host, "PART_Sidebar").Bounds.Width);
        Assert.False(Find<ContentPresenter>(branch, "Label").IsVisible);
        Assert.True(branch.IsExpanded);

        view.Window.Width = 390;
        view.Pump();
        host.ToggleSidebar();
        view.Pump();
        Assert.Equal(AppLayoutOverlay.Sidebar, host.ActiveOverlay);
        Assert.False(sidebar.IsRail);
        Assert.True(child.Invoke());
        view.Pump();
        Assert.Equal(AppLayoutOverlay.None, host.ActiveOverlay);
        Assert.Same(child, menu.SelectedItem);
        Assert.True(branch.IsExpanded);
    }

    [AvaloniaTheory]
    [InlineData(FlowDirection.LeftToRight)]
    [InlineData(FlowDirection.RightToLeft)]
    public void SidebarHeaderKeepsItsIconCenteredAndRestoresTheTitleAfterExpanding(FlowDirection direction)
    {
        var geometry = Geometry.Parse("M0,0 H8 V8 H0 Z");
        var sidebar = new AlderSidebar { Header = "Workspace", HeaderIcon = geometry };
        var host = new AlderSidebarHost { Sidebar = sidebar, Content = "Page", FlowDirection = direction };
        AlderMotion.SetReduced(host, true);
        using var view = new NavigationWindow(host, 1000);

        var icon = Find<ContentPresenter>(sidebar, "PART_HeaderIcon");
        var label = Find<ContentPresenter>(sidebar, "PART_HeaderPresenter");
        var glyph = Assert.IsType<AlderPathIcon>(icon.Child);
        Assert.Same(geometry, glyph.Data);
        Assert.True(glyph.IsEffectivelyVisible);
        Assert.True(label.IsEffectivelyVisible);
        Assert.Equal("Workspace", Assert.IsType<TextBlock>(label.Child).Text);

        host.ToggleSidebar();
        view.Pump();
        Assert.True(sidebar.IsRail);
        Assert.True(glyph.IsEffectivelyVisible);
        Assert.False(label.IsEffectivelyVisible);
        var center = glyph.TranslatePoint(new Rect(glyph.Bounds.Size).Center, sidebar)!.Value;
        Assert.InRange(center.X - sidebar.Bounds.Width / 2, -.5, .5);

        sidebar.Header = "Updated workspace";
        host.ToggleSidebar();
        view.Pump();
        Assert.False(sidebar.IsRail);
        Assert.True(label.IsEffectivelyVisible);
        Assert.Equal("Updated workspace", Assert.IsType<TextBlock>(label.Child).Text);
        Assert.True(glyph.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void SidebarSubmenuSupportsKeyboardAndInheritedMobilePolicy()
    {
        var child = new AlderSidebarItem { Header = "Child" };
        var branch = new AlderSidebarItem { Header = "Branch", Items = { child } };
        var menu = new AlderSidebarMenu { Items = { branch } };
        AlderSidebar.SetCloseMobileOnAction(menu, false);
        var host = new AlderSidebarHost { Sidebar = new AlderSidebar { Content = menu }, Content = "Main" };
        AlderMotion.SetReduced(host, true);
        using var view = new NavigationWindow(host, 390);
        host.ToggleSidebar();
        view.Pump();
        Assert.True(branch.Focus());
        view.Press(PhysicalKey.Enter);
        Assert.True(branch.IsExpanded);
        Assert.Equal(1, Find<AlderAccordionPanel>(branch, "Submenu").Progress);
        Assert.True(child.Invoke());
        Assert.Equal(AppLayoutOverlay.Sidebar, host.ActiveOverlay);
    }

    [AvaloniaFact]
    public void SidebarSelectsOneItemAcrossRootsChildrenAndGroups()
    {
        var child = new AlderSidebarItem { Header = "Child" };
        var branch = new AlderSidebarItem { Header = "Branch", IsExpanded = true, Items = { child } };
        var dashboard = new AlderSidebarItem { Header = "Dashboard", IsCurrent = true };
        var settings = new AlderSidebarItem { Header = "Settings" };
        var main = new AlderSidebarMenu { Items = { dashboard, branch } };
        var tools = new AlderSidebarMenu { Items = { settings } };
        var sidebar = new AlderSidebar
        {
            Content = new StackPanel { Children = { main, new AlderSidebarGroup { Content = tools } } }
        };
        using var view = new NavigationWindow(sidebar);

        Assert.Same(dashboard, sidebar.SelectedItem);
        Assert.True(child.Invoke());
        Assert.Same(child, sidebar.SelectedItem);
        Assert.False(dashboard.IsSelected);
        Assert.Single(main.SelectedItems);
        Assert.True(settings.Invoke());
        Assert.Same(settings, sidebar.SelectedItem);
        Assert.False(child.IsSelected);
        Assert.Empty(main.SelectedItems);
        Assert.Single(tools.SelectedItems);
        sidebar.SelectedItem = child;
        Assert.True(child.IsSelected);
        Assert.False(settings.IsSelected);
        Assert.Empty(tools.SelectedItems);
    }

    [AvaloniaFact]
    public void SidebarResolvesDataBoundChildrenAndUpdatesTheSelectedModel()
    {
        var detail = new SidebarNode("Detail");
        var root = new SidebarNode("Root", detail);
        var menu = new AlderSidebarMenu
        {
            ItemsSource = new[] { root },
            ItemTemplate = new FuncTreeDataTemplate<SidebarNode>(
                (item, _) => new TextBlock { Text = item.Title }, item => item.Children)
        };
        var sidebar = new AlderSidebar { Content = menu, SelectedItem = detail };
        using var view = new NavigationWindow(sidebar);
        var branch = Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(0));
        branch.IsExpanded = true;
        view.Pump();
        var child = Assert.IsType<AlderSidebarItem>(menu.TreeContainerFromItem(detail));
        Assert.Same(detail, menu.SelectedItem);
        Assert.True(child.IsSelected);
        sidebar.SelectedItem = null;
        Assert.False(child.IsSelected);
        Assert.True(child.Invoke());
        Assert.Same(detail, sidebar.SelectedItem);
        Assert.Single(menu.SelectedItems);
    }

    private sealed class SidebarNode(string title, params SidebarNode[] children)
    {
        public string Title { get; } = title;
        public SidebarNode[] Children { get; } = children;
    }

    [AvaloniaFact]
    public void SidebarVirtualizesExpandedBranchesAndRestoresTheirSelection()
    {
        var children = Enumerable.Range(0, 10_000).Select(index => new SidebarNode($"Child {index}")).ToArray();
        var roots = Enumerable.Range(0, 10_000).Select(index => new SidebarNode($"Root {index}"))
            .Prepend(new SidebarNode("Branch", children)).ToArray();
        var menu = new AlderSidebarMenu
        {
            ItemsSource = roots,
            ItemTemplate = new FuncTreeDataTemplate<SidebarNode>(
                (item, _) => new TextBlock { Text = item.Title }, item => item.Children)
        };
        var scroll = new ScrollViewer { Content = menu, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AlderMotion.SetReduced(scroll, true);
        using var view = new NavigationWindow(scroll);

        var branch = Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(0));
        Assert.Empty(branch.GetRealizedContainers());
        Assert.True(branch.Focus());
        view.Press(PhysicalKey.ArrowRight);
        Assert.True(branch.IsExpanded);
        Assert.InRange(branch.GetRealizedContainers().Count(), 1, 32);
        Assert.InRange(scroll.Extent.Height, 740_000, 800_000);
        branch.ScrollIntoView(7500);
        view.Pump();
        var child = Assert.IsType<AlderSidebarItem>(branch.ContainerFromIndex(7500));
        Assert.True(child.Invoke());
        Assert.Same(children[7500], menu.SelectedItem);

        menu.ScrollIntoView(9000);
        Assert.True(Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(9000)).Focus());
        menu.ScrollIntoView(9010);
        view.Pump();
        Assert.Null(menu.ContainerFromIndex(0));
        Assert.Same(children[7500], menu.SelectedItem);

        menu.ScrollIntoView(0);
        view.Pump();
        branch = Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(0));
        Assert.True(branch.IsExpanded);
        branch.ScrollIntoView(7500);
        view.Pump();
        child = Assert.IsType<AlderSidebarItem>(branch.ContainerFromIndex(7500));
        Assert.True(child.IsSelected);
        Assert.True(child.Focus(NavigationMethod.Tab));
        for (int i = 0; i < 30; i++) view.Press(PhysicalKey.ArrowDown);
        Assert.Same(children[7530], menu.SelectedItem);
        view.Press(PhysicalKey.ArrowUp);
        Assert.Same(children[7529], menu.SelectedItem);
        Assert.InRange(branch.GetRealizedContainers().Count(), 1, 32);
    }

    [AvaloniaFact]
    public void SidebarTabsBackToAnOffscreenSelectedItem()
    {
        var nodes = Enumerable.Range(0, 1000).Select(index => new SidebarNode($"Item {index}")).ToArray();
        var menu = new AlderSidebarMenu
        {
            ItemsSource = nodes,
            ItemTemplate = new FuncDataTemplate<SidebarNode>((item, _) => new TextBlock { Text = item.Title })
        };
        var before = new Button { Content = "Before menu" };
        DockPanel.SetDock(before, Dock.Top);
        var scroll = new ScrollViewer { Content = menu, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        using var view = new NavigationWindow(new DockPanel { Children = { before, scroll } });

        Assert.True(before.Focus());
        menu.SelectedItem = nodes[700];
        view.Pump();
        Assert.True(Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(700)).IsSelected);
        menu.ScrollIntoView(0);
        view.Pump();
        Assert.Null(menu.ContainerFromIndex(700));
        view.Press(PhysicalKey.Tab);

        var chosen = Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(700));
        Assert.True(chosen.IsSelected);
        Assert.True(chosen.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void SidebarReusesVisibleChildrenWhenARealizedBranchReopens()
    {
        var children = Enumerable.Range(0, 10_000).Select(index => new SidebarNode($"Child {index}")).ToArray();
        int headersBuilt = 0;
        int childSourcesRead = 0;
        var menu = new AlderSidebarMenu
        {
            ItemsSource = new[] { new SidebarNode("Branch", children) },
            ItemTemplate = new FuncTreeDataTemplate<SidebarNode>((item, _) =>
            {
                headersBuilt++;
                return new TextBlock { Text = item.Title };
            }, item =>
            {
                childSourcesRead++;
                return item.Children;
            })
        };
        var scroll = new ScrollViewer { Content = menu, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AlderMotion.SetReduced(scroll, true);
        using var view = new NavigationWindow(scroll);
        var branch = Assert.IsType<AlderSidebarItem>(menu.ContainerFromIndex(0));
        Assert.Empty(branch.GetRealizedContainers());
        branch.IsExpanded = true;
        view.Pump();
        var first = branch.ContainerFromIndex(0);
        Assert.NotNull(first);
        int initialHeaders = headersBuilt;
        int initialChildSources = childSourcesRead;
        Assert.InRange(initialHeaders, 2, 33);

        for (int i = 0; i < 8; i++)
        {
            branch.IsExpanded = false;
            view.Pump();
            branch.IsExpanded = true;
            view.Pump();
            Assert.Same(first, branch.ContainerFromIndex(0));
        }
        Assert.Equal(initialHeaders, headersBuilt);
        Assert.Equal(initialChildSources, childSourcesRead);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SidebarSearchFindsCollapsedChildrenAndUsesTheNormalSelectionPath(bool useDialogHost)
    {
        var child = new AlderSidebarItem { Header = "Details", SearchKeywords = "quarterly-report" };
        int invoked = 0;
        child.Invoked += (_, _) => invoked++;
        var branch = new AlderSidebarItem { Header = "Reports", Items = { child } };
        var home = new AlderSidebarItem { Header = "Home", IsCurrent = true };
        var menu = new AlderSidebarMenu { Items = { home, branch } };
        var sidebar = new AlderSidebar
        {
            Content = new AlderSidebarGroup { Header = "Workspace", Content = menu },
            SearchGesture = new KeyGesture(Key.K, KeyModifiers.Control)
        };
        Control root = useDialogHost ? new DialogHost { Content = sidebar } : sidebar;
        AlderMotion.SetReduced(root, true);
        using var view = new NavigationWindow(root);
        var trigger = Find<AlderButton>(sidebar, "PART_Search");
        Assert.False(trigger.IsEffectivelyVisible);
        sidebar.IsSearchEnabled = true;
        sidebar.IsRail = useDialogHost;
        view.Pump();
        Assert.True(trigger.IsEffectivelyVisible);
        Assert.Equal(useDialogHost, trigger.IsIconOnly);
        Assert.False(branch.IsExpanded);
        Assert.Same(home, sidebar.SelectedItem);

        if (useDialogHost)
        {
            Assert.True(home.Focus());
            view.Window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
            view.Window.KeyReleaseQwerty(PhysicalKey.K, RawInputModifiers.Control);
        }
        else
        {
            var point = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, view.Window)!.Value;
            view.Window.MouseDown(point, MouseButton.Left);
            view.Window.MouseUp(point, MouseButton.Left);
        }
        view.Pump();
        var search = Assert.IsAssignableFrom<AlderSearchField>(view.Window.FocusManager!.GetFocusedElement());
        var palette = search.FindAncestorOfType<AlderCommand>()!;
        view.Window.KeyTextInput("quarterly-report");
        view.Pump();
        Assert.Equal("Details", palette.ActiveItem?.Label);
        view.Press(PhysicalKey.Enter);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        view.Pump();
        Assert.Same(child, sidebar.SelectedItem);
        Assert.Same(child, Assert.Single(menu.SelectedItems));
        Assert.True(branch.IsExpanded);
        Assert.Equal(1, invoked);
        Assert.False(palette.IsAttachedToVisualTree());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SidebarRailIsClickableWithoutDrawingAnExtraLineOnHover(bool hosted)
    {
        var sidebar = new AlderSidebar { ShowRail = true, Content = "Navigation" };
        Control root = hosted ? new AlderAppLayout { Sidebar = sidebar, Content = "Page" } : sidebar;
        AlderMotion.SetReduced(root, true);
        using var view = new NavigationWindow(root, 1000);
        var rail = Find<Button>(sidebar, "PART_Rail");
        var indicator = Find<Border>(rail, "Edge");
        Assert.False(indicator.IsVisible);

        var center = HoverRail();
        view.Window.MouseDown(center, MouseButton.Left);
        view.Window.MouseUp(center, MouseButton.Left);
        view.Pump();
        Assert.True(sidebar.IsRail);
        HoverRail();

        Point HoverRail()
        {
            var point = rail.TranslatePoint(new Rect(rail.Bounds.Size).Center, view.Window)!.Value;
            view.Window.MouseMove(point);
            view.Pump();
            Assert.True(rail.IsPointerOver);
            Assert.False(indicator.IsVisible);
            return point;
        }
    }

    [AvaloniaFact]
    public void SidebarRailCentersTheIconWithinTheSelectedRowAndPreservesKeyboardFocus()
    {
        var child = new AlderSidebarItem { Header = "Child" };
        var branch = new AlderSidebarItem
        {
            Header = "Directory", Icon = Geometry.Parse("M0,0 H8 V8 H0 Z"), IsExpanded = true,
            IsCurrent = true, Badge = 4, Actions = new Button { Content = "Action" }, Items = { child }
        };
        var sidebar = new AlderSidebar { Content = new AlderSidebarMenu { Items = { branch } } };
        var host = new AlderSidebarHost { Sidebar = sidebar, Content = "Page" };
        AlderMotion.SetReduced(host, true);
        using var view = new NavigationWindow(host, 1000);
        Assert.True(child.Focus(NavigationMethod.Tab));
        host.ToggleSidebar();
        view.Pump();

        Assert.True(branch.IsFocused);
        Assert.True(branch.IsExpanded);
        Assert.False(branch.Invoke());
        view.Press(PhysicalKey.ArrowLeft);
        Assert.True(branch.IsExpanded);
        var surface = Find<Border>(branch, "RowSurface");
        var icon = Find<PathIcon>(branch, "RowIcon");
        Assert.Equal(new Size(36, 36), surface.Bounds.Size);
        Assert.Equal(new CornerRadius(16), surface.CornerRadius);
        Assert.Equal(new Rect(surface.Bounds.Size).Center, icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, surface));
        var focusRing = Find<Border>(branch, "FocusRing");
        Assert.True(focusRing.IsVisible);
        Assert.Equal(new Thickness(-4), focusRing.Margin);

        host.FlowDirection = FlowDirection.RightToLeft;
        host.SidebarPlacement = AppLayoutSidebarPlacement.End;
        view.Pump();
        Assert.Equal(new Rect(surface.Bounds.Size).Center, icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, surface));

        host.ToggleSidebar();
        view.Pump();
        Assert.True(child.Invoke());
        Assert.True(child.Focus(NavigationMethod.Tab));
        host.ToggleSidebar();
        view.Pump();
        Assert.True(branch.IsFocused);
        Assert.Same(child, sidebar.SelectedItem);
        Assert.False(branch.IsSelected);
    }

    private static T Find<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private sealed class NavigationWindow : IDisposable
    {
        public NavigationWindow(Control child, double width = 680)
        {
            Window = new Window { Width = width, Height = 420, Content = child };
            Window.Show();
            Pump();
        }

        public Window Window { get; }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Press(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Dispose() => Window.Close();
    }
}
