using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderPressableFeedback
{
    private static readonly TimeSpan RippleAttack = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan RippleGrowth = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan RippleMinimum = TimeSpan.FromMilliseconds(225);
    private static readonly TimeSpan RippleFade = TimeSpan.FromMilliseconds(375);
    private static readonly TimeSpan LegacyRippleDuration = TimeSpan.FromMilliseconds(420);
    private static readonly LinearEasing RippleLinear = new();
    private static readonly CubicEaseOut LegacyRippleEase = new();

    private Point rippleOrigin;
    private long rippleStarted;
    private long rippleReleased;
    private IDisposable? rippleTimer;

    private bool DrawsRippleByFrame => rippleStarted != 0 && ink?.Composition is null;

    private void BeginRipple(Point position)
    {
        if (!CanInteract || !Effects.HasFlag(PressableFeedbackEffects.Ripple) ||
            target is { } motionTarget && AlderMotion.GetReduced(motionTarget)) return;
        rippleTimer?.Dispose();
        rippleTimer = null;
        rippleOrigin = position;
        rippleStarted = Stopwatch.GetTimestamp();
        rippleReleased = 0;

        if (ink is not { } layer || rippleClip is not { } clip)
        {
            StartTimer();
            return;
        }

        layer.StopComposition();
        if (clip.Child is null) clip.Child = layer;
        // Non-solid ink can depend on the current drawing bounds. A fade that overlaps the
        // attack uses the original opacity product instead of a single compositor curve.
        if (clip.Child != layer || FeedbackInk is not ISolidColorBrush brush || PressedOpacity <= 0 ||
            (!usesLegacyRipple && (MinimumPressDuration ?? RippleMinimum) < RippleAttack) ||
            !layer.StartComposition(clip.Bounds.Size, brush))
        {
            StartTimer();
            return;
        }

        var visual = layer.Composition!;
        var geometry = GeometryForRipple(new Rect(clip.Bounds.Size));
        var growth = usesLegacyRipple ? LegacyRippleDuration : RippleDuration ?? RippleGrowth;
        var ease = usesLegacyRipple ? (Easing)LegacyRippleEase : RippleLinear;
        AnimateVector(visual, nameof(CompositionVisual.Scale), Scale(geometry.StartRadius), Scale(geometry.EndRadius), growth, ease);
        if (!usesLegacyRipple)
            AnimateVector(visual, nameof(CompositionVisual.Translation), Offset(geometry.Start), Offset(geometry.End), growth, ease);
        else
            visual.Translation = Offset(position);

        var opacity = (float)PressedOpacity;
        AnimateOpacity(layer, usesLegacyRipple ? opacity : 0, usesLegacyRipple ? 0 : opacity,
            usesLegacyRipple ? LegacyRippleDuration : RippleAttack, usesLegacyRipple ? LegacyRippleEase : ButtonEase);
        if (usesLegacyRipple)
            rippleTimer = DispatcherTimer.RunOnce(StopRipple, LegacyRippleDuration);

        static Vector3D Scale(double radius) => new(radius / Ink.Radius, radius / Ink.Radius, 1);
        static Vector3D Offset(Point point) => new(point.X, point.Y, 0);
    }

    private void EndRipple()
    {
        if (usesLegacyRipple || rippleStarted == 0 || rippleReleased != 0) return;
        rippleReleased = Stopwatch.GetTimestamp();
        if (ink?.Composition is null) return;
        var remaining = (MinimumPressDuration ?? RippleMinimum) - Stopwatch.GetElapsedTime(rippleStarted, rippleReleased);
        if (remaining > TimeSpan.Zero)
            rippleTimer = DispatcherTimer.RunOnce(FadeRipple, remaining);
        else
            FadeRipple();
    }

    private void FadeRipple()
    {
        if (ink is not { Composition: not null } layer) return;
        var elapsed = TimeSpan.FromMilliseconds(Math.Max(0, RippleFadeElapsedMilliseconds()));
        if (elapsed >= RippleFade)
        {
            StopRipple();
            return;
        }

        AnimateOpacity(layer, (float)PressedOpacity, 0, RippleFade, ButtonEase, -elapsed);
        rippleTimer = DispatcherTimer.RunOnce(StopRipple, RippleFade - elapsed);
    }

    private void FallBackToDrawingRipple()
    {
        if (ink?.Composition is null) return;
        ink.StopComposition();
        rippleTimer?.Dispose();
        rippleTimer = null;
        if (rippleStarted != 0) StartTimer();
    }

    private void StopRipple()
    {
        rippleStarted = rippleReleased = 0;
        rippleTimer?.Dispose();
        rippleTimer = null;
        ink?.StopComposition();
        if (rippleClip is { } clip && clip.Child == ink) clip.Child = null;
    }

    private double RippleFadeElapsedMilliseconds() => Math.Min(
        (Stopwatch.GetElapsedTime(rippleStarted) - (MinimumPressDuration ?? RippleMinimum)).TotalMilliseconds,
        Stopwatch.GetElapsedTime(rippleReleased).TotalMilliseconds);

    private double RippleOpacity()
    {
        if (rippleStarted == 0 || !CanInteract || target is { } motionTarget && AlderMotion.GetReduced(motionTarget)) return 0;
        var elapsed = Stopwatch.GetElapsedTime(rippleStarted);
        if (usesLegacyRipple) return PressedOpacity * Math.Pow(1 - Fraction(elapsed, LegacyRippleDuration), 3);
        double opacity = PressedOpacity * ButtonEase.Ease(Fraction(elapsed, RippleAttack));
        return rippleReleased == 0 ? opacity : opacity *
            (1 - ButtonEase.Ease(Math.Clamp(RippleFadeElapsedMilliseconds() / RippleFade.TotalMilliseconds, 0, 1)));
    }

    private RippleGeometry GeometryForRipple(Rect area)
    {
        var position = rippleOrigin;
        if (usesLegacyRipple)
        {
            var x = Math.Max(position.X, area.Width - position.X);
            var y = Math.Max(position.Y, area.Height - position.Y);
            var radius = Math.Sqrt(x * x + y * y);
            return new(position, position, .05 * radius, radius);
        }

        var startRadius = Math.Max(.5, Math.Floor(Math.Max(area.Width, area.Height) * .2) / 2);
        var endRadius = (Math.Sqrt(area.Width * area.Width + area.Height * area.Height) + 87) / 2;
        return new(position + new Vector(startRadius, startRadius), area.Center, startRadius, endRadius);
    }

    private readonly record struct RippleGeometry(Point Start, Point End, double StartRadius, double EndRadius);

    private static void AnimateOpacity(Ink layer, float from, float to, TimeSpan duration, Easing easing,
        TimeSpan delay = default)
    {
        var visual = layer.Composition!;
        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = duration;
        opacity.DelayTime = delay;
        opacity.InsertKeyFrame(0, from);
        opacity.InsertKeyFrame(1, to, easing);
        // Keep the control's opacity at the animation target. A draw-list update copies it to the visual.
        layer.Opacity = to;
        visual.Opacity = to;
        visual.StartAnimation(nameof(CompositionVisual.Opacity), opacity);
    }

    private static void AnimateVector(CompositionVisual visual, string property, Vector3D from, Vector3D to, TimeSpan duration, Easing easing)
    {
        if (property == nameof(CompositionVisual.Scale)) visual.Scale = duration == TimeSpan.Zero ? to : from;
        else visual.Translation = duration == TimeSpan.Zero ? to : from;
        if (duration == TimeSpan.Zero) return;
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }

    private sealed class Ink(AlderPressableFeedback owner) : Control
    {
        internal const double Radius = 32;
        private static readonly RadialGradientBrush SoftEdge = new()
        {
            RadiusX = RelativeScalar.Middle, RadiusY = RelativeScalar.Middle,
            GradientStops = [new(Colors.White, .65), new(Colors.Transparent, 1)]
        };

        private ISolidColorBrush? retainedBrush;
        private Size paintSize;
        internal CompositionVisual? Composition { get; private set; }

        internal bool StartComposition(Size size, ISolidColorBrush brush)
        {
            if (size.Width <= 0 || size.Height <= 0 || ElementComposition.GetElementVisual(this) is not { } visual) return false;
            retainedBrush = brush;
            if (brush is AvaloniaObject mutable) mutable.PropertyChanged += BrushChanged;
            paintSize = size;
            Composition = visual;
            InvalidateVisual();
            return true;
        }

        internal void StopComposition()
        {
            if (Composition is not { } visual) return;
            Composition = null;
            if (retainedBrush is AvaloniaObject mutable) mutable.PropertyChanged -= BrushChanged;
            retainedBrush = null;
            visual.StopAnimation(nameof(CompositionVisual.Opacity));
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.StopAnimation(nameof(CompositionVisual.Translation));
            Opacity = 1;
            visual.Opacity = 1;
            visual.Scale = new Vector3D(1, 1, 1);
            visual.Translation = default;
            InvalidateVisual();
        }

        private void BrushChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => InvalidateVisual();

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty && Composition is not null && Bounds.Size != paintSize)
                owner.FallBackToDrawingRipple();
        }

        public override void Render(DrawingContext context)
        {
            if (retainedBrush is { } solid)
            {
                DrawCircle(context, solid, default, Radius);
                return;
            }
            if (owner.FeedbackInk is not { } brush) return;
            var opacity = owner.RippleOpacity();
            if (opacity <= 0) return;
            using var alpha = context.PushOpacity(opacity);
            var geometry = owner.GeometryForRipple(new Rect(Bounds.Size));
            var elapsed = Stopwatch.GetElapsedTime(owner.rippleStarted);
            var fraction = owner.usesLegacyRipple ? LegacyRippleEase.Ease(Fraction(elapsed, LegacyRippleDuration)) :
                Fraction(elapsed, owner.RippleDuration ?? RippleGrowth);
            var center = geometry.Start + (geometry.End - geometry.Start) * fraction;
            DrawCircle(context, brush, center, geometry.StartRadius + (geometry.EndRadius - geometry.StartRadius) * fraction);
        }

        private void DrawCircle(DrawingContext context, IBrush brush, Point center, double radius)
        {
            if (owner.usesLegacyRipple)
            {
                context.DrawEllipse(brush, null, center, radius, radius);
                return;
            }
            var circle = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using var mask = context.PushOpacityMask(SoftEdge, circle);
            context.FillRectangle(brush, circle);
        }
    }
}
