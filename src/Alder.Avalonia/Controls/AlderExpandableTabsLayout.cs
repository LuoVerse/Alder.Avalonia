using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal static class ExpandableTabsMotion
{
    private static readonly SplineEasing ExitEase = new(.16, 1, .3, 1);
    private static readonly double SpringEnd = Spring(1);

    internal static double FadeOut(double progress) => ExitEase.Ease(progress);
    internal static double Enter(double progress) => Spring(progress) / SpringEnd;
    internal static double Mix(double from, double to, double progress) => from + (to - from) * progress;

    private static double Spring(double progress)
    {
        double time = 7.8 * progress;
        return 1 - (1 + time) * Math.Exp(-time);
    }
}

internal struct ExpandableTabsFrameClock
{
    private static readonly long MaxFrameStep = Stopwatch.Frequency / 30;
    private long previousFrame;
    private bool firstFrame;
    public long Current { get; private set; }

    public void Start()
    {
        Current = Stopwatch.GetTimestamp();
        previousFrame = Current;
        firstFrame = true;
    }

    public long Advance()
    {
        long now = Stopwatch.GetTimestamp();
        // A cold layout or render may miss a tick. Resume from the displayed
        // position and limit the distance covered by the next visible frame.
        long elapsed = Math.Min(now - previousFrame, firstFrame ? MaxFrameStep / 2 : MaxFrameStep);
        firstFrame = false;
        previousFrame = now;
        Current += elapsed;
        return Current;
    }
}

internal sealed class AlderExpandableTabsShell : Panel
{
    internal const double BarHeight = 52;
    internal const double BarInset = 8;
    internal const double Gap = 4;

    private readonly AlderTabs.HeaderSpring width = new(-12.5, -15, .58);
    private readonly AlderTabs.HeaderSpring height = new(-12.5, -15, .58);
    private ExpandableTabsFrameClock clock;
    private bool laidOut;
    private bool framePending;

    protected override Size MeasureOverride(Size available)
    {
        if (TemplatedParent is not AlderExpandableTabs tabs || Children.Count != 2) return base.MeasureOverride(available);
        var body = Children[0];
        var bar = Children[1];
        bar.Measure(new Size(available.Width, BarHeight));
        body.Measure(new Size(available.Width, double.PositiveInfinity));

        bool open = tabs.SelectedIndex >= 0;
        var closedWidth = 2 * BarInset + Math.Max(0, tabs.ItemCount - 1) * Gap + tabs.ItemCount * AlderExpandableTabHeader.CompactWidth;
        var targetWidth = closedWidth;
        var targetHeight = BarHeight + (open && body.DesiredSize.Height > 0 ? Gap + body.DesiredSize.Height : 0);
        if (open)
        {
            // Reserve the final rail width so the shell doesn't chase the label's intermediate width.
            var selected = tabs.ContainerFromIndex(tabs.SelectedIndex);
            var header = selected?.GetVisualDescendants().OfType<AlderExpandableTabHeader>().FirstOrDefault();
            var railWidth = header is null ? bar.DesiredSize.Width : closedWidth + header.ExpandedWidth - AlderExpandableTabHeader.CompactWidth;
            targetWidth = Math.Max(railWidth, body.DesiredSize.Width);
            targetWidth = Math.Max(targetWidth, tabs.ExpandedMinWidth - tabs.BorderThickness.Left - tabs.BorderThickness.Right);
            targetHeight = Math.Max(targetHeight, tabs.ExpandedMinHeight - tabs.BorderThickness.Top - tabs.BorderThickness.Bottom);
        }
        targetWidth = Math.Min(available.Width, targetWidth);
        targetHeight = Math.Min(available.Height, targetHeight);
        if (open && body is AlderExpandableTabsViewport viewport)
            viewport.SetContentWidth(targetWidth);

        if (!laidOut || AlderMotion.GetReduced(tabs))
        {
            width.Jump(targetWidth);
            height.Jump(targetHeight);
        }
        else
        {
            if (!width.IsMoving && !height.IsMoving) clock.Start();
            long time = clock.Current;
            width.MoveTo(targetWidth, time);
            height.MoveTo(targetHeight, time);
            ScheduleFrame();
        }

        body.IsHitTestVisible = open;
        return new Size(Math.Clamp(width.Offset, 0, available.Width), Math.Clamp(height.Offset, 0, available.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 2) return base.ArrangeOverride(finalSize);
        double dockHeight = Math.Min(BarHeight, finalSize.Height);
        Children[0].Arrange(new Rect(0, 0, finalSize.Width, Math.Max(0, finalSize.Height - dockHeight - Gap)));
        Children[1].Arrange(new Rect(0, finalSize.Height - dockHeight, finalSize.Width, dockHeight));
        laidOut = true;
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        laidOut = framePending = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void Advance(TimeSpan _)
    {
        framePending = false;
        if (!this.IsAttachedToVisualTree()) return;
        long time = clock.Advance();
        width.Advance(time);
        height.Advance(time);
        InvalidateMeasure();
        ScheduleFrame();
    }

    private void ScheduleFrame()
    {
        if (framePending || (!width.IsMoving && !height.IsMoving) || TopLevel.GetTopLevel(this) is not { } top) return;
        framePending = true;
        top.RequestAnimationFrame(Advance);
    }
}

internal sealed class AlderExpandableTabsViewport : Border
{
    private double contentWidth;

    internal void SetContentWidth(double width)
    {
        if (contentWidth == width) return;
        contentWidth = width;
        InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            // The rows keep their layout while the animated viewport reveals or clips them.
            var size = new Size(Math.Max(finalSize.Width, contentWidth),
                Math.Max(finalSize.Height, child.DesiredSize.Height + Padding.Top + Padding.Bottom));
            child.Arrange(new Rect(size).Deflate(Padding));
        }
        return finalSize;
    }
}

internal sealed class AlderExpandableTabsRail : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, available.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width + Math.Max(0, Children.Count - 1) * AlderExpandableTabsShell.Gap, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double totalWidth = 0;
        foreach (var child in Children) totalWidth += child.DesiredSize.Width;
        double gap = Children.Count > 1 ? Math.Max(AlderExpandableTabsShell.Gap, (finalSize.Width - totalWidth) / (Children.Count - 1)) : 0;
        double x = Children.Count == 1 ? Math.Max(0, (finalSize.Width - totalWidth) / 2) : 0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + gap;
        }
        return finalSize;
    }
}

internal sealed class AlderExpandableTabHeader : Panel
{
    internal const double CompactWidth = 32;
    private const double IconSize = 16;
    private const double ExpandedIconInset = 10;
    private const double LabelGap = 7;
    private const double EndInset = 16;

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AlderExpandableTabHeader, bool>(nameof(IsExpanded));

    private readonly AlderTabs.HeaderSpring width = new(-15.5, -20.5, .46);
    private readonly RectangleGeometry labelClip = new();
    private SolidColorBrush? labelInk;
    private ExpandableTabsFrameClock clock;
    private double labelProgress;
    private double labelFrom;
    private long? labelStarted;
    private bool previousExpanded;
    private bool laidOut;
    private bool framePending;

    static AlderExpandableTabHeader() =>
        AffectsMeasure<AlderExpandableTabHeader>(IsExpandedProperty, AlderMotion.ReducedProperty);

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    internal double ExpandedWidth { get; private set; } = CompactWidth;

    protected override Size MeasureOverride(Size available)
    {
        if (Children.Count != 2) return base.MeasureOverride(available);
        Children[0].Measure(new Size(IconSize, IconSize));
        Children[1].Measure(new Size(double.PositiveInfinity, available.Height));
        ExpandedWidth = ExpandedIconInset + IconSize + LabelGap + Children[1].DesiredSize.Width + EndInset;
        var target = IsExpanded ? ExpandedWidth : CompactWidth;
        if (!laidOut || AlderMotion.GetReduced(this))
        {
            width.Jump(target);
            labelStarted = null;
            labelProgress = IsExpanded ? 1 : 0;
        }
        else
        {
            if (!width.IsMoving && labelStarted is null) clock.Start();
            long time = clock.Current;
            width.MoveTo(target, time);
            if (previousExpanded != IsExpanded)
            {
                labelFrom = labelProgress;
                labelStarted = time;
            }
            ScheduleFrame();
        }
        previousExpanded = IsExpanded;
        return new Size(width.Offset, 36);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 2) return base.ArrangeOverride(finalSize);
        double centered = (finalSize.Width - IconSize) / 2;
        double hidden = 1 - labelProgress;
        double iconX = Math.Min(centered, ExpandableTabsMotion.Mix(ExpandedIconInset, centered, hidden * hidden * hidden));
        Children[0].Arrange(new Rect(iconX, (finalSize.Height - IconSize) / 2, IconSize, IconSize));
        var label = Children[1];
        double x = iconX + IconSize + LabelGap * labelProgress;
        label.Arrange(new Rect(x, 0, label.DesiredSize.Width, finalSize.Height));
        DrawLabelOpacity(label);
        double revealWidth = Math.Min(label.DesiredSize.Width * labelProgress, Math.Max(0, finalSize.Width - x));
        bool clipLabel = labelProgress < 1 || revealWidth < label.DesiredSize.Width;
        if (clipLabel)
            labelClip.Rect = new Rect(0, 0, revealWidth, finalSize.Height);
        label.Clip = clipLabel ? labelClip : null;
        laidOut = true;
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        laidOut = framePending = false;
        labelStarted = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ScheduleFrame()
    {
        if (framePending || (!width.IsMoving && labelStarted is null) || TopLevel.GetTopLevel(this) is not { } top) return;
        framePending = true;
        top.RequestAnimationFrame(Advance);
    }

    private void Advance(TimeSpan _)
    {
        framePending = false;
        if (!this.IsAttachedToVisualTree()) return;
        long time = clock.Advance();
        if (labelStarted is { } start)
        {
            double duration = previousExpanded ? 380 : 160;
            double progress = Math.Min(1, Stopwatch.GetElapsedTime(start, time).TotalMilliseconds / duration);
            double easing = previousExpanded ? ExpandableTabsMotion.Enter(progress) : ExpandableTabsMotion.FadeOut(progress);
            labelProgress = Math.Clamp(ExpandableTabsMotion.Mix(labelFrom, previousExpanded ? 1 : 0, easing), 0, 1);
            if (progress >= 1) labelStarted = null;
            InvalidateArrange();
        }
        if (width.IsMoving)
        {
            width.Advance(time);
            InvalidateMeasure();
        }
        ScheduleFrame();
    }

    private void DrawLabelOpacity(Control label)
    {
        if (label is ContentPresenter text)
        {
            if (labelProgress is > 0 and < 1 && text is { Content: string, ContentTemplate: null } &&
                TemplatedParent is TabItem { Foreground: ISolidColorBrush foreground })
            {
                // Fade the glyph ink directly. A visual opacity would composite
                // the whole label in a separate layer during the reveal.
                labelInk ??= new SolidColorBrush();
                labelInk.Color = foreground.Color;
                labelInk.Opacity = foreground.Opacity * labelProgress;
                text.Foreground = labelInk;
                text.Opacity = 1;
                return;
            }
            if (labelInk is not null && ReferenceEquals(text.Foreground, labelInk))
                text.ClearValue(ContentPresenter.ForegroundProperty);
        }
        label.Opacity = labelProgress;
    }
}
