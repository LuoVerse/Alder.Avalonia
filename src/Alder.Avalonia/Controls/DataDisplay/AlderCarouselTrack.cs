using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class AlderCarouselViewport : Border
{
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<AlderCarouselViewport, double>(nameof(AspectRatio));

    static AlderCarouselViewport() => AffectsMeasure<AlderCarouselViewport>(AspectRatioProperty);

    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (AspectRatio <= 0 || !double.IsFinite(availableSize.Width)) return base.MeasureOverride(availableSize);
        var viewport = new Size(availableSize.Width, Math.Min(availableSize.Height, availableSize.Width / AspectRatio));
        base.MeasureOverride(viewport);
        return viewport;
    }
}

/// <summary>Realizes the visible carousel slides and their immediate neighbors.</summary>
internal sealed class AlderCarouselTrack : VirtualizingPanel
{
    private readonly SortedDictionary<int, Entry> containers = [];
    private readonly Dictionary<int, int> slots = [];
    private readonly TranslateTransform offset = new();
    private int anchor;
    private int coveredFirst = int.MaxValue;
    private int coveredLast = int.MinValue;
    private double slideWidth;

    public AlderCarouselTrack() => RenderTransform = offset;

    public double Step { get; private set; }

    internal void InvalidatePosition()
    {
        if (ItemsControl is not AlderCarousel carousel) return;
        var (first, last) = VisibleSlots(carousel);
        if (!carousel.CanLoop)
        {
            first = Math.Max(0, first);
            last = Math.Min(Items.Count - 1, last);
        }
        if (first < coveredFirst || last > coveredLast) InvalidateMeasure();
        else Translate(carousel.Position, Bounds.Width);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (ItemsControl is not AlderCarousel carousel) return default;
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 360;
        var (slideWidth, step) = Dimensions(carousel, width);
        Step = step;
        var (first, last) = VisibleSlots(carousel);
        anchor = first;
        slots.Clear();
        coveredFirst = int.MaxValue;
        coveredLast = int.MinValue;
        for (int slot = first; slot <= last; slot++) AddSlot(slot, carousel.CanLoop);
        AddSlot(first - 1, carousel.CanLoop);
        AddSlot(last + 1, carousel.CanLoop);

        foreach (var (index, entry) in containers.ToArray())
            if (!slots.ContainsKey(index) && !entry.IsOwnContainer) Release(index, entry);

        double height = 0;
        foreach (var index in slots.Keys)
        {
            var container = Realize(index);
            container.Measure(new Size(slideWidth, availableSize.Height));
            height = Math.Max(height, container.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (ItemsControl is not AlderCarousel carousel) return finalSize;
        (slideWidth, Step) = Dimensions(carousel, finalSize.Width);
        foreach (var (index, entry) in containers)
        {
            if (!slots.TryGetValue(index, out int slot))
            {
                entry.Control.Arrange(new Rect(-finalSize.Width, 0, 0, 0));
                continue;
            }
            entry.Control.Arrange(new Rect((slot - anchor) * Step, 0, slideWidth, finalSize.Height));
        }
        Translate(carousel.Position, finalSize.Width);
        return finalSize;
    }

    private void Translate(double position, double viewportWidth)
    {
        offset.X = (anchor - position) * Step;
        foreach (var (index, entry) in containers)
        {
            double left = slots.TryGetValue(index, out int slot) ? (slot - position) * Step : -double.MaxValue;
            entry.SetInViewport(left < viewportWidth - .01 && left + slideWidth > .01);
        }
    }

    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        if (oldValue is AlderCarousel previous && previous.Track == this) previous.Track = null;
        base.OnItemsControlChanged(oldValue);
        if (ItemsControl is AlderCarousel current) current.Track = this;
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        foreach (var (index, entry) in containers.ToArray()) Release(index, entry);
        slots.Clear();
        coveredFirst = int.MaxValue;
        coveredLast = int.MinValue;
        InvalidateMeasure();
    }

    protected override Control? ScrollIntoView(int index)
    {
        (ItemsControl as AlderCarousel)?.GoTo(index);
        return ContainerFromIndex(index);
    }

    protected override Control? ContainerFromIndex(int index) =>
        containers.TryGetValue(index, out var entry) ? entry.Control : null;

    protected override int IndexFromContainer(Control container)
    {
        foreach (var (index, entry) in containers)
            if (entry.Control == container) return index;
        return -1;
    }

    protected override IEnumerable<Control> GetRealizedContainers() => containers.Values.Select(item => item.Control);

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap) => null;

    private void AddSlot(int slot, bool loop)
    {
        if (Items.Count == 0 || !loop && (slot < 0 || slot >= Items.Count)) return;
        int index = loop ? AlderCarousel.Mod(slot, Items.Count) : slot;
        if (!slots.TryAdd(index, slot)) return;
        coveredFirst = Math.Min(coveredFirst, slot);
        coveredLast = Math.Max(coveredLast, slot);
    }

    private Control Realize(int index)
    {
        if (containers.TryGetValue(index, out var existing)) return existing.Control;

        var item = Items[index];
        var generator = ItemContainerGenerator!;
        bool needsContainer = generator.NeedsContainer(item, index, out var key);
        var container = needsContainer ? generator.CreateContainer(item, index, key) : (Control)item!;
        generator.PrepareItemContainer(container, item, index);
        var next = containers.FirstOrDefault(pair => pair.Key > index);
        int visualIndex = next.Value is null ? Children.Count : Children.IndexOf(next.Value.Control);
        InsertInternalChild(visualIndex, container);
        containers.Add(index, new Entry(container, !needsContainer));
        generator.ItemContainerPrepared(container, item, index);
        return container;
    }

    private void Release(int index, Entry entry)
    {
        containers.Remove(index);
        entry.RestoreInteraction();
        if (!entry.IsOwnContainer) ItemContainerGenerator?.ClearItemContainer(entry.Control);
        RemoveInternalChild(entry.Control);
    }

    private static (int First, int Last) VisibleSlots(AlderCarousel carousel)
    {
        int first = (int)Math.Floor(carousel.Position + 1e-6);
        int last = first + carousel.VisibleSlides - 1;
        if (carousel.Position - first > 1e-6) last++;
        return (first, last);
    }

    private static (double Width, double Step) Dimensions(AlderCarousel carousel, double viewport)
    {
        int count = carousel.VisibleSlides;
        double gap = count > 1 ? Math.Min(carousel.SlideSpacing, viewport / count / 2) : carousel.SlideSpacing;
        double width = (viewport - gap * (count - 1)) / count;
        return (width, width + gap);
    }

    private sealed class Entry(Control control, bool isOwnContainer)
    {
        private IDisposable? tabNavigation;
        private IDisposable? hitTesting;

        public Control Control { get; } = control;
        public bool IsOwnContainer { get; } = isOwnContainer;

        public void SetInViewport(bool visible)
        {
            if (visible) RestoreInteraction();
            else
            {
                tabNavigation ??= Control.SetValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.None, BindingPriority.Animation);
                hitTesting ??= Control.SetValue(IsHitTestVisibleProperty, false, BindingPriority.Animation);
            }
        }

        public void RestoreInteraction()
        {
            tabNavigation?.Dispose();
            hitTesting?.Dispose();
            tabNavigation = hitTesting = null;
        }
    }
}
