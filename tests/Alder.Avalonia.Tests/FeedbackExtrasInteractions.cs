using System.Globalization;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class FeedbackExtrasInteractions
{
    [AvaloniaFact]
    public void NumberValueFormatsCurrencyCompactPercentAccountingAndLocale()
    {
        var number = new AlderNumberValue { Culture = CultureInfo.GetCultureInfo("en-US"), Value = 228441, Style = NumberValueStyle.Currency };
        Assert.Equal("$228,441.00", number.FormattedValue);
        number.Currency = "JPY";
        Assert.Equal("¥228,441", number.FormattedValue);
        number.Currency = "USD";
        number.Value = -1234.56;
        number.FormatOptions = new NumberValueFormatOptions { Accounting = true };
        Assert.Equal("($1,234.56)", number.FormattedValue);
        number.FormatOptions = null;
        number.Style = NumberValueStyle.Percent;
        number.Value = .033;
        number.MaximumFractionDigits = 1;
        Assert.Equal("3.3%", number.FormattedValue);

        number.Style = NumberValueStyle.Decimal;
        number.Notation = NumberValueNotation.Compact;
        number.Value = 45678;
        number.MaximumFractionDigits = null;
        Assert.Equal("46K", number.FormattedValue);
        number.Value = 12_345;
        number.Culture = CultureInfo.GetCultureInfo("zh-CN");
        Assert.Equal("1.2万", number.FormattedValue);

        number.Culture = CultureInfo.GetCultureInfo("de-DE");
        number.Notation = NumberValueNotation.Standard;
        number.Value = 1234.5;
        Assert.Equal("1.234,5", number.FormattedValue);
        number.Value = -0d;
        number.SignDisplay = NumberValueSignDisplay.ExceptZero;
        Assert.Equal("0", number.FormattedValue);
    }

    [AvaloniaFact]
    public void RatingDisplaysAFractionAndCommitsOnlyInteractiveSelections()
    {
        var rating = new AlderRating { Value = 3.25, IsReadOnly = true, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var window = Show(rating);
        try
        {
            var layer = rating.GetVisualDescendants().OfType<StackPanel>().Single(part => part.Name == "PART_Items");
            var fourth = (Panel)layer.Children[3];
            var fill = Assert.IsType<AlderPathIcon>(fourth.Children[1]);
            Assert.Equal(5, Assert.IsType<RectangleGeometry>(fill.Clip).Rect.Width);
            var second = layer.Children[1];
            var at = Center(second, window);
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Assert.Equal(3.25, rating.Value);

            rating.IsReadOnly = false;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Pump(window);
            Assert.Equal(2, rating.Value);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Assert.Equal(3, rating.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(FlowDirection.LeftToRight)]
    [InlineData(FlowDirection.RightToLeft)]
    public void RatingPreviewStaysOnTheLastItemWhileCrossingTheGap(FlowDirection direction)
    {
        var rating = new AlderRating
        {
            Value = 2, FlowDirection = direction,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        };
        AlderMotion.SetReduced(rating, true);
        var window = Show(rating);
        try
        {
            var items = rating.GetVisualDescendants().OfType<StackPanel>().Single(part => part.Name == "PART_Items");
            var third = Center(items.Children[2], window);
            var fourth = Center(items.Children[3], window);
            var fifth = Center(items.Children[4], window);
            var fill = (AlderPathIcon)((Panel)items.Children[4]).Children[1];

            window.MouseMove(fifth);
            Assert.True(fill.IsVisible && fill.Opacity == 1);
            window.MouseMove(new Point((fourth.X + fifth.X) / 2, fifth.Y));
            Assert.True(fill.IsVisible && fill.Opacity == 1);
            Assert.Equal(2, rating.Value);

            window.MouseMove(third);
            Assert.True(!fill.IsVisible || fill.Opacity == 0);
            window.MouseDown(third, MouseButton.Left);
            window.MouseMove(fourth);
            window.MouseUp(fourth, MouseButton.Left);
            Assert.Equal(4, rating.Value);

            window.MouseMove(fifth);
            window.MouseMove(new Point(0, 0));
            Assert.True(!fill.IsVisible || fill.Opacity == 0);
            Assert.Equal(4, rating.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TrendIndicatorDirectionAndSemanticColorAreIndependent()
    {
        var chip = new AlderTrendChip { Trend = TrendDirection.Down, Color = AlderStatusColor.Success, Content = "-4%" };
        var window = Show(chip);
        try
        {
            var surface = chip.GetVisualDescendants().OfType<AlderChip>().Single();
            Assert.Equal(AlderStatusColor.Success, surface.Color);
            var down = surface.Icon;
            Assert.NotNull(down);
            chip.Trend = TrendDirection.Up;
            Assert.NotSame(down, surface.Icon);
            Assert.Equal(AlderStatusColor.Success, surface.Color);
            chip.Color = null;
            chip.Trend = TrendDirection.Neutral;
            Assert.Null(surface.Icon);
            Assert.Equal(AlderStatusColor.Default, surface.Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TrendChipUsesPillCornersAndHonorsExplicitBrushes()
    {
        var trend = new AlderTrendChip { Content = "+3.3%" };
        var window = Show(new AlderContentControl { Content = trend, Foreground = Brushes.Magenta });
        try
        {
            var surface = trend.GetVisualDescendants().OfType<AlderChip>().Single();
            var fill = surface.Background;
            var ink = surface.Foreground;
            Assert.NotSame(Brushes.Magenta, ink);

            foreach (var (size, radius) in new[] { (AlderSize.Small, 10), (AlderSize.Medium, 12), (AlderSize.Large, 14) })
            {
                trend.Size = size;
                Assert.Equal(new CornerRadius(radius), surface.CornerRadius);
            }
            trend.CornerRadius = new CornerRadius(4);
            Assert.Equal(trend.CornerRadius, surface.CornerRadius);

            trend.Background = Brushes.Navy;
            trend.Foreground = Brushes.White;
            Assert.Same(Brushes.Navy, surface.Background);
            Assert.Same(Brushes.White, surface.Foreground);
            trend.ClearValue(AlderTrendChip.BackgroundProperty);
            trend.ClearValue(AlderTrendChip.ForegroundProperty);
            Assert.Same(fill, surface.Background);
            Assert.Same(ink, surface.Foreground);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrendChipDefaultPaletteMatchesTheVariantAndTheme(bool dark)
    {
        string neutral = dark ? "#27272A" : "#EBEBEC";
        string ink = dark ? "#FCFCFC" : "#18181B";
        string up = dark ? "#74D88F" : "#2B7745";
        string down = dark ? "#EB7872" : "#A43533";
        var expected = new[]
        {
            (ChipVariant.Primary, TrendDirection.Up, "#17C964", "#18181B"),
            (ChipVariant.Primary, TrendDirection.Down, dark ? "#DB3B3E" : "#FF383C", "#FCFCFC"),
            (ChipVariant.Primary, TrendDirection.Neutral, neutral, ink),
            (ChipVariant.Secondary, TrendDirection.Up, neutral, up),
            (ChipVariant.Secondary, TrendDirection.Down, neutral, down),
            (ChipVariant.Secondary, TrendDirection.Neutral, neutral, ink),
            (ChipVariant.Tertiary, TrendDirection.Up, "Transparent", up),
            (ChipVariant.Tertiary, TrendDirection.Down, "Transparent", down),
            (ChipVariant.Tertiary, TrendDirection.Neutral, "Transparent", ink),
            (ChipVariant.Soft, TrendDirection.Up, dark ? "#1F17C964" : "#2617C964", up),
            (ChipVariant.Soft, TrendDirection.Down, dark ? "#26DB3B3E" : "#26FF383C", down),
            (ChipVariant.Soft, TrendDirection.Neutral, dark ? "#8027272A" : "#80EBEBEC", ink),
        };
        var panel = new StackPanel();
        foreach (var (variant, direction, _, _) in expected)
            panel.Children.Add(new AlderTrendChip { Content = "3.3%", Prefix = "+", Suffix = "MoM", Variant = variant, Trend = direction });
        var window = Show(panel);
        try
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Pump(window);
            for (int i = 0; i < expected.Length; i++)
            {
                var chip = (AlderTrendChip)panel.Children[i];
                var (_, direction, background, foreground) = expected[i];
                var surface = chip.GetVisualDescendants().OfType<AlderChip>().Single();
                var arrow = surface.GetVisualDescendants().OfType<AlderPathIcon>().First();
                var labels = surface.GetVisualDescendants().OfType<TextBlock>().ToDictionary(text => text.Text!);
                Assert.Equal(Color.Parse(background), Paint(surface.Background));
                Assert.Equal(Color.Parse(foreground), Paint(labels["3.3%"].Foreground));
                Assert.Equal(Color.Parse(foreground), Paint(labels["+"].Foreground));
                Assert.Equal(Color.Parse(dark ? "#9F9FA9" : "#71717A"), Paint(labels["MoM"].Foreground));
                Assert.Equal(direction != TrendDirection.Neutral, arrow.IsEffectivelyVisible);
                Assert.Equal(Color.Parse(foreground), Paint(arrow.Foreground));
            }
        }
        finally { window.Close(); }

        static Color Paint(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    }

    [AvaloniaFact]
    public async Task ButtonHoldCancelsOnExitAndInvokesTheExistingCommandOnce()
    {
        int commands = 0, completions = 0;
        var button = new AlderButton { Content = "Confirm", Command = new ActionCommand(() => commands++) };
        var feedback = new AlderPressableFeedback
        {
            Effects = PressableFeedbackEffects.HoldConfirm,
            Duration = TimeSpan.FromMilliseconds(65),
            CompletionCommand = new ActionCommand(() => completions++)
        };
        button.Feedback = feedback;
        var window = Show(button);
        try
        {
            var center = Center(button, window);
            window.MouseDown(center, MouseButton.Left);
            Assert.Equal(PressableFeedbackState.Holding, feedback.State);
            window.MouseMove(new Point(175, 120));
            await Task.Delay(120);
            window.MouseUp(new Point(175, 120), MouseButton.Left);
            Assert.Equal(0, commands);
            Assert.Equal(0, completions);

            window.MouseDown(center, MouseButton.Left);
            await WaitFor(() => feedback.State == PressableFeedbackState.Complete);
            await Task.Delay(45);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(1, commands);
            Assert.Equal(1, completions);

            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            await Task.Delay(100);
            Assert.Equal(1, commands);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task AlderButtonRunsItsProgressAndRestoresOnReset()
    {
        int clicks = 0, completions = 0;
        var button = new AlderButton { Content = "Apply", Command = new ActionCommand(() => clicks++) };
        var feedback = new AlderPressableFeedback
        {
            Effects = PressableFeedbackEffects.ProgressFeedback, AutoReset = false,
            Sweep = PressableSweep.Up, Duration = TimeSpan.FromMilliseconds(60),
            CompletionCommand = new ActionCommand(() => completions++)
        };
        button.Feedback = feedback;
        var window = Show(button);
        try
        {
            var center = Center(button, window);
            for (int i = 0; i < 2; i++)
            {
                window.MouseDown(center, MouseButton.Left);
                window.MouseUp(center, MouseButton.Left);
            }
            await WaitFor(() => feedback.State == PressableFeedbackState.Complete);
            Assert.Equal(1, clicks);
            Assert.Equal(1, completions);
            Assert.Equal(1, feedback.Progress);
            feedback.Reset();
            Assert.Equal(PressableFeedbackState.Idle, feedback.State);
            Assert.Equal(0, feedback.Progress);

            button.Feedback = null;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(2, clicks);
            Assert.Equal(1, completions);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PressableFeedbackBelongsToAlderControls()
    {
        int clicks = 0;
        var button = new Button { Content = "Apply", Command = new ActionCommand(() => clicks++) };
        Assert.False(AvaloniaPropertyRegistry.Instance.IsRegistered(button, AlderButton.FeedbackProperty));
        var window = Show(button);
        try
        {
            var center = Center(button, window);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(1, clicks);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SidebarSpringFollowsPointerAndKeyboardWithoutChangingNavigation()
    {
        var home = new AlderSidebarItem { Header = "Home", IsCurrent = true };
        int invokeCount = 0;
        var inbox = new AlderSidebarItem { Header = "Inbox", Command = new ActionCommand(() => invokeCount++) };
        var sidebar = new AlderSidebar { Content = new AlderSidebarMenu { Items = { home, inbox } } };
        var window = Show(sidebar);
        AlderMotion.SetReduced(window, false);
        try
        {
            var row = inbox.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Row");
            var surface = inbox.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RowSurface");
            var visual = ElementComposition.GetElementVisual(surface)!;
            var feedback = Assert.IsType<AlderPressableFeedback>(Assert.IsType<AlderButton>(row).Feedback);
            Assert.Same(surface, feedback.ScaleTarget);

            var center = Center(row, window);
            var bounds = row.Bounds;
            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            Assert.Equal(.98, visual.Scale.X, 4);
            Assert.Equal(bounds, row.Bounds);
            Assert.Same(home, sidebar.SelectedItem);
            window.MouseMove(new Point(center.X, center.Y + 80));
            Assert.Equal(1, visual.Scale.X);
            window.MouseUp(new Point(center.X, center.Y + 80), MouseButton.Left);
            Assert.Equal(0, invokeCount);

            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(1, visual.Scale.X);
            Assert.Same(inbox, sidebar.SelectedItem);
            Assert.Equal(1, invokeCount);

            Assert.True(inbox.Focus(NavigationMethod.Tab));
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.Equal(.98, visual.Scale.X, 4);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.Equal(1, visual.Scale.X);
            Assert.Equal(2, invokeCount);

            AlderMotion.SetReduced(window, true);
            window.MouseDown(center, MouseButton.Left);
            Assert.Null(visual.ImplicitAnimations);
            Assert.Equal(1, visual.Scale.X);
            window.MouseUp(center, MouseButton.Left);
            Assert.Equal(3, invokeCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task LegacyButtonRippleFinishesWhilePressedAndExplicitRippleWaitsForRelease()
    {
        var legacy = new AlderButton { Content = "Original", IsRippleEnabled = true };
        var explicitRipple = new AlderButton { Content = "Explicit" };
        explicitRipple.Feedback = new AlderPressableFeedback();
        var window = Show(new StackPanel { Children = { legacy, explicitRipple } });
        AlderMotion.SetReduced(window, false);
        try
        {
            static Border ClipOf(AlderButton button) => button.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "RippleClip");
            var first = Center(legacy, window);
            window.MouseDown(first, MouseButton.Left);
            Assert.NotNull(ClipOf(legacy).Child);
            await WaitFor(() => ClipOf(legacy).Child is null);
            Assert.True(legacy.IsPressed);
            window.MouseUp(first, MouseButton.Left);

            var second = Center(explicitRipple, window);
            window.MouseDown(second, MouseButton.Left);
            await Task.Delay(465);
            Assert.NotNull(ClipOf(explicitRipple).Child);
            window.MouseUp(second, MouseButton.Left);
            await WaitFor(() => ClipOf(explicitRipple).Child is null);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 420, Height = 240, Content = content };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int n = 0; n < 80 && !condition(); n++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class ActionCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}
