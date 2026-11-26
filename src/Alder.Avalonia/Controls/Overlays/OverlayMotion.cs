using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;

namespace Alder.Avalonia.Controls;

internal static class OverlayMotion
{
    public static void Animate(Control control, bool entering, Vector3D offset, double scale,
        TimeSpan duration, TimeSpan fadeDuration, Point pivot, Easing easing)
    {
        if (ElementComposition.GetElementVisual(control) is not { } visual) return;
        visual.CenterPoint = new Vector3D(pivot.X, pivot.Y, 0);
        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = fadeDuration;
        if (entering) opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(1, entering ? 1 : 0, easing);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), opacity);

        var unit = new Vector3D(1, 1, 1);
        var scaled = new Vector3D(scale, scale, 1);
        if (scale != 1) AnimateVector(nameof(CompositionVisual.Scale), entering ? scaled : null, entering ? unit : scaled);
        AnimateVector(nameof(CompositionVisual.Translation), entering ? offset : null, entering ? default : offset);

        void AnimateVector(string property, Vector3D? from, Vector3D to)
        {
            var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
            animation.Duration = duration;
            if (from is { } initial) animation.InsertKeyFrame(0, initial);
            animation.InsertKeyFrame(1, to, easing);
            visual.StartAnimation(property, animation);
        }
    }

    public static void Shift(Control control, Vector offset, TimeSpan duration, Easing easing)
        => Translate(control, new Vector3D(offset.X, offset.Y, 0), default, duration, easing);

    public static void ShiftFromCurrent(Control control, Vector offset, TimeSpan duration, Easing easing)
    {
        if (ElementComposition.GetElementVisual(control) is not { } visual) return;
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        animation.SetScalarParameter("deltaX", (float)offset.X);
        animation.SetScalarParameter("deltaY", (float)offset.Y);
        animation.InsertExpressionKeyFrame(0, "this.StartingValue + Vector3(deltaX, deltaY, 0)");
        animation.InsertKeyFrame(1, default, easing);
        visual.StartAnimation(nameof(CompositionVisual.Translation), animation);
    }

    public static void Translate(Control control, Vector3D? from, Vector3D to, TimeSpan duration, Easing easing)
    {
        if (ElementComposition.GetElementVisual(control) is not { } visual) return;
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        if (from is { } initial) animation.InsertKeyFrame(0, initial);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(nameof(CompositionVisual.Translation), animation);
    }

    public static void MoveBetween(CompositionVisual visual, Rect from, Rect to, TimeSpan? duration = null, Easing? easing = null)
    {
        var compositor = visual.Compositor;
        easing ??= new SplineEasing(.25, .1, .25, 1);
        var translation = compositor.CreateVector3DKeyFrameAnimation();
        translation.Duration = duration ?? TimeSpan.FromMilliseconds(220);
        // The compositor rebases an in-flight transition when the selected bounds change.
        translation.SetScalarParameter("deltaX", (float)(from.X - to.X));
        translation.SetScalarParameter("deltaY", (float)(from.Y - to.Y));
        translation.InsertExpressionKeyFrame(0, "this.StartingValue + Vector3(deltaX, deltaY, 0)");
        translation.InsertKeyFrame(1, default, easing);
        var scale = compositor.CreateVector3DKeyFrameAnimation();
        scale.Duration = translation.Duration;
        scale.SetScalarParameter("ratioX", (float)(from.Width / to.Width));
        scale.SetScalarParameter("ratioY", (float)(from.Height / to.Height));
        scale.InsertExpressionKeyFrame(0, "this.StartingValue * Vector3(ratioX, ratioY, 1)");
        scale.InsertKeyFrame(1, new Vector3D(1, 1, 1), easing);
        visual.StartAnimation(nameof(CompositionVisual.Translation), translation);
        visual.StartAnimation(nameof(CompositionVisual.Scale), scale);
    }

    public static void Scale(Control surface, double factor, Point pivot, TimeSpan duration, Easing easing)
    {
        if (ElementComposition.GetElementVisual(surface) is not { } visual) return;
        visual.CenterPoint = new Vector3D(pivot.X, pivot.Y, 0);
        var scale = new Vector3D(factor, factor, 1);
        if (duration == TimeSpan.Zero || AlderMotion.GetReduced(surface))
        {
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.Scale = scale;
            return;
        }

        var transition = visual.Compositor.CreateVector3DKeyFrameAnimation();
        transition.Duration = duration;
        transition.InsertKeyFrame(1, scale, easing);
        visual.StartAnimation(nameof(CompositionVisual.Scale), transition);
    }

    public static void Reset(Control control)
    {
        if (ElementComposition.GetElementVisual(control) is not { } visual) return;
        visual.StopAnimation(nameof(CompositionVisual.Opacity));
        visual.StopAnimation(nameof(CompositionVisual.Scale));
        visual.StopAnimation(nameof(CompositionVisual.Translation));
        visual.Opacity = 1;
        visual.Scale = new Vector3D(1, 1, 1);
        visual.Translation = default;
    }
}
