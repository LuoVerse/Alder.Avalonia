using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class KanbanCardPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty = StackPanel.SpacingProperty.AddOwner<KanbanCardPanel>();
    public static readonly StyledProperty<IBrush?> SlotBrushProperty =
        AvaloniaProperty.Register<KanbanCardPanel, IBrush?>(nameof(SlotBrush));

    private static readonly TimeSpan MoveDuration = TimeSpan.FromMilliseconds(180);
    private static readonly CubicEaseOut MoveEasing = new();

    private sealed class DropSlot(int index)
    {
        public int Index { get; } = index;
        public double FullHeight { get; set; }
        public double Height { get; set; }
        public double StartHeight { get; set; }
        public double EndHeight { get; set; }
        public Rect Bounds { get; set; }
    }

    private readonly SlotLayer slot;
    private readonly List<DropSlot> slots = [];
    private DropSlot? targetSlot;
    private TimeSpan? transitionStart;
    private Action<TimeSpan>? advanceSlot;
    private bool transitioning;
    private bool frameQueued;
    private bool animateArrange;

    static KanbanCardPanel()
    {
        AffectsMeasure<KanbanCardPanel>(SpacingProperty);
    }

    public KanbanCardPanel()
    {
        slot = new SlotLayer(this) { IsHitTestVisible = false, ZIndex = -1 };
        VisualChildren.Add(slot);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public IBrush? SlotBrush { get => GetValue(SlotBrushProperty); set => SetValue(SlotBrushProperty, value); }
    internal Rect DropSlotBounds => targetSlot?.Bounds ?? default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlotBrushProperty)
            slot?.InvalidateVisual();
    }

    public void SetDropSlot(int index, double height)
    {
        if (index < 0 ? targetSlot is null : targetSlot?.Index == index && targetSlot.FullHeight == height) return;
        targetSlot = null;
        if (index >= 0)
        {
            int insertion = slots.FindIndex(gap => gap.Index >= index);
            if (insertion >= 0 && slots[insertion].Index == index) targetSlot = slots[insertion];
            else
            {
                targetSlot = new DropSlot(index);
                slots.Insert(insertion < 0 ? slots.Count : insertion, targetSlot);
            }
            targetSlot.FullHeight = height;
        }

        // Retarget from the space currently on screen when the pointer changes its mind mid-animation.
        foreach (var gap in slots)
        {
            gap.StartHeight = gap.Height;
            gap.EndHeight = gap == targetSlot ? height : 0;
        }
        transitionStart = null;
        transitioning = true;
        if (!AlderMotion.GetReduced(this) && TopLevel.GetTopLevel(this) is { } root) QueueFrame(root);
        else ApplyTransition(1);
    }

    public void CompleteDrop()
    {
        // The moved card now supplies the space. Retain the previous card offsets for the final transition.
        ResetSlots();
        animateArrange = true;
        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetSlots();
        frameQueued = false;
        animateArrange = false;
        InvalidateMeasure();
        base.OnDetachedFromVisualTree(e);
    }

    private void ResetSlots()
    {
        targetSlot = null;
        slots.Clear();
        transitioning = false;
        transitionStart = null;
    }

    private void QueueFrame(TopLevel root)
    {
        if (frameQueued) return;
        frameQueued = true;
        root.RequestAnimationFrame(advanceSlot ??= AnimateSlot);
    }

    private void AnimateSlot(TimeSpan time)
    {
        frameQueued = false;
        if (!transitioning || TopLevel.GetTopLevel(this) is not { } root) return;
        var start = transitionStart ??= time;
        var progress = AlderMotion.GetReduced(this) ? 1 : Math.Clamp((time - start) / MoveDuration, 0, 1);
        ApplyTransition(progress);
        if (transitioning) QueueFrame(root);
    }

    private void ApplyTransition(double progress)
    {
        double amount = MoveEasing.Ease(progress);
        foreach (var gap in slots)
            gap.Height = gap.StartHeight + (gap.EndHeight - gap.StartHeight) * amount;
        if (progress >= 1)
        {
            slots.RemoveAll(static gap => gap.EndHeight <= 0);
            transitioning = false;
        }
        InvalidateMeasure();
    }

    private double GetGapSpacing(DropSlot gap) => gap.FullHeight <= 0 ? 0 : Spacing * Math.Clamp(gap.Height / gap.FullHeight, 0, 1);

    private double GetGapExtent(DropSlot gap, bool hasCards)
    {
        return gap.Height + (hasCards ? GetGapSpacing(gap) : 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var constraint = new Size(availableSize.Width, double.PositiveInfinity);
        double width = 0;
        double height = 0;
        int count = 0;
        foreach (var card in Children)
        {
            if (!card.IsVisible) continue;
            card.Measure(constraint);
            width = Math.Max(width, card.DesiredSize.Width);
            height += card.DesiredSize.Height;
            count++;
        }
        foreach (var gap in slots) height += GetGapExtent(gap, count > 0);
        return new Size(width, height + Math.Max(0, count - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var animate = animateArrange && !AlderMotion.GetReduced(this);
        animateArrange = false;
        slot.Arrange(new Rect(finalSize));
        double top = 0;
        int nextGap = 0;
        int lastVisible = Children.Count - 1;
        while (lastVisible >= 0 && !Children[lastVisible].IsVisible) lastVisible--;
        for (int index = 0; index <= Children.Count; index++)
        {
            while (nextGap < slots.Count && Math.Min(slots[nextGap].Index, Children.Count) == index)
            {
                var gap = slots[nextGap++];
                double trailingSpace = index > lastVisible && lastVisible >= 0 ? Spacing - GetGapSpacing(gap) : 0;
                gap.Bounds = new Rect(0, Math.Max(0, top - trailingSpace), finalSize.Width, gap.Height);
                top += GetGapExtent(gap, lastVisible >= 0);
            }
            if (index == Children.Count) break;
            var card = Children[index];
            if (!card.IsVisible) continue;
            var previous = card.Bounds;
            card.Arrange(new Rect(0, top, finalSize.Width, card.DesiredSize.Height));
            double offset = previous.Y - card.Bounds.Y;
            if (animate && previous.Height > 0 && Math.Abs(offset) >= .5)
                OverlayMotion.ShiftFromCurrent(card, new Vector(0, offset), MoveDuration, MoveEasing);
            top += card.DesiredSize.Height + Spacing;
        }
        slot.InvalidateVisual();
        return finalSize;
    }

    private sealed class SlotLayer(KanbanCardPanel panel) : Control
    {
        public override void Render(DrawingContext context)
        {
            if (panel.slots.Count == 0) return;
            var outline = panel.SlotBrush is { } brush ? new Pen(brush, 1, DashStyle.Dash) : null;
            foreach (var gap in panel.slots)
            {
                if (gap.Bounds is not { Width: > 2, Height: > 2 } rect) continue;
                double radius = Math.Min(10, (rect.Height - 1) / 2);
                context.DrawRectangle(null, outline, rect.Deflate(.5), radius, radius);
            }
        }
    }
}
