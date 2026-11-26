using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alder.Avalonia.Controls;
using Xunit;

namespace Alder.Avalonia.Tests;

public class ColorInteractions
{
    [Theory]
    [InlineData("  RGBA ( 100%, 5e1%, 0, 50% )  ", "#FF800080", ColorSpace.Rgb)]
    [InlineData("HSL( 120 DeG , 100%, 50% )", "#00FF00", ColorSpace.Hsl)]
    [InlineData("HsVa(240, 100%, 1, .25)", "#0000FF40", ColorSpace.Hsb)]
    [InlineData("  AliceBlue  ", "#F0F8FF", ColorSpace.Rgb)]
    [InlineData("  #abc8  ", "#AABBCC88", ColorSpace.Rgb)]
    public void ColorParserPreservesSupportedFormats(string text, string formatted, ColorSpace space)
    {
        Assert.True(AlderColorValue.TryParse(text, out var color));
        Assert.Equal(formatted, color.ToString());
        Assert.Equal(space, color.Space);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("rgb(1, 2, 3, 1, 1, 1)")]
    [InlineData("rgb(1, 2, 3, 1,)")]
    [InlineData("rgb(1,,3)")]
    [InlineData("rgb(1%, 2, Infinity)")]
    [InlineData("hsl(30%, 10%, 20%)")]
    [InlineData("hsl(30grad, 10%, 20%)")]
    [InlineData("hsl(30, 110%, 20%)")]
    public void ColorParserRejectsInvalidArguments(string? text)
    {
        Assert.False(AlderColorValue.TryParse(text, out var color));
        Assert.Equal(default, color);
    }

    [AvaloniaFact]
    public void ColorFieldCommitsValidTextAndKeepsInvalidEdits()
    {
        var field = new AlderColorField { Value = AlderColorValue.Parse("#ABC8") };
        var window = Show(field);
        try
        {
            Assert.Equal(Color.FromArgb(136, 170, 187, 204), field.Value.ToColor());
            Assert.Equal("#AABBCC88", field.Text);
            Assert.True(field.Focus());
            field.Text = "#broken";
            Press(window, PhysicalKey.Enter);

            Assert.True(field.HasInvalidInput);
            Assert.Equal("#broken", field.Text);
            Assert.Equal("#AABBCC88", field.Value.ToString());
            Press(window, PhysicalKey.Escape);
            Assert.False(field.HasInvalidInput);
            Assert.Equal("#AABBCC88", field.Text);

            field.Text = "hsla(180, 80%, 25%, 50%)";
            Press(window, PhysicalKey.Enter);
            Assert.Equal(ColorSpace.Hsl, field.Value.Space);
            Assert.Equal(.5, field.Value.Alpha);
            Assert.Equal(180, field.Value.Hue);

            field.Text = "#808080";
            Assert.True(field.Commit());
            Assert.Equal(180, field.Value.Hue);
            field.Channel = ColorChannel.Saturation;
            field.Culture = CultureInfo.GetCultureInfo("de-DE");
            field.Text = "45,125";
            Assert.True(field.Commit());
            Assert.Equal(180, field.Value.Hue);
            Assert.Equal(45.125, field.Value.GetChannel(ColorChannel.Saturation), 10);
            Assert.False(field.HasInvalidInput);
            field.Text = "150";
            Assert.False(field.Commit());
            Assert.Equal("150", field.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ColorAreaAndSliderEditChannelsWithPointerAndKeyboard()
    {
        var area = new AlderColorArea { Value = AlderColorValue.FromHsb(190, .5, .5, .4), HorizontalAlignment = HorizontalAlignment.Left };
        var slider = new AlderColorSlider { Channel = ColorChannel.Alpha, ColorValue = area.Value, HorizontalAlignment = HorizontalAlignment.Left };
        var window = Show(new StackPanel { Spacing = 12, Children = { area, slider } });
        AlderMotion.SetReduced(window, true);
        try
        {
            int areaCommits = 0;
            area.ColorChangeCompleted += (_, _) => areaCommits++;
            var start = ToWindow(area, window, new Point(24, 100));
            var end = ToWindow(area, window, new Point(206, 10));
            window.MouseDown(start, MouseButton.Left);
            Assert.Equal(20, Part<Border>(area, "PART_Thumb").Width);
            window.MouseMove(end);
            Assert.Equal(0, areaCommits);
            window.MouseUp(end, MouseButton.Left);
            Assert.Equal(16, Part<Border>(area, "PART_Thumb").Width);
            Assert.Equal(1, areaCommits);
            Assert.Equal(190, area.Value.Hue);
            Assert.Equal(.4, area.Value.Alpha);
            Press(window, PhysicalKey.ArrowLeft);
            Assert.Equal(2, areaCommits);

            Assert.True(slider.Focus());
            Assert.Equal(100d, slider.Maximum);
            Assert.Equal(40d, slider.Value);
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal(.41, slider.ColorValue.Alpha, 8);
            Assert.Contains("41", slider.ChannelText);
            var peer = ControlAutomationPeer.CreatePeerForElement(slider);
            Assert.IsAssignableFrom<IRangeValueProvider>(peer);

            int sliderCommits = 0;
            slider.ColorChangeCompleted += (_, _) => sliderCommits++;
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var thumbCenter = ToWindow(thumb, window, new Rect(thumb.Bounds.Size).Center);
            var dragEnd = ToWindow(slider, window, new Point(slider.Bounds.Width - 12, slider.Bounds.Height / 2));
            window.MouseDown(thumbCenter, MouseButton.Left);
            window.MouseMove(dragEnd);
            Assert.Equal(0, sliderCommits);
            window.MouseUp(dragEnd, MouseButton.Left);
            Assert.Equal(1, sliderCommits);
            Assert.True(slider.ColorValue.Alpha > .9);

            slider.Channel = ColorChannel.Red;
            Assert.Equal(255d, slider.Maximum);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ColorPickerSynchronizesEditorsAndPublishesOnlyAppliedDrafts()
    {
        var original = AlderColorValue.Parse("#0485F7");
        var picker = new AlderColorPicker { Value = original, CommitOnApply = true, ShowAlpha = true };
        var window = Show(picker);
        AlderMotion.SetReduced(window, true);
        try
        {
            picker.Open();
            Pump();
            Assert.True(picker.IsOpen);
            var area = window.GetVisualDescendants().OfType<AlderColorArea>().Single();
            var field = window.GetVisualDescendants().OfType<AlderColorField>().Single();
            var alpha = window.GetVisualDescendants().OfType<AlderColorSlider>().Single(s => s.Channel == ColorChannel.Alpha);
            alpha.SetCurrentValue(RangeBase.ValueProperty, 55d);
            Pump();
            Assert.Equal(.55, picker.DraftValue.Alpha);
            Assert.Equal(picker.DraftValue, area.Value);
            Assert.Equal(picker.DraftValue.ToString(), field.Text);
            Assert.Equal(original, picker.Value);

            field.Text = "#broken";
            Assert.False(picker.Apply());
            Assert.True(field.HasInvalidInput);
            Assert.Equal(original, picker.Value);
            field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Pump();
            Assert.False(picker.IsOpen);
            Assert.Equal(original, picker.DraftValue);
            Assert.True(Part<Button>(picker, "PART_Trigger").IsFocused);

            picker.Open();
            Pump();
            field.Text = "#FF880080";
            Assert.True(picker.Apply());
            Assert.False(picker.IsOpen);
            Assert.Equal("#FF880080", picker.Value.ToString());

            picker.Open();
            Pump();
            field.Text = "#FFFFFF";
            Click(window, new Point(460, 400));
            Assert.Equal("#FF880080", picker.Value.ToString());
            Assert.Equal(picker.Value, picker.DraftValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SwatchPickerUsesNativeSingleSelectionAndSkipsDisabledSwatches()
    {
        var first = new AlderColorSwatchPickerItem { Color = Colors.Blue, ColorName = "Blue swatch" };
        var disabled = new AlderColorSwatchPickerItem { Color = Colors.Red, IsEnabled = false };
        var last = new AlderColorSwatchPickerItem { Color = Color.FromArgb(0, 18, 32, 48) };
        var palette = new AlderColorSwatchPicker
        {
            Items = { first, disabled, last }, SelectedIndex = 0,
            Size = ColorSwatchSize.Large, Shape = ColorSwatchShape.Square
        };
        var window = Show(palette);
        AlderMotion.SetReduced(window, true);
        try
        {
            Assert.Equal(1, Part<PathIcon>(first, "SelectedMark").RenderTransform!.Value.M11);
            var swatch = first.GetVisualDescendants().OfType<AlderColorSwatch>().Single();
            Assert.Equal(36, first.Bounds.Width);
            Assert.Equal(30, swatch.Bounds.Width);
            Assert.Equal(ColorSwatchShape.Square, swatch.Shape);
            Assert.Equal("Blue swatch", ControlAutomationPeer.CreatePeerForElement(first).GetName());
            Assert.Equal("#12203000", ControlAutomationPeer.CreatePeerForElement(last).GetName());

            Assert.True(first.Focus());
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal(last, palette.SelectedItem);
            Assert.False(first.IsSelected);
            Assert.True(last.IsSelected);
            Assert.Equal(1, Part<PathIcon>(last, "SelectedMark").RenderTransform!.Value.M11);
            Assert.Equal(0, Part<PathIcon>(first, "SelectedMark").RenderTransform!.Value.M11);

            var disabledCenter = ToWindow(disabled, window, new Rect(disabled.Bounds.Size).Center);
            window.MouseDown(disabledCenter, MouseButton.Left);
            window.MouseUp(disabledCenter, MouseButton.Left);
            Assert.Equal(last, palette.SelectedItem);

            palette.Layout = ColorSwatchPickerLayout.Stack;
            Pump();
            Assert.True(first.Focus());
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(last, palette.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DefaultColorPickerPublishesLiveAndDismissesThroughOverlay()
    {
        var picker = new AlderColorPicker();
        var window = Show(picker);
        AlderMotion.SetReduced(window, true);
        try
        {
            var trigger = Part<Button>(picker, "PART_Trigger");
            Click(window, ToWindow(trigger, window, new Rect(trigger.Bounds.Size).Center));
            Assert.True(picker.IsOpen);
            var host = Assert.Single(window.GetVisualDescendants().OfType<OverlayPopupHost>());
            var pane = Part<Border>(host, "ColorPickerSurface");
            Assert.Equal(new Size(248, 308), pane.Bounds.Size);
            var area = Assert.Single(host.GetVisualDescendants().OfType<AlderColorArea>());
            var hue = Assert.Single(host.GetVisualDescendants().OfType<AlderColorSlider>(), s => s.IsEffectivelyVisible);
            Assert.Equal(new Size(232, 232), area.Bounds.Size);
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<AlderColorField>(), f => f.IsEffectivelyVisible);

            var color = AlderColorValue.FromHsb(270, .6, .9);
            area.SetCurrentValue(AlderColorArea.ValueProperty, color);
            Assert.Equal(color, picker.Value);
            Assert.Equal(color, hue.ColorValue);

            Click(window, new Point(460, 400));
            Assert.False(picker.IsOpen);
            Assert.Equal(color, picker.Value);
        }
        finally { window.Close(); }
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private static Window Show(Control control)
    {
        var window = new Window { Width = 480, Height = 420, Content = control };
        window.Show();
        Pump();
        return window;
    }

    private static T Part<T>(Control owner, string name) where T : Control =>
        owner.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static Point ToWindow(Control control, Window window, Point point) => control.TranslatePoint(point, window)!.Value;

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
