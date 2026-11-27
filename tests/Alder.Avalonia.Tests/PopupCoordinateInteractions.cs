using Alder.Avalonia.Controls;
using Alder.Avalonia.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

// Platform configurations use separate headless dispatchers from the default test session.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HeadlessSessionCollection
{
    public const string Name = "Separate headless sessions";
}

[Collection(HeadlessSessionCollection.Name)]
public sealed class PopupCoordinateInteractions
{
    [Theory]
    [InlineData(typeof(PhysicalDesktopTestApp), 1.25, true)]
    [InlineData(typeof(LogicalDesktopTestApp), 2, true)]
    [InlineData(typeof(CompositorPositionedTestApp), 2, false)]
    public async Task PopoverMenuAndTooltipKeepTheirAnchor(Type appType, double scale, bool canPositionWindow)
    {
        await using var session = HeadlessUnitTestSession.StartNew(appType);
        await session.Dispatch(() =>
        {
            var popover = new AlderPopover
            {
                Placement = PlacementMode.Bottom, Offset = 8, Content = new Border { Width = 116, Height = 70 }
            };
            popover.Popup.ShouldUseOverlayLayer = false;
            var trigger = new AlderButton { Content = "Popover", Width = 100, Flyout = popover };
            var menu = new AlderUserMenu { Title = "Account", Width = 120, Items = { "Profile", "Settings" } };
            var tooltip = new AlderButton { Content = "Tooltip", Width = 100 };
            AlderToolTip.SetTip(tooltip, "Help");
            AlderToolTip.SetShowDelay(tooltip, 0);
            AlderToolTip.SetOffset(tooltip, 8);
            var content = new StackPanel
            {
                Spacing = 42, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Children = { trigger, menu, tooltip }
            };
            var window = new AlderTestWindow { Width = 440, Height = 390, Content = content };
            AlderMotion.SetReduced(window, true);
            window.Show();
            try
            {
                ConfigureWindow(window, scale, canPositionWindow);
                Refresh(window);

                Click(window, trigger);
                Refresh(window);
                Assert.True(popover.IsOpen);
                var popoverSurface = Assert.IsAssignableFrom<Control>(popover.Popup.Child);
                Assert.NotSame(window, TopLevel.GetTopLevel(popoverSurface));
                CheckGap(trigger, popoverSurface, 8, PlacementMode.Bottom);
                popover.Hide();
                Refresh(window);

                var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
                flyout.Popup.ShouldUseOverlayLayer = false;
                Click(window, menu);
                Refresh(window);
                Assert.True(flyout.IsOpen);
                var menuSurface = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child).GetVisualDescendants()
                    .OfType<Border>().Single(border => border.Name == "Surface");
                Assert.NotSame(window, TopLevel.GetTopLevel(menuSurface));
                CheckGap(menu, menuSurface, menu.MenuGap, PlacementMode.BottomEdgeAlignedRight);
                flyout.Hide();
                Refresh(window);

                window.MouseMove(Center(tooltip, window));
                Refresh(window);
                var tip = window.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "AlderToolTipSurface");
                CheckGap(tooltip, tip, 8, PlacementMode.Top);
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Refresh(window);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "AlderToolTipSurface");

                void CheckGap(Control anchor, Control surface, double gap, PlacementMode placement)
                {
                    var start = anchor.TranslatePoint(default, window)!.Value;
                    var topLeft = window.PointToClient(surface.PointToScreen(default));
                    double deltaY = placement == PlacementMode.Top
                        ? start.Y - topLeft.Y - surface.Bounds.Height
                        : topLeft.Y - start.Y - anchor.Bounds.Height;
                    double deltaX = placement == PlacementMode.BottomEdgeAlignedRight
                        ? topLeft.X + surface.Bounds.Width - start.X - anchor.Bounds.Width
                        : topLeft.X + surface.Bounds.Width / 2 - start.X - anchor.Bounds.Width / 2;
                    Assert.InRange(deltaY, gap - 1, gap + 1);
                    Assert.InRange(deltaX, -1, 1);
                }
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(typeof(PhysicalDesktopTestApp), 1.25, true)]
    [InlineData(typeof(LogicalDesktopTestApp), 2, true)]
    [InlineData(typeof(CompositorPositionedTestApp), 2, false)]
    public async Task DragPreviewFollowsThePointer(Type appType, double scale, bool canPositionWindow)
    {
        await using var session = HeadlessUnitTestSession.StartNew(appType);
        await session.Dispatch(async () =>
        {
            await DataGridInteractions.VerifyDraggedRowPreview(window => ConfigureWindow(window, scale, canPositionWindow));
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(typeof(LogicalDesktopTestApp), true)]
    [InlineData(typeof(CompositorPositionedTestApp), false)]
    public async Task NativeFlyoutAnchorsAtTwoTimesRenderScaling(Type appType, bool canPositionWindow)
    {
        await using var session = HeadlessUnitTestSession.StartNew(appType);
        await session.Dispatch(() =>
        {
            var flyout = new AlderMenuFlyout { Placement = PlacementMode.Bottom };
            flyout.Items.Add(new AlderMenuItem { Header = "Edit" });
            flyout.Popup.ShouldUseOverlayLayer = false;
            var trigger = new AlderButton
            {
                Content = "Options", Width = 120, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var window = new AlderTestWindow { Width = 380, Height = 360, Content = trigger };
            AlderMotion.SetReduced(window, true);
            window.Show();
            try
            {
                window.SetRenderScaling(2);
                var position = new PixelPoint(52, 56);
                window.Position = position;
                Assert.Equal(canPositionWindow ? position : default, window.Position);

                flyout.ShowAt(trigger);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var menu = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
                Assert.NotSame(window, TopLevel.GetTopLevel(menu));
                Assert.True(menu.IsAttachedToVisualTree() && menu.Bounds.Width > 0);

                var triggerStart = trigger.TranslatePoint(default, window)!.Value;
                var popupStart = window.PointToClient(menu.PointToScreen(default));
                Assert.InRange(popupStart.Y - (triggerStart.Y + trigger.Bounds.Height), 0, 16);
                Assert.InRange(popupStart.X + menu.Bounds.Width / 2 - (triggerStart.X + trigger.Bounds.Width / 2), -1, 1);
                flyout.Hide();
                Assert.False(flyout.IsOpen);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    private static void ConfigureWindow(Window window, double scale, bool canPositionWindow)
    {
        window.SetRenderScaling(scale);
        var position = new PixelPoint(52, 56);
        window.Position = position;
        Assert.Equal(canPositionWindow ? position : default, window.Position);
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Click(Window window, Control control)
    {
        var point = Center(control, window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Refresh(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}

public class PhysicalDesktopTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<PhysicalDesktopTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { OverlayPopups = false });

    public override void Initialize()
    {
        Styles.Add(new AlderTheme());
    }
}

public sealed class LogicalDesktopTestApp : PhysicalDesktopTestApp
{
    public new static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LogicalDesktopTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseLogicalDesktopCoordinates = true, OverlayPopups = false });
}

public sealed class CompositorPositionedTestApp : PhysicalDesktopTestApp
{
    public new static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CompositorPositionedTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseLogicalDesktopCoordinates = true, OverlayPopups = false, SupportsWindowPositioning = false
        });
}
