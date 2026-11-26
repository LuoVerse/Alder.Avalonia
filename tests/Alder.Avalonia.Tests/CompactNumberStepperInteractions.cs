using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class CompactNumberStepperInteractions
{
    [AvaloniaFact]
    public async Task CompactStepperAnimatesBothDirectionsAndRestoresTheEditor()
    {
        var number = CreateStepper();
        number.Value = 8;
        var window = new Window { Width = 240, Height = 100, Content = number };
        AlderMotion.SetReduced(window, false);
        window.Show();
        try
        {
            Pump(window);
            var editor = Part<TextBox>(number, "PART_TextBox");
            Click(window, Part<RepeatButton>(number, "PART_IncreaseButton"));
            Assert.Equal(9m, number.Value);
            Assert.Equal(0, editor.Opacity);
            Assert.False(editor.IsFocused);
            Click(window, Part<RepeatButton>(number, "PART_DecreaseButton"));
            Assert.Equal(8m, number.Value);
            Assert.Equal(0, editor.Opacity);

            for (int i = 0; i < 45 && editor.Opacity == 0; i++)
            {
                await Task.Delay(20);
                Pump(window);
            }
            Assert.Equal(1, editor.Opacity);
            Assert.Equal("8", editor.Text);

            AlderMotion.SetReduced(window, true);
            Click(window, Part<RepeatButton>(number, "PART_IncreaseButton"));
            Assert.Equal(9m, number.Value);
            Assert.Equal(1, editor.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CompactStepperDoesNotAnimateWhenARecycledCellReceivesAnotherRow()
    {
        var number = CreateStepper();
        number.DataContext = new Row(5);
        number.Bind(AlderNumberField.ValueProperty, new Binding(nameof(Row.Points)));
        var window = new Window { Width = 240, Height = 100, Content = number };
        AlderMotion.SetReduced(window, false);
        window.Show();
        try
        {
            Pump(window);
            var editor = Part<TextBox>(number, "PART_TextBox");
            number.DataContext = new Row(13);
            Pump(window);
            Assert.Equal(13m, number.Value);
            Assert.Equal("13", editor.Text);
            Assert.Equal(1, editor.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReversedStepperKeepsDirectionAndBounds()
    {
        var number = new AlderNumberStepper { Minimum = 0, Maximum = 6, Value = 5, Increment = 2, IsReversed = true };
        var window = new Window { Width = 340, Height = 110, Content = number };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            var plus = Part<RepeatButton>(number, "PART_IncreaseButton");
            var minus = Part<RepeatButton>(number, "PART_DecreaseButton");
            Assert.Equal(92, number.Bounds.Width);
            Assert.True(plus.Bounds.X < minus.Bounds.X);
            Click(window, plus);
            Assert.Equal(6, number.Value);
            Assert.Equal("6", Part<TextBox>(number, "PART_TextBox").Text);
            Assert.False(plus.IsEnabled);
            Click(window, minus);
            Assert.Equal(4, number.Value);
        }
        finally { window.Close(); }
    }

    public sealed record Row(decimal Points);

    private static AlderNumberStepper CreateStepper() => new()
    {
        Size = AlderSize.Small,
        Minimum = 0, Maximum = 99, FormatString = "0"
    };

    private static T Part<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().First(item => item.Name == name);

    private static void Click(Window window, Control button)
    {
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
