using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace Alder.Avalonia.Controls;

internal readonly record struct BloomMenuMotion(TimeSpan Duration, Easing Ease)
{
    internal static readonly BloomMenuMotion Morph = new(TimeSpan.FromMilliseconds(550), new SpringEasing(.55, 300, 32, .9));
    internal static readonly BloomMenuMotion ReducedMorph = new(TimeSpan.FromMilliseconds(150), new SplineEasing(.25, .1, .25, 1));
    internal static readonly BloomMenuMotion Iris = new(TimeSpan.FromMilliseconds(450), new SplineEasing(.16, 1, .3, 1));
    internal static readonly BloomMenuMotion Item = new(TimeSpan.FromMilliseconds(380), new SpringEasing(.38, 440, 34, 1));
    internal static readonly BloomMenuMotion Body = new(TimeSpan.FromMilliseconds(145), ReducedMorph.Ease);
    internal static readonly BloomMenuMotion ExitContent = new(TimeSpan.FromMilliseconds(110), ReducedMorph.Ease);
    internal static readonly BloomMenuMotion Caption = new(TimeSpan.FromMilliseconds(95), ReducedMorph.Ease);

    internal void Fade(CompositionVisual visual, float target, TimeSpan delay = default, float? from = null)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        Configure(animation, delay);
        if (from is { } initial) animation.InsertKeyFrame(0, initial);
        else StartFromCurrent(animation);
        animation.InsertKeyFrame(1, target, Ease);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), animation);
    }

    internal void Resize(CompositionVisual visual, Size target, Size? from = null)
    {
        var animation = visual.Compositor.CreateVectorKeyFrameAnimation();
        Configure(animation);
        if (from is { } initial) animation.InsertKeyFrame(0, new Vector(initial.Width, initial.Height));
        else StartFromCurrent(animation);
        animation.InsertKeyFrame(1, new Vector(target.Width, target.Height), Ease);
        visual.StartAnimation(nameof(CompositionVisual.Size), animation);
    }

    internal void Translate(CompositionVisual visual, Vector target, Vector? from = null)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        Configure(animation);
        if (from is { } initial) animation.InsertKeyFrame(0, new Vector3D(initial.X, initial.Y, 0));
        else StartFromCurrent(animation);
        animation.InsertKeyFrame(1, new Vector3D(target.X, target.Y, 0), Ease);
        visual.StartAnimation(nameof(CompositionVisual.Translation), animation);
    }

    internal void Scale(CompositionVisual visual, Vector3D target, Vector3D? from = null, TimeSpan delay = default)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        Configure(animation, delay);
        if (from is { } initial) animation.InsertKeyFrame(0, initial);
        else StartFromCurrent(animation);
        animation.InsertKeyFrame(1, target, Ease);
        visual.StartAnimation(nameof(CompositionVisual.Scale), animation);
    }

    private void Configure(KeyFrameAnimation animation, TimeSpan delay = default)
    {
        animation.Duration = Duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
    }

    private static void StartFromCurrent(KeyFrameAnimation animation) =>
        animation.InsertExpressionKeyFrame(0, "this.StartingValue", new LinearEasing());

    // Sample the same damped oscillator at the compositor's frame time.
    private sealed class SpringEasing(double seconds, double stiffness, double damping, double mass) : Easing
    {
        private readonly double decay = damping / (2 * mass);
        private readonly double frequency = Math.Sqrt(stiffness / mass - Math.Pow(damping / (2 * mass), 2));

        public override double Ease(double progress)
        {
            if (progress >= 1) return 1;
            var time = progress * seconds;
            return 1 - Math.Exp(-decay * time) * (Math.Cos(frequency * time) + decay / frequency * Math.Sin(frequency * time));
        }
    }
}
