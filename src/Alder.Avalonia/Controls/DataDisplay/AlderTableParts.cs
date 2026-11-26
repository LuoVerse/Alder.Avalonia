using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderTableView : TableView
{
    public static readonly StyledProperty<TableVariant> VariantProperty = AlderTable.VariantProperty.AddOwner<AlderTableView>();
    public TableVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public static readonly StyledProperty<bool> ShowColumnHeadersProperty = AlderTable.ShowColumnHeadersProperty.AddOwner<AlderTableView>();
    public bool ShowColumnHeaders { get => GetValue(ShowColumnHeadersProperty); set => SetValue(ShowColumnHeadersProperty, value); }

    internal AlderTable? OwnerTable { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ScrollViewer>("PART_ScrollViewer") is { } scroll)
        {
            scroll.Classes.Set("dataGridScroll", OwnerTable is AlderDataGrid);
            scroll.Classes.Set("without-column-headers", !ShowColumnHeaders);
        }
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        RefreshRow(container, item, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (OwnerTable is AlderDataGrid grid) grid.StopRowReorderMotion(container);
        base.ClearContainerForItemOverride(container);
    }

    private void RefreshRow(Control container, object? item, int index)
    {
        if (OwnerTable is not { } table || item is null) return;
        container.IsEnabled = !table.IsRowDisabled(item);
        if (table is AlderDataGrid grid)
        {
            grid.UpdateReorderRow(container, item);
            container.Classes.Set("data-grid-row", true);
            container.Height = grid.Virtualized ? grid.RowHeight : double.NaN;
            container.MinHeight = double.IsNaN(container.Height) ? table.MinRowHeight : Math.Min(table.MinRowHeight, container.Height);
        }
        else container.MinHeight = table.MinRowHeight;
        container.Classes.Set("last-row", Variant == TableVariant.Primary && index == ItemCount - 1);
        container.Classes.Set("secondary", Variant == TableVariant.Secondary);
    }

    internal void RefreshRealizedRows()
    {
        foreach (var row in GetRealizedContainers())
        {
            int index = IndexFromContainer(row);
            if (index >= 0 && index < ItemCount) RefreshRow(row, ItemsView[index], index);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
            RefreshRealizedRows();
        if (change.Property == ShowColumnHeadersProperty && Scroll is ScrollViewer scroll)
            scroll.Classes.Set("without-column-headers", !ShowColumnHeaders);
    }
}

internal sealed class AlderTableSortButton : Button
{
    public static readonly StyledProperty<AlderTableColumn?> ColumnProperty =
        AvaloniaProperty.Register<AlderTableSortButton, AlderTableColumn?>(nameof(Column));
    public static readonly StyledProperty<TableSortDirection?> DirectionProperty =
        AvaloniaProperty.Register<AlderTableSortButton, TableSortDirection?>(nameof(Direction));
    public static readonly StyledProperty<bool> CanSortProperty =
        AvaloniaProperty.Register<AlderTableSortButton, bool>(nameof(CanSort));

    public AlderTableColumn? Column { get => GetValue(ColumnProperty); set => SetValue(ColumnProperty, value); }
    public TableSortDirection? Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }
    public bool CanSort { get => GetValue(CanSortProperty); set => SetValue(CanSortProperty, value); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (CanSort) base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (CanSort) base.OnPointerReleased(e);
    }

    protected override void OnClick()
    {
        if (CanSort && Column is { } column)
            this.FindAncestorOfType<AlderTable>()?.SortBy(column);
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != DirectionProperty) return;
        PseudoClasses.Set(":sorted", Direction.HasValue);
        PseudoClasses.Set(":descending", Direction == TableSortDirection.Descending);
    }
}

internal sealed class AlderTableResizeThumb : Thumb
{
    private const double DefaultGridResizeMinimum = 48;
    private ResizeDrag? drag;
    private sealed record ResizeDrag(TableViewColumnHeader Header, TableViewColumn Column, TableView Table,
        double PointerX, double Width, bool FromStart);

    public AlderTableResizeThumb() =>
        this[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.TableResizeColumn");

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Classes.Set("gridResizer", this.FindAncestorOfType<AlderDataGrid>() is not null);
        if (this.FindAncestorOfType<TableViewColumnHeader>() is { Column: { TableView: { } table } column } header)
            header.ZIndex = table.Columns.Count - table.Columns.IndexOf(column);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            this.FindAncestorOfType<TableViewColumnHeader>() is not
                { Column: { CanUserEffectivelyResize: true, TableView: { } table } column } header ||
            !double.IsFinite(column.ActualWidth)) return;

        drag = new(header, column, table, e.GetPosition(table).X, column.ActualWidth,
            header.Classes.Contains("grid-resize-from-start"));
        header.Classes.Set("column-resizing", true);
        PseudoClasses.Set(":resizing", true);
        e.Pointer.Capture(this);
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (drag is not { } active || !ReferenceEquals(e.Pointer.Captured, this))
        {
            base.OnPointerMoved(e);
            return;
        }
        if (ReferenceEquals(active.Header.Column, active.Column))
        {
            double delta = e.GetPosition(active.Table).X - active.PointerX;
            ResizeTo(active.Header, active.Column, active.Width + (active.FromStart ? -delta : delta));
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndResize();
        if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndResize();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndResize();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Left or Key.Right) ||
            this.FindAncestorOfType<TableViewColumnHeader>() is not { Column: { } column } header) return;

        bool grow = (e.Key == Key.Right) == (FlowDirection == FlowDirection.LeftToRight);
        if (header.Classes.Contains("grid-resize-from-start")) grow = !grow;
        e.Handled = ResizeTo(header, column, column.ActualWidth + (grow ? 8 : -8));
    }

    private static bool ResizeTo(TableViewColumnHeader header, TableViewColumn column, double width)
    {
        if (!column.CanUserEffectivelyResize || !double.IsFinite(width)) return false;
        double minimum = column.MinWidth;
        double maximum = Math.Max(minimum, column.MaxWidth);
        if (header.Classes.Contains("data-grid-header"))
            minimum = Math.Max(minimum, Math.Min(DefaultGridResizeMinimum, maximum));

        double resized = Math.Clamp(width, minimum, maximum);
        if (Math.Abs(resized - column.ActualWidth) >= .01)
            column.SetCurrentValue(TableViewColumn.WidthProperty, new GridLength(resized));
        return true;
    }

    private void EndResize()
    {
        drag?.Header.Classes.Set("column-resizing", false);
        PseudoClasses.Set(":resizing", false);
        drag = null;
    }
}

internal sealed class AlderTableCheck : AlderCheckBox
{
    internal AlderTable? Table { get; init; }
    internal object? Row { get; init; }
    internal bool SelectAll { get; init; }

    protected override Type StyleKeyOverride => typeof(AlderCheckBox);

    public AlderTableCheck()
    {
        MinHeight = 20;
        Padding = new Thickness(0);
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Table is not null) Table.RowsChanged += Refresh;
        Variant = SelectAll ? CheckBoxVariant.Primary : CheckBoxVariant.Secondary;
        this[!AutomationProperties.NameProperty] = new DynamicResourceExtension(
            SelectAll ? "AlderText.TableSelectAll" : "AlderText.TableSelectRow");
        Refresh(null, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Table is not null) Table.RowsChanged -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        Table?.ToggleSelection(Row, SelectAll);
        Refresh(null, EventArgs.Empty);
    }

    private void Refresh(object? sender, EventArgs e)
    {
        IsChecked = SelectAll ? Table?.GetSelectAllState() : Row is not null && Table?.IsRowSelected(Row) == true;
        IsEnabled = SelectAll ? Table?.HasSelectableRows == true : Row is not null && Table?.IsRowDisabled(Row) == false;
    }
}

internal sealed class AlderTableExpander : Button
{
    public static readonly StyledProperty<object?> RowProperty =
        AvaloniaProperty.Register<AlderTableExpander, object?>(nameof(Row));
    public static readonly StyledProperty<TableViewColumn?> ColumnProperty =
        AvaloniaProperty.Register<AlderTableExpander, TableViewColumn?>(nameof(Column));

    public object? Row { get => GetValue(RowProperty); set => SetValue(RowProperty, value); }
    public TableViewColumn? Column { get => GetValue(ColumnProperty); set => SetValue(ColumnProperty, value); }
    private AlderTable? owner;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        owner = this.FindAncestorOfType<AlderTable>();
        if (owner is not null) owner.RowsChanged += Refresh;
        Refresh(null, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (owner is not null) owner.RowsChanged -= Refresh;
        owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnProperty || change.Property == RowProperty)
            Refresh(null, EventArgs.Empty);
    }

    protected override void OnClick()
    {
        if (Row is not null) owner?.ToggleExpanded(Row);
        base.OnClick();
    }

    private void Refresh(object? sender, EventArgs e)
    {
        var state = Row is { } item && owner is not null ? owner.GetTreeState(item, Column) : default;
        IsVisible = state.HasChildren || state.Depth > 0;
        IsHitTestVisible = Focusable = state.HasChildren;
        Width = state.HasChildren ? 32 : 0;
        Margin = new Thickness(state.Depth * 16, 0, state.HasChildren ? 4 : 0, 0);
        if (PseudoClasses.Contains(":expanded") != state.IsExpanded || AutomationProperties.GetName(this) is null)
            this[!AutomationProperties.NameProperty] = new DynamicResourceExtension(
                state.IsExpanded ? "AlderText.TableCollapseRow" : "AlderText.TableExpandRow");
        PseudoClasses.Set(":expanded", state.IsExpanded);
        PseudoClasses.Set(":branch", state.HasChildren);
    }
}
