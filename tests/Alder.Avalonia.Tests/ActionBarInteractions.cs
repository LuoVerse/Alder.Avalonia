using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
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

public sealed class ActionBarInteractions
{
    [AvaloniaTheory]
    [InlineData(0d)]
    [InlineData(16d)]
    public void ActionBarRestoresFocusAndLeavesInputOutsideTheSurfaceAvailable(double blurRadius)
    {
        int outsideClicks = 0;
        var underlying = new AlderButton { Content = "Select", HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12) };
        underlying.Click += (_, _) => outsideClicks++;
        var first = new AlderButton { Content = "Edit" };
        var second = new AlderButton { Content = "Export" };
        var close = new AlderButton { Content = "Clear" };
        var bar = new AlderActionBar
        {
            Prefix = new TextBlock { Text = "2" }, Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } },
            Suffix = close, SurfaceBackdropBlurRadius = blurRadius
        };
        using var preview = new ActionBarWindow(new Grid { Children = { underlying, bar } });
        var surface = Part<Border>(bar, "PART_Surface");
        Assert.False(surface.IsVisible);
        Assert.False(bar.ClipToBounds);
        Assert.False(surface.ClipToBounds);
        Assert.True(underlying.Focus());

        bar.IsOpen = true;
        preview.Pump();
        Assert.True(surface.IsVisible && surface.IsHitTestVisible);
        preview.Click(underlying);
        Assert.Equal(1, outsideClicks);
        Assert.True(first.Focus());
        preview.Key(PhysicalKey.ArrowRight);
        Assert.True(second.IsFocused);
        preview.Key(PhysicalKey.ArrowRight);
        Assert.True(close.IsFocused);
        preview.Key(PhysicalKey.Escape);
        Assert.False(bar.IsOpen);
        Assert.False(surface.IsVisible);
        Assert.True(underlying.IsFocused);
    }

    [AvaloniaFact]
    public void ActionBarUsesHostWidthAndOnlyPlacesSeparatorsBetweenPopulatedSlots()
    {
        var edit = new AlderButton { Content = "Edit", Icon = Geometry.Parse("M0,0 L8,8 L0,8 Z"), Classes = { "action-bar-label" } };
        AutomationProperties.SetName(edit, "Edit selected files");
        var bar = new AlderActionBar { Prefix = "2", Content = edit, IsOpen = true };
        using var preview = new ActionBarWindow(new Grid { Children = { bar } }, width: 510);
        var label = Part<ContentPresenter>(edit, "PART_ContentPresenter");
        Assert.True(edit.IsIconOnly);
        Assert.False(label.IsVisible);
        Assert.Equal("Edit selected files", AutomationProperties.GetName(edit));
        Assert.True(Part<Border>(bar, "LeadingSeparator").IsVisible);
        Assert.False(Part<Border>(bar, "TrailingSeparator").IsVisible);

        preview.Window.Width = 830;
        preview.Pump();
        Assert.False(edit.IsIconOnly);
        Assert.True(label.IsVisible);
        bar.Orientation = Orientation.Vertical;
        bar.Prefix = null;
        bar.Suffix = "Clear";
        bar.IsAttached = false;
        preview.Pump();
        var separator = Part<Border>(bar, "TrailingSeparator");
        Assert.True(separator.IsVisible);
        Assert.Equal(1, separator.Height);
        Assert.Equal(24, separator.Width);
        Assert.Equal(Brushes.Transparent, Part<Border>(bar, "PART_Surface").Background);
        bar.ShowSeparators = false;
        Assert.False(separator.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(0d)]
    [InlineData(16d)]
    public async Task ActionBarHonorsDismissalCommandAndCanReopenDuringExit(double blurRadius)
    {
        var bar = new AlderActionBar { Content = new AlderButton { Content = "Edit" }, IsOpen = true, SurfaceBackdropBlurRadius = blurRadius };
        var parameter = new object();
        var command = new DismissCommand(value =>
        {
            Assert.Same(parameter, value);
            bar.IsOpen = false;
        });
        bar.DismissCommand = command;
        bar.DismissCommandParameter = parameter;
        using var preview = new ActionBarWindow(new Grid { Children = { bar } }, reducedMotion: false);
        var surface = Part<Border>(bar, "PART_Surface");
        Assert.False(bar.TryDismiss());
        Assert.True(bar.IsOpen);
        command.IsAllowed = true;
        Assert.True(bar.TryDismiss());
        Assert.False(surface.IsHitTestVisible);
        Assert.True(surface.IsVisible);

        await Task.Delay(35);
        bar.IsOpen = true;
        await preview.WaitUntil(() => surface.Opacity == 1);
        Assert.True(surface.IsVisible && surface.IsHitTestVisible);
        Assert.Equal(1, surface.Opacity);

        bar.IsOpen = false;
        await preview.WaitUntil(() => !surface.IsVisible);
        Assert.False(surface.IsVisible);
    }

    private static T Part<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private sealed class DismissCommand(Action<object?> execute) : ICommand
    {
        public bool IsAllowed { get; set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => IsAllowed;
        public void Execute(object? parameter) => execute(parameter);
    }

    private sealed class ActionBarWindow : IDisposable
    {
        public ActionBarWindow(Control content, double width = 810, bool reducedMotion = true)
        {
            Window = new AlderTestWindow { Width = width, Height = 320, Content = content };
            AlderMotion.SetReduced(Window, reducedMotion);
            Window.Show();
            Pump();
        }

        public Window Window { get; }

        public void Click(Control control)
        {
            var center = control.TranslatePoint(new Rect(control.Bounds.Size).Center, Window)!.Value;
            Window.MouseMove(center);
            Window.MouseDown(center, MouseButton.Left);
            Window.MouseUp(center, MouseButton.Left);
            Pump();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 40 && !condition(); i++)
            {
                await Task.Delay(20);
                Pump();
            }
        }

        public void Dispose() => Window.Close();
    }
}
