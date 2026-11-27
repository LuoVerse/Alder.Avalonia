using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class KanbanCardPanel : VirtualizingPanel
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
    private readonly SortedDictionary<int, Entry> realized = [];
    private double[] heights = [];
    private double[] positions = [];
    private double estimate = 64;
    private double measuredWidth = double.NaN;
    private bool hasEstimate;

    private sealed record Entry(Control Card, bool IsGenerated);

    static KanbanCardPanel()
    {
        AffectsMeasure<KanbanCardPanel>(SpacingProperty);
    }

    public KanbanCardPanel()
    {
        slot = new SlotLayer(this) { IsHitTestVisible = false, ZIndex = -1 };
        VisualChildren.Add(slot);
        EffectiveViewportChanged += (_, _) => InvalidateMeasure();
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
        if (ItemsControl is not AlderKanbanColumn column) return default;
        int count = Items.Count;
        var constraint = new Size(availableSize.Width, double.PositiveInfinity);
        if (heights.Length != count || measuredWidth != availableSize.Width)
        {
            heights = new double[count];
            positions = new double[count];
            Array.Fill(heights, double.NaN);
            measuredWidth = availableSize.Width;
            hasEstimate = false;
        }
        var scroll = column.CardsScroll;
        double offset = scroll?.Offset.Y ?? 0;
        double viewport = scroll is { Viewport.Height: > 0 } ? scroll.Viewport.Height :
            TopLevel.GetTopLevel(this)?.ClientSize.Height ?? estimate;
        double min = Math.Max(0, offset - estimate);
        double max = offset + viewport + estimate;
        double width = 0;
        double top = 0;
        int nextGap = 0;
        for (int index = 0; index < count; index++)
        {
            top = PlaceGaps(index, top, ref nextGap, count != 0);
            positions[index] = top;
            double height = HeightAt(index);
            if ((top + height >= min && top <= max) || realized.TryGetValue(index, out var entry) && IsInUse(entry.Card))
            {
                var card = Realize(index);
                card.Measure(constraint);
                heights[index] = height = card.IsVisible ? card.DesiredSize.Height : 0;
                width = Math.Max(width, card.DesiredSize.Width);
                if (!hasEstimate && height > 0)
                {
                    estimate = height;
                    hasEstimate = true;
                }
            }
            if (height > 0) top += height + Spacing;
        }
        top = PlaceGaps(count, top, ref nextGap, count != 0);
        foreach (var (index, entry) in realized.ToArray())
        {
            if (!IsInUse(entry.Card) &&
                (positions[index] + HeightAt(index) < min || positions[index] > max))
                Release(index, entry);
        }
        return new Size(width, Math.Max(0, top - (count > 0 ? Spacing : 0)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var animate = animateArrange && !AlderMotion.GetReduced(this);
        animateArrange = false;
        slot.Arrange(new Rect(finalSize));
        foreach (var (index, entry) in realized)
        {
            var card = entry.Card;
            if (index >= positions.Length || !card.IsVisible) continue;
            var previous = card.Bounds;
            card.Arrange(new Rect(0, positions[index], finalSize.Width, HeightAt(index)));
            double offset = previous.Y - card.Bounds.Y;
            if (animate && previous.Height > 0 && Math.Abs(offset) >= .5)
                OverlayMotion.ShiftFromCurrent(card, new Vector(0, offset), MoveDuration, MoveEasing);
        }
        foreach (var gap in slots) gap.Bounds = gap.Bounds.WithWidth(finalSize.Width);
        slot.InvalidateVisual();
        return finalSize;
    }

    private double PlaceGaps(int index, double top, ref int nextGap, bool hasCards)
    {
        while (nextGap < slots.Count && Math.Min(slots[nextGap].Index, Items.Count) == index)
        {
            var gap = slots[nextGap++];
            double trailing = hasCards && index == Items.Count ? Spacing - GetGapSpacing(gap) : 0;
            gap.Bounds = new Rect(0, Math.Max(0, top - trailing), 0, gap.Height);
            top += GetGapExtent(gap, hasCards);
        }
        return top;
    }

    private static bool IsInUse(Control card) =>
        card.IsKeyboardFocusWithin || card.Classes.Contains("kanban-drag-source");

    private double HeightAt(int index) =>
        Items[index] is Control { IsVisible: false } ? 0 :
        double.IsNaN(heights[index]) ? estimate : heights[index];

    private Control Realize(int index)
    {
        if (realized.TryGetValue(index, out var existing)) return existing.Card;
        var item = Items[index];
        var generator = ItemContainerGenerator!;
        bool generated = generator.NeedsContainer(item, index, out var key);
        var card = generated ? generator.CreateContainer(item, index, key) : (Control)item!;
        generator.PrepareItemContainer(card, item, index);
        int visualIndex = realized.Keys.Count(i => i < index);
        InsertInternalChild(visualIndex, card);
        realized.Add(index, new Entry(card, generated));
        generator.ItemContainerPrepared(card, item, index);
        return card;
    }

    private void Release(int index, Entry entry)
    {
        realized.Remove(index);
        if (entry.IsGenerated) ItemContainerGenerator?.ClearItemContainer(entry.Card);
        RemoveInternalChild(entry.Card);
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        var old = realized.ToArray();
        realized.Clear();
        foreach (var (oldIndex, entry) in old)
        {
            int index = UpdatedIndex(oldIndex, e);
            if ((uint)index < (uint)items.Count && realized.TryAdd(index, entry))
            {
                if (oldIndex != index)
                    ItemContainerGenerator?.ItemContainerIndexChanged(entry.Card, oldIndex, index);
                continue;
            }
            if (entry.IsGenerated) ItemContainerGenerator?.ClearItemContainer(entry.Card);
            RemoveInternalChild(entry.Card);
        }
        int visualIndex = 0;
        foreach (var entry in realized.Values)
        {
            int previous = Children.IndexOf(entry.Card);
            if (previous != visualIndex) Children.Move(previous, visualIndex);
            visualIndex++;
        }
        heights = [];
        InvalidateMeasure();
    }

    private static int UpdatedIndex(int index, NotifyCollectionChangedEventArgs change)
    {
        int oldStart = change.OldStartingIndex, newStart = change.NewStartingIndex;
        int oldCount = change.OldItems?.Count ?? 0, newCount = change.NewItems?.Count ?? 0;
        if (change.Action == NotifyCollectionChangedAction.Add && newStart >= 0)
            return index < newStart ? index : index + newCount;
        if (oldStart < 0 || change.Action == NotifyCollectionChangedAction.Reset) return -1;
        bool inMovedRange = index >= oldStart && index < oldStart + oldCount;
        if (change.Action == NotifyCollectionChangedAction.Move && newStart >= 0)
        {
            if (inMovedRange) return newStart + index - oldStart;
            int afterRemoval = index < oldStart ? index : index - oldCount;
            return afterRemoval < newStart ? afterRemoval : afterRemoval + oldCount;
        }
        if (inMovedRange) return -1;
        return index < oldStart ? index : index + newCount - oldCount;
    }

    protected override Control? ContainerFromIndex(int index) =>
        realized.TryGetValue(index, out var entry) ? entry.Card : null;

    protected override int IndexFromContainer(Control container)
    {
        foreach (var (index, entry) in realized)
            if (entry.Card == container) return index;
        return -1;
    }

    protected override IEnumerable<Control> GetRealizedContainers() => realized.Values.Select(entry => entry.Card);

    protected override Control? ScrollIntoView(int index)
    {
        if ((uint)index >= (uint)Items.Count) return null;
        if (ContainerFromIndex(index) is { } card)
        {
            card.BringIntoView();
            return card;
        }
        if (ItemsControl is not AlderKanbanColumn { CardsScroll: { } scroll } || positions.Length != Items.Count)
            return null;
        double offset = positions[index];
        if (offset > scroll.Offset.Y)
            offset = Math.Max(0, offset + HeightAt(index) - scroll.Viewport.Height);
        scroll.Offset = scroll.Offset.WithY(offset);
        InvalidateMeasure();
        scroll.UpdateLayout();
        return ContainerFromIndex(index);
    }

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        if (from is not Control control) return null;
        int index = IndexFromContainer(control);
        return direction switch
        {
            NavigationDirection.Up when index > 0 => ScrollIntoView(index - 1),
            NavigationDirection.Down when index >= 0 && index + 1 < Items.Count => ScrollIntoView(index + 1),
            _ => null
        };
    }

    internal int GetInsertionIndex(double y)
    {
        for (int i = 0; i < positions.Length && i < Items.Count; i++)
        {
            var height = HeightAt(i);
            if (height > 0 && y < positions[i] + height / 2) return i;
        }
        return Items.Count;
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
