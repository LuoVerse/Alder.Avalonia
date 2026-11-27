using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class LoaderGlyph(IBrush ink)
{
    internal static readonly SplineEasing Ease = new(.42, 0, .58, 1);
    private static readonly Geometry RingArc = Geometry.Parse("M10 0 A10 10 0 0 1 -.29 10");
    private static readonly Geometry Sparkle = Geometry.Parse(
        "M0 -1 C.08 -.42 .42 -.08 1 0 C.42 .08 .08 .42 0 1 C-.08 .42 -.42 .08 -1 0 C-.42 -.08 -.08 -.42 0 -1Z");
    private static readonly Geometry InfinityTrack = Geometry.Parse(
        "M0 0 C4.29 -7.71 16.29 -7.71 16.29 0 C16.29 7.71 4.29 7.71 0 0 C-4.29 -7.71 -16.29 -7.71 -16.29 0 C-16.29 7.71 -4.29 7.71 0 0Z");

    private readonly Pen ring = new(ink, 2.5, lineCap: PenLineCap.Round);
    private readonly Pen outline = new(ink, 2);
    private readonly Pen classic = new(ink, 1.92, lineCap: PenLineCap.Round);
    private readonly Pen orbit = new(ink, 1.2);
    private readonly Pen trail = new(ink, 2.36, lineCap: PenLineCap.Round);

    public void Draw(DrawingContext context, AiLoaderVariant variant, double seconds, bool still)
    {
        switch (variant)
        {
            case AiLoaderVariant.Ring: DrawRing(context, seconds, still); break;
            case AiLoaderVariant.Classic: DrawClassic(context, seconds, still); break;
            case AiLoaderVariant.Dots: DrawDots(context, seconds, still); break;
            case AiLoaderVariant.Pulse: DrawPulse(context, seconds, still); break;
            case AiLoaderVariant.Scan: DrawScan(context, seconds, still); break;
            case AiLoaderVariant.Grid: DrawGrid(context, seconds, still); break;
            case AiLoaderVariant.Shuffle: DrawShuffle(context, seconds, still); break;
            case AiLoaderVariant.Wave: DrawWave(context, seconds, still); break;
            case AiLoaderVariant.Sparkle: DrawSparkles(context, seconds, still); break;
            case AiLoaderVariant.Atom: DrawAtom(context, seconds, still); break;
            case AiLoaderVariant.Morph: DrawMorph(context, seconds, still); break;
            case AiLoaderVariant.Helix: DrawHelix(context, seconds, still); break;
            case AiLoaderVariant.Twist: DrawTwist(context, seconds, still); break;
            case AiLoaderVariant.Infinity: DrawInfinity(context, seconds, still); break;
        }
    }

    private void DrawRing(DrawingContext context, double seconds, bool still)
    {
        DrawRingRail(context);
        using (context.PushTransform(Matrix.CreateRotation(still ? 0 : Cycle(seconds / .8) * Math.Tau)))
            DrawRingArc(context);
    }

    internal void DrawRingRail(DrawingContext context)
    {
        using var track = context.PushOpacity(.2);
        context.DrawEllipse(null, ring, default, 10, 10);
    }

    internal void DrawRingArc(DrawingContext context) => context.DrawGeometry(null, ring, RingArc);

    private void DrawClassic(DrawingContext context, double seconds, bool still)
    {
        for (int index = 0; index < 12; index++)
        {
            double angle = index * Math.Tau / 12;
            var direction = new Vector(Math.Cos(angle), Math.Sin(angle));
            using var fade = context.PushOpacity(1 - .85 * Cycle((still ? 0 : seconds / 1.2) - index / 12d));
            context.DrawLine(classic, (Point)(direction * 6.98), (Point)(direction * 10.82));
        }
    }

    private void DrawDots(DrawingContext context, double seconds, bool still)
    {
        for (int index = 0; index < 3; index++)
        {
            double phase = Cycle(seconds / 1.2 - index * .1);
            double strength = phase < .8 ? Ease.Ease(1 - Math.Abs(phase / .4 - 1)) : 0;
            Dot(context, new Point((index - 1) * 9, 0), 3 * (still ? 1 : .85 + .15 * strength),
                still ? .7 : .35 + .65 * strength);
        }
    }

    private void DrawPulse(DrawingContext context, double seconds, bool still)
    {
        double strength = Breathe(seconds / 1.4);
        Dot(context, default, 4.8 * (still ? 1 : .92 + .08 * strength), still ? .7 : .45 + .55 * strength);
    }

    private void DrawScan(DrawingContext context, double seconds, bool still)
    {
        DrawScanRail(context);
        using var offset = context.PushTransform(Matrix.CreateTranslation(0, still ? 0 : -5.5 + 11 * Breathe(seconds / 1.8)));
        DrawScanLine(context);
    }

    internal void DrawScanRail(DrawingContext context)
    {
        using var track = context.PushOpacity(.22);
        context.DrawRectangle(null, outline, new Rect(-9, -9, 18, 18), 4, 4);
    }

    internal void DrawScanLine(DrawingContext context) => context.DrawLine(ring, new Point(-4.5, 0), new Point(4.5, 0));

    private void DrawGrid(DrawingContext context, double seconds, bool still)
    {
        ReadOnlySpan<double> rest = [1, .85, .55, .3, .15];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                double phase = Cycle(seconds / .64 - (x + y) * .115);
                double strength = phase < .55 ? Breathe(phase / .55) : 0;
                using var fade = context.PushOpacity(still ? rest[x + y] : .12 + .88 * strength);
                context.DrawRectangle(ink, null, new Rect(x * 9 - 12, y * 9 - 12, 6, 6), 1.5, 1.5);
            }
        }
    }

    private void DrawShuffle(DrawingContext context, double seconds, bool still)
    {
        ReadOnlySpan<Point> corners = [new(-10, -10), new(1, -10), new(1, 1), new(-10, 1)];
        double cycle = still ? 0 : Cycle(seconds / 3.6) * 4;
        int step = (int)cycle;
        double move = Ease.Ease(Math.Clamp((cycle - step - .56) / .2, 0, 1));
        int empty = 3 - step;
        int moving = (empty + 3) % 4;
        for (int position = 0; position < corners.Length; position++)
        {
            if (position == empty) continue;
            var from = corners[position];
            var point = position == moving ? from + (corners[empty] - from) * move : from;
            using var fade = context.PushOpacity(position == moving ? .4 + .6 * (still ? 1 : Breathe(move / 2)) : .4);
            context.DrawRectangle(ink, null, new Rect(point, new Size(9, 9)), 2.2, 2.2);
        }
    }

    private void DrawWave(DrawingContext context, double seconds, bool still)
    {
        for (int index = 0; index < 5; index++)
        {
            double strength = Breathe((still ? 0 : seconds / .9) - index * .14);
            double height = 24 * (.3 + .7 * strength);
            using var fade = context.PushOpacity(.35 + .65 * strength);
            context.DrawRectangle(ink, null, new Rect(index * 5.4 - 12, -height / 2, 2.4, height), 1.2, 1.2);
        }
    }

    private void DrawSparkles(DrawingContext context, double seconds, bool still)
    {
        ReadOnlySpan<Point> positions = [new(0, -1), new(-7.7, -6.7), new(7.7, -5.8), new(6.7, 6.2), new(-6.7, 6.7)];
        ReadOnlySpan<double> radii = [8, 4.4, 3.6, 4.4, 3.2];
        for (int index = 0; index < positions.Length; index++)
        {
            double phase = Cycle(seconds / 1.4 + index * .19);
            double twinkle = still ? 1 : phase < .4 ? Ease.Ease(phase / .4) : 1 - Ease.Ease((phase - .4) / .6);
            if (twinkle <= 0) continue;
            double radius = radii[index] * twinkle;
            using var fade = context.PushOpacity(still ? .525 : .75 * twinkle);
            var p = positions[index];
            using var transform = context.PushTransform(Matrix.CreateScale(radius, radius) * Matrix.CreateTranslation(p.X, p.Y));
            context.DrawGeometry(ink, null, Sparkle);
        }
    }

    private void DrawAtom(DrawingContext context, double seconds, bool still)
    {
        context.DrawEllipse(null, orbit, default, 11.4, 11.4);
        for (int index = 0; index < 3; index++)
        {
            double phase = Cycle(seconds / .6 + index / 3d);
            double flatten = .12 + .88 * Math.Abs(Math.Cos(phase * Math.PI));
            using var rotation = context.PushTransform(Matrix.CreateRotation((index * 60 - 30) * Math.PI / 180));
            context.DrawEllipse(null, orbit, default, 11.4, 11.4 * (still ? .42 : flatten));
        }
    }

    private void DrawMorph(DrawingContext context, double seconds, bool still)
    {
        double phase = still ? 0 : Cycle(seconds / 3.32) * 6;
        double triangle = Math.Clamp(1 - Math.Abs(phase - 1.5) / 1.5, 0, 1);
        double square = Math.Clamp(1 - Math.Abs(phase - 4.5) / 1.5, 0, 1);
        var top = new Point(0, -8.8);
        var right = new Point(8.4, 6.2);
        var left = new Point(-8.4, 6.2);
        for (int index = 0; index < 18; index++)
        {
            double angle = index * Math.Tau / 18;
            double x = Math.Sin(angle), y = -Math.Cos(angle);
            var circle = new Point(8.5 * x, 8.5 * y);
            var trianglePoint = index switch
            {
                <= 6 => top + (right - top) * (index / 6d),
                <= 12 => right + (left - right) * ((index - 6) / 6d),
                _ => left + (top - left) * ((index - 12) / 6d)
            };
            double side = Math.Max(Math.Abs(x), Math.Abs(y));
            var squarePoint = new Point(x * 7.1 / side, y * 7.1 / side);
            var point = circle + (trianglePoint - circle) * Ease.Ease(triangle) + (squarePoint - circle) * Ease.Ease(square);
            Dot(context, point, 1.02, .88);
        }
    }

    private void DrawHelix(DrawingContext context, double seconds, bool still)
    {
        double rotation = still ? .55 : Cycle(seconds / 5.712) * Math.Tau;
        for (int index = 0; index < 18; index++)
        {
            double level = index / 17d;
            double spread = 8.3 * Math.Sin(Math.PI * level);
            for (int strand = 0; strand < 2; strand++)
            {
                double angle = level * Math.Tau * 1.58 + strand * Math.PI + rotation;
                double depth = (1 + Math.Cos(angle)) / 2;
                var p = new Point(spread * Math.Sin(angle), (level - .5) * 17.4 + .8 * (depth - .5));
                Dot(context, p, .31 + .37 * depth, .18 + .67 * depth);
            }
        }
    }

    private void DrawTwist(DrawingContext context, double seconds, bool still)
    {
        for (int layer = 0; layer < 7; layer++)
        {
            double level = (layer - 3) / 3d;
            double radius = 8.5 * Math.Sqrt(1 - level * level);
            double phase = Cycle(seconds / 2.77 - layer * .075);
            double turn = still ? 0 : Ease.Ease(Math.Clamp(phase / .055, 0, 1)) *
                Ease.Ease(Math.Clamp((.5 - phase) / .055, 0, 1)) * Math.PI / 2;
            int count = layer is 0 or 6 ? 1 : 6;
            for (int index = 0; index < count; index++)
            {
                double angle = index * Math.Tau / count + layer * .72 + turn;
                double depth = (1 + Math.Sin(angle)) / 2;
                var p = new Point(radius * Math.Cos(angle), level * 8.4 + radius * Math.Sin(angle) * .32);
                Dot(context, p, .4 + .31 * depth, .30 + .55 * depth);
            }
        }
    }

    private void DrawInfinity(DrawingContext context, double seconds, bool still)
    {
        using (context.PushOpacity(.2))
            context.DrawGeometry(null, trail, InfinityTrack);

        double lead = (still ? .22 : Cycle(seconds / 1.2));
        for (int index = 5; index >= 0; index--)
        {
            double phase = lead - index * .021;
            using var fade = context.PushOpacity(1 - index * .136);
            context.DrawLine(trail, InfinityPoint(phase - .012), InfinityPoint(phase + .012));
        }
    }

    private static Point InfinityPoint(double cycle)
    {
        double phase = Cycle(cycle) * 4;
        double t = phase - Math.Floor(phase);
        if (phase is >= 1 and < 2 or >= 3) t = 1 - t;
        double u = 1 - t;
        double x = 3 * u * u * t * 4.29 + (3 * u * t * t + t * t * t) * 16.29;
        double y = -3 * u * t * 7.71;
        if (phase >= 2) x = -x;
        if (phase is >= 1 and < 2 or >= 3) y = -y;
        return new Point(x, y);
    }

    private void Dot(DrawingContext context, Point center, double radius, double opacity)
    {
        using var fade = context.PushOpacity(opacity);
        context.DrawEllipse(ink, null, center, radius, radius);
    }

    private static double Cycle(double progress) => progress - Math.Floor(progress);
    private static double Breathe(double progress) => Ease.Ease(1 - Math.Abs(2 * Cycle(progress) - 1));
}
