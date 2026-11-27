using System.Diagnostics;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Alder.Avalonia.AotSmoke;

internal static class TransitioningContentRenderSmoke
{
    public static void Run()
    {
        var window = new AlderTestWindow { Width = 280, Height = 180 };
        window.Show();
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var (width, scale) in new[] { (280, 1d), (390, 1.25d), (480, 2d) })
            {
                window.Width = width;
                window.RequestedThemeVariant = theme;
                window.Background = theme == ThemeVariant.Light ? Brushes.WhiteSmoke : Brushes.Black;
                window.SetRenderScaling(scale);
                VerifyFrame(window, theme, width, scale, AlderContentTransition.Slide);
                VerifyFrame(window, theme, width, scale, AlderContentTransition.Reveal);
                VerifyFrame(window, theme, width, scale, AlderContentTransition.Reveal, reversed: true);
            }
        }
        finally { window.Close(); }
    }

    private static void VerifyFrame(Window window, ThemeVariant theme, int width, double scale, AlderContentTransition mode,
        bool reversed = false)
    {
        var reveal = mode == AlderContentTransition.Reveal;
        var outgoing = new Border { Background = Brushes.Red };
        var incoming = new Border { Background = Brushes.Blue, Margin = reveal ? new Thickness(0, 18, 0, 0) : default };
        var control = new AlderTransitioningContentControl
        {
            Content = outgoing, Transition = mode, Duration = TimeSpan.FromSeconds(1),
            Margin = new Thickness(18), Height = 120, CornerRadius = new CornerRadius(14), IsTransitionReversed = reversed
        };
        window.Content = new AlderScrollViewer { Content = control };
        using var before = Capture(window);
        var origin = control.TranslatePoint(default, window)!.Value;
        var frame = new Rect(origin, control.Bounds.Size);
        var corner = origin + new Vector(1, 1);
        var edge = new Point(frame.Left + .5, frame.Center.Y);
        var left = new Point(frame.Left + 16, frame.Center.Y);
        var right = new Point(frame.Right - 16, frame.Center.Y);
        var entrance = reversed ? right : left;
        var exit = reversed ? left : right;
        var transparent = new Point(entrance.X, frame.Top + 8);
        var color = ((ISolidColorBrush)control.Background!).Color;
        var surface = new SKColor(color.R, color.G, color.B, color.A);
        Check(Pixel(before, left, scale) == SKColors.Red, "initial content fills the viewport");
        Check(Pixel(before, corner, scale) == Pixel(before, origin - new Vector(1, 1), scale), "the rounded corner exposes the window surface");
        Check(Pixel(before, edge, scale) != SKColors.Red, "the border paints outside the content");

        control.Content = incoming;
        window.UpdateLayout();
        var presenter = control.GetVisualDescendants().OfType<ContentPresenter>().Single(part => part.Content == incoming);
        var sawMiddle = false;
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
        {
            Pump(window);
            double fraction = !reveal
                ? 1 - (presenter.RenderTransform?.Value.M31 ?? 0) / presenter.Bounds.Width
                : (presenter.Clip as RectangleGeometry)?.Rect.Width / presenter.Bounds.Width ?? 1;
            if (!sawMiddle && fraction is > .30 and < .70)
            {
                using var middle = Capture(window);
                Check(Pixel(middle, corner, scale) == Pixel(before, corner, scale), "moving content respects the rounded clip");
                Check(Pixel(middle, edge, scale) == Pixel(before, edge, scale), "the outside border stays fixed");
                if (reveal)
                {
                    Check(Pixel(middle, entrance, scale) == SKColors.Blue && Pixel(middle, exit, scale) == SKColors.Red &&
                          Pixel(middle, transparent, scale) == surface,
                          "the old page remains beside the new page, without showing through it");
                    var y = (int)(frame.Center.Y * scale);
                    int startX = (int)(left.X * scale);
                    int endX = (int)(right.X * scale);
                    for (var x = startX; x <= endX; x++)
                    {
                        var pixel = middle.GetPixel(x, y);
                        Check(pixel == SKColors.Red || pixel == SKColors.Blue,
                            $"the boundary joins both pages without a gap or blend ({x}, {pixel})");
                    }
                }
                else
                {
                    Check(Pixel(middle, left, scale) == SKColors.Red && Pixel(middle, right, scale) == SKColors.Blue,
                        "both sliding layers are visible inside the clip");
                }
                sawMiddle = true;
            }
            if (sawMiddle && fraction >= 1)
                break;
            Thread.Sleep(15);
        }

        Check(sawMiddle, "an intermediate transition frame renders");
        using var after = Capture(window);
        Check(Pixel(after, left, scale) == SKColors.Blue, "the incoming layer fills the viewport after the transition");

        void Check(bool condition, string behavior)
        {
            if (!condition)
                throw new InvalidOperationException($"Transitioning Content {mode} ({theme.Key}, {width} DIP, {scale}×) failed: {behavior}.");
        }
    }

    private static SKBitmap Capture(Window window)
    {
        Pump(window);
        using var frame = window.CaptureRenderedFrame()!;
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(stream.ToArray());
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static SKColor Pixel(SKBitmap frame, Point point, double scale) =>
        frame.GetPixel((int)(point.X * scale), (int)(point.Y * scale));
}
