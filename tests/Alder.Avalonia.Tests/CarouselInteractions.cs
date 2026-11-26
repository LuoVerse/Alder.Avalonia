using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class CarouselInteractions
{
    [AvaloniaFact]
    public void CarouselNavigatesThreeVisibleSlidesAndKeepsSelectionAfterInsertion()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(1, 40).Select(i => $"Slide {i}"));
        var carousel = new AlderCarousel
        {
            ItemsSource = items, SelectedIndex = 1, SlidesPerView = 3, ShowThumbnails = true,
            Layout = AlderCarouselLayout.Modal
        };
        using var preview = new CarouselWindow(carousel);
        var previous = Part<Button>(carousel, "PART_Previous");
        var next = Part<Button>(carousel, "PART_Next");
        Assert.Equal(38, carousel.SnapCount);
        Assert.Equal("Slide 2", carousel.SelectedItem);
        Assert.True(preview.Bounds(previous).Right <= preview.Bounds(Part<Border>(carousel, "PART_Viewport")).Left);
        Assert.Equal(5, carousel.GetVisualDescendants().OfType<AlderCarouselItem>().Count());

        preview.Click(next);
        Assert.Equal(2, carousel.SelectedIndex);
        var thumbnail = Part<StackPanel>(carousel, "PART_Thumbnails").Children.OfType<Button>().First();
        preview.Click(thumbnail);
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.False(previous.IsEnabled);

        carousel.GoTo(5, animate: false);
        preview.Pump();
        var selected = carousel.SelectedItem;
        items.Insert(0, "Inserted slide");
        preview.Pump();
        Assert.Equal(6, carousel.SelectedIndex);
        Assert.Same(selected, carousel.SelectedItem);

        Assert.True(carousel.Focus());
        preview.Key(PhysicalKey.End);
        Assert.Equal(carousel.SnapCount - 1, carousel.SelectedIndex);
        Assert.False(next.IsEnabled);
        Assert.InRange(carousel.GetVisualDescendants().OfType<AlderCarouselItem>().Count(), 3, 5);
        Assert.True(Part<ScrollViewer>(carousel, "PART_ThumbnailStrip").Offset.X > 0);

        carousel.Layout = AlderCarouselLayout.Miniatures;
        preview.Pump();
        Assert.True(preview.Bounds(Part<Border>(carousel, "PART_Viewport")).Bottom <= preview.Bounds(previous).Top);
        var thumbnails = Part<ScrollViewer>(carousel, "PART_ThumbnailStrip");
        Assert.InRange(Math.Abs(preview.Bounds(thumbnails).Center.Y - preview.Bounds(previous).Center.Y), 0, 1);
    }

    [AvaloniaFact]
    public void CarouselSupportsInlineSlidesAndRemovesTheirContainers()
    {
        var first = new AlderCarouselItem { Content = new TextBlock { Text = "First" }, Thumbnail = "A" };
        var second = new AlderCarouselItem { Content = new TextBlock { Text = "Second" }, Thumbnail = "B" };
        var carousel = new AlderCarousel { Items = { first, second }, ShowThumbnails = true };
        using var preview = new CarouselWindow(carousel);
        Assert.Same(first, carousel.ContainerFromIndex(0));
        Assert.Equal("A", ((Button)Part<StackPanel>(carousel, "PART_Thumbnails").Children[0]).Content);
        carousel.MoveNext();
        preview.Pump();
        Assert.Same(second, carousel.SelectedItem);
        carousel.Items.Remove(first);
        preview.Pump();
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.Same(second, carousel.ContainerFromIndex(0));
        Assert.False(Part<ScrollViewer>(carousel, "PART_ThumbnailStrip").IsVisible);
    }

    [AvaloniaFact]
    public async Task CarouselWrapsAndTracksDragWithoutRebuildingVisibleSlides()
    {
        var carousel = new AlderCarousel { Loop = true, ItemsSource = new[] { "One", "Two", "Three" } };
        using var preview = new CarouselWindow(carousel, reducedMotion: false);
        var initial = carousel.ContainerFromIndex(0)!;
        var viewport = preview.Bounds(Part<Border>(carousel, "PART_Viewport"));
        var point = viewport.Center;
        preview.Window.MouseMove(point);
        preview.Window.MouseDown(point, MouseButton.Left);
        await Task.Delay(36);
        var release = point - new Vector(viewport.Width * .7, 0);
        preview.Window.MouseMove(release);
        preview.Pump();
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.Same(initial, carousel.ContainerFromIndex(0));
        Assert.True(preview.Bounds(initial).Left < viewport.Left - viewport.Width / 2);
        preview.Window.MouseUp(release, MouseButton.Left);
        await preview.WaitUntil(() => carousel.SelectedIndex > 0);
        Assert.InRange(carousel.SelectedIndex, 1, 2);
        carousel.GoTo(0, animate: false);
        carousel.MovePrevious();
        Assert.Equal(2, carousel.SelectedIndex);
        carousel.MoveNext();
        Assert.Equal(0, carousel.SelectedIndex);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarouselFollowsSmallMovesWithoutRelayoutAndKeepsTheLoopSeamContinuous(bool rightToLeft)
    {
        var carousel = new AlderCarousel
        {
            Loop = true, SlideSpacing = 0, ItemsSource = Enumerable.Range(1, 6).ToArray(),
            FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };
        using var preview = new CarouselWindow(carousel, reducedMotion: false);
        var viewport = preview.Bounds(Part<Border>(carousel, "PART_Viewport"));
        var track = carousel.GetVisualDescendants().OfType<Panel>().Single(panel => panel.GetType().Name == "AlderCarouselTrack");
        var first = carousel.ContainerFromIndex(0)!;
        var last = carousel.ContainerFromIndex(5)!;
        double contentLeft = preview.Bounds(first).Left;
        int layouts = 0;
        track.LayoutUpdated += (_, _) => layouts++;
        var start = viewport.Center;
        using var finger = rightToLeft ? preview.Window.TouchBegin(start) : null;
        if (finger is null)
        {
            preview.Window.MouseMove(start);
            preview.Window.MouseDown(start, MouseButton.Left);
        }

        double distance = viewport.Width * .24 * (rightToLeft ? -1 : 1);
        for (int i = 1; i <= 12; i++)
        {
            var point = start + new Vector(distance * i / 12, 0);
            if (finger is null) preview.Window.MouseMove(point);
            else preview.Window.TouchMove(finger, point);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.InRange(preview.Bounds(first).Left - contentLeft, distance - .05, distance + .05);
        Assert.Same(last, carousel.ContainerFromIndex(5));
        Assert.InRange(layouts, 0, 2);

        await Task.Delay(112);
        var release = start + new Vector(distance, 0);
        if (finger is null) preview.Window.MouseUp(release, MouseButton.Left);
        else preview.Window.TouchEnd(finger, release);
        await preview.WaitUntil(() => Math.Abs(preview.Bounds(first).Left - contentLeft) < .01, forceLayout: false);
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.InRange(preview.Bounds(first).Left, contentLeft - .01, contentLeft + .01);

        layouts = 0;
        carousel.MovePrevious();
        await preview.WaitUntil(() => Math.Abs(preview.Bounds(last).Left - contentLeft) < .01, forceLayout: false);
        Assert.Equal(5, carousel.SelectedIndex);
        Assert.InRange(preview.Bounds(last).Left, contentLeft - .01, contentLeft + .01);
        Assert.Same(last, carousel.ContainerFromIndex(5));
        Assert.InRange(layouts, 0, 2);

        layouts = 0;
        carousel.MoveNext();
        await preview.WaitUntil(() => Math.Abs(preview.Bounds(first).Left - contentLeft) < .01, forceLayout: false);
        Assert.Equal(0, carousel.SelectedIndex);
        Assert.InRange(preview.Bounds(first).Left, contentLeft - .01, contentLeft + .01);
        Assert.Same(first, carousel.ContainerFromIndex(0));
        Assert.InRange(layouts, 0, 2);
    }

    [AvaloniaFact]
    public async Task CarouselAutoplayCanBePausedAndStopsOffTree()
    {
        var carousel = new AlderCarousel
        {
            Loop = true, ItemsSource = new[] { 1, 2, 3, 4, 5 }, IsAutoPlayEnabled = true,
            AutoPlayInterval = TimeSpan.FromMilliseconds(140), ShowPlayPause = true, PauseOnHover = false
        };
        using var preview = new CarouselWindow(carousel, reducedMotion: false);
        await preview.WaitUntil(() => carousel.SelectedIndex > 0);
        Assert.True(carousel.SelectedIndex > 0);
        carousel.PauseOnHover = true;
        preview.Window.MouseMove(preview.Bounds(Part<Border>(carousel, "PART_Viewport")).Center);
        preview.Pump();
        int hoveredAt = carousel.SelectedIndex;
        await Task.Delay(280);
        preview.Pump();
        Assert.Equal(hoveredAt, carousel.SelectedIndex);
        preview.Window.MouseMove(new Point(2, 2));
        preview.Pump();
        await preview.WaitUntil(() => carousel.SelectedIndex != hoveredAt);
        Assert.NotEqual(hoveredAt, carousel.SelectedIndex);

        preview.Click(Part<Button>(carousel, "PART_PlayPause"));
        Assert.False(carousel.IsAutoPlayEnabled);
        int pausedAt = carousel.SelectedIndex;
        await Task.Delay(280);
        preview.Pump();
        Assert.Equal(pausedAt, carousel.SelectedIndex);

        carousel.IsAutoPlayEnabled = true;
        preview.Window.Content = null;
        await Task.Delay(280);
        preview.Pump();
        Assert.Equal(pausedAt, carousel.SelectedIndex);
    }

    private static T Part<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private sealed class CarouselWindow : IDisposable
    {
        public CarouselWindow(AlderCarousel control, bool reducedMotion = true)
        {
            control.Margin = new Thickness(16);
            Window = new Window { Width = 660, Height = 320, Content = control };
            AlderMotion.SetReduced(Window, reducedMotion);
            Window.Show();
            Pump();
        }

        public Window Window { get; }
        public Rect Bounds(Control control)
        {
            var start = control.TranslatePoint(default, Window)!.Value;
            var end = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), Window)!.Value;
            return new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), control.Bounds.Width, control.Bounds.Height);
        }
        public void Click(Control control)
        {
            var point = Bounds(control).Center;
            Window.MouseMove(point);
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);
            Pump();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public async Task WaitUntil(Func<bool> condition, bool forceLayout = true)
        {
            for (int i = 0; i < 40 && !condition(); i++)
            {
                await Task.Delay(20);
                if (forceLayout) Pump();
                else Dispatcher.UIThread.RunJobs();
            }
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
