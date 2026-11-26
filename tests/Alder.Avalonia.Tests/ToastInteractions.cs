using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ToastInteractions
{
    [AvaloniaTheory]
    [InlineData(ToastPlacement.Top)]
    [InlineData(ToastPlacement.Bottom)]
    public async Task AFullStackShowsNewToastWhileTheOldestIsStillExiting(ToastPlacement placement)
    {
        var window = new Window { Width = 520, Height = 450 };
        window.Show();
        try
        {
            var host = Toast.Register(window, "activity");
            host.Placement = placement;
            host.MaxVisible = 2;
            host.IsExpanded = true;
            var oldest = Toast.Show(Notice("First"), window, "activity");
            var second = host.Show(Notice("Second"));
            window.UpdateLayout();
            var outgoing = Card(host, "First");
            var oldPosition = outgoing.TranslatePoint(default, host);

            var newest = host.Show(Notice("Third") with { Description = "Changes the stack height" });
            window.UpdateLayout();
            Assert.True(Card(host, "Third").IsHitTestVisible);
            Assert.True(Card(host, "Second").IsHitTestVisible);
            Assert.False(outgoing.IsHitTestVisible);
            Assert.True(outgoing.IsAttachedToVisualTree());
            Assert.Equal(oldPosition, outgoing.TranslatePoint(default, host));
            Assert.False(oldest.TryUpdate(Notice("Too late")));

            Assert.Equal(ToastCloseReason.Evicted, await oldest.Completion.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(outgoing.IsAttachedToVisualTree());
            Assert.False(second.Completion.IsCompleted);
            Assert.False(newest.Completion.IsCompleted);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WindowLimitUpdatesLiveAndStacksKeepOnlyTheirNewestNotifications()
    {
        var window = new AlderWindow { Width = 520, Height = 450, ToastMaxVisible = 5 };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            var host = Toast.Register(window);
            var named = Toast.Register(window, "separate");
            Assert.Equal(5, host.MaxVisible);
            Assert.Equal(3, named.MaxVisible);
            var independent = named.Show(Notice("Independent"));
            var top = host.Show(Notice("Top") with { Placement = ToastPlacement.Top });
            var handles = Enumerable.Range(1, 7).Select(n => host.Show(Notice($"Activity {n}"))).ToArray();
            window.UpdateLayout();
            Assert.Equal(6, host.GetVisualDescendants().OfType<ToastCard>().Count());
            Assert.All(handles.Take(2), handle => Assert.Equal(ToastCloseReason.Evicted, handle.Completion.Result));

            window.ToastMaxVisible = 2;
            window.UpdateLayout();
            Assert.Equal(2, host.MaxVisible);
            Assert.Equal(3, host.GetVisualDescendants().OfType<ToastCard>().Count());
            Assert.Equal(3, named.MaxVisible);
            Assert.All(handles.Take(5), handle => Assert.Equal(ToastCloseReason.Evicted, handle.Completion.Result));
            Assert.All(handles.Skip(5).Append(top).Append(independent), handle => Assert.False(handle.Completion.IsCompleted));
            Assert.True(Card(host, "Activity 7").IsHitTestVisible);

            window.ToastMaxVisible = 6;
            window.UpdateLayout();
            Assert.Equal(3, host.GetVisualDescendants().OfType<ToastCard>().Count());
            var remaining = Enumerable.Range(8, 3).Select(n => host.Show(Notice($"Activity {n}")))
                .Concat(handles.Skip(5)).Append(top).Append(independent).ToArray();
            window.Close();
            Assert.All(remaining, handle => Assert.Equal(ToastCloseReason.HostRemoved, handle.Completion.Result));
        }
        finally { if (window.IsVisible) window.Close(); }
    }

    private static ToastOptions Notice(string title) => new(title) { Duration = TimeSpan.Zero };

    private static ToastCard Card(ToastHost host, string title) => host.GetVisualDescendants()
        .OfType<ToastCard>().Single(card => card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == title));
}
