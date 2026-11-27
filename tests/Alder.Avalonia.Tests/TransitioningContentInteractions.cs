using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class TransitioningContentInteractions
{
    [AvaloniaTheory]
    [InlineData(AlderContentTransition.CrossFade)]
    [InlineData(AlderContentTransition.Slide)]
    [InlineData(AlderContentTransition.Rise)]
    [InlineData(AlderContentTransition.Scale)]
    [InlineData(AlderContentTransition.Blur)]
    [InlineData(AlderContentTransition.Reveal)]
    public async Task PreservesTheFrameAndReleasesTheOutgoingContentAfterEachTransition(AlderContentTransition mode)
    {
        const string first = "First";
        const string second = "Second";
        var control = new AlderTransitioningContentControl
        {
            Content = first, Transition = mode, Duration = TimeSpan.FromMilliseconds(180), Height = 120,
            ContentTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text })
        };
        var window = new AlderTestWindow { Width = 300, Height = 180, Content = control };
        window.Show();
        try
        {
            Pump(window);
            var initial = Active(control);
            var viewport = control.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Viewport");
            var frame = control.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Frame");
            Assert.Equal(first, initial.Content);
            Assert.Equal(first, Assert.IsType<TextBlock>(initial.Child).Text);
            Assert.Null(initial.RenderTransform);
            Assert.True(viewport.ClipToBounds);
            Assert.False(frame.IsHitTestVisible);
            var bounds = frame.Bounds;

            TransitionCompletedEventArgs? result = null;
            control.TransitionCompleted += (_, args) => result = args;
            control.Content = second;
            window.UpdateLayout();
            var incoming = Presenters(control).Single(presenter => Equals(presenter.Content, second));
            Assert.True(initial.IsVisible);
            Assert.False(initial.IsHitTestVisible);
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(initial));
            Assert.True(incoming.IsHitTestVisible);
            Assert.Equal(second, Assert.IsType<TextBlock>(incoming.Child).Text);
            Assert.Equal(bounds, frame.Bounds);

            switch (mode)
            {
                case AlderContentTransition.Slide:
                    Assert.True(incoming.RenderTransform!.Value.M31 > 0);
                    break;
                case AlderContentTransition.Rise:
                    Assert.Equal(24, incoming.RenderTransform!.Value.M32);
                    break;
                case AlderContentTransition.Scale:
                    Assert.Equal(.94, incoming.RenderTransform!.Value.M11, 5);
                    break;
                case AlderContentTransition.Blur:
                    Assert.Equal(6, Assert.IsType<BlurEffect>(incoming.Effect).Radius);
                    break;
                case AlderContentTransition.Reveal:
                    Assert.Equal(0, Assert.IsType<RectangleGeometry>(incoming.Clip).Rect.Width);
                    break;
                default:
                    Assert.Equal(0, incoming.Opacity);
                    break;
            }

            await WaitFor(window, () => result?.HasRunToCompletion == true && initial.Content is null);
            Assert.Equal(first, result!.From);
            Assert.Equal(second, result.To);
            Assert.Equal(bounds, frame.Bounds);
            Assert.Same(incoming, Active(control));
            AssertResting(incoming);
            AssertResting(initial);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task DirectionRetargetingAndInheritedReducedMotionKeepTheLatestContent()
    {
        var first = new AlderButton { Content = "First" };
        var second = new AlderButton { Content = "Second" };
        var third = new AlderButton { Content = "Third" };
        var control = new AlderTransitioningContentControl
        {
            Content = first, Transition = AlderContentTransition.Slide, Duration = TimeSpan.FromSeconds(1)
        };
        var window = new AlderTestWindow { Width = 300, Height = 180, Content = control };
        window.Show();
        try
        {
            Pump(window);
            Assert.True(first.Focus());
            control.IsTransitionReversed = true;
            control.Content = second;
            window.UpdateLayout();
            Assert.True(Presenters(control).Single(p => p.Content == second).RenderTransform!.Value.M31 < 0);
            Assert.False(first.IsFocused);

            control.IsTransitionReversed = false;
            control.Content = third;
            window.UpdateLayout();
            Assert.True(Presenters(control).Single(p => p.Content == third).RenderTransform!.Value.M31 > 0);
            AlderMotion.SetReduced(window, true);
            await WaitFor(window, () => Presenters(control).Count(p => p.Content is not null) == 1);
            Assert.Same(third, Active(control).Content);
            AssertResting(Active(control));

            control.Transition = AlderContentTransition.Blur;
            control.Content = first;
            window.UpdateLayout();
            var reduced = Presenters(control).Single(p => p.Content == first);
            Assert.Equal(0, reduced.Opacity);
            Assert.Null(reduced.Effect);
            Assert.Null(reduced.RenderTransform);
            Assert.Null(reduced.Clip);
            await WaitFor(window, () => Presenters(control).Count(p => p.IsVisible) == 1);
            Assert.Same(first, Active(control).Content);
            AssertResting(Active(control));

            control.Duration = TimeSpan.Zero;
            control.Content = third;
            window.UpdateLayout();
            Assert.Same(third, Active(control).Content);
            AssertResting(Active(control));

            control.Transition = AlderContentTransition.None;
            control.Duration = null;
            control.Content = null;
            window.UpdateLayout();
            Assert.Null(Active(control).Content);
            AssertResting(Active(control));
        }
        finally { window.Close(); }
    }

    private static IReadOnlyList<ContentPresenter> Presenters(AlderTransitioningContentControl control) =>
        control.GetVisualDescendants().OfType<ContentPresenter>()
            .Where(presenter => presenter.TemplatedParent == control).ToArray();

    private static ContentPresenter Active(AlderTransitioningContentControl control) =>
        Assert.Single(Presenters(control), presenter => presenter.IsVisible);

    private static void AssertResting(ContentPresenter presenter)
    {
        Assert.Equal(1, presenter.Opacity);
        Assert.Null(presenter.RenderTransform);
        Assert.Null(presenter.Effect);
        Assert.Null(presenter.Clip);
        Assert.True(presenter.IsHitTestVisible);
        Assert.NotEqual(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(presenter));
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WaitFor(Window window, Func<bool> predicate)
    {
        for (int i = 0; i < 100 && !predicate(); i++)
        {
            await Task.Delay(10);
            Pump(window);
        }
        Assert.True(predicate());
    }
}
