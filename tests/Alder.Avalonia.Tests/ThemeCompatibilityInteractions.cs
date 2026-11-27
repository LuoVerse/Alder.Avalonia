using Alder.Avalonia.Controls;
using Alder.Avalonia.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Themes.Fluent2;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

[Collection(HeadlessSessionCollection.Name)]
public sealed class ThemeCompatibilityInteractions
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdvancedControlsScrollToTheirLastItemWithOnlyAlderTheme(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(AlderOnlyTestApp));
        await session.Dispatch(() =>
        {
            var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var firstAction = new AlderExpandableActionBarItem { Content = "First document" };
            var lastAction = new AlderExpandableActionBarItem { Content = "Download selected document" };
            var bar = new AlderExpandableActionBar
            {
                Size = AlderExpandableActionBarSize.Small,
                MaxWidth = 174,
                Items = { firstAction, lastAction }
            };
            WithWindow(bar, variant, window =>
            {
                var scroll = FindViewport(bar, "PART_ScrollViewer");
                Assert.True(firstAction.Focus(NavigationMethod.Tab));
                Key(window, PhysicalKey.ArrowRight);
                Assert.True(lastAction.IsFocused);
                Assert.True(scroll.Offset.X > 0);
            });

            var menu = new AlderBloomMenu
            {
                IsOpen = true,
                ItemsSource = Enumerable.Range(1, 12).Select(i => $"Action {i}").ToArray()
            };
            WithWindow(menu, variant, window =>
            {
                var scroll = FindViewport(window, "PART_GridViewport");
                Key(window, PhysicalKey.End);
                Assert.True(Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(11)).IsFocused);
                Assert.True(scroll.Offset.Y > 0);
            });

            var images = new AlderImageView
            {
                ViewportHeight = 100,
                ItemsSource = Enumerable.Range(1, 24).Select(i => new AlderImageViewItem { Title = $"Photo {i}" }).ToArray()
            };
            WithWindow(images, variant, window =>
            {
                var scroll = FindViewport(images, "PART_ThumbnailScroll");
                images.SelectedIndex = 23;
                Pump(window);
                var strip = Assert.Single(scroll.GetVisualDescendants().OfType<AlderFilmstrip>());
                Assert.IsType<AlderButton>(strip.ContainerFromIndex(23));
                Assert.True(scroll.Offset.X > 0);
            });
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AdvancedControlsRealizeItemsAndIconsWithOnlyAlderTheme()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(AlderOnlyTestApp));
        await session.Dispatch(() =>
        {
            var action = new AlderOverflowAction { Content = "Archive" };
            var bar = new AlderOverflowActions
            {
                IsExpanded = true,
                OverflowActions = { action }
            };
            WithWindow(bar, ThemeVariant.Light, _ =>
            {
                Assert.Contains(action, bar.GetVisualDescendants());
                var toggle = Assert.Single(bar.GetVisualDescendants().OfType<AlderOverflowActionsToggle>());
                var icon = Assert.Single(toggle.GetVisualDescendants().OfType<AlderPathIcon>(), item => item.Name == "CloseIcon");
                Assert.NotEmpty(icon.GetVisualChildren());
            });
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SeparateDialogProvidesItsOwnOverlayWithOnlyAlderTheme()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(AlderOnlyTestApp));
        await session.Dispatch(() =>
        {
            WithWindow(new AlderButton { Content = "Open dialog" }, ThemeVariant.Light, owner =>
            {
                var content = new AlderSelect { ItemsSource = new[] { "One", "Two" } };
                var result = Dialog.ShowWindowAsync(owner, content);
                Pump(owner);
                var dialog = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(content));
                try
                {
                    Assert.NotSame(owner, dialog);
                    Assert.NotNull(OverlayLayer.GetOverlayLayer(content));
                }
                finally
                {
                    dialog.Close();
                    Pump(owner);
                }
                Assert.True(result.IsCompleted);
            });
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false, 650)]
    [InlineData(false, 305)]
    [InlineData(true, 650)]
    [InlineData(true, 305)]
    public async Task Fluent2SupportsWrappingAlderCardsAndActions(bool dark, int width)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(Fluent2AlderTestApp));
        await session.Dispatch(() =>
        {
            var cards = new FlexPanel { Wrap = FlexWrap.Wrap, ColumnSpacing = 12, RowSpacing = 12 };
            foreach (var title in new[] { "Revenue", "Orders", "Customers" })
            {
                var card = new AlderKpi { Title = title, Value = 120 };
                Flex.SetBasis(card, new FlexBasis(140));
                Flex.SetGrow(card, 1);
                cards.Children.Add(card);
            }

            var actions = new WrapPanel
            {
                ItemWidth = 120, ItemSpacing = 12, LineSpacing = 8,
                ItemsAlignment = WrapPanelItemsAlignment.StretchAll
            };
            foreach (var label in new[] { "Edit", "Archive", "Duplicate" })
                actions.Children.Add(new AlderButton { Content = label, FullWidth = true });

            var input = new TextBox { Text = "Fluent2" };
            var window = new AlderWindow
            {
                Width = width, Height = 500, ShowTitleBar = false,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Margin = new Thickness(20), Spacing = 12, Children = { cards, actions, input } }
            };

            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                bool wraps = width == 305;
                var firstCard = cards.Children[0].Bounds;
                Assert.True(firstCard.Width > 0);
                Assert.InRange(Math.Abs(firstCard.Width - cards.Children[1].Bounds.Width), 0, 1);
                Assert.Equal(wraps, cards.Children[2].Bounds.Y > firstCard.Y);
                Assert.All(cards.Children, card => Assert.NotEmpty(card.GetVisualDescendants().OfType<TextBlock>()));

                var firstAction = actions.Children[0].Bounds;
                var lastAction = actions.Children[2].Bounds;
                Assert.True(firstAction.Width >= 120);
                Assert.InRange(Math.Abs(firstAction.Width - actions.Children[1].Bounds.Width), 0, 1);
                Assert.Equal(wraps, lastAction.Y > firstAction.Y);
                Assert.InRange(Math.Abs(actions.Bounds.Width - lastAction.Right), 0, 1);
                Assert.NotEmpty(input.GetVisualDescendants());

                var button = Assert.IsType<AlderButton>(actions.Children[0]);
                Assert.Equal(AlderSize.Medium, button.Size);
                button.SetValue(AlderButton.SizeProperty, AlderSize.Large);
                Assert.Equal(AlderSize.Large, button.Size);
                button.IsPending = true;
                Assert.True(button.GetValue(AlderButton.IsPendingProperty));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    private static AlderScrollViewer FindViewport(Control root, string name)
    {
        var scroll = Assert.Single(root.GetVisualDescendants().OfType<AlderScrollViewer>(), item => item.Name == name);
        Assert.Single(scroll.GetVisualDescendants().OfType<ScrollContentPresenter>());
        return scroll;
    }

    private static void WithWindow(Control content, ThemeVariant variant, Action<Window> check)
    {
        var window = new AlderWindow
        {
            Width = 360, Height = 250, ShowTitleBar = false, RequestedThemeVariant = variant, Content = content
        };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            check(window);
        }
        finally { window.Close(); }
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}

public sealed class AlderOnlyTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<AlderOnlyTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize() => Styles.Add(new AlderTheme());
}

public sealed class Fluent2AlderTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Fluent2AlderTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new Fluent2Theme());
        Styles.Add(new AlderTheme());
    }
}
