using System.Diagnostics;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class OverflowActionsInteractions
{
    [AvaloniaFact]
    public async Task ToggleSpringsOpenAndCanReverseWhileClosing()
    {
        var overflow = new AlderOverflowAction { Content = "Schedule publication" };
        var bar = new AlderOverflowActions
        {
            PrimaryActions = { new AlderOverflowAction { Content = "Preview" } },
            OverflowActions = { overflow }
        };
        var window = Show(bar);
        try
        {
            var toggle = Find<AlderOverflowActionsToggle>(bar, "PART_Toggle");
            var extra = Find<ItemsControl>(bar, "PART_Overflow");
            var peer = Assert.IsAssignableFrom<IExpandCollapseProvider>(ControlAutomationPeer.CreatePeerForElement(toggle));
            var collapsedWidth = bar.Bounds.Width;
            Assert.Equal(ExpandCollapseState.Collapsed, peer.ExpandCollapseState);
            Assert.False(extra.IsVisible);

            Click(toggle, window);
            Assert.True(bar.IsExpanded);
            Assert.Equal(ExpandCollapseState.Expanded, peer.ExpandCollapseState);
            await WaitFor(window, () => bar.Bounds.Width > collapsedWidth + 10);
            Assert.InRange(extra.Opacity, .01, .99);

            double peakWidth = bar.Bounds.Width;
            await WaitFor(window, () =>
            {
                peakWidth = Math.Max(peakWidth, bar.Bounds.Width);
                return extra.Opacity == 1 && Find<AlderPathIcon>(bar, "CloseIcon").Effect is null &&
                    peakWidth > bar.Bounds.Width + 2;
            });
            await Settle(window);
            var expandedWidth = bar.Bounds.Width;
            Assert.True(peakWidth > expandedWidth + 2);

            peer.Collapse();
            Assert.False(extra.IsHitTestVisible);
            await WaitFor(window, () => bar.Bounds.Width < expandedWidth - 12);
            peer.Expand();
            await Settle(window);
            Assert.Equal(expandedWidth, bar.Bounds.Width);
            Assert.Equal(1, Find<AlderPathIcon>(bar, "CloseIcon").Opacity);
            Assert.Equal(0, Find<AlderPathIcon>(bar, "MoreIcon").Opacity);

            peer.Collapse();
            await WaitFor(window, () => !extra.IsVisible);
            Assert.Equal(collapsedWidth, bar.Bounds.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CompactRailKeepsItsToggleVisibleAndRestoresFocus()
    {
        var primary = new AlderOverflowAction { Content = "Primary action" };
        var disabled = new AlderOverflowAction { Content = "Unavailable", IsEnabled = false };
        var overflow = new AlderOverflowAction { Content = "Schedule an entire collection" };
        var bar = new AlderOverflowActions
        {
            Size = AlderSize.Small, MaxWidth = 220,
            PrimaryActions = { primary }, OverflowActions = { disabled, overflow }
        };
        var window = Show(bar, reduced: true);
        try
        {
            var toggle = Find<AlderOverflowActionsToggle>(bar, "PART_Toggle");
            var viewport = Find<AlderScrollViewer>(bar, "PART_Scroller");
            Assert.Equal(32, toggle.Bounds.Height);
            Assert.Equal(32, primary.Bounds.Height);
            Assert.True(toggle.Focus());
            Key(window, PhysicalKey.Enter);
            Assert.True(bar.IsExpanded);
            Assert.True(viewport.Extent.Width > viewport.Viewport.Width);
            await Settle(window);

            double toggleX = Center(toggle, window).X;
            Assert.True(primary.Focus(NavigationMethod.Tab));
            Key(window, PhysicalKey.ArrowRight);
            Assert.True(overflow.IsFocused);
            Assert.True(viewport.Offset.X > 0);
            Assert.Equal(toggleX, Center(toggle, window).X);
            Assert.Null(Find<ItemsControl>(bar, "PART_Overflow").Effect);

            Key(window, PhysicalKey.Escape);
            Assert.False(bar.IsExpanded);
            Assert.True(toggle.IsFocused);
            Assert.Equal("Show extra actions", ControlAutomationPeer.CreatePeerForElement(toggle).GetName());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OverflowActionsRunTheirCommandBeforeAutomaticCollapse()
    {
        var bar = new AlderOverflowActions { CollapseOnAction = true };
        var action = new AlderOverflowAction { Content = "Pin" };
        var trace = new List<string>();
        action.Command = new TestCommand(() => trace.Add(bar.IsExpanded ? "command" : "collapsed"));
        bar.PrimaryActions.Add(new AlderOverflowAction { Content = "Preview" });
        var window = Show(bar, reduced: true);
        try
        {
            var toggle = Find<AlderOverflowActionsToggle>(bar, "PART_Toggle");
            Assert.False(toggle.IsVisible);
            bar.OverflowActions.Add(action);
            Pump(window);
            Assert.True(toggle.IsVisible);
            bar.ActionInvoked += (_, args) =>
            {
                Assert.Same(action, args.Action);
                Assert.True(bar.IsExpanded);
                trace.Add("event");
            };

            Tap(toggle);
            Assert.True(bar.IsExpanded);
            Tap(action);
            Assert.False(bar.IsExpanded);
            Assert.Equal(["command", "event"], trace);
            action.InvokeConfirmedClick();
            Assert.Equal(2, trace.Count);

            bar.OverflowActions.Clear();
            Pump(window);
            Assert.False(toggle.IsVisible);
        }
        finally { window.Close(); }

        void Tap(Control control)
        {
            var point = Center(control, window);
            using var contact = window.TouchBegin(point);
            window.TouchEnd(contact, point);
            Pump(window);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void ClosingAfterAnActionPreservesTheInputModality(bool startWithKeyboardFocus, bool invokeWithKeyboard)
    {
        var action = new AlderOverflowAction { Content = "Archive" };
        var bar = new AlderOverflowActions
        {
            Size = AlderSize.Small, CollapseOnAction = true,
            PrimaryActions = { new AlderOverflowAction { Content = "Copy" } },
            OverflowActions = { action }
        };
        var window = Show(bar, reduced: true);
        try
        {
            var toggle = Find<AlderOverflowActionsToggle>(bar, "PART_Toggle");
            Click(toggle, window);
            Assert.True(bar.IsExpanded);
            if (startWithKeyboardFocus || invokeWithKeyboard)
                Assert.True(action.Focus(startWithKeyboardFocus ? NavigationMethod.Tab : NavigationMethod.Pointer));
            if (invokeWithKeyboard)
                Key(window, PhysicalKey.Enter);
            else
                Click(action, window);

            Assert.False(bar.IsExpanded);
            Assert.True(toggle.IsFocused);
            window.MouseMove(new Point(window.ClientSize.Width - 1, window.ClientSize.Height - 1));
            Pump(window);
            Assert.Equal(invokeWithKeyboard, Find<ButtonTint>(toggle, "Tint").Opacity > 0);
        }
        finally { window.Close(); }
    }

    private sealed class TestCommand(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }

    private static Window Show(Control content, bool reduced = false)
    {
        var window = new Window { Width = 650, Height = 220, Content = content };
        AlderMotion.SetReduced(window, reduced);
        window.Show();
        Pump(window);
        return window;
    }

    private static T Find<T>(Control owner, string name) where T : Control =>
        owner.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Click(Control control, Window window)
    {
        var point = Center(control, window);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(window);
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump(window);
    }

    private static async Task WaitFor(Window window, Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.ElapsedMilliseconds < 1400)
        {
            await Task.Delay(12);
            Pump(window);
        }
        Assert.True(condition());
    }

    private static async Task Settle(Window window)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 700)
        {
            await Task.Delay(15);
            Pump(window);
        }
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
