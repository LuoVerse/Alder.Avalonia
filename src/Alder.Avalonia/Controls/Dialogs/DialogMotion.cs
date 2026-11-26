using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;

namespace Alder.Avalonia.Controls;

internal static class DialogMotion
{
    private static readonly Vector3D FullScale = new(1, 1, 1);
    private static readonly LinearEasing DrawerScrimEasing = new();

    public static TimeSpan Play(Control surface, Control? backdrop, OverlayOptions options,
        DialogPlacement placement, bool entering)
    {
        if (AlderMotion.GetReduced(surface)) return TimeSpan.Zero;

        if (options.Animation is { } custom)
        {
            var time = entering ? custom.EnterDuration : custom.ExitDuration;
            if (time <= TimeSpan.Zero) return TimeSpan.Zero;
            var offset = entering ? custom.EnterOffset : custom.ExitOffset;
            var scale = entering ? custom.EnterScale : custom.ExitScale;
            var center = new Rect(surface.Bounds.Size).Center;
            var ease = new SplineEasing(.32, .72, 0, 1);
            if (backdrop is not null && ElementComposition.GetElementVisual(backdrop) is { } scrimVisual)
                Fade(scrimVisual, entering ? custom.BackdropEnterDuration ?? time : time, entering, ease);
            OverlayMotion.Animate(surface, entering, new Vector3D(offset.X, offset.Y, 0),
                double.IsFinite(scale) && scale > 0 ? scale : 1, time, time, center, ease);
            return time;
        }

        bool drawer = options is DrawerOptions;
        var duration = TimeSpan.FromMilliseconds(entering ? 250 : drawer ? 200 : 100);
        var easing = drawer ? new SplineEasing(.32, .72, 0, 1) : new SplineEasing(.25, .46, .45, .94);

        if (backdrop is not null && ElementComposition.GetElementVisual(backdrop) is { } scrim)
            Fade(scrim, entering && !drawer ? TimeSpan.FromMilliseconds(150) : duration, entering,
                options is DrawerOptions and not SheetOptions ? DrawerScrimEasing : easing);
        if (ElementComposition.GetElementVisual(surface) is not { } visual) return TimeSpan.Zero;

        var size = surface.Bounds.Size;
        if (options is DrawerOptions drawerOptions)
        {
            var edge = drawerOptions.Placement switch
            {
                DrawerPlacement.Top => new Vector3D(0, -size.Height, 0),
                DrawerPlacement.Left => new Vector3D(-size.Width, 0, 0),
                DrawerPlacement.Right => new Vector3D(size.Width, 0, 0),
                _ => new Vector3D(0, size.Height, 0)
            };
            Translate(visual, duration, entering ? edge : null, entering ? default : edge, easing);
            return duration;
        }

        var originY = placement switch
        {
            DialogPlacement.Top => 0,
            DialogPlacement.Bottom => size.Height,
            _ => size.Height / 2
        };
        visual.CenterPoint = new Vector3D(size.Width / 2, originY, 0);
        Fade(visual, duration, entering, easing);
        if (options is not DialogOptions { Size: DialogSize.Full })
        {
            AnimateVector(visual, nameof(CompositionVisual.Scale), duration,
                entering ? new Vector3D(1.05, 1.05, 1) : null,
                entering ? FullScale : new Vector3D(.95, .95, 1), easing);
            double y = placement switch { DialogPlacement.Top => -4, DialogPlacement.Bottom => 4, _ => 0 };
            Translate(visual, duration, entering ? new Vector3D(0, y, 0) : null, default, easing);
        }
        return duration;
    }

    public static void SetDragOffset(Control surface, Vector3D offset)
    {
        if (ElementComposition.GetElementVisual(surface) is not { } visual) return;
        visual.StopAnimation(nameof(CompositionVisual.Translation));
        visual.Translation = offset;
    }

    public static void SnapBack(Control surface)
    {
        if (ElementComposition.GetElementVisual(surface) is not { } visual) return;
        Translate(visual, TimeSpan.FromMilliseconds(300), null, default, new SplineEasing(.32, .72, 0, 1));
    }

    private static void Fade(CompositionVisual visual, TimeSpan duration, bool entering, Easing easing)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        if (entering) animation.InsertKeyFrame(0, 0);
        animation.InsertKeyFrame(1, entering ? 1 : 0, easing);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), animation);
    }

    private static void Translate(CompositionVisual visual, TimeSpan duration, Vector3D? from, Vector3D to, Easing easing)
        => AnimateVector(visual, nameof(CompositionVisual.Translation), duration, from, to, easing);

    private static void AnimateVector(CompositionVisual visual, string property, TimeSpan duration,
        Vector3D? from, Vector3D to, Easing easing)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        if (from is { } start) animation.InsertKeyFrame(0, start);
        // An implicit start picks up the current presentation value on interruption.
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }
}
