using Avalonia;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class DataGridColumnLayout
{
    private readonly record struct Slot(double X, double Width, TableColumnPin Pin, bool Edge);
    private readonly Slot[] slots;
    private readonly double offset;
    private readonly double startWidth;
    private readonly double endWidth;
    private readonly double viewport;
    private readonly double extent;

    public bool HasPinnedColumns => startWidth + endWidth > 0;

    private DataGridColumnLayout(Slot[] slots, double offset, double startWidth, double endWidth, double viewport, double extent)
    {
        this.slots = slots;
        this.offset = offset;
        this.startWidth = startWidth;
        this.endWidth = endWidth;
        this.viewport = viewport;
        this.extent = extent;
    }

    public static DataGridColumnLayout? Create(IReadOnlyList<TableViewColumn> columns, double viewport, double offset)
    {
        if (viewport <= 0 || !double.IsFinite(viewport) || columns.Count == 0)
            return null;

        double Width(TableViewColumn column) => column is { IsVisible: true } && double.IsFinite(column.ActualWidth)
            ? Math.Max(0, column.ActualWidth) : 0;

        var slots = new Slot[columns.Count];
        double starts = 0, ends = 0, extent = 0;
        int lastStart = -1, firstEnd = -1;
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (column.IsVisible && !double.IsFinite(column.ActualWidth)) return null;
            double width = Width(column);
            var pin = width > 0 && column is AlderTableColumn model ? model.Pin : TableColumnPin.None;
            slots[i] = new Slot(0, width, pin, false);
            extent += width;
            if (pin == TableColumnPin.Start)
            {
                starts += width;
                lastStart = i;
            }
            else if (pin == TableColumnPin.End)
            {
                ends += width;
                if (firstEnd < 0) firstEnd = i;
            }
        }

        // Fall back to regular scrolling when the pinned groups cannot leave a usable viewport.
        if (starts + ends > viewport - (extent > starts + ends ? 32 : 0))
        {
            starts = ends = 0;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = slots[i] with { Pin = TableColumnPin.None };
        }

        double start = 0;
        double center = starts;
        double end = viewport - ends;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.Pin == TableColumnPin.Start)
            {
                slots[i] = slot with { X = start, Edge = i == lastStart };
                start += slot.Width;
            }
            else if (slot.Pin == TableColumnPin.End)
            {
                slots[i] = slot with { X = end, Edge = i == firstEnd };
                end += slot.Width;
            }
            else
            {
                slots[i] = slot with { X = center };
                center += slot.Width;
            }
        }
        return new(slots, offset, starts, ends, viewport, extent);
    }

    public bool IsNearViewport(int index)
    {
        if ((uint)index >= (uint)slots.Length) return true;
        var slot = slots[index];
        const double overscan = 24;
        return slot.Pin != TableColumnPin.None ||
            slot.X + slot.Width > offset + startWidth - overscan &&
            slot.X < offset + viewport - endWidth + overscan;
    }

    public static bool Equals(DataGridColumnLayout? a, DataGridColumnLayout? b) =>
        ReferenceEquals(a, b) || a is not null && b is not null &&
        a.offset == b.offset && a.viewport == b.viewport && a.extent == b.extent &&
        a.startWidth == b.startWidth && a.endWidth == b.endWidth && a.slots.AsSpan().SequenceEqual(b.slots);

    public bool Arrange(global::Avalonia.Controls.Controls children, double height, bool header)
    {
        if (children.Count != slots.Length) return false;
        for (int i = 0; i < children.Count; i++)
        {
            var slot = slots[i];
            var child = children[i];
            if (slot.Width <= 0)
            {
                if (child.IsVisible) child.Arrange(new Rect(0, 0, 0, height));
                Reset(child);
                continue;
            }

            bool pinned = slot.Pin != TableColumnPin.None;
            double x = pinned ? slot.X : slot.X - offset;
            // Row coordinates include the scroll offset because the body is already translated.
            child.Arrange(new Rect(header ? x : x + offset, 0, slot.Width, height));
            int z = pinned ? children.Count * 2 - i : header ? children.Count - i : 0;
            child.ZIndex = z;
            bool startEdge = slot.Pin == TableColumnPin.Start && slot.Edge && offset > .5;
            bool endEdge = slot.Pin == TableColumnPin.End && slot.Edge && offset + viewport < extent - .5;
            child.Classes.Set("grid-pin-start-edge", startEdge);
            child.Classes.Set("grid-pin-end-edge", endEdge);
            if (header)
                child.Classes.Set("grid-resize-from-start", slot.Pin == TableColumnPin.End);
            if (pinned)
                child.ClearValue(Visual.ClipProperty);
            else
            {
                var clip = new Rect(startWidth - x, 0, Math.Max(0, viewport - startWidth - endWidth), height);
                if (child.Clip is not RectangleGeometry geometry || geometry.Rect != clip)
                    child.Clip = new RectangleGeometry(clip);
            }
        }
        return true;
    }

    private static void Reset(Control cell)
    {
        if (cell.Clip is not null) cell.ClearValue(Visual.ClipProperty);
        cell.Classes.Set("grid-pin-start-edge", false);
        cell.Classes.Set("grid-pin-end-edge", false);
        if (cell is TableViewColumnHeader) cell.Classes.Set("grid-resize-from-start", false);
    }

    public static void Reset(global::Avalonia.Controls.Controls children, bool headers)
    {
        for (int i = 0; i < children.Count; i++)
        {
            Reset(children[i]);
            children[i].ZIndex = headers ? children.Count - i : 0;
        }
    }
}

internal sealed class AlderDataGridHeadersPresenter : TableViewColumnHeadersPresenter
{
    private AlderDataGrid? grid;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        grid = this.FindAncestorOfType<AlderDataGrid>();
        grid?.SetHeaders(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        grid?.SetHeaders(null);
        grid = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (grid is not null)
            foreach (var header in Children)
                header.Classes.Set("data-grid-header", true);
        var measured = base.MeasureOverride(availableSize);
        return grid is not null ? measured.WithHeight(Math.Max(grid.HeadingHeight, measured.Height)) : measured;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (grid is null) return arranged;
        if (grid.ArrangeHeaders(finalSize.Width) is { HasPinnedColumns: true } layout)
            layout.Arrange(Children, finalSize.Height, header: true);
        else DataGridColumnLayout.Reset(Children, headers: true);
        return arranged;
    }
}

internal sealed class AlderDataGridCellsPresenter : TableViewCellsPresenter
{
    private AlderDataGrid? grid;
    private AlderTableView? table;
    private readonly Dictionary<TableViewCell, IDisposable> suppressedCells = [];

    public AlderDataGridCellsPresenter() => Children.CollectionChanged += (_, change) =>
    {
        if (change.Action == NotifyCollectionChangedAction.Reset)
            RestoreCells();
        else if (change.OldItems is { } previous && change.Action is not NotifyCollectionChangedAction.Move)
            foreach (var cell in previous.OfType<TableViewCell>())
                if (suppressedCells.Remove(cell, out var suppression)) suppression.Dispose();
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        table = this.FindAncestorOfType<AlderTableView>();
        grid = table?.OwnerTable as AlderDataGrid;
        if (table?.OwnerTable is { } owner) owner.RowsChanged += OnRowsChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (table?.OwnerTable is { } owner) owner.RowsChanged -= OnRowsChanged;
        RestoreCells();
        table = null;
        grid = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnRowsChanged(object? sender, EventArgs e)
    {
        foreach (var cell in Children.OfType<TableViewCell>())
            cell.Classes.Set("tree-cell", table?.OwnerTable?.IsTreeColumn(cell.Column) == true);
    }

    private void RestoreCells()
    {
        foreach (var suppression in suppressedCells.Values) suppression.Dispose();
        suppressedCells.Clear();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = grid?.GetColumnLayout();
        UpdateCellVisibility(layout);
        foreach (var cell in Children.OfType<TableViewCell>())
        {
            var column = cell.Column as AlderTableColumn;
            cell.Classes.Set("tree-cell", table?.OwnerTable?.IsTreeColumn(column) == true);
            cell.Classes.Set("data-grid-cell", grid is not null && column is not null &&
                !column.Classes.Contains("selection") && !column.Classes.Contains("reorder-handle"));
        }
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = grid?.GetColumnLayout();
        UpdateCellVisibility(layout);
        // The header has already measured the shared widths. Arrange pinned cells at their final
        // positions directly so scrolling doesn't arrange each realized cell at both positions.
        if (layout is { HasPinnedColumns: true } && layout.Arrange(Children, finalSize.Height, header: false))
            return finalSize;
        var arranged = base.ArrangeOverride(finalSize);
        if (grid is not null) DataGridColumnLayout.Reset(Children, headers: false);
        return arranged;
    }

    private void UpdateCellVisibility(DataGridColumnLayout? layout)
    {
        // Native TableView uses cell indexes for editing and automation. Keep
        // those shells and defer the offscreen templates when row height is fixed.
        bool fixedHeight = table?.OwnerTable is { } owner && double.IsFinite(owner.RowHeight);
        var scroll = table?.Scroll as ScrollViewer;
        double width = scroll?.Viewport.Width is > 0 and var viewport ? viewport : table?.MeasuredWidth ?? 0;
        double offset = scroll?.Offset.X ?? 0;
        bool canDefer = fixedHeight && (grid is null ? double.IsFinite(width) && width > 0 : grid.Virtualized && layout is not null);
        if (!canDefer)
        {
            if (suppressedCells.Count != 0) RestoreCells();
            return;
        }
        double x = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not TableViewCell cell) continue;
            double columnWidth = cell.Column is { IsVisible: true } column ? column.ActualWidth : 0;
            bool outside = layout is not null ? !layout.IsNearViewport(i) : x + columnWidth < offset - 24 || x > offset + width + 24;
            x += columnWidth;
            bool suppress = outside && !cell.IsKeyboardFocusWithin;
            if (suppress)
            {
                if (!suppressedCells.ContainsKey(cell) &&
                    cell.SetValue(IsVisibleProperty, false, BindingPriority.Animation) is { } value)
                    suppressedCells.Add(cell, value);
            }
            else if (suppressedCells.Remove(cell, out var previous)) previous.Dispose();
        }
    }
}
