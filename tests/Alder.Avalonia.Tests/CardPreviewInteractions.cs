using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class CardPreviewInteractions
{
    [AvaloniaFact]
    public async Task HoverCardKeepsThePreviewOpenAcrossTheGapAndWarmsTheNextCard()
    {
        var trigger = new Button { Content = "First" };
        var secondTrigger = new Button { Content = "Second" };
        var first = new AlderHoverCard { Trigger = trigger, Content = "First preview", OpenDelay = 180, CloseDelay = 65 };
        var second = new AlderHoverCard { Trigger = secondTrigger, Content = "Second preview", OpenDelay = 1500, CloseDelay = 65 };
        var panel = new Canvas { Children = { first, second } };
        Canvas.SetLeft(first, 128);
        Canvas.SetTop(first, 160);
        Canvas.SetLeft(second, 252);
        Canvas.SetTop(second, 160);
        var window = Show(panel, reduced: true);
        try
        {
            window.MouseMove(Center(trigger, window));
            Assert.False(first.IsOpen);
            await WaitFor(() => first.IsOpen);
            var preview = Surface(window);
            var triggerTop = trigger.TranslatePoint(default, window)!.Value;
            var previewBottom = preview.TranslatePoint(new Point(0, preview.Bounds.Height), window)!.Value.Y;
            Assert.True(previewBottom < triggerTop.Y);
            window.MouseMove(new Point(Center(trigger, window).X, (previewBottom + triggerTop.Y) / 2));
            window.MouseMove(Center(preview, window));
            await Task.Delay(180);
            Pump();
            Assert.True(first.IsOpen);

            window.MouseMove(Center(secondTrigger, window));
            Pump();
            Assert.True(second.IsOpen);
            Assert.False(first.IsOpen);
            Assert.Single(window.GetVisualDescendants(), visual => (visual as Control)?.Name == "AlderHoverCardSurface");

            window.MouseMove(new Point(6, window.Bounds.Height - 6));
            await WaitFor(() => !second.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HoverCardSupportsKeyboardFocusEscapeAndControlledReopen()
    {
        var trigger = new Button { Content = "Open profile" };
        var action = new Button { Content = "Follow" };
        var outside = new Button { Content = "Outside" };
        var card = new AlderHoverCard { Trigger = trigger, Content = action, ShowArrow = true };
        var panel = new Canvas { Children = { card, outside } };
        Canvas.SetLeft(card, 130);
        Canvas.SetTop(card, 160);
        Canvas.SetLeft(outside, 12);
        Canvas.SetTop(outside, 220);
        var window = Show(panel, reduced: true);
        try
        {
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Pump();
            Assert.True(card.IsOpen);
            Assert.True(trigger.IsFocused);
            Press(window, PhysicalKey.Tab);
            Assert.True(action.IsFocused);
            Press(window, PhysicalKey.Escape);
            Assert.False(card.IsOpen);
            Assert.True(trigger.IsFocused);

            card.IsOpen = true;
            Pump();
            Assert.True(action.IsAttachedToVisualTree());
            bool clicked = false;
            outside.Click += (_, _) => clicked = true;
            var point = Center(outside, window);
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            Assert.True(clicked);
            Assert.False(card.IsOpen);

            card.IsOpen = true;
            Pump();
            card.IsEnabled = false;
            Pump();
            Assert.False(card.IsOpen);
            Assert.False(action.IsAttachedToVisualTree());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task TouchHoldOpensTheHoverCardAndMovesFocusIntoIt()
    {
        var trigger = new Button { Content = "Preview" };
        var action = new Button { Content = "More" };
        var card = new AlderHoverCard
        {
            Trigger = trigger, Content = action, OpenDelay = 10_000,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(card, reduced: true);
        try
        {
            var point = Center(trigger, window);
            using var touch = window.TouchBegin(point);
            await WaitFor(() => card.IsOpen && action.IsFocused);
            window.TouchEnd(touch, point);
            Assert.True(card.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task HoloCardTiltsAndSettlesWithoutMovingItsLayoutSlot()
    {
        var card = new AlderHoloCard
        {
            Width = 185, Height = 220, Content = new TextBlock { Text = "Artwork" },
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(card, reduced: false);
        try
        {
            var rotator = card.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Rotator");
            var shadow = card.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Shadow");
            var content = card.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
            var bounds = card.Bounds;
            var point = card.TranslatePoint(new Point(140, 60), window)!.Value;
            Assert.Equal(0, shadow.Opacity);
            window.MouseMove(point);
            await WaitFor(() => rotator.RenderTransform is MatrixTransform { Matrix.IsIdentity: false });
            var tilt = Assert.IsType<MatrixTransform>(rotator.RenderTransform);
            Assert.True(tilt.Matrix.M13 < 0 && tilt.Matrix.M23 > 0);
            Assert.True(Assert.IsType<TranslateTransform>(content.RenderTransform) is { X: < 0, Y: > 0 });
            Assert.True(shadow.Opacity > 0);
            Assert.Equal(bounds, card.Bounds);
            Assert.False(card.ClipToBounds);
            Assert.False(rotator.ClipToBounds);
            Assert.True(card.IsActive);

            window.MouseMove(card.TranslatePoint(new Point(45, 160), window)!.Value);
            await WaitFor(() => tilt.Matrix is { M13: > 0, M23: < 0 });
            Assert.True(Assert.IsType<TranslateTransform>(content.RenderTransform) is { X: > 0, Y: < 0 });

            window.MouseMove(new Point(6, 6));
            await WaitFor(() => !card.IsActive);
            Assert.Null(rotator.RenderTransform);
            Assert.Equal(0, shadow.Opacity);

            window.MouseMove(point);
            await WaitFor(() => shadow.Opacity > 0);
            AlderMotion.SetReduced(window, true);
            window.MouseMove(new Point(6, 6));
            window.MouseMove(point);
            Pump();
            Assert.False(card.IsActive);
            Assert.Null(rotator.RenderTransform);
            Assert.Equal(0, shadow.Opacity);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content, bool reduced)
    {
        var window = new Window { Width = 440, Height = 340, Content = content };
        AlderMotion.SetReduced(window, reduced);
        window.Show();
        Pump();
        return window;
    }

    private static Control Surface(Window window) => window.GetVisualDescendants().OfType<Control>()
        .Single(control => control.Name == "AlderHoverCardSurface");

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static async Task WaitFor(Func<bool> predicate)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Pump();
            if (predicate()) return;
            await Task.Delay(15);
        }
        Assert.True(predicate());
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
