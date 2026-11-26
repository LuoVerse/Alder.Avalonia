using System.Runtime.CompilerServices;
using Avalonia.Animation.Easings;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace Alder.Avalonia.Controls;

internal static class FeedbackMotion
{
    internal static readonly TimeSpan DefaultSpinDuration = TimeSpan.FromMilliseconds(750);
    internal static readonly TimeSpan RingSpinDuration = TimeSpan.FromSeconds(1);
    private static readonly LinearEasing SpinEasing = new();
    private static readonly ConditionalWeakTable<Compositor, ScalarKeyFrameAnimation> SpinnerAnimations = new();
    private static readonly ConditionalWeakTable<Compositor, ScalarKeyFrameAnimation> RingAnimations = new();

    public static void Spin(CompositionVisual visual, TimeSpan duration)
    {
        var animation = duration == DefaultSpinDuration
            ? SpinnerAnimations.GetValue(visual.Compositor, c => CreateSpin(c, DefaultSpinDuration))
            : duration == RingSpinDuration
                ? RingAnimations.GetValue(visual.Compositor, c => CreateSpin(c, RingSpinDuration))
                : CreateSpin(visual.Compositor, duration);
        // A full turn is visually at rest and ensures the reset to zero is serialized.
        visual.RotationAngle = MathF.Tau;
        visual.StartAnimation(nameof(CompositionVisual.RotationAngle), animation);
    }

    public static void StopSpin(CompositionVisual visual)
    {
        visual.StopAnimation(nameof(CompositionVisual.RotationAngle));
        visual.RotationAngle = 0;
    }

    private static ScalarKeyFrameAnimation CreateSpin(Compositor compositor, TimeSpan duration)
    {
        var spin = compositor.CreateScalarKeyFrameAnimation();
        spin.Duration = duration;
        spin.IterationBehavior = AnimationIterationBehavior.Forever;
        spin.StopBehavior = AnimationStopBehavior.SetToInitialValue;
        spin.InsertKeyFrame(0, 0);
        spin.InsertKeyFrame(1, MathF.Tau, SpinEasing);
        return spin;
    }
}
