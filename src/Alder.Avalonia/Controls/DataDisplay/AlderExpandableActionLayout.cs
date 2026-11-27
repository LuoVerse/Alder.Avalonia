using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderExpandableActionRail : AlderToolbar
{
    // The highlight follows the Web item spring (mass .62, damping 34, stiffness 460).
    private readonly AlderTabs.HeaderSpring left = new(AlderExpandableActionItemEasing.SlowRate, AlderExpandableActionItemEasing.FastRate, .36);
    private readonly AlderTabs.HeaderSpring width = new(AlderExpandableActionItemEasing.SlowRate, AlderExpandableActionItemEasing.FastRate, .36);
    private AlderExpandableActionBarItem? target;
    private Rect targetBounds;
    private AlderExpandableActionBar? bar;
    private bool framePending;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (this.FindAncestorOfType<AlderExpandableActionBar>() is not { } owner) return arranged;

        owner.UpdateHighlight(this);
        var next = owner.HighlightedItem;
        bar = owner;
        if (next is null || !next.IsVisible)
        {
            target = null;
            bar.PositionHighlight(default);
            return arranged;
        }

        targetBounds = next.Bounds;
        if (target is null || AlderMotion.GetReduced(this) || (target == next && !left.IsMoving && !width.IsMoving))
        {
            left.Jump(targetBounds.X);
            width.Jump(targetBounds.Width);
        }
        else
        {
            long now = Stopwatch.GetTimestamp();
            left.MoveTo(targetBounds.X, now);
            width.MoveTo(targetBounds.Width, now);
            ScheduleFrame();
        }

        target = next;
        Position();
        return arranged;
    }

    private void Position() => bar?.PositionHighlight(new Rect(left.Offset, targetBounds.Y, width.Offset, targetBounds.Height));

    private void ScheduleFrame()
    {
        if (framePending || (!left.IsMoving && !width.IsMoving) || TopLevel.GetTopLevel(this) is not { } top) return;
        framePending = true;
        top.RequestAnimationFrame(_ =>
        {
            framePending = false;
            if (!this.IsAttachedToVisualTree() || target is null) return;
            long now = Stopwatch.GetTimestamp();
            left.Advance(now);
            width.Advance(now);
            Position();
            ScheduleFrame();
        });
    }
}

internal sealed class AlderExpandableActionHighlight : Control
{
    private Rect bounds;
    private IBrush? brush;

    internal void Show(Rect rectangle, IBrush? fill)
    {
        if (bounds == rectangle && brush == fill) return;
        bounds = rectangle;
        brush = fill;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (bounds.Width > 0 && brush is not null)
            context.DrawRectangle(brush, null, new RoundedRect(bounds, bounds.Height / 2));
    }
}

internal sealed class AlderExpandableActionContent : Panel
{
    private readonly RectangleGeometry labelClip = new();
    private readonly RectangleGeometry shortcutClip = new();
    private readonly TranslateTransform labelOffset = new();
    private readonly BlurEffect labelBlur = new();

    static AlderExpandableActionContent() => AffectsMeasure<AlderExpandableActionContent>(AlderExpandableActionBar.RevealProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        // Keep the label's text measured at its full width. Clip the reveal layer
        // during the transition so changing rail width never changes text layout.
        foreach (var child in Children)
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        double reveal = GetValue(AlderExpandableActionBar.RevealProperty);
        double width = Children[0].DesiredSize.Width;
        for (int index = 1; index < Children.Count; index++)
            width += (Children[index].DesiredSize.Width + Gap(index)) * reveal;
        return new Size(width, Children[0].DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var reveal = GetValue(AlderExpandableActionBar.RevealProperty);
        var icon = Children[0];
        ArrangeCentered(icon, 0, finalSize.Height);

        var label = (Decorator)Children[1];
        double x = icon.DesiredSize.Width + Gap(1) * reveal;
        Reveal(label, labelClip, x, finalSize.Height, reveal);
        if (label.Child is { } text)
        {
            labelOffset.X = -4 * (1 - reveal);
            labelBlur.Radius = 3 * (1 - reveal);
            text.RenderTransform = labelOffset;
            text.Effect = reveal is > 0 and < 1 ? labelBlur : null;
        }

        var shortcut = Children[2];
        x += label.DesiredSize.Width * reveal + Gap(2) * reveal;
        Reveal(shortcut, shortcutClip, x, finalSize.Height, reveal);

        var badge = Children[3];
        x += shortcut.DesiredSize.Width * reveal + Gap(3) * reveal;
        var badgeSize = badge.DesiredSize;
        // The badge starts over the trailing corner of the icon and joins the rail as it opens.
        var cornerX = icon.DesiredSize.Width + 6 - badgeSize.Width;
        var badgeX = cornerX + (x - cornerX) * reveal;
        var badgeY = (finalSize.Height - badgeSize.Height) / 2;
        badge.Arrange(new Rect(badgeX, badgeY - 6 * (1 - reveal), badgeSize.Width, badgeSize.Height));
        return finalSize;
    }

    private static void Reveal(Control child, RectangleGeometry clip, double x, double height, double reveal)
    {
        clip.Rect = new Rect(0, 0, child.DesiredSize.Width * reveal, child.DesiredSize.Height);
        child.Clip = reveal < 1 ? clip : null;
        child.Opacity = reveal;
        ArrangeCentered(child, x, height);
    }

    private static void ArrangeCentered(Control child, double x, double height)
    {
        var size = child.DesiredSize;
        child.Arrange(new Rect(x, (height - size.Height) / 2, size.Width, size.Height));
    }

    private double Gap(int index) => Children[index].DesiredSize.Width <= 0 ? 0 : index switch { 1 => 8, 2 => 4, _ => 2 };
}


internal sealed class AlderExpandableActionItemEasing : Easing
{
    internal const double SlowRate = -24.275;
    internal const double FastRate = -30.563;
    private const double Seconds = .32;
    private static readonly double End = Spring(Seconds);

    public override double Ease(double progress) => Spring(progress * Seconds) / End;

    private static double Spring(double time) =>
        1 + (FastRate * Math.Exp(SlowRate * time) - SlowRate * Math.Exp(FastRate * time)) / (SlowRate - FastRate);
}
