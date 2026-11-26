using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Paints changing digits over a numeric field or counter during a value transition.</summary>
internal sealed class AlderNumberStepValue : TemplatedControl
{
    private IDisposable? hiddenText;
    private StepAnimation? animation;
    private Action<TimeSpan>? onFrame;
    private bool framePending;
    private double progress;

    public void Roll(TextBox editor, string before, string after, int direction, bool useStepperTiming)
        => Roll(editor, new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch),
            editor.FontSize, editor.Foreground, before, after, direction, useStepperTiming);

    public void Roll(TextBlock counter, string before, string after, int direction)
        => Roll(counter, new Typeface(counter.FontFamily, counter.FontStyle, counter.FontWeight, counter.FontStretch),
            counter.FontSize, counter.Foreground, before, after, direction, useStepperTiming: true);

    private void Roll(Visual source, Typeface face, double fontSize, IBrush? foreground,
        string before, string after, int direction, bool useStepperTiming)
    {
        Stop();
        if (before == after || TopLevel.GetTopLevel(this) is null || Bounds.Width <= 0) return;

        FormattedText Format(char digit) => new(digit.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            face, fontSize, foreground ?? Brushes.Black);
        double digitAdvance = Format('0').WidthIncludingTrailingWhitespace;
        var previous = MakeLine(before, Format, digitAdvance);
        var next = MakeLine(after, Format, digitAdvance);
        animation = new StepAnimation(previous, next, direction, Stopwatch.GetTimestamp(), useStepperTiming);
        progress = 0;
        hiddenText = source.SetValue(OpacityProperty, 0d, BindingPriority.Animation);
        InvalidateVisual();
        RequestFrame();
    }

    public void Stop()
    {
        if (animation is null) return;
        animation = null;
        hiddenText?.Dispose();
        hiddenText = null;
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (animation is not { } change) return;
        var oldLine = change.Old;
        var newLine = change.New;
        double height = Math.Max(oldLine.Height, newLine.Height);
        double y = (Bounds.Height - height) / 2;
        double travel = height * .8;
        double oldStart = (Bounds.Width - oldLine.Width) / 2;
        double newStart = (Bounds.Width - newLine.Width) / 2;

        for (int fromRight = Math.Max(oldLine.Glyphs.Length, newLine.Glyphs.Length); fromRight > 0; fromRight--)
        {
            var old = fromRight <= oldLine.Glyphs.Length ? oldLine.Glyphs[^fromRight] : null;
            var next = fromRight <= newLine.Glyphs.Length ? newLine.Glyphs[^fromRight] : null;
            double oldX = oldStart + (old?.X ?? 0);
            double newX = newStart + (next?.X ?? 0);
            double x = old is null ? newX : next is null ? oldX : oldX + (newX - oldX) * progress;
            if (old is not null && next is not null && old.Character == next.Character)
            {
                context.DrawText(next.Text, new Point(x, y));
                continue;
            }

            if (old is not null)
            {
                using var fade = context.PushOpacity(1 - progress);
                context.DrawText(old.Text, new Point(x, y - change.Direction * travel * progress));
            }
            if (next is not null)
            {
                using var fade = context.PushOpacity(progress);
                context.DrawText(next.Text, new Point(x, y + change.Direction * travel * (1 - progress)));
            }
        }
    }

    private void RequestFrame()
    {
        if (framePending || TopLevel.GetTopLevel(this) is not { } root) return;
        framePending = true;
        root.RequestAnimationFrame(onFrame ??= RenderFrame);
    }

    private void RenderFrame(TimeSpan _)
    {
        framePending = false;
        if (animation is not { } change) return;
        double elapsed = Stopwatch.GetElapsedTime(change.Started).TotalMilliseconds;
        double duration = change.UseStepperTiming ? 600 : 205;
        if (elapsed >= duration || AlderMotion.GetReduced(this))
        {
            Stop();
            return;
        }
        if (change.UseStepperTiming)
        {
            // The web step starts gently and has a spring tail. After 600 ms the remainder is subpixel.
            double response = elapsed / 96;
            progress = 1 - (1 + response) * Math.Exp(-response);
        }
        else
        {
            double remaining = 1 - elapsed / duration;
            progress = 1 - remaining * remaining * remaining;
        }
        InvalidateVisual();
        RequestFrame();
    }

    private static Line MakeLine(string text, Func<char, FormattedText> format, double digitAdvance)
    {
        var glyphs = new Glyph[text.Length];
        double width = 0;
        double height = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var formatted = format(text[i]);
            double advance = char.IsDigit(text[i]) ? digitAdvance : formatted.WidthIncludingTrailingWhitespace;
            glyphs[i] = new Glyph(text[i], formatted, width + (advance - formatted.WidthIncludingTrailingWhitespace) / 2);
            width += advance;
            height = Math.Max(height, formatted.Height);
        }
        return new Line(glyphs, width, height);
    }

    private sealed record StepAnimation(Line Old, Line New, int Direction, long Started, bool UseStepperTiming);
    private sealed record Line(Glyph[] Glyphs, double Width, double Height);
    private sealed record Glyph(char Character, FormattedText Text, double X);
}
