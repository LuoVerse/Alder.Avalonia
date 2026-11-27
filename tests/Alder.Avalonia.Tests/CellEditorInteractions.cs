using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class CellEditorInteractions
{
    [AvaloniaFact]
    public void CellColorPickerSelectsAPresetWithoutClosingAndHonorsReadOnly()
    {
        var blue = AlderColorValue.Parse("#3B82F6");
        var red = AlderColorValue.Parse("#EF4444");
        var picker = new AlderCellColorPicker { Width = 252, Value = blue, Presets = [red, blue] };
        var window = Show(picker);
        try
        {
            var trigger = Part<Button>(picker, "PART_Trigger");
            Click(window, Center(trigger, window));
            Assert.True(picker.IsOpen);

            var choices = window.GetVisualDescendants().OfType<AlderColorSwatchPickerItem>().ToArray();
            Assert.Equal(2, choices.Length);
            Assert.True(choices[1].IsSelected);

            Click(window, Center(choices[0], window));
            Assert.Equal(red.ToColor(), picker.Value.ToColor());
            Assert.True(picker.IsOpen);
            Assert.True(choices[0].IsSelected);

            picker.IsReadOnly = true;
            Pump(window);
            Assert.False(picker.IsOpen);
            Click(window, Center(trigger, window));
            Assert.False(picker.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CellSelectOpensFromTheLabelAndCommitsAKeyboardSelection()
    {
        var select = new AlderCellSelect
        {
            Width = 252, Label = "Theme", ItemsSource = new[] { "Default", "Dark", "System" }, SelectedIndex = 0
        };
        var window = Show(select);
        try
        {
            var trigger = Part<Button>(select, "PART_Trigger");
            var point = trigger.TranslatePoint(new Point(16, 18), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            Pump(window);
            Assert.True(select.IsDropDownOpen);
            window.MouseUp(point, MouseButton.Left);
            Pump(window);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            Assert.False(select.IsDropDownOpen);
            Assert.Equal("Dark", select.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CellSelectCanReopenWhileItsExitAnimationIsStillRunning()
    {
        var select = new AlderCellSelect { ItemsSource = new[] { "Default", "Dark" } };
        var window = Show(select);
        try
        {
            AlderMotion.SetReduced(window, false);
            select.IsDropDownOpen = true;
            Pump(window);
            Pump(window);
            var frame = Part<Control>(window, "AlderPopoverSurface");
            select.IsDropDownOpen = false;
            Assert.False(frame.IsHitTestVisible);

            select.IsDropDownOpen = true;
            Pump(window);
            Assert.True(Part<Control>(window, "AlderPopoverSurface").IsHitTestVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CellSliderDragsAndSnapsWithTheConfiguredStep()
    {
        var slider = new AlderCellSlider { Width = 252, Maximum = 100, Value = 50, Step = 5, ValueFormat = "0" };
        var window = Show(slider);
        try
        {
            var start = Center(slider, window);
            var end = start + new Vector(76, 56);
            var handle = Part<Border>(slider, "Handle");
            window.MouseMove(start);
            Pump(window);
            Assert.Equal(0.4, handle.Opacity);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end);
            Pump(window);
            Assert.Equal(0.7, handle.Opacity);
            Assert.Equal(1.5, handle.RenderTransform!.Value.M22);
            window.MouseUp(end, MouseButton.Left);
            Pump(window);

            Assert.Equal(80, slider.Value);
            Assert.Equal("80", slider.ValueText);
            Assert.Equal(0, handle.Opacity);
            Assert.True(slider.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal(85, slider.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CellSwitchUsesTheWholeRowAndRespectsTheDisabledState()
    {
        var cell = new AlderCellSwitch { Width = 252, Content = "Animations", IsChecked = false };
        var window = Show(cell);
        try
        {
            var point = cell.TranslatePoint(new Point(18, 18), window)!.Value;
            Click(window, point);
            Assert.True(cell.IsChecked);
            Assert.True(cell.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Space);
            Assert.False(cell.IsChecked);

            cell.IsEnabled = false;
            Click(window, point);
            Assert.False(cell.IsChecked);
        }
        finally { window.Close(); }
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(element => element.Name == name);

    private static Window Show(Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(24), Children = { control } };
        var window = new AlderTestWindow { Width = 460, Height = 620, Content = panel };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Click(Window window, Point position)
    {
        window.MouseMove(position);
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
        Pump(window);
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
