using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderOverflowActionsPanel : Panel
{
    private readonly RectangleGeometry viewport = new();
    private readonly TranslateTransform overflowOffset = new();
    private readonly BlurEffect overflowBlur = new();
    private double reveal;

    internal bool OverflowContainsFocus => Children.Count == 2 && Children[1].IsKeyboardFocusWithin;
    private double Spacing => TemplatedParent is AlderOverflowActions { Size: AlderSize.Small } ? 4 : 6;

    internal void Present(bool expanded, double width, double opacity, bool reduced)
    {
        if (Children.Count != 2) return;
        if (reveal != width)
        {
            reveal = width;
            InvalidateMeasure();
        }

        var overflow = Children[1];
        overflow.IsVisible = expanded || width > 0 || opacity > 0;
        overflow.IsHitTestVisible = expanded;
        KeyboardNavigation.SetTabNavigation(overflow, expanded ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        overflow.Opacity = opacity;
        overflowOffset.X = reduced ? 0 : -6 * (1 - opacity);
        overflow.RenderTransform = overflowOffset;
        ApplyBlur(overflow, overflowBlur, reduced ? 0 : 4 * (1 - opacity));

    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        return new Size(PrimaryEnd + ExtraWidth,
            Children.Max(child => child.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(Children[0], 0, finalSize.Height);
        var extra = Children[1];
        var extraWidth = ExtraWidth;
        // Clip only the secondary group as its slot changes; keep the outer shadow
        // and the toggle's focus ring outside this moving clip.
        var gap = Children[0].DesiredSize.Width > 0 ? Spacing * Math.Min(reveal, 1) : 0;
        viewport.Rect = new Rect(0, 0, Math.Max(0, extraWidth - gap), finalSize.Height);
        extra.Clip = reveal >= 1 ? null : viewport;
        Place(extra, PrimaryEnd + gap, finalSize.Height);
        return finalSize;
    }

    private double PrimaryEnd => Children[0].DesiredSize.Width;
    private double ExtraWidth => Children[1].DesiredSize.Width > 0
        ? (Children[1].DesiredSize.Width + (PrimaryEnd > 0 ? Spacing : 0)) * reveal : 0;

    private static void Place(Control control, double x, double height)
    {
        var desired = control.DesiredSize;
        control.Arrange(new Rect(x, (height - desired.Height) / 2, desired.Width, desired.Height));
    }

    internal static void ApplyBlur(Visual visual, BlurEffect effect, double radius)
    {
        effect.Radius = radius;
        visual.Effect = radius > .01 && visual.Opacity is > 0 and < 1 ? effect : null;
    }
}
