using System.Diagnostics;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

internal sealed class SheetSnap(Control owner, IReadOnlyList<double> points, double? initialPoint)
{
    private double[] stops = [1];
    private double extent;
    private bool queued;
    private bool animating;
    private long started;
    private double from;
    private double target;
    private double dragOrigin;

    public double Fraction { get; private set; } = 1;
    public bool IsExpanded => Fraction >= .999;

    public double BackdropOpacity
    {
        get
        {
            var partial = stops.Length > 1 ? stops[^2] : 0;
            return Math.Clamp((Fraction - partial) / (1 - partial), 0, 1);
        }
    }

    public void UpdateExtent(double value)
    {
        if (!double.IsFinite(value) || value <= 0 || extent == value) return;
        bool firstLayout = extent <= 0;
        double current = animating ? target : Fraction;
        var index = firstLayout ? 0 : Array.IndexOf(stops, stops.MinBy(stop => Math.Abs(stop - current)));
        extent = value;
        stops = points.Select(point => point <= 1 ? point : Math.Clamp(point / value, .05, 1))
            .Append(1).Distinct().Order().ToArray();
        target = firstLayout && initialPoint is { } point
            ? stops.MinBy(stop => Math.Abs(stop - (point <= 1 ? point : point / value)))
            : stops[Math.Min(index, stops.Length - 1)];
        if (animating)
        {
            from = Fraction;
            started = Stopwatch.GetTimestamp();
        }
        else Fraction = target;
    }

    public void BeginDrag()
    {
        Stop();
        dragOrigin = Fraction;
    }

    public void Drag(double offset)
    {
        if (AlderMotion.GetReduced(owner)) return;
        target = Math.Clamp(dragOrigin - offset, .04, 1);
        QueueFrame();
    }

    public bool Release(double offset, double velocity, bool canDismiss)
    {
        double release = Math.Clamp(dragOrigin - offset, 0, 1);
        double projected = Math.Clamp(release - velocity * .11, 0, 1);
        if (canDismiss && projected < stops[0] * .55) return true;
        AnimateTo(stops.MinBy(stop => Math.Abs(stop - projected)));
        return false;
    }

    public void CancelDrag() => AnimateTo(stops.MinBy(stop => Math.Abs(stop - dragOrigin)));

    public void Step(int direction)
    {
        var current = animating ? target : Fraction;
        var closest = Array.IndexOf(stops, stops.MinBy(stop => Math.Abs(stop - current)));
        AnimateTo(stops[Math.Clamp(closest + direction, 0, stops.Length - 1)]);
    }

    public void Stop()
    {
        animating = false;
        target = Fraction;
    }

    private void AnimateTo(double value)
    {
        target = value;
        from = Fraction;
        started = Stopwatch.GetTimestamp();
        animating = !AlderMotion.GetReduced(owner);
        if (animating) QueueFrame();
        else Commit(target);
    }

    private void QueueFrame()
    {
        if (queued || TopLevel.GetTopLevel(owner) is not { } root) return;
        queued = true;
        root.RequestAnimationFrame(Tick);
    }

    private void Tick(TimeSpan time)
    {
        queued = false;
        if (TopLevel.GetTopLevel(owner) is null) return;
        if (!animating)
        {
            Commit(target);
            return;
        }

        double progress = Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds / 260, 0, 1);
        double remaining = 1 - progress;
        Commit(target + (from - target) * remaining * remaining * remaining * remaining);
        animating = progress < 1;
        if (animating) QueueFrame();
    }

    private void Commit(double value)
    {
        if (value == Fraction) return;
        Fraction = value;
        owner.InvalidateMeasure();
    }
}
