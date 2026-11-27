using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

// A fixed-width strip can bring an unmaterialized index into view without
// sending a bring-into-view request to the surrounding page.
internal sealed class AlderFilmstrip : ItemsControl
{
    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<AlderFilmstrip, double>(nameof(ItemWidth));
    public static readonly StyledProperty<double> SpacingProperty =
        StackPanel.SpacingProperty.AddOwner<AlderFilmstrip>();

    private readonly AvaloniaList<int> indices = [];

    public AlderFilmstrip() => ItemsSource = indices;

    public double ItemWidth { get => GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public Func<int, Button>? CreateButton { get; set; }

    public void UpdateCount(int count, bool reset)
    {
        if (reset) indices.Clear();
        int formerLast = indices.Count - 1;
        if (count < indices.Count) indices.RemoveRange(count, indices.Count - count);
        if (count > indices.Count) indices.AddRange(Enumerable.Range(indices.Count, count - indices.Count));
        if (formerLast >= 0 && formerLast < count - 1 && ContainerFromIndex(formerLast) is Button previous)
            SetSpacing(previous, formerLast);
        if (count > 0 && ContainerFromIndex(count - 1) is Button last) SetSpacing(last, count - 1);
    }

    public static bool IsTailChange(NotifyCollectionChangedEventArgs change, int count) =>
        change.Action == NotifyCollectionChangedAction.Add && change.NewStartingIndex >= 0 &&
            change.NewStartingIndex + change.NewItems!.Count == count ||
        change.Action == NotifyCollectionChangedAction.Remove && change.OldStartingIndex == count;

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        var button = CreateButton!(index);
        SetSpacing(button, index);
        return button;
    }

    private void SetSpacing(Button button, int index) =>
        button.Margin = new Thickness(0, 0, index + 1 == ItemCount ? 0 : Spacing, 0);

    public void Reveal(ScrollViewer? scroller, int index, double inset = 0)
    {
        if (scroller is null || !scroller.IsEffectivelyVisible || (uint)index >= (uint)ItemCount) return;
        double width = scroller.Viewport.Width, offset = scroller.Offset.X;
        if (width <= 0 || scroller.Extent.Width <= width) return;
        double left = Margin.Left + index * (ItemWidth + Spacing);
        double right = left + ItemWidth;
        if (left - inset < offset) offset = left - inset;
        else if (right + inset > offset + width) offset = right + inset - width;
        scroller.Offset = new Vector(Math.Clamp(offset, 0, scroller.Extent.Width - width), scroller.Offset.Y);
    }
}
