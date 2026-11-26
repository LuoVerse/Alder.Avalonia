using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Paints the effort meter while the underlying slider owns input and accessibility.</summary>
internal sealed partial class AlderModelEffortTrack : Control
{
    private const double TrackHeight = 16;
    internal const double ThumbWidth = 14;
    private static readonly Color DefaultAccent = Color.Parse("#0485F7");
    private static readonly SplineEasing SweepEase = new(.42, 0, .58, 1);
    private static readonly Geometry Star = Geometry.Parse("M0 -3 Q.35 -.35 3 0 Q.35 .35 0 3 Q-.35 .35 -3 0 Q-.35 -.35 0 -3 Z");

    [GeneratedStyledProperty] public partial double Value { get; set; }
    [GeneratedStyledProperty] public partial double Maximum { get; set; }
    [GeneratedStyledProperty] public partial double AccentAmount { get; set; }
    [GeneratedStyledProperty] public partial double SheenAmount { get; set; }
    [GeneratedStyledProperty] public partial double UltraAmount { get; set; }
    [GeneratedStyledProperty] public partial double HoverAmount { get; set; }
    [GeneratedStyledProperty] public partial IBrush? AccentBrush { get; set; }
    [GeneratedStyledProperty(DefaultValue = true)] public partial bool ShowOuterSparks { get; set; }
    [GeneratedStyledProperty] public partial bool Focused { get; set; }

    private readonly Action<TimeSpan> frame;
    private bool framePending;
    private TimeSpan? lastFrame;
    private double sheenSeconds;
    private double sparkSeconds;

    static AlderModelEffortTrack() => AffectsRender<AlderModelEffortTrack>(
        ValueProperty, MaximumProperty, AccentAmountProperty, SheenAmountProperty, UltraAmountProperty, HoverAmountProperty,
        AccentBrushProperty, ShowOuterSparksProperty, FocusedProperty, AlderMotion.ReducedProperty);

    internal double ThumbStart => Maximum > 0
        ? Math.Max(0, Bounds.Width - ThumbWidth) * Math.Clamp(Value / Maximum, 0, 1)
        : 0;

    public AlderModelEffortTrack()
    {
        frame = Advance;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        lastFrame = null;
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width < ThumbWidth || Maximum <= 0)
        {
            lastFrame = null;
            return;
        }

        bool dark = ActualThemeVariant == ThemeVariant.Dark;
        bool reduced = AlderMotion.GetReduced(this);
        var track = new Rect(0, (Bounds.Height - TrackHeight) / 2, Bounds.Width, TrackHeight);
        var thumbX = ThumbStart;
        var fill = track.WithWidth(thumbX + ThumbWidth);
        var thumb = new Rect(thumbX + 2, track.Y + 2, 10, 12);
        var railColor = dark ? Color.FromRgb(39, 39, 42) : Color.FromRgb(235, 235, 236);
        var edge = dark ? Color.FromArgb(16, 255, 255, 255) : Color.FromArgb(13, 24, 24, 27);
        var plain = dark ? Mix(Color.FromRgb(86, 86, 88), Color.FromRgb(99, 99, 102), HoverAmount)
            : Mix(Color.FromRgb(188, 188, 189), Color.FromRgb(174, 174, 177), HoverAmount);
        var accent = (AccentBrush as ISolidColorBrush)?.Color ?? DefaultAccent;
        var head = Mix(dark ? Color.FromRgb(24, 24, 27) : Colors.White, accent, .45);
        var tail = dark ? Mix(accent, Colors.White, .15) : Mix(accent, Colors.Black, .27);

        if (UltraAmount > 0)
            DrawGlow(context, track, thumb, accent, dark);

        context.DrawRectangle(Brush(railColor), new Pen(Brush(edge)), new RoundedRect(track.Deflate(.5), 5.5));
        IBrush fillBrush = AccentAmount <= 0
            ? Brush(plain)
            : new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, .5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, .5, RelativeUnit.Relative),
                GradientStops =
                [
                    new(Mix(plain, head, AccentAmount), 0),
                    new(Mix(plain, tail, AccentAmount), 1)
                ]
            };
        context.DrawRectangle(fillBrush, null, new RoundedRect(fill, 6));

        if (!reduced && SheenAmount > 0)
            DrawSheen(context, fill, dark);

        DrawTicks(context, track, fill.Right, dark);
        if (!reduced && UltraAmount > 0)
        {
            using (context.PushClip(new RoundedRect(fill, 6)))
                DrawSparks(context, track, sparkSeconds, dark, outer: false);
            if (ShowOuterSparks)
                DrawSparks(context, track, sparkSeconds, dark, outer: true);
        }

        var thumbShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(52, 0, 0, 0) });
        var thumbOutline = Focused ? new Pen(Brush(accent), 2) : null;
        context.DrawRectangle(Brush(Colors.White), thumbOutline, new RoundedRect(thumb, 4), thumbShadow);

        if (reduced || !Classes.Contains("accent-effort") || !IsEffectivelyVisible)
        {
            lastFrame = null;
        }
        else if (!framePending && TopLevel.GetTopLevel(this) is { } root)
        {
            framePending = true;
            root.RequestAnimationFrame(frame);
        }
    }

    private void DrawTicks(DrawingContext context, Rect track, double fillEnd, bool dark)
    {
        int count = Math.Max(1, (int)Maximum);
        var travel = track.Width - ThumbWidth;
        var uncovered = Brush(dark ? Color.FromRgb(105, 105, 110) : Color.FromRgb(186, 186, 189));
        var covered = Brush(Color.FromArgb(190, 255, 255, 255));

        for (int step = 0; step <= count; step++)
        {
            var x = ThumbWidth / 2 + travel * step / count;
            context.DrawRectangle(x < fillEnd ? covered : uncovered, null, new Rect(x - 1, track.Center.Y - 2, 2, 4), 1, 1);
        }
    }

    private void DrawGlow(DrawingContext context, Rect track, Rect thumb, Color accent, bool dark)
    {
        var glow = Color.FromArgb((byte)((dark ? 96 : 65) * UltraAmount), accent.R, accent.G, accent.B);
        var sideInset = track.Width * .02 * (1 - UltraAmount);
        var halo = new BoxShadows(new BoxShadow { Blur = 12, Spread = 1, Color = glow });
        context.DrawRectangle(null, null, new RoundedRect(track.Deflate(new Thickness(sideInset, 0)), 6), halo);
        context.DrawRectangle(null, null, new RoundedRect(thumb, 4),
            new BoxShadows(new BoxShadow { Blur = 11, Spread = 4, Color = glow }));
    }

    private void DrawSheen(DrawingContext context, Rect fill, bool dark)
    {
        const double sweepDuration = 4.8;
        double cycle = sheenSeconds % 8;
        if (cycle >= sweepDuration) return;

        var x = fill.Width * (2 * SweepEase.Ease(cycle / sweepDuration) - .5);
        const double directionX = .9063;
        const double directionY = .4226;
        var length = fill.Width * directionX + fill.Height * directionY;
        var halfX = length * directionX / 2;
        var halfY = length * directionY / 2;
        var color = dark ? Color.FromRgb(163, 207, 255) : Colors.White;
        var peak = Color.FromArgb((byte)(46 * SheenAmount), color.R, color.G, color.B);
        var shoulder = Color.FromArgb((byte)(18 * SheenAmount), color.R, color.G, color.B);
        var clear = Color.FromArgb(0, color.R, color.G, color.B);
        var band = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x - halfX, fill.Height / 2 - halfY, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(x + halfX, fill.Height / 2 + halfY, RelativeUnit.Absolute),
            GradientStops = [new(clear, .42), new(shoulder, .47), new(peak, .5), new(shoulder, .53), new(clear, .58)]
        };
        using (context.PushClip(new RoundedRect(fill, 6)))
            context.DrawRectangle(band, null, fill);
    }

    private void DrawSparks(DrawingContext context, Rect track, double time, bool dark, bool outer)
    {
        int count = outer ? 13 : 17;
        var speed = outer ? 1.2 : 3.5;
        var color = outer ? (dark ? Color.FromRgb(175, 224, 255) : Color.FromRgb(0, 114, 204)) : Colors.White;
        for (int i = 0; i < count; i++)
        {
            double phase = (time * .36 + i * .273) % 1;
            double fraction = (i * .618 + time * speed / track.Width) % 1;
            double x = 4 + (track.Width - 8) * fraction;
            double y = outer
                ? (i % 2 == 0 ? track.Top - 1 - phase * 3 : track.Bottom + 1 + phase * 2)
                : track.Top + 3 + i * 7 % 11;
            double opacity = UltraAmount * Math.Sin(phase * Math.PI) * (.45 + .55 * fraction);
            var ink = Brush(Color.FromArgb((byte)(opacity * (outer ? 210 : 235)), color.R, color.G, color.B));
            if (i % 5 == 0)
            {
                using var transform = context.PushTransform(Matrix.CreateTranslation(x, y));
                context.DrawGeometry(ink, null, Star);
            }
            else
            {
                context.DrawEllipse(ink, null, new Point(x, y), .8, .8);
            }
        }
    }

    private void Advance(TimeSpan time)
    {
        framePending = false;
        if (!this.IsAttachedToVisualTree() || !IsEffectivelyVisible || AlderMotion.GetReduced(this) ||
            !Classes.Contains("accent-effort"))
        {
            lastFrame = null;
            return;
        }
        double elapsed = lastFrame is { } previous ? (time - previous).TotalSeconds : 0;
        lastFrame = time;
        sheenSeconds += elapsed;
        if (Classes.Contains("ultra-effort"))
            sparkSeconds += elapsed;
        InvalidateVisual();
    }

    private static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount));

    private static SolidColorBrush Brush(Color color) => new(color);
}
