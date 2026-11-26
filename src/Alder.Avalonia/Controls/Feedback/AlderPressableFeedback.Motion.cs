using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderPressableFeedback
{
    private static readonly ConditionalWeakTable<Compositor, ImplicitAnimationCollection> SurfaceScaleAnimations = new();
    private static readonly ConditionalWeakTable<Compositor, ImplicitAnimationCollection> SurfaceTintAnimations = new();
    private static readonly ConditionalWeakTable<Compositor, ImplicitAnimationCollection> FeedbackTintAnimations = new();
    private static readonly ConditionalWeakTable<Compositor, ImplicitAnimationCollection> SpringScaleAnimations = new();
    private static readonly SplineEasing ButtonEase = new(.25, .1, .25, 1);
    private static readonly TimeSpan SpringDuration = TimeSpan.FromMilliseconds(220);
    private IPointer? springPointer;
    private bool springPointerInside;
    private Key? springKey;
    private CompositionVisual? springVisual;
    private ImplicitAnimationCollection? originalAnimations;
    private Vector3D originalScale;
    private Vector3D originalCenter;

    private bool HasSpringScale => Effects.HasFlag(PressableFeedbackEffects.SpringScale);

    private void SetButtonSpringClass(bool enabled)
    {
        if (target is AlderButton button)
            ((IPseudoClasses)button.Classes).Set(":spring-feedback", enabled);
    }

    internal void BeginSpringKey(Key key)
    {
        if (!HasSpringScale || springKey is not null || !CanInteract) return;
        springKey = key;
        RefreshSpringScale();
    }

    internal void EndSpringKey(Key key)
    {
        if (springKey == key) CancelSpringKey();
    }

    internal void CancelSpringKey()
    {
        if (springKey is null) return;
        springKey = null;
        RefreshSpringScale();
    }

    private void RefreshSpringScale()
    {
        if (target is not { } owner || !HasSpringScale) return;
        var surface = ScaleTarget ?? owner;
        var pressed = CanInteract && (springPointer is not null && springPointerInside || springKey is not null);
        if (AlderMotion.GetReduced(owner))
        {
            ReleaseSpringVisual();
            return;
        }
        if (springVisual is null)
        {
            if (!pressed || ElementComposition.GetElementVisual(surface) is not { } visual) return;
            springVisual = visual;
            originalAnimations = visual.ImplicitAnimations;
            originalScale = visual.Scale;
            originalCenter = visual.CenterPoint;
            visual.ImplicitAnimations = SpringScaleAnimations.GetValue(visual.Compositor, compositor =>
                CreateScaleAnimation(compositor, SpringDuration, new PressSpringEasing()));
        }
        var width = surface.Bounds.Width;
        var height = surface.Bounds.Height;
        springVisual.CenterPoint = new Vector3D(width / 2, height / 2, originalCenter.Z);
        var scale = pressed ? PressScale : 1;
        springVisual.Scale = new Vector3D(originalScale.X * scale, originalScale.Y * scale, originalScale.Z);
    }

    private void ReleaseSpringVisual()
    {
        if (springVisual is not { } visual) return;
        springVisual = null;
        visual.ImplicitAnimations = null;
        visual.StopAnimation(nameof(CompositionVisual.Scale));
        visual.Scale = originalScale;
        visual.CenterPoint = originalCenter;
        visual.ImplicitAnimations = originalAnimations;
        originalAnimations = null;
    }

    private sealed class PressSpringEasing : Easing
    {
        // Response of the navigation press spring (stiffness 500, damping 30, mass .6).
        private const double Decay = 25;
        private static readonly double Frequency = Math.Sqrt(500 / .6 - Decay * Decay);

        public override double Ease(double progress)
        {
            if (progress >= 1) return 1;
            double time = progress * SpringDuration.TotalSeconds;
            double angle = Frequency * time;
            return 1 - Math.Exp(-Decay * time) * (Math.Cos(angle) + Decay / Frequency * Math.Sin(angle));
        }
    }

    internal static void ConfigureSurfaceScale(CompositionVisual visual, bool reducedMotion)
    {
        visual.ImplicitAnimations = reducedMotion ? null : SurfaceScaleAnimations.GetValue(visual.Compositor, compositor =>
            CreateScaleAnimation(compositor, TimeSpan.FromMilliseconds(250), ButtonEase));
        if (reducedMotion) visual.StopAnimation(nameof(CompositionVisual.Scale));
    }

    private static ImplicitAnimationCollection CreateScaleAnimation(Compositor compositor, TimeSpan duration, Easing easing)
    {
        var scale = compositor.CreateVector3DKeyFrameAnimation();
        scale.Target = nameof(CompositionVisual.Scale);
        scale.Duration = duration;
        scale.InsertExpressionKeyFrame(1, "this.FinalValue", easing);
        var animations = compositor.CreateImplicitAnimationCollection();
        animations[scale.Target] = scale;
        return animations;
    }

    internal static void UpdateSurfaceScale(CompositionVisual visual, double pressedScale, double activeScale, bool reducedMotion)
    {
        var scale = activeScale == 1 && !reducedMotion ? pressedScale : activeScale;
        visual.Scale = new Vector3D(scale, scale, 1);
    }

    internal static void ConfigureSurfaceTint(CompositionVisual visual, float opacity, bool reducedMotion, bool feedbackFade)
    {
        visual.Opacity = opacity;
        var animationsByCompositor = feedbackFade ? FeedbackTintAnimations : SurfaceTintAnimations;
        visual.ImplicitAnimations = reducedMotion ? null : animationsByCompositor.GetValue(visual.Compositor, compositor =>
        {
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.Target = nameof(CompositionVisual.Opacity);
            fade.Duration = TimeSpan.FromMilliseconds(feedbackFade ? 150 : 100);
            fade.InsertExpressionKeyFrame(1, "this.FinalValue", feedbackFade ? ButtonEase : new SplineEasing(0, 0, .2, 1));
            var animations = compositor.CreateImplicitAnimationCollection();
            animations[fade.Target] = fade;
            return animations;
        });
        if (reducedMotion) visual.StopAnimation(nameof(CompositionVisual.Opacity));
    }
}
