using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class FloatingTocInteractions
{
    [AvaloniaFact]
    public void NavigationKeepsTheRequestedHeadingWhenScrollingClampsAtTheEnd()
    {
        string[] keys = ["first", "second", "third", "last"];
        var document = new StackPanel();
        var toc = new AlderFloatingToc();
        foreach (string key in keys)
        {
            var section = new Border { Height = 80, Child = new TextBlock { Text = key } };
            AlderFloatingToc.SetAnchorKey(section, key);
            document.Children.Add(section);
            toc.Items.Add(new(key, key));
        }

        var scroll = new ScrollViewer { Height = 210, Content = document };
        toc.ScrollHost = scroll;
        AlderMotion.SetReduced(toc, true);
        var window = new Window { Width = 420, Height = 250, Content = new Grid { Children = { scroll, toc } } };
        window.Show();
        try
        {
            UpdateLayout();
            Assert.True(toc.NavigateTo("third"));
            UpdateLayout();

            double end = scroll.Extent.Height - scroll.Viewport.Height;
            Assert.Equal(end, scroll.Offset.Y, .5);
            Assert.Equal("third", toc.ActiveKey);

            scroll.Offset = new Vector(0, 12);
            UpdateLayout();
            Assert.Equal("first", toc.ActiveKey);

            scroll.Offset = new Vector(0, end);
            UpdateLayout();
            Assert.Equal("last", toc.ActiveKey);
        }
        finally { window.Close(); }

        void UpdateLayout()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
