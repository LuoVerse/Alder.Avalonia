using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alder.Avalonia.Controls;
using Xunit;

namespace Alder.Avalonia.Tests;

public class WindowInteractions
{
    [AvaloniaFact]
    public async Task SalashWindowShowsTheReplacementBeforeClosingAndReleasesItsOwnHosts()
    {
        var next = new AlderWindow { Width = 480, Height = 320 };
        var handoff = new TaskCompletionSource<Window?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = new List<DialogResult>();
        var context = new TestDialogContext();
        var splash = new TestSalashWindow(result => { results.Add(result); return handoff.Task; }) { DataContext = context };
        AlderMotion.SetReduced(splash, true);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        splash.Closed += (_, _) => closed.TrySetResult();
        splash.Show();
        try
        {
            Pump();
            var toast = Toast.Show(new("Startup") { Duration = TimeSpan.Zero }, splash);
            var message = MessageBox.ShowAsync("Startup", owner: splash);
            ToastHandle? nextToast = null;
            next.Opened += (_, _) =>
            {
                Assert.True(splash.IsVisible);
                nextToast = Toast.Show(new("Main window") { Duration = TimeSpan.Zero }, next);
            };

            Assert.Empty(results);
            context.Complete(DialogResult.OK);
            context.Complete(DialogResult.Cancel);
            Pump();
            Assert.Equal([DialogResult.OK], results);
            Assert.True(splash.IsVisible);
            handoff.SetResult(next);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(next.IsVisible);
            Assert.Null(splash.DefaultDialogHost);
            Assert.Null(ToastHost.Find(splash, null));
            Assert.Equal(DialogResult.None, await message);
            Assert.Equal(ToastCloseReason.HostRemoved, await toast.Completion);
            Assert.NotNull(next.DefaultDialogHost);
            Assert.NotNull(nextToast);
            Assert.False(nextToast.Completion.IsCompleted);
        }
        finally
        {
            handoff.TrySetResult(null);
            if (splash.IsVisible) splash.Close();
            if (next.IsVisible) next.Close();
        }
    }

    [AvaloniaFact]
    public async Task SalashWindowRetainsItsHostsWhenTheFactoryFailsAndCanRetry()
    {
        var failure = new InvalidOperationException("Factory failed");
        var results = new List<DialogResult>();
        var context = new TestDialogContext();
        var splash = new TestSalashWindow(result =>
        {
            results.Add(result);
            return results.Count == 1 ? Task.FromException<Window?>(failure) : Task.FromResult<Window?>(null);
        }) { DataContext = context };
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        splash.TransitionFailed += (_, error) => failed.TrySetResult(error);
        splash.Closed += (_, _) => closed.TrySetResult();
        splash.Show();
        try
        {
            var toast = Toast.Show(new("Startup") { Duration = TimeSpan.Zero }, splash);
            context.Complete(DialogResult.OK);
            Assert.Same(failure, await failed.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(splash.IsVisible);
            Assert.NotNull(splash.DefaultDialogHost);
            Assert.False(toast.Completion.IsCompleted);
            splash.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(ToastCloseReason.HostRemoved, await toast.Completion);
            Assert.Equal([DialogResult.OK, DialogResult.None], results);
        }
        finally { if (splash.IsVisible) splash.Close(); }
    }

    [AvaloniaFact]
    public async Task SalashWindowTracksTheCurrentViewModelAndReturnsItsModalResult()
    {
        var oldContext = new TestDialogContext();
        var context = new TestDialogContext();
        var results = new List<DialogResult>();
        var splash = new TestSalashWindow(result => { results.Add(result); return Task.FromResult<Window?>(null); })
        {
            DataContext = oldContext
        };
        var owner = new AlderWindow();
        owner.Show();
        try
        {
            var completion = splash.ShowDialog<DialogResult>(owner);
            Assert.True(oldContext.HasCloseSubscribers);
            oldContext.Complete(DialogResult.OK);
            splash.DataContext = context;
            Assert.False(oldContext.HasCloseSubscribers);
            Pump();
            Assert.Empty(results);
            Assert.True(splash.IsVisible);

            await Task.Run(() => context.Complete(DialogResult.Cancel));
            Assert.Equal(DialogResult.Cancel, await completion.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Equal([DialogResult.Cancel], results);
            Assert.False(context.HasCloseSubscribers);
        }
        finally { owner.Close(); }
    }

    private sealed class TestSalashWindow(Func<DialogResult, Task<Window?>> createNextWindow) : AlderSalashWindow
    {
        protected override Task<Window?> CreateNextWindow() => createNextWindow(CloseResult);
    }

    private sealed class TestDialogContext : IDialogContext
    {
        public event EventHandler<DialogResult>? RequestClose;
        public bool HasCloseSubscribers => RequestClose is not null;
        public void Complete(DialogResult result) => RequestClose?.Invoke(this, result);
    }

    [AvaloniaFact]
    public async Task WindowProvidesHostsBeforeOpenedAndKeepsNamedHostsIndependent()
    {
        var owner = new AlderButton { Content = "Page" };
        var named = new DialogHost { HostId = "local", Content = new TextBlock { Text = "Nested region" } };
        var window = new AlderWindow { Width = 560, Height = 440, Content = new StackPanel { Children = { owner, named } } };
        AlderMotion.SetReduced(window, true);
        ToastHandle? toast = null;
        window.Opened += (_, _) => toast = Toast.Show(new("Default queue") { Duration = TimeSpan.Zero }, owner);
        window.Show();
        try
        {
            var defaultDialog = Part<DialogHost>(window, "PART_DialogHost");
            var defaultToast = Assert.Single(window.GetVisualDescendants().OfType<ToastHost>());
            Assert.Same(defaultToast, Toast.Register(window));
            var namedToast = Toast.Register(window, "secondary");
            Assert.NotSame(defaultToast, namedToast);
            window.ToastSafePadding = new Thickness(11, 22, 33, 44);
            Assert.Equal(window.ToastSafePadding, defaultToast.SafePadding);
            Assert.NotEqual(window.ToastSafePadding, namedToast.SafePadding);

            var dialog = new AlderButton { Content = "Dialog" };
            var dialogResult = Dialog.ShowModalAsync(dialog, owner: owner);
            Pump();
            Assert.Same(defaultDialog, dialog.FindAncestorOfType<DialogHost>());
            Assert.True(Dialog.Close(dialog, DialogResult.OK));
            Assert.Equal(DialogResult.OK, await dialogResult);

            var drawer = new AlderButton { Content = "Drawer" };
            var drawerResult = Drawer.ShowAsync(drawer, owner: window, hostId: "local");
            Pump();
            Assert.Same(named, drawer.FindAncestorOfType<DialogHost>());
            Assert.True(Dialog.Close(drawer));
            Assert.Equal(DialogResult.Cancel, await drawerResult);

            var sheet = new AlderButton { Content = "Sheet" };
            var sheetResult = Sheet.ShowAsync(sheet, owner: window);
            Pump();
            Assert.Same(defaultDialog, sheet.FindAncestorOfType<DialogHost>());
            Assert.True(Dialog.Close(sheet));
            Assert.Equal(DialogResult.Cancel, await sheetResult);

            var message = MessageBox.ShowAsync("Confirmation", owner: window);
            Pump();
            var text = Assert.Single(defaultDialog.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Confirmation");
            Assert.True(Dialog.Close(text, DialogResult.OK));
            Assert.Equal(DialogResult.OK, await message);

            var pendingDialog = Dialog.ShowModalAsync(new TextBlock(), owner: window);
            window.Close();
            Assert.Equal(DialogResult.None, await pendingDialog);
            Assert.NotNull(toast);
            Assert.Equal(ToastCloseReason.HostRemoved, await toast.Completion);
        }
        finally { if (window.IsVisible) window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingOneAlderWindowKeepsTheOtherWindowsQueue()
    {
        var left = new AlderWindow { Width = 400, Height = 300 };
        var right = new AlderWindow { Width = 400, Height = 300 };
        AlderMotion.SetReduced(left, true);
        AlderMotion.SetReduced(right, true);
        left.Show();
        right.Show();
        try
        {
            var notice = new ToastOptions("Pending") { Duration = TimeSpan.Zero };
            var first = Toast.Show(notice, left);
            var second = Toast.Show(notice, right);
            left.Close();
            Assert.Equal(ToastCloseReason.HostRemoved, await first.Completion);
            Assert.False(second.Completion.IsCompleted);
            second.Close();
            Assert.Equal(ToastCloseReason.Dismissed, await second.Completion);
        }
        finally
        {
            if (left.IsVisible) left.Close();
            right.Close();
        }
    }

    [AvaloniaFact]
    public void CaptionButtonsFollowWindowStateAndCapabilities()
    {
        var window = new AlderWindow { Width = 480, Height = 320, Title = "Window", CanResize = false };
        window.Classes.Remove("mac");
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            var maximize = Part<Button>(window, "PART_MaximizeButton");
            var minimize = Part<Button>(window, "PART_MinimizeButton");
            var close = Part<Button>(window, "PART_CloseButton");
            var grips = Part<Panel>(window, "PART_ResizeGrips");
            var icon = Assert.IsType<AlderPathIcon>(maximize.Content);
            Assert.False(maximize.IsEnabled);
            Assert.False(grips.IsVisible);
            var maximizeIcon = icon.Data;
            Assert.NotNull(maximizeIcon);

            window.CanResize = true;
            window.CanMinimize = false;
            Assert.False(minimize.IsEnabled);
            Assert.True(maximize.IsEnabled);
            Click(window, maximize);
            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal("Restore window", AutomationProperties.GetName(maximize));
            Assert.NotSame(maximizeIcon, icon.Data);
            Assert.False(grips.IsVisible);

            Click(window, maximize);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Same(maximizeIcon, icon.Data);
            Assert.True(grips.IsVisible);
            window.CanMinimize = true;
            Click(window, minimize);
            Assert.Equal(WindowState.Minimized, window.WindowState);
            window.WindowState = WindowState.Normal;

            bool closed = false;
            window.Closed += (_, _) => closed = true;
            Click(window, close);
            Assert.True(closed);
        }
        finally { if (window.IsVisible) window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SalashWindowUsesAlderWindowCaptionButtons(bool mac)
    {
        var window = new AlderWindow { Width = 500, Height = 460 };
        var input = new AlderInput();
        var action = new AlderButton { Content = "Sign in" };
        var splash = new TestSalashWindow(_ => Task.FromResult<Window?>(null))
        {
            Width = 500, Height = 460, Content = new StackPanel { Children = { input, action } }
        };
        window.Classes.Set("mac", mac);
        splash.Classes.Set("mac", mac);
        AlderMotion.SetReduced(splash, true);
        window.Show();
        splash.Show();
        try
        {
            Pump();
            var prefix = mac ? "PART_Mac" : "PART_";
            foreach (var name in new[] { "CloseButton", "MinimizeButton", "MaximizeButton" })
            {
                var expected = Part<Button>(window, prefix + name);
                var actual = Part<Button>(splash, prefix + name);
                Assert.True(actual.IsEffectivelyVisible);
                Assert.Same(expected.Theme, actual.Theme);
                Assert.Equal(expected.TranslatePoint(default, window), actual.TranslatePoint(default, splash));
                Assert.False(expected.IsTabStop);
                Assert.False(actual.IsTabStop);
                Assert.Null(actual.FocusAdorner);
            }

            Assert.True(input.Focus());
            Tab();
            Assert.True(action.IsFocused);
            Tab();
            Assert.True(input.IsFocused);
            Tab(RawInputModifiers.Shift);
            Assert.True(action.IsFocused);

            var maximize = Part<Button>(splash, prefix + "MaximizeButton");
            var frame = Part<Border>(splash, "PART_WindowFrame");
            var outline = Part<Border>(splash, "PART_WindowOutline");
            Assert.Equal(new CornerRadius(18), frame.CornerRadius);
            Assert.True(outline.IsVisible);
            Click(splash, maximize);
            Assert.Equal(WindowState.Maximized, splash.WindowState);
            Assert.Equal(default, frame.CornerRadius);
            Assert.False(outline.IsVisible);
            Click(splash, maximize);
            Assert.Equal(WindowState.Normal, splash.WindowState);
            Assert.Equal(new CornerRadius(18), frame.CornerRadius);
            Click(splash, Part<Button>(splash, prefix + "CloseButton"));
            Assert.False(splash.IsVisible);
        }
        finally
        {
            if (splash.IsVisible) splash.Close();
            window.Close();
            Pump();
        }

        void Tab(RawInputModifiers modifiers = RawInputModifiers.None)
        {
            splash.KeyPressQwerty(PhysicalKey.Tab, modifiers);
            splash.KeyReleaseQwerty(PhysicalKey.Tab, modifiers);
            Pump();
        }
    }

    [AvaloniaFact]
    public void RemovingDecorationOverridesRestoresCustomChrome()
    {
        var window = new AlderWindow { Width = 480, Height = 320 };
        var decorated = new Style(selector => selector.OfType<AlderWindow>())
        {
            Setters = { new Setter(Window.WindowDecorationsProperty, WindowDecorations.Full) }
        };
        window.Styles.Add(decorated);
        window.Show();
        try
        {
            Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
            Assert.DoesNotContain(":custom-chrome", window.Classes);

            window.Styles.Remove(decorated);
            Assert.Equal(WindowDecorations.None, window.WindowDecorations);
            Assert.Contains(":custom-chrome", window.Classes);

            window.WindowDecorations = WindowDecorations.BorderOnly;
            Assert.DoesNotContain(":custom-chrome", window.Classes);
            window.ClearValue(Window.WindowDecorationsProperty);
            Assert.Equal(WindowDecorations.None, window.WindowDecorations);
            Assert.Contains(":custom-chrome", window.Classes);
        }
        finally { window.Close(); }
    }

    public static bool UsesMacResizeGrips => OperatingSystem.IsMacOS();

    [AvaloniaFact(Skip = "The managed borderless resize grips are used on macOS.", SkipUnless = nameof(UsesMacResizeGrips))]
    public void MacResizeCoalescesPointerMovesAndAppliesTheReleasePosition()
    {
        var window = new AlderWindow { Width = 480, Height = 320, MaxWidth = 580, MaxHeight = 400 };
        window.Show();
        try
        {
            Pump();
            var initial = window.ClientSize;
            var corner = new Point(initial.Width - 2, initial.Height - 2);
            var sizes = new List<Size>();
            window.Resized += (_, e) => sizes.Add(e.ClientSize);
            var grips = Part<Panel>(window, "PART_ResizeGrips");
            PointerPressedEventArgs? pressed = null;
            grips.AddHandler(InputElement.PointerPressedEvent, (_, e) => pressed = e, RoutingStrategies.Bubble, handledEventsToo: true);
            window.MouseDown(corner, MouseButton.Left);
            Assert.NotNull(pressed);
            var properties = pressed.GetCurrentPoint(window).Properties;
            // Dispatch a burst before advancing the render timer. The headless mouse helpers pump each input.
            foreach (var movement in new[] { new Vector(30, 20), new Vector(45, 34), new Vector(90, 60) })
                grips.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, grips, pressed.Pointer,
                    window, corner + movement, pressed.Timestamp, properties, KeyModifiers.None));
            Assert.Empty(sizes);
            Pump();
            Assert.Equal([new Size(570, 380)], sizes);

            window.MouseUp(corner + new Vector(125, 115), MouseButton.Left);
            Pump();
            Assert.Equal(new Size(580, 400), window.ClientSize);

            var position = window.Position;
            window.MouseDown(new Point(2, 2), MouseButton.Left);
            window.MouseUp(new Point(32, 22), MouseButton.Left);
            Pump();
            Assert.Equal(new Size(550, 380), window.ClientSize);
            var scale = window.DesktopScaling;
            Assert.Equal(new PixelPoint(position.X + (int)Math.Round(30 * scale), position.Y + (int)Math.Round(20 * scale)), window.Position);
        }
        finally { window.Close(); }
    }

    private static T Part<T>(AlderWindow window, string name) where T : Control =>
        Assert.Single(window.GetVisualDescendants().OfType<T>(), part => part.Name == name);

    private static void Click(AlderWindow window, Button button)
    {
        Pump();
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
