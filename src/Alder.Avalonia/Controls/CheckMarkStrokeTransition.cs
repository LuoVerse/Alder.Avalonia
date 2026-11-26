using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Shapes;

namespace Alder.Avalonia.Controls;

internal sealed class CheckMarkStrokeTransition : InterpolatingTransitionBase<double>
{
    private static readonly SplineEasing EraseEasing = new(.4, 0, .2, 1);

    public CheckMarkStrokeTransition()
    {
        Property = Shape.StrokeDashOffsetProperty;
        Duration = TimeSpan.FromMilliseconds(200);
    }

    protected override double Interpolate(double progress, double from, double to)
    {
        // Drawing starts after the fill is visible. Erasing begins immediately.
        var amount = to < from
            ? Math.Clamp((progress * Duration.TotalMilliseconds - 15) / 150, 0, 1)
            : EraseEasing.Ease(progress);
        return from + (to - from) * amount;
    }
}
