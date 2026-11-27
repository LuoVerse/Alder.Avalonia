using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ResizableInteractions
{
    [AvaloniaFact]
    public void PointerResizeTracksMovementRespectsLimitsAndCanReturnToItsOrigin()
    {
        var left = new AlderResizablePanel
        {
            DefaultSize = new GridLength(180), MinSize = new GridLength(140), MaxSize = new GridLength(260),
            GroupResizeBehavior = ResizableGroupResizeBehavior.PreservePixelSize
        };
        var right = new AlderResizablePanel { MinSize = Percent(30) };
        var handle = new AlderResizableHandle();
        var group = new AlderResizable { Children = { left, handle, right } };
        var window = Show(group, 600);
        try
        {
            var point = Center(handle, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(40, 0));
            Pump(window);
            Assert.True(handle.IsDragging);
            Assert.Equal(220, left.Bounds.Width, 1);
            Assert.Equal(220, left.Size!.Value.Value, 1);

            window.MouseMove(point + new Vector(250, 0));
            Pump(window);
            Assert.Equal(260, left.Bounds.Width, 1);
            window.MouseMove(point);
            Pump(window);
            Assert.Equal(180, left.Bounds.Width, 1);
            window.MouseUp(point, MouseButton.Left);
            Assert.False(handle.IsDragging);

            window.Width = 330;
            Pump(window);
            Assert.Equal(180, left.Bounds.Width, 1);
            Assert.InRange(right.Bounds.Width, 148, 150);

            handle.IsEnabled = false;
            point = Center(handle, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(30, 0));
            window.MouseUp(point + new Vector(30, 0), MouseButton.Left);
            Pump(window);
            Assert.False(handle.IsDragging);
            Assert.Equal(180, left.Bounds.Width, 1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VerticalHandleUsesFivePercentKeyboardStepsAndTogglesCollapse()
    {
        var top = new AlderResizablePanel { DefaultSize = Percent(35), MinSize = Percent(20), IsCollapsible = true };
        var bottom = new AlderResizablePanel { DefaultSize = Percent(65), MinSize = Percent(20) };
        var handle = new AlderResizableHandle { Type = ResizableHandleType.Pill };
        var group = new AlderResizable { Orientation = Orientation.Vertical, Children = { top, handle, bottom } };
        var window = Show(group, 350);
        try
        {
            double available = group.Bounds.Height - 1;
            Assert.True(handle.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(40, top.Size!.Value.Value, 5);
            Assert.InRange(top.Bounds.Height, available * .4 - 1, available * .4 + 1);
            Assert.Equal(available, top.Bounds.Height + bottom.Bounds.Height);

            Press(window, PhysicalKey.Enter);
            Assert.True(top.IsCollapsed);
            Assert.Equal(0, top.Bounds.Height);
            Assert.Equal(available, bottom.Bounds.Height, 1);
            Press(window, PhysicalKey.Enter);
            Assert.False(top.IsCollapsed);
            Assert.InRange(top.Bounds.Height, available * .4 - 1, available * .4 + 1);

            Press(window, PhysicalKey.Home);
            Assert.True(top.IsCollapsed);
            var point = Center(handle, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(0, 85));
            Pump(window);
            Assert.False(top.IsCollapsed);
            Assert.InRange(top.Bounds.Height, available * .2 - 1, available * .2 + 8);
            window.MouseMove(point);
            Pump(window);
            Assert.True(top.IsCollapsed);
            Assert.Equal(0, top.Bounds.Height);
            window.MouseUp(point, MouseButton.Left);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DragContinuesIntoTheNextPaneWhenAnAdjacentPaneReachesItsLimit()
    {
        var left = new AlderResizablePanel { DefaultSize = new GridLength(200) };
        var middle = new AlderResizablePanel { DefaultSize = new GridLength(200), MinSize = new GridLength(150) };
        var right = new AlderResizablePanel { DefaultSize = new GridLength(200), MinSize = new GridLength(100) };
        var handle = new AlderResizableHandle();
        var group = new AlderResizable { Children = { left, handle, middle, new AlderResizableHandle(), right } };
        var window = Show(group, 602);
        try
        {
            var point = Center(handle, window);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(90, 0));
            Pump(window);
            Assert.Equal(290, left.Bounds.Width, 1);
            Assert.Equal(150, middle.Bounds.Width, 1);
            Assert.Equal(160, right.Bounds.Width, 1);
            window.MouseUp(point + new Vector(90, 0), MouseButton.Left);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PaneConstraintsRecoverWhenANarrowNestedLayoutGrows()
    {
        var dock = new AlderResizablePanel
        {
            DefaultSize = new GridLength(180), MinSize = new GridLength(140),
            GroupResizeBehavior = ResizableGroupResizeBehavior.PreservePixelSize
        };
        var editor = new AlderResizablePanel { DefaultSize = Percent(60), MinSize = Percent(20) };
        var terminal = new AlderResizablePanel { DefaultSize = Percent(40), MinSize = Percent(20) };
        var nested = new AlderResizable { Orientation = Orientation.Vertical, Children = { editor, new AlderResizableHandle(), terminal } };
        var workspace = new AlderResizablePanel { Content = nested, MinSize = new GridLength(110) };
        var group = new AlderResizable { Children = { dock, new AlderResizableHandle(), workspace } };
        var window = Show(group, 640);
        try
        {
            var editorHeight = editor.Bounds.Height;
            window.Width = 200;
            Pump(window);
            Assert.True(dock.Bounds.Width < 140);
            Assert.True(workspace.Bounds.Width < 110);
            Assert.InRange(dock.Bounds.Width + workspace.Bounds.Width, 198, 200);
            Assert.Equal(group.Bounds.Width, dock.Bounds.Width + workspace.Bounds.Width + 1);
            Assert.Equal(editorHeight, editor.Bounds.Height, 1);

            window.Width = 640;
            Pump(window);
            Assert.Equal(180, dock.Bounds.Width, 1);
            Assert.Equal(459, workspace.Bounds.Width, 1);
            Assert.Equal(editorHeight, editor.Bounds.Height, 1);
            Assert.Null(dock.Size);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void HalfPixelPanelEdgeLeavesExactlyOneDipForTheSeparator(int scale)
    {
        var before = new AlderResizablePanel { DefaultSize = Percent(50) };
        var after = new AlderResizablePanel { DefaultSize = Percent(50) };
        var handle = new AlderResizableHandle();
        var group = new AlderResizable { Children = { before, handle, after } };
        var window = Show(group, 746);
        try
        {
            window.SetRenderScaling(scale);
            Pump(window);
            var line = handle.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "SeparatorLine");
            var left = line.TranslatePoint(default, group)!.Value.X;
            Assert.Equal(before.Bounds.Right, left);
            Assert.Equal(after.Bounds.Left, left + line.Bounds.Width);
            Assert.Equal(group.Bounds.Width, after.Bounds.Right);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TouchResizesAVerticalGroupInsideAScrollViewer()
    {
        var before = new AlderResizablePanel();
        var handle = new AlderResizableHandle();
        var group = new AlderResizable
        {
            Height = 500, Orientation = Orientation.Vertical, Children = { before, handle, new AlderResizablePanel() }
        };
        var scroll = new AlderScrollViewer { Content = group };
        var window = Show(scroll, 450);
        try
        {
            var point = Center(handle, window);
            var initial = before.Bounds.Height;
            var touch = window.TouchBegin(point, RawInputModifiers.None);
            window.TouchMove(touch, point + new Vector(0, -35), RawInputModifiers.None);
            Pump(window);
            Assert.True(handle.IsDragging);
            Assert.Equal(initial - 35, before.Bounds.Height, 1);
            Assert.Equal(0, scroll.Offset.Y);
            window.TouchEnd(touch, point + new Vector(0, -35), RawInputModifiers.None);
            Assert.False(handle.IsDragging);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task HandleLineSettlesToTheHoverAndDragBrushes()
    {
        var handle = new AlderResizableHandle();
        var group = new AlderResizable { Children = { new AlderResizablePanel(), handle, new AlderResizablePanel() } };
        var window = Show(group, 450, reducedMotion: false);
        try
        {
            var line = handle.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "SeparatorLine");
            var point = Center(handle, window) - new Vector(2, 0);
            window.MouseMove(point);
            Pump(window);
            Assert.True(handle.IsPointerOver);
            await Task.Delay(200);
            Pump(window);
            Assert.Equal(Color.Parse("#D8D8D8"), Assert.IsAssignableFrom<ISolidColorBrush>(line.Background).Color);

            window.MouseDown(point, MouseButton.Left);
            Pump(window);
            await Task.Delay(200);
            Pump(window);
            Assert.Equal(Color.Parse("#260485F7"), Assert.IsAssignableFrom<ISolidColorBrush>(line.Background).Color);
            window.MouseUp(point, MouseButton.Left);
        }
        finally { window.Close(); }
    }

    private static GridLength Percent(double value) => new(value, GridUnitType.Star);
    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static Window Show(Control content, int width, bool reducedMotion = true)
    {
        var window = new AlderTestWindow { Width = width, Height = 400, Content = content, RequestedThemeVariant = ThemeVariant.Light };
        AlderMotion.SetReduced(window, reducedMotion);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
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
}
