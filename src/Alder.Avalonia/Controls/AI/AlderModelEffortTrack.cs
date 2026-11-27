using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Paints the effort meter while the underlying slider owns input and accessibility.</summary>
internal sealed partial class AlderModelEffortTrack : Control
{
    private const double TrackHeight = 16;
    private const double SheenDuration = 8;
    private const double SheenTravel = 4.8;
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
    private long? sweepStarted;
    private CompositionLinearGradientBrush? sheenBrush;
    private double sheenWidth;
    private bool? sheenDark;
    private Visual[]? visibilityAncestors;
    private bool inViewport = true;

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
        EffectiveViewportChanged += (_, e) =>
        {
            bool visible = new Rect(Bounds.Size).Intersects(e.EffectiveViewport);
            if (visible == inViewport) return;
            inViewport = visible;
            if (!visible) StopSweepClock();
            InvalidateVisual();
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (visibilityAncestors is not null)
        {
            foreach (var ancestor in visibilityAncestors) ancestor.PropertyChanged -= OnAncestorChanged;
            visibilityAncestors = null;
        }
        StopSweepClock();
        sheenBrush = null;
        sheenDark = null;
        inViewport = true;
        lastFrame = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == IsVisibleProperty && !IsVisible) ||
            (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this)))
            StopSweepClock();
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width < ThumbWidth || Maximum <= 0)
        {
            StopSweepClock();
            lastFrame = null;
            return;
        }

        bool dark = ActualThemeVariant == ThemeVariant.Dark;
        bool reduced = AlderMotion.GetReduced(this);
        bool animate = !reduced && inViewport && IsEffectivelyVisible && Classes.Contains("accent-effort");
        if (animate) sweepStarted ??= Stopwatch.GetTimestamp();
        else StopSweepClock();
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

        // Ultra still redraws its particles. Compose only the settled Max band.
        bool composed = animate && SheenAmount == 1 && UltraAmount <= 0 &&
            !Classes.Contains("ultra-effort") && TryDrawComposedSheen(context, fill, dark);
        if (!composed)
        {
            StopBrush();
            if (!reduced && SheenAmount > 0)
                DrawSheen(context, fill, dark);
        }

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

        bool needsFrame = animate && (UltraAmount > 0 || (SheenAmount > 0 && !composed));
        if (!needsFrame)
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

    private bool TryDrawComposedSheen(DrawingContext context, Rect fill, bool dark)
    {
        // During snapping the fill width changes every frame. Use the existing drawing for
        // those frames so the gradient geometry stays exact without restarting animations.
        if (Math.Abs(GetBaseValue(ValueProperty).GetValueOrDefault() - Value) > .001 ||
            ElementComposition.GetElementVisual(this)?.Compositor is not { } compositor)
            return false;

        if (sheenBrush is null)
        {
            sheenBrush = compositor.CreateLinearGradientBrush();
            visibilityAncestors = this.GetVisualAncestors().ToArray();
            foreach (var ancestor in visibilityAncestors) ancestor.PropertyChanged += OnAncestorChanged;
        }
        var brush = sheenBrush;
        if (sheenDark != dark)
        {
            var color = dark ? Color.FromRgb(163, 207, 255) : Colors.White;
            brush.GradientStops =
            [
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), .42),
                new GradientStop(Color.FromArgb(18, color.R, color.G, color.B), .47),
                new GradientStop(Color.FromArgb(46, color.R, color.G, color.B), .5),
                new GradientStop(Color.FromArgb(18, color.R, color.G, color.B), .53),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), .58)
            ];
            sheenDark = dark;
        }
        if (Math.Abs(sheenWidth - fill.Width) > .05)
        {
            var halfLength = (fill.Width * .9063 + fill.Height * .4226) / 2;
            var halfX = halfLength * .9063;
            var halfY = halfLength * .4226;
            var delay = TimeSpan.FromSeconds(-SweepPhase);
            StartEndpoint(brush, nameof(CompositionLinearGradientBrush.StartPoint), fill, -halfX, -halfY, delay);
            StartEndpoint(brush, nameof(CompositionLinearGradientBrush.EndPoint), fill, halfX, halfY, delay);
            sheenWidth = fill.Width;
        }
        using (context.PushClip(new RoundedRect(fill, 6)))
            context.DrawRectangle(brush, null, fill);
        return true;
    }

    private static void StartEndpoint(
        CompositionLinearGradientBrush brush, string property, Rect fill, double x, double y, TimeSpan delay)
    {
        var animation = brush.Compositor.CreateRelativePointKeyFrameAnimation();
        animation.Duration = TimeSpan.FromSeconds(SheenDuration);
        animation.DelayTime = delay;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        var start = new RelativePoint(-fill.Width * .5 + x, fill.Height / 2 + y, RelativeUnit.Absolute);
        var end = new RelativePoint(fill.Width * 1.5 + x, fill.Height / 2 + y, RelativeUnit.Absolute);
        animation.InsertKeyFrame(0, start);
        animation.InsertKeyFrame((float)(SheenTravel / SheenDuration), end, SweepEase);
        animation.InsertKeyFrame(1, end);
        brush.StartAnimation(property, animation);
    }

    private void OnAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsVisibleProperty) return;
        if (change.GetNewValue<bool>()) InvalidateVisual();
        else StopSweepClock();
    }

    private double SweepPhase => (sheenSeconds + (sweepStarted is { } timestamp
        ? Stopwatch.GetElapsedTime(timestamp).TotalSeconds : 0)) % SheenDuration;

    private void StopSweepClock()
    {
        sheenSeconds = SweepPhase;
        sweepStarted = null;
        StopBrush();
    }

    private void StopBrush()
    {
        if (sheenWidth == 0 || sheenBrush is not { } brush) return;
        brush.StopAnimation(nameof(CompositionLinearGradientBrush.StartPoint));
        brush.StopAnimation(nameof(CompositionLinearGradientBrush.EndPoint));
        sheenWidth = 0;
    }

    private void DrawSheen(DrawingContext context, Rect fill, bool dark)
    {
        double cycle = SweepPhase;
        if (cycle >= SheenTravel) return;

        var x = fill.Width * (2 * SweepEase.Ease(cycle / SheenTravel) - .5);
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
