using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class DisplayGroupPanel : Panel
{
    private int columns;
    private double columnWidth;
    private double[] rowHeights = [];
    private readonly DividerLayer dividers;

    public int MaxColumns { get; init; }
    public double MinimumColumnWidth { get; init; }
    public double Gap { get; init; }
    public bool ShowDividers { get; init; }

    public DisplayGroupPanel()
    {
        dividers = new DividerLayer(this) { ZIndex = -1, IsHitTestVisible = false };
        VisualChildren.Add(dividers);
        ActualThemeVariantChanged += (_, _) => dividers.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            columns = 0;
            rowHeights = [];
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width
            : Math.Max(1, MinimumColumnWidth) * Math.Min(MaxColumns, Children.Count) + Gap * (Math.Min(MaxColumns, Children.Count) - 1);
        columns = Math.Clamp((int)((width + Gap) / (Math.Max(1, MinimumColumnWidth) + Gap)), 1, Math.Min(MaxColumns, Children.Count));
        columnWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        rowHeights = new double[(Children.Count + columns - 1) / columns];
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeights[i / columns] = Math.Max(rowHeights[i / columns], Children[i].DesiredSize.Height);
        }
        return new Size(width, rowHeights.Sum() + Gap * (rowHeights.Length - 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        dividers.Arrange(new Rect(finalSize));
        dividers.InvalidateVisual();
        if (columns == 0) return finalSize;
        columnWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        double y = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            int column = i % columns;
            double height = rowHeights[i / columns];
            Children[i].Arrange(new Rect(column * (columnWidth + Gap), y, columnWidth, height));
            if (column == columns - 1) y += height + Gap;
        }
        return finalSize;
    }

    private void DrawDividers(DrawingContext context)
    {
        if (!ShowDividers || columns == 0 ||
            !this.TryFindResource("AlderSeparator", ActualThemeVariant, out var value) || value is not IBrush brush) return;
        double y = 0;
        for (int i = 0; i < rowHeights.Length - 1; i++)
        {
            y += rowHeights[i];
            context.FillRectangle(brush, new Rect(0, y, Bounds.Width, Gap));
            y += Gap;
        }
        for (int i = 0; i < Children.Count; i++)
            if (i % columns < columns - 1 && i + 1 < Children.Count)
            {
                var bounds = Children[i].Bounds;
                context.FillRectangle(brush, new Rect(bounds.Right, bounds.Top, Gap, bounds.Height));
            }
    }

    private sealed class DividerLayer(DisplayGroupPanel owner) : Control
    {
        public override void Render(DrawingContext context) => owner.DrawDividers(context);
    }
}
