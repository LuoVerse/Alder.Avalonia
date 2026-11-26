using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies whether sorting is performed in the grid or delegated to the caller.</summary>
public enum DataGridSortMode
{
    /// <summary>Sorts the displayed rows in the control.</summary>
    Local,
    /// <summary>Reports sort changes so the caller can order the data.</summary>
    External
}

/// <summary>A virtualized table with pinned columns and optional command-driven row reordering.</summary>
public sealed partial class AlderDataGrid : AlderTable
{
    /// <summary>Identifies the <see cref="Virtualized"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> VirtualizedProperty =
        AvaloniaProperty.Register<AlderDataGrid, bool>(nameof(Virtualized), true);
    /// <summary>Identifies the <see cref="RowHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<AlderDataGrid, double>(nameof(RowHeight), double.NaN, validate: IsLayoutHeight);
    /// <summary>Identifies the <see cref="HeadingHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> HeadingHeightProperty =
        AvaloniaProperty.Register<AlderDataGrid, double>(nameof(HeadingHeight), 36, validate: value => value > 0 && double.IsFinite(value));
    /// <summary>Identifies the <see cref="SortMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<DataGridSortMode> SortModeProperty =
        AvaloniaProperty.Register<AlderDataGrid, DataGridSortMode>(nameof(SortMode));
    /// <summary>Identifies the <see cref="PinSelectionColumn"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> PinSelectionColumnProperty =
        AvaloniaProperty.Register<AlderDataGrid, bool>(nameof(PinSelectionColumn), true);

    /// <summary>Whether rows are realized only near the viewport.</summary>
    public bool Virtualized { get => GetValue(VirtualizedProperty); set => SetValue(VirtualizedProperty, value); }
    /// <summary>Fixed row height in virtualized mode. NaN retains natural row heights.</summary>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    /// <summary>The height of the grid's heading row in device-independent pixels.</summary>
    public double HeadingHeight { get => GetValue(HeadingHeightProperty); set => SetValue(HeadingHeightProperty, value); }
    /// <summary>Whether sort requests are handled locally or by the caller.</summary>
    public DataGridSortMode SortMode { get => GetValue(SortModeProperty); set => SetValue(SortModeProperty, value); }
    /// <summary>Whether the selection column stays at the leading edge during horizontal scrolling.</summary>
    public bool PinSelectionColumn { get => GetValue(PinSelectionColumnProperty); set => SetValue(PinSelectionColumnProperty, value); }

    private DataGridColumnLayout? columnLayout;
    private bool hasColumnLayout;
    private Rect effectiveViewport;
    private AlderDataGridHeadersPresenter? headers;
    private const string HorizontalScrollingClass = "dataGridScrollingHorizontal";
    private const string VerticalScrollingClass = "dataGridScrollingVertical";
    private static readonly TimeSpan ScrollBarIdleDelay = TimeSpan.FromMilliseconds(600);
    private IDisposable? hideHorizontalScrollBar;
    private IDisposable? hideVerticalScrollBar;
    private ScrollViewer? scrollingView;

    private protected override bool CanRequestMoreRows => effectiveViewport.Intersects(new Rect(Bounds.Size));
    private protected override bool ApplyLocalSort => SortMode == DataGridSortMode.Local;
    private protected override TableColumnPin SelectionColumnPin => PinSelectionColumn ? TableColumnPin.Start : TableColumnPin.None;

    private protected override double GetVerticalExtent(ScrollViewer scroll)
    {
        // During recycling the panel can briefly report an extent based on too few measured rows.
        double rowHeight = Virtualized && double.IsFinite(RowHeight) ? RowHeight : MinRowHeight;
        return Math.Max(scroll.Extent.Height, VisibleRowCount * rowHeight);
    }

    internal ScrollViewer? GridScroll => NativeView?.Scroll as ScrollViewer;
    internal double HorizontalOffset => GridScroll?.Offset.X ?? 0;
    internal double ViewportWidth => GridScroll?.Viewport.Width is > 0 and var width ? width : Bounds.Width;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EffectiveViewportChanged += OnViewportChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= OnViewportChanged;
        effectiveViewport = default;
        ResetScrollBarActivity();
        CancelReorder();
        CancelRowReorderMotion();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        effectiveViewport = e.EffectiveViewport;
        if (CanRequestMoreRows) QueueLoadCheck();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CancelReorder();
        CancelRowReorderMotion();
        ResetScrollBarActivity();
        headers = null;
        hasColumnLayout = false;
        base.OnApplyTemplate(e);
        UpdateRows();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowHeightProperty || change.Property == VirtualizedProperty)
            UpdateRows();
        else if (change.Property == PinSelectionColumnProperty || change.Property == ReorderCommandProperty ||
                 change.Property == CanReorderRowsProperty)
        {
            CancelReorder();
            RebuildColumns();
        }
        else if (change.Property == SortModeProperty)
            RebuildRows();

        if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
            CancelRowReorderMotion();

        if (change.Property == ItemsSourceProperty || change.Property == SortDescriptorProperty ||
            change.Property == DisabledKeysProperty || change.Property == ExpandedKeysProperty ||
            change.Property == AllowReorderWhenSortedProperty)
            CancelReorder();
        if (change.Property == HeadingHeightProperty)
            headers?.InvalidateMeasure();
    }

    private void UpdateRows()
    {
        if (NativeView is not AlderTableView table) return;
        table.Classes.Set("grid-unvirtualized", !Virtualized);
        table.RefreshRealizedRows();
    }

    internal void SetHeaders(AlderDataGridHeadersPresenter? value)
    {
        headers = value;
        hasColumnLayout = false;
    }

    private protected override void OnColumnsUpdated() => InvalidateColumnLayout();

    private protected override void OnColumnUpdated(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == AlderTableColumn.PinProperty || change.Property == TableViewColumn.WidthProperty ||
            change.Property == TableViewColumn.MinWidthProperty || change.Property == TableViewColumn.MaxWidthProperty ||
            change.Property == TableViewColumn.IsVisibleProperty)
            InvalidateColumnLayout();
    }

    private protected override void OnTableScrollChanged(ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.X != 0 || e.ViewportDelta.X != 0 || e.ExtentDelta.X != 0)
            InvalidateColumnLayout();

        if (GridScroll is not { } scroll) return;
        if (scrollingView != scroll) ResetScrollBarActivity();
        scrollingView = scroll;
        UpdateScrollBarActivity(scroll, HorizontalScrollingClass, e.OffsetDelta.X, scroll.ScrollBarMaximum.X, ref hideHorizontalScrollBar);
        UpdateScrollBarActivity(scroll, VerticalScrollingClass, e.OffsetDelta.Y, scroll.ScrollBarMaximum.Y, ref hideVerticalScrollBar);
    }

    private static void UpdateScrollBarActivity(ScrollViewer scroll, string activeClass, double delta, double maximum, ref IDisposable? hideTimer)
    {
        if (maximum <= 0)
        {
            hideTimer?.Dispose();
            hideTimer = null;
            scroll.Classes.Set(activeClass, false);
        }
        else if (delta != 0)
        {
            hideTimer?.Dispose();
            scroll.Classes.Set(activeClass, true);
            hideTimer = DispatcherTimer.RunOnce(() => scroll.Classes.Set(activeClass, false), ScrollBarIdleDelay);
        }
    }

    private void ResetScrollBarActivity()
    {
        hideHorizontalScrollBar?.Dispose();
        hideVerticalScrollBar?.Dispose();
        hideHorizontalScrollBar = null;
        hideVerticalScrollBar = null;
        scrollingView?.Classes.Set(HorizontalScrollingClass, false);
        scrollingView?.Classes.Set(VerticalScrollingClass, false);
        scrollingView = null;
    }

    private void InvalidateColumnLayout()
    {
        hasColumnLayout = false;
        headers?.InvalidateArrange();
        InvalidateRowsArrange();
    }

    private void InvalidateRowsArrange()
    {
        if (NativeView is null) return;
        foreach (var row in NativeView.GetRealizedContainers())
            row.FindDescendantOfType<AlderDataGridCellsPresenter>()?.InvalidateArrange();
    }

    internal DataGridColumnLayout? ArrangeHeaders(double width)
    {
        var next = CreateColumnLayout(width);
        bool changed = !hasColumnLayout || !DataGridColumnLayout.Equals(next, columnLayout);
        hasColumnLayout = true;
        if (changed)
        {
            columnLayout = next;
            InvalidateRowsArrange();
        }

        return columnLayout;
    }

    internal DataGridColumnLayout? GetColumnLayout()
    {
        if (!hasColumnLayout)
        {
            columnLayout = CreateColumnLayout(ViewportWidth);
            hasColumnLayout = true;
        }
        return columnLayout;
    }

    private DataGridColumnLayout? CreateColumnLayout(double width) =>
        NativeView?.Columns is { } columns ? DataGridColumnLayout.Create(columns, width, HorizontalOffset) : null;

    private static bool IsLayoutHeight(double value) => double.IsNaN(value) || value > 0 && double.IsFinite(value);
}
