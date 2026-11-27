using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class IconCompatibilityInteractions
{
    [AvaloniaFact]
    public void ChipSwitchesBetweenTheVectorFastPathAndAnIconControl()
    {
        var geometry = Geometry.Parse("M0 0H16V16H0Z");
        var chip = new AlderChip { Icon = geometry, Content = "Status" };
        var window = Show(chip);
        try
        {
            var slot = Assert.Single(chip.GetVisualDescendants().OfType<AlderChipIcon>(), icon => icon.IsVisible);
            Assert.Same(geometry, slot.Icon);
            Assert.Null(slot.Child);
            var icon = new Border { Width = 24, Height = 18, Background = Brushes.Pink };
            chip.Icon = icon;
            chip.Size = AlderSize.Small;
            Pump(window);
            Assert.Contains(icon, slot.GetVisualDescendants());
            Assert.Equal(chip.IconSize, slot.Bounds.Width);
            chip.Icon = geometry;
            Pump(window);
            Assert.Same(geometry, slot.Icon);
            Assert.Null(slot.Child);
            Assert.Null(icon.GetVisualParent());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RatingSharesOneIconSourceAndReusesItAfterResizing()
    {
        var icon = new Border { Width = 40, Height = 24, Background = Brushes.Magenta };
        var rating = new AlderRating { Count = 4, Value = 2.5, Icon = icon };
        var window = Show(rating);
        try
        {
            Check(20);
            rating.Size = AlderSize.Large;
            Pump(window);
            Check(28);
            void Check(double extent)
            {
                Assert.Single(rating.GetVisualDescendants(), part => ReferenceEquals(part, icon));
                var layers = rating.GetVisualDescendants().OfType<Border>().Where(part => part.OpacityMask is VisualBrush).ToArray();
                Assert.Equal(8, layers.Length);
                var mask = Assert.IsType<VisualBrush>(layers[0].OpacityMask);
                Assert.All(layers, part => Assert.Same(mask, part.OpacityMask));
                Assert.Equal(new Size(extent, extent), mask.Visual!.Bounds.Size);
                Assert.Contains(layers,
                    layer => layer.Clip is RectangleGeometry clip && clip.Rect == new Rect(0, 0, extent / 2, extent));
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BloomCaptionMirrorsTheLiveIconDuringOpenAndReleasesReplacedControl()
    {
        var icon = new Border { Width = 22, Height = 18, Background = Brushes.Magenta };
        var menu = new AlderBloomMenu { Header = "Actions", Icon = icon, Items = { new AlderBloomMenuItem { Content = "Open" } } };
        var window = Show(menu);
        try
        {
            menu.IsOpen = true;
            Pump(window);
            var host = Assert.Single(window.GetVisualDescendants().OfType<AlderBloomMenuSurface>());
            var caption = Assert.Single(host.GetVisualDescendants().OfType<AlderBloomMenuCaption>());
            var mirror = Assert.IsType<Border>(caption.Icon);
            var brush = Assert.IsType<VisualBrush>(mirror.Background);
            Assert.True(brush.Visual!.IsVisualAncestorOf(icon));
            Assert.Single(window.GetVisualDescendants(), part => ReferenceEquals(part, icon));

            var replacement = Geometry.Parse("M0 0H16V16H0Z");
            menu.Icon = replacement;
            Pump(window);
            Assert.Null(icon.GetVisualParent());
            Assert.Same(replacement, caption.Icon);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DetachedIconCanBeReusedInANewContainer()
    {
        var icon = new Border { Width = 24, Height = 24 };
        var previous = new AlderButton { Icon = icon };
        var panel = new StackPanel { Children = { previous } };
        var window = Show(panel);
        try
        {
            Assert.True(previous.IsVisualAncestorOf(icon));
            panel.Children.Clear();
            Assert.Null(icon.GetVisualParent());

            var next = new AlderItemCard { Icon = icon, Size = AlderSize.Small };
            panel.Children.Add(next);
            Pump(window);
            Assert.True(next.IsVisualAncestorOf(icon));
            next.Size = AlderSize.Large;
            Pump(window);
            var slot = Assert.Single(next.GetVisualDescendants().OfType<AlderIconPresenter>());
            Assert.Equal(new Size(20, 20), slot.Bounds.Size);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AnExplicitIconTemplateReceivesTheOriginalData()
    {
        const string payload = "M0 0H16V16H0Z";
        var item = new AlderItemCard
        {
            Icon = payload,
            IconTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text }),
        };
        var window = Show(item);
        try
        {
            var slot = Assert.Single(item.GetVisualDescendants().OfType<AlderIconPresenter>());
            Assert.Equal(payload, Assert.Single(slot.GetVisualDescendants().OfType<TextBlock>()).Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MarkdownActionIconTransfersWhenTheFirstBlockIsRemoved()
    {
        var icon = new Border { Width = 24, Height = 24 };
        var first = new Alder.NativeMarkdown.UI.Rendering.IconButton { Icon = icon };
        var second = new Alder.NativeMarkdown.UI.Rendering.IconButton { Icon = icon };
        var panel = new StackPanel { Children = { first, second } };
        var window = Show(panel);
        try
        {
            Assert.True(first.IsVisualAncestorOf(icon));
            Assert.Contains(second.GetVisualDescendants().OfType<Border>(), part => part.Background is VisualBrush);
            panel.Children.Remove(first);
            Pump(window);
            Assert.True(second.IsVisualAncestorOf(icon));
        }
        finally { window.Close(); }
    }

    private static Window Show(Control control)
    {
        var window = new Window { Width = 420, Height = 320, Content = control };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
