using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ExpandableActionBarInteractions
{
    [AvaloniaFact]
    public async Task MouseRevealsLabelsAndAnimatesTheHoveredIcon()
    {
        var send = Item("Send", AlderActionIconMotion.Send);
        var bell = Item("Alerts", AlderActionIconMotion.Bell);
        var bar = new AlderExpandableActionBar { Items = { send, bell }, SelectedIndex = 0 };
        var window = Show(bar);
        try
        {
            var icon = send.GetVisualDescendants().OfType<AlderIconPresenter>().Single(part => part.Name == "PART_Icon");
            double collapsed = bar.Bounds.Width;
            Assert.Contains(":highlighted", send.Classes);

            // Approach from the center so the same button remains under the pointer as it grows.
            window.MouseMove(Center(bell, window));
            Pump(window);
            Assert.True(bar.IsExpanded);
            await WaitFor(window, () => bar.Bounds.Width > collapsed + 5);
            Assert.InRange(bar.GetValue(AlderExpandableActionBar.RevealProperty), .001, .999);

            await WaitFor(window, () => bar.GetValue(AlderExpandableActionBar.RevealProperty) == 1);
            window.MouseMove(Center(send, window));
            Pump(window);
            Assert.True(send.IsPointerOver);
            await WaitFor(window, () => icon.RenderTransform?.Value.M31 > .5);

            window.MouseMove(Center(bell, window));
            Pump(window);
            Assert.Contains(":highlighted", bell.Classes);
            Assert.DoesNotContain(":highlighted", send.Classes);
            Assert.True(icon.RenderTransform?.Value.IsIdentity ?? true);

            window.MouseMove(new Point(3, 3));
            Pump(window);
            Assert.True(bar.IsExpanded); // The exit delay lets the pointer re-enter.
            await WaitFor(window, () => !bar.IsExpanded);
            await WaitFor(window, () => bar.GetValue(AlderExpandableActionBar.RevealProperty) == 0);
            Assert.Equal(collapsed, bar.Bounds.Width);
            Assert.Contains(":highlighted", send.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PointerKeepsTheLastHoveredActionWhileInTrackWhitespace(bool bottomPadding, bool reduced)
    {
        var first = Item("Send", AlderActionIconMotion.Send);
        var copy = Item("Copy");
        var export = Item("Export");
        var bar = new AlderExpandableActionBar { Items = { first, copy, export }, SelectedIndex = 0 };
        var window = Show(bar, reduced);
        try
        {
            window.MouseMove(Center(copy, window));
            await WaitFor(window, () => bar.GetValue(AlderExpandableActionBar.RevealProperty) == 1);

            window.MouseMove(Center(copy, window));
            Pump(window);
            Assert.Same(copy, bar.HighlightedItem);
            var copyRight = copy.TranslatePoint(new Point(copy.Bounds.Width, 0), window)!.Value.X;
            var exportLeft = export.TranslatePoint(default, window)!.Value.X;
            var copyBottom = copy.TranslatePoint(new Point(0, copy.Bounds.Height), window)!.Value.Y;
            var barBottom = bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y;
            var center = Center(copy, window);
            var whitespace = bottomPadding
                ? new Point(center.X, (copyBottom + barBottom) / 2)
                : new Point((copyRight + exportLeft) / 2, center.Y);

            window.MouseMove(whitespace);
            Pump(window);
            Assert.True(bar.IsPointerOver);
            Assert.False(copy.IsPointerOver);
            Assert.True(bar.IsExpanded);
            Assert.Same(copy, bar.HighlightedItem);
            Assert.DoesNotContain(":highlighted", first.Classes);
            Assert.Equal(0, bar.SelectedIndex);

            window.MouseMove(Center(export, window));
            Pump(window);
            Assert.Same(export, bar.HighlightedItem);

            window.MouseMove(new Point(3, 3));
            Pump(window);
            Assert.Same(first, bar.HighlightedItem);
            await WaitFor(window, () => !bar.IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(AlderActionIconMotion.Copy)]
    [InlineData(AlderActionIconMotion.Download)]
    [InlineData(AlderActionIconMotion.Archive)]
    [InlineData(AlderActionIconMotion.Bell)]
    [InlineData(AlderActionIconMotion.Settings)]
    public async Task PresetPlaysOnceThenReturnsToRest(AlderActionIconMotion motion)
    {
        var item = Item("Action", motion);
        var bar = new AlderExpandableActionBar
        {
            ExpandOnHover = false, ExpandOnFocus = false, IsExpanded = true, Items = { item }
        };
        var window = Show(bar);
        try
        {
            var icon = item.GetVisualDescendants().OfType<AlderIconPresenter>().Single(part => part.Name == "PART_Icon");
            window.MouseMove(Center(item, window));
            Pump(window);
            await WaitFor(window, () =>
            {
                var matrix = icon.RenderTransform?.Value ?? Matrix.Identity;
                return motion switch
                {
                    AlderActionIconMotion.Copy => matrix.M31 > 1 && matrix.M32 < -1,
                    AlderActionIconMotion.Download => matrix.M32 > 2,
                    AlderActionIconMotion.Archive => matrix.M11 < .94,
                    AlderActionIconMotion.Bell => Math.Abs(matrix.M12) > .15,
                    AlderActionIconMotion.Settings => Math.Abs(matrix.M12) > .8,
                    _ => false
                };
            });
            await WaitFor(window, () => icon.RenderTransform is null);
            Assert.True(item.IsPointerOver);
            window.MouseMove(new Point(3, 3));
            Pump(window);
            AlderMotion.SetReduced(window, true);
            window.MouseMove(Center(item, window));
            Pump(window);
            Assert.Null(icon.RenderTransform);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CompactBarKeepsTheLastActionReachableByKeyboard()
    {
        var first = Item("First document");
        var disabled = Item("Unavailable export");
        disabled.IsEnabled = false;
        var last = Item("Download selected document");
        int invoked = 0;
        last.Click += (_, _) => invoked++;
        var bar = new AlderExpandableActionBar
        {
            Size = AlderExpandableActionBarSize.Small, MaxWidth = 174, CollapseDelay = TimeSpan.Zero,
            Items = { first, disabled, last }
        };
        var window = Show(bar, reduced: true);
        try
        {
            Assert.Equal(28, first.Bounds.Height);
            Assert.True(first.Focus(NavigationMethod.Tab));
            Pump(window);
            Assert.True(bar.IsExpanded);
            Assert.Equal(1, bar.GetValue(AlderExpandableActionBar.RevealProperty));
            var scroll = bar.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(scroll.Extent.Width > scroll.Viewport.Width);

            Key(window, PhysicalKey.ArrowRight);
            Assert.True(last.IsFocused);
            Assert.True(scroll.Offset.X > 0);
            Key(window, PhysicalKey.Enter);
            Assert.Equal(1, invoked);
            Assert.Equal(2, bar.SelectedIndex);

            Key(window, PhysicalKey.Escape);
            Assert.False(bar.IsExpanded);
            Assert.Equal(0, bar.GetValue(AlderExpandableActionBar.RevealProperty));
            Assert.True(last.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FirstTouchRevealsBeforeInvokingAndOutsideTouchDismisses()
    {
        var item = Item("Copy");
        var outside = new Button { Content = "Outside", HorizontalAlignment = HorizontalAlignment.Left };
        var bar = new AlderExpandableActionBar { CollapseDelay = TimeSpan.Zero, Items = { item } };
        int invoked = 0, outsideClicks = 0;
        item.Click += (_, _) => invoked++;
        outside.Click += (_, _) => outsideClicks++;
        var window = Show(new Grid { Children = { outside, bar } }, reduced: true);
        try
        {
            Tap(item);
            Assert.True(bar.IsExpanded);
            Assert.Equal(0, invoked);
            Tap(item);
            Assert.Equal(1, invoked);
            Tap(outside);
            Assert.False(bar.IsExpanded);
            Assert.Equal(0, outsideClicks);

            bar.ExpandOnHover = false;
            Tap(item);
            Assert.False(bar.IsExpanded);
            Assert.Equal(2, invoked);
        }
        finally { window.Close(); }

        void Tap(Control control)
        {
            var center = Center(control, window);
            using var contact = window.TouchBegin(center);
            window.TouchEnd(contact, center);
            Pump(window);
        }
    }

    private static AlderExpandableActionBarItem Item(string label, AlderActionIconMotion motion = AlderActionIconMotion.None) =>
        new() { Content = label, Icon = Geometry.Parse("M1,1 L15,1 L15,15 L1,15 Z"), IconMotion = motion };

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static Window Show(Control content, bool reduced = false)
    {
        var window = new Window { Width = 530, Height = 240, Content = content };
        AlderMotion.SetReduced(window, reduced);
        window.Show();
        Pump(window);
        return window;
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
    }

    private static async Task WaitFor(Window window, Func<bool> condition)
    {
        for (int frame = 0; frame < 75 && !condition(); frame++)
        {
            await Task.Delay(12);
            Pump(window);
        }
        Assert.True(condition());
    }
}
