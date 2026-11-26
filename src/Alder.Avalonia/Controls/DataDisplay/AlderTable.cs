using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Threading;
using NativeSelectionMode = global::Avalonia.Controls.SelectionMode;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment of the table surface and rows.</summary>
public enum TableVariant
{
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary
}
/// <summary>Controls how many rows can be selected.</summary>
public enum TableSelectionMode
{
    /// <summary>Disables row selection.</summary>
    None,
    /// <summary>Allows at most one selected row.</summary>
    Single,
    /// <summary>Allows multiple selected rows.</summary>
    Multiple
}

/// <summary>A virtualized tabular view with explicit columns, keyed selection and optional tree rows.</summary>
/// <remarks>Provide a finite height so the internal scrolling viewport can limit realized rows.</remarks>
public partial class AlderTable : TemplatedControl
{
    /// <summary>Whether to display the column header row above the table's data.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowColumnHeaders { get; set; }

    /// <summary>Identifies the <see cref="ItemsSource"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<AlderTable, IEnumerable?>(nameof(ItemsSource));
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<TableVariant> VariantProperty =
        AvaloniaProperty.Register<AlderTable, TableVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="SelectionMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<TableSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<AlderTable, TableSelectionMode>(nameof(SelectionMode));
    /// <summary>Identifies the <see cref="RowKeySelector"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<object, object>?> RowKeySelectorProperty =
        AvaloniaProperty.Register<AlderTable, Func<object, object>?>(nameof(RowKeySelector));
    /// <summary>Identifies the <see cref="SelectedKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyCollection<object>?> SelectedKeysProperty =
        AvaloniaProperty.Register<AlderTable, IReadOnlyCollection<object>?>(nameof(SelectedKeys), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="DisabledKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyCollection<object>?> DisabledKeysProperty =
        AvaloniaProperty.Register<AlderTable, IReadOnlyCollection<object>?>(nameof(DisabledKeys));
    /// <summary>Identifies the <see cref="ShowSelectionCheckboxes"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSelectionCheckboxesProperty =
        AvaloniaProperty.Register<AlderTable, bool>(nameof(ShowSelectionCheckboxes));
    /// <summary>Identifies the <see cref="CanResizeColumns"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> CanResizeColumnsProperty =
        AvaloniaProperty.Register<AlderTable, bool>(nameof(CanResizeColumns));
    /// <summary>Identifies the <see cref="SortDescriptor"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderTableSort?> SortDescriptorProperty =
        AvaloniaProperty.Register<AlderTable, AlderTableSort?>(nameof(SortDescriptor), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="SortChangedCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> SortChangedCommandProperty =
        AvaloniaProperty.Register<AlderTable, ICommand?>(nameof(SortChangedCommand));
    /// <summary>Identifies the <see cref="ChildrenSelector"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<AlderTable, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));
    /// <summary>Identifies the <see cref="TreeColumnKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TreeColumnKeyProperty =
        AvaloniaProperty.Register<AlderTable, string?>(nameof(TreeColumnKey));
    /// <summary>Identifies the <see cref="ExpandedKeys"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyCollection<object>?> ExpandedKeysProperty =
        AvaloniaProperty.Register<AlderTable, IReadOnlyCollection<object>?>(nameof(ExpandedKeys), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="Footer"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderTable, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<AlderTable, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="EmptyContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<AlderTable, object?>(nameof(EmptyContent));
    /// <summary>Identifies the <see cref="EmptyContentTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> EmptyContentTemplateProperty =
        AvaloniaProperty.Register<AlderTable, IDataTemplate?>(nameof(EmptyContentTemplate));
    /// <summary>Identifies the <see cref="IsLoading"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<AlderTable, bool>(nameof(IsLoading));
    /// <summary>Identifies the <see cref="HasMoreItems"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> HasMoreItemsProperty =
        AvaloniaProperty.Register<AlderTable, bool>(nameof(HasMoreItems));
    /// <summary>Identifies the <see cref="LoadMoreCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> LoadMoreCommandProperty =
        AvaloniaProperty.Register<AlderTable, ICommand?>(nameof(LoadMoreCommand));
    /// <summary>Identifies the <see cref="MinRowHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinRowHeightProperty =
        AvaloniaProperty.Register<AlderTable, double>(nameof(MinRowHeight), 45, validate: height => height >= 0 && double.IsFinite(height));
    /// <summary>Identifies the <see cref="VisibleRowCount"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderTable, int> VisibleRowCountProperty =
        AvaloniaProperty.RegisterDirect<AlderTable, int>(nameof(VisibleRowCount), table => table.VisibleRowCount);

    /// <summary>The explicit columns used to build table headers and cells.</summary>
    [Content]
    public AvaloniaList<AlderTableColumn> Columns { get; } = new() { ResetBehavior = ResetBehavior.Remove };
    /// <summary>The root rows displayed by the table.</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public TableVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the table permits no selection, one selection, or multiple selected rows.</summary>
    public TableSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    /// <summary>A function that returns the stable identity of a row. The row itself is used when no selector is supplied.</summary>
    public Func<object, object>? RowKeySelector { get => GetValue(RowKeySelectorProperty); set => SetValue(RowKeySelectorProperty, value); }
    /// <summary>The row keys in the current selection. The property binds two-way by default.</summary>
    /// <remarks>Selection gestures publish a replacement collection. Supply a new collection for view-model updates.</remarks>
    public IReadOnlyCollection<object>? SelectedKeys { get => GetValue(SelectedKeysProperty); set => SetValue(SelectedKeysProperty, value); }
    /// <summary>The keys for rows unavailable to selection.</summary>
    public IReadOnlyCollection<object>? DisabledKeys { get => GetValue(DisabledKeysProperty); set => SetValue(DisabledKeysProperty, value); }
    /// <summary>Whether rows display a selection checkbox.</summary>
    public bool ShowSelectionCheckboxes { get => GetValue(ShowSelectionCheckboxesProperty); set => SetValue(ShowSelectionCheckboxesProperty, value); }
    /// <summary>Whether resizable columns show interactive resize handles.</summary>
    public bool CanResizeColumns { get => GetValue(CanResizeColumnsProperty); set => SetValue(CanResizeColumnsProperty, value); }
    /// <summary>The current column key and direction, or null when no sort is active.</summary>
    public AlderTableSort? SortDescriptor { get => GetValue(SortDescriptorProperty); set => SetValue(SortDescriptorProperty, value); }
    /// <summary>The command notified when the sort descriptor changes through the table.</summary>
    /// <remarks>For external sorting, update the data source in this command and disable <see cref="ApplyLocalSort"/>.</remarks>
    public ICommand? SortChangedCommand { get => GetValue(SortChangedCommandProperty); set => SetValue(SortChangedCommandProperty, value); }
    /// <summary>A function that supplies child rows for an expandable row.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector { get => GetValue(ChildrenSelectorProperty); set => SetValue(ChildrenSelectorProperty, value); }
    /// <summary>The column that displays the tree indentation and expansion affordance.</summary>
    public string? TreeColumnKey { get => GetValue(TreeColumnKeyProperty); set => SetValue(TreeColumnKeyProperty, value); }
    /// <summary>The keys of rows whose children should be visible.</summary>
    public IReadOnlyCollection<object>? ExpandedKeys { get => GetValue(ExpandedKeysProperty); set => SetValue(ExpandedKeysProperty, value); }
    /// <summary>The optional content displayed in the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>The data template used to present the footer.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>The placeholder content shown when no rows are available.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    /// <summary>The data template used to present the empty content.</summary>
    public IDataTemplate? EmptyContentTemplate { get => GetValue(EmptyContentTemplateProperty); set => SetValue(EmptyContentTemplateProperty, value); }
    /// <summary>Whether the table is waiting for rows to load.</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    /// <summary>Whether another page of rows can be requested.</summary>
    public bool HasMoreItems { get => GetValue(HasMoreItemsProperty); set => SetValue(HasMoreItemsProperty, value); }
    /// <summary>The command that requests more rows when the table reaches its loading boundary.</summary>
    public ICommand? LoadMoreCommand { get => GetValue(LoadMoreCommandProperty); set => SetValue(LoadMoreCommandProperty, value); }
    /// <summary>The minimum height of a data row in device-independent pixels.</summary>
    public double MinRowHeight { get => GetValue(MinRowHeightProperty); set => SetValue(MinRowHeightProperty, value); }
    /// <summary>The number of rows in the current flattened view.</summary>
    public int VisibleRowCount { get => field; private set => SetAndRaise(VisibleRowCountProperty, ref field, value); }

    private readonly Dictionary<object, (int Index, int Depth, bool HasChildren, object? ParentKey)> rows = [];
    private readonly AvaloniaList<object> visibleItems = [];
    private readonly HashSet<INotifyCollectionChanged> observedCollections = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<AlderTableColumn> observedColumns = [];
    private HashSet<object> disabledKeys = [];
    private HashSet<object> selectedKeys = [];
    private HashSet<object> expandedKeys = [];
    private TableView? view;
    private bool applyingSelection;
    private bool refreshPending;
    private bool loadPending;
    private int requestedRowCount = -1;

    internal event EventHandler? RowsChanged;
    private protected TableView? NativeView => view;
    private protected virtual bool ApplyLocalSort => true;
    private protected virtual bool CanRequestMoreRows => true;
    private protected virtual double GetVerticalExtent(ScrollViewer scroll) => scroll.Extent.Height;
    private protected virtual TableColumnPin SelectionColumnPin => TableColumnPin.None;
    private protected virtual AlderTableColumn? CreateLeadingColumn() => null;
    private protected virtual void OnColumnsUpdated() { }
    private protected virtual void OnColumnUpdated(AvaloniaPropertyChangedEventArgs change) { }
    private protected virtual void OnTableScrollChanged(ScrollChangedEventArgs e) { }

    internal bool HasSelectableRows => selectableRowCount > 0;
    private int selectableRowCount;

    /// <summary>Initializes a new instance of <see cref="AlderTable"/>.</summary>
    public AlderTable() => Columns.CollectionChanged += (_, _) => RebuildColumns();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (view is not null)
        {
            view.SelectionChanged -= OnViewSelectionChanged;
            view.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
            view.Columns.Clear();
            view.ItemsSource = null;
        }

        base.OnApplyTemplate(e);
        view = e.NameScope.Find<TableView>("PART_Table");
        if (view is null) return;
        if (view is AlderTableView tableView) tableView.OwnerTable = this;
        view.ItemsSource = visibleItems;
        view.SelectionChanged += OnViewSelectionChanged;
        view.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
        view.SelectionMode = SelectionMode == TableSelectionMode.Multiple
            ? NativeSelectionMode.Multiple | NativeSelectionMode.Toggle
            : NativeSelectionMode.Single;
        RebuildColumns();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (view is not null) RebuildRows();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        StopObservingCollections();
        base.OnDetachedFromLogicalTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (view is null) return;

        if (change.Property == ItemsSourceProperty)
        {
            requestedRowCount = -1;
            RebuildRows();
        }
        else if (change.Property == ExpandedKeysProperty || change.Property == ChildrenSelectorProperty ||
                 change.Property == DisabledKeysProperty || change.Property == RowKeySelectorProperty ||
                 change.Property == SortDescriptorProperty || change.Property == MinRowHeightProperty)
        {
            UpdateSortIndicators();
            RebuildRows();
        }
        else if (change.Property == SelectedKeysProperty)
        {
            ApplySelection();
        }
        else if (change.Property == SelectionModeProperty || change.Property == ShowSelectionCheckboxesProperty)
        {
            applyingSelection = true;
            view.Selection.Clear();
            view.SelectionMode = SelectionMode == TableSelectionMode.Multiple
                ? NativeSelectionMode.Multiple | NativeSelectionMode.Toggle
                : NativeSelectionMode.Single;
            applyingSelection = false;
            RebuildColumns();
        }
        else if (change.Property == TreeColumnKeyProperty)
        {
            RowsChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == HasMoreItemsProperty || change.Property == LoadMoreCommandProperty || change.Property == IsLoadingProperty)
        {
            if (change.Property != IsLoadingProperty) requestedRowCount = -1;
            QueueLoadCheck();
        }
    }

    private protected void RebuildColumns()
    {
        foreach (var column in observedColumns) column.PropertyChanged -= OnColumnChanged;
        observedColumns.Clear();
        if (view is null) return;

        view.Columns.Clear();
        if (ShowSelectionCheckboxes && SelectionMode != TableSelectionMode.None)
        {
            var selection = new AlderTableColumn
            {
                Width = new GridLength(40),
                Pin = SelectionColumnPin,
                CanUserResize = false,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                CellTemplate = new FuncDataTemplate<object>((item, _) => new AlderTableCheck { Table = this, Row = item })
            };
            if (SelectionMode == TableSelectionMode.Multiple)
            {
                selection.Header = "Select all";
                selection.HeaderTemplate = new FuncDataTemplate<object>((_, _) => new AlderTableCheck { Table = this, SelectAll = true });
            }
            selection.Classes.Add("selection");
            view.Columns.Add(selection);
        }
        if (CreateLeadingColumn() is { } leading) view.Columns.Add(leading);
        view.Columns.AddRange(Columns);
        foreach (var column in Columns)
        {
            if (observedColumns.Add(column)) column.PropertyChanged += OnColumnChanged;
        }
        UpdateSortIndicators();
        RebuildRows();
        OnColumnsUpdated();
    }

    private void OnColumnChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        OnColumnUpdated(change);
        if (change.Property == AlderTableColumn.KeyProperty || change.Property == AlderTableColumn.SortComparisonProperty)
        {
            UpdateSortIndicators();
            RebuildRows();
        }
    }

    private void UpdateSortIndicators()
    {
        foreach (var column in Columns)
        {
            column.SortDirection = SortDescriptor is { } sort && sort.ColumnKey == column.Key ? sort.Direction : null;
        }
    }

    internal void SortBy(AlderTableColumn column)
    {
        if (!column.CanInvokeSort || column.Key is not { } key) return;
        var direction = SortDescriptor is { Direction: TableSortDirection.Ascending } sort && sort.ColumnKey == key
            ? TableSortDirection.Descending : TableSortDirection.Ascending;
        var descriptor = new AlderTableSort(key, direction);
        SetCurrentValue(SortDescriptorProperty, descriptor);
        if (SortChangedCommand?.CanExecute(descriptor) == true) SortChangedCommand.Execute(descriptor);
    }

    private protected void RebuildRows()
    {
        if (view is null) return;
        StopObservingCollections();
        rows.Clear();
        disabledKeys = DisabledKeys?.ToHashSet() ?? [];
        expandedKeys = ExpandedKeys?.ToHashSet() ?? [];
        var output = new List<object>();
        var sort = SortDescriptor;
        var compare = !ApplyLocalSort || sort is null ? null : Columns.FirstOrDefault(column => column.Key == sort.ColumnKey)?.SortComparison;
        var comparer = compare is null ? null : Comparer<object>.Create(compare);
        if (ItemsSource is { } source)
            AppendRows(source, 0, null, output, comparer, sort?.Direction == TableSortDirection.Descending);
        selectableRowCount = rows.Keys.Count(key => !disabledKeys.Contains(key));

        applyingSelection = true;
        try
        {
            UpdateVisibleItems(output);
            if (view is AlderTableView tableView) tableView.RefreshRealizedRows();
        }
        finally
        {
            applyingSelection = false;
        }
        VisibleRowCount = output.Count;
        PseudoClasses.Set(":empty", output.Count == 0);
        ApplySelection();
        QueueLoadCheck();
    }

    private void UpdateVisibleItems(List<object> output)
    {
        // Keep the common rows realized so focus and active chevron transitions survive tree changes.
        int start = 0;
        while (start < visibleItems.Count && start < output.Count &&
               ReferenceEquals(visibleItems[start], output[start]))
            start++;

        int oldEnd = visibleItems.Count;
        int newEnd = output.Count;
        while (oldEnd > start && newEnd > start &&
               ReferenceEquals(visibleItems[oldEnd - 1], output[newEnd - 1]))
        {
            oldEnd--;
            newEnd--;
        }

        if (oldEnd == start && newEnd == start) return;
        OnVisibleRowsChanging(output);
        if (oldEnd > start) visibleItems.RemoveRange(start, oldEnd - start);
        if (newEnd > start) visibleItems.InsertRange(start, output.GetRange(start, newEnd - start));
    }

    private protected virtual void OnVisibleRowsChanging(IReadOnlyList<object> nextRows) { }

    private void AppendRows(IEnumerable source, int depth, object? parentKey, List<object> output, IComparer<object>? comparer, bool descending)
    {
        ObserveCollection(source);
        IEnumerable<object> items = source.OfType<object>();
        if (comparer is not null)
            items = descending ? items.OrderByDescending(item => item, comparer) : items.OrderBy(item => item, comparer);

        foreach (var item in items)
        {
            object key = GetRowKey(item);
            if (rows.ContainsKey(key)) continue;

            object[]? children = null;
            bool hasChildren = false;
            if (ChildrenSelector?.Invoke(item) is { } childSource)
            {
                ObserveCollection(childSource);
                var childItems = childSource.OfType<object>();
                if (expandedKeys.Contains(key))
                {
                    children = childItems.ToArray();
                    hasChildren = children.Length > 0;
                }
                else
                {
                    hasChildren = childItems.Any();
                }
            }
            rows.Add(key, (output.Count, depth, hasChildren, parentKey));
            output.Add(item);
            if (children is { Length: > 0 }) AppendRows(children, depth + 1, key, output, comparer, descending);
        }
    }

    private void ObserveCollection(IEnumerable source)
    {
        if (((ILogical)this).IsAttachedToLogicalTree && source is INotifyCollectionChanged observable && observedCollections.Add(observable))
            observable.CollectionChanged += OnSourceCollectionChanged;
    }

    private void StopObservingCollections()
    {
        foreach (var source in observedCollections) source.CollectionChanged -= OnSourceCollectionChanged;
        observedCollections.Clear();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (refreshPending) return;
        refreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            refreshPending = false;
            if (((ILogical)this).IsAttachedToLogicalTree) RebuildRows();
        });
    }

    internal object? GetRowParentKey(object key) => rows.TryGetValue(key, out var row) ? row.ParentKey : null;
    internal object GetRowKey(object item) => RowKeySelector?.Invoke(item) ?? item;
    internal bool IsRowDisabled(object item) => disabledKeys.Contains(GetRowKey(item));
    internal bool IsRowSelected(object item) => selectedKeys.Contains(GetRowKey(item));

    internal (int Depth, bool HasChildren, bool IsExpanded) GetTreeState(object item, TableViewColumn? column)
    {
        if (ChildrenSelector is null || column is not AlderTableColumn definition ||
            (TreeColumnKey is null ? !ReferenceEquals(column, Columns.FirstOrDefault(candidate => candidate.IsVisible)) : definition.Key != TreeColumnKey))
            return default;
        object key = GetRowKey(item);
        return rows.TryGetValue(key, out var row) ? (row.Depth, row.HasChildren, expandedKeys.Contains(key)) : default;
    }

    internal void ToggleExpanded(object item)
    {
        var key = GetRowKey(item);
        if (!rows.TryGetValue(key, out var row) || !row.HasChildren) return;
        var next = new HashSet<object>(expandedKeys);
        if (!next.Remove(key)) next.Add(key);
        SetCurrentValue(ExpandedKeysProperty, next.ToArray());
    }

    private void ApplySelection()
    {
        if (view is null || applyingSelection) return;
        selectedKeys = (SelectionMode == TableSelectionMode.None ? [] : SelectedKeys ?? [])
            .Where(key => !disabledKeys.Contains(key)).Take(SelectionMode == TableSelectionMode.Single ? 1 : int.MaxValue).ToHashSet();

        applyingSelection = true;
        try
        {
            using var selectionBatch = view.Selection.BatchUpdate();
            view.Selection.Clear();
            if (selectedKeys.Count > 0)
            {
                if (selectedKeys.Count <= view.ItemCount / 2)
                    SelectRowsByKey(view);
                else
                    for (int index = 0; index < view.ItemCount; index++)
                        if (view.ItemsView[index] is { } item && IsRowSelected(item)) view.Selection.Select(index);
            }
        }
        finally
        {
            applyingSelection = false;
        }
        RowsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectRowsByKey(TableView table)
    {
        var indexes = new List<int>(selectedKeys.Count);
        foreach (var key in selectedKeys)
            if (rows.TryGetValue(key, out var row)) indexes.Add(row.Index);

        // Keep the native selection order identical to selection by displayed row.
        indexes.Sort();
        foreach (int index in indexes) table.Selection.Select(index);
    }

    private void OnViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (applyingSelection) return;
        if (SelectionMode == TableSelectionMode.None)
        {
            ApplySelection();
            return;
        }
        var next = SelectionMode == TableSelectionMode.Single ? [] : new HashSet<object>(selectedKeys);
        foreach (var item in e.RemovedItems.OfType<object>()) next.Remove(GetRowKey(item));
        foreach (var item in e.AddedItems.OfType<object>())
            if (!IsRowDisabled(item)) next.Add(GetRowKey(item));
        selectedKeys = next;
        applyingSelection = true;
        SetCurrentValue(SelectedKeysProperty, next.ToArray());
        applyingSelection = false;
        RowsChanged?.Invoke(this, EventArgs.Empty);
    }

    internal bool? GetSelectAllState()
    {
        int selected = selectedKeys.Count(key => rows.ContainsKey(key) && !disabledKeys.Contains(key));
        if (selectableRowCount == 0 || selected == 0) return false;
        return selected == selectableRowCount ? true : null;
    }

    internal void ToggleSelection(object? item, bool all)
    {
        if (SelectionMode == TableSelectionMode.None) return;
        var next = new HashSet<object>(selectedKeys);
        if (all)
        {
            var eligible = rows.Keys.Where(key => !disabledKeys.Contains(key));
            if (GetSelectAllState() == true) next.ExceptWith(eligible);
            else next.UnionWith(eligible);
        }
        else if (item is not null && !IsRowDisabled(item))
        {
            var key = GetRowKey(item);
            if (!next.Remove(key))
            {
                if (SelectionMode == TableSelectionMode.Single) next.Clear();
                next.Add(key);
            }
        }
        SetCurrentValue(SelectedKeysProperty, next.ToArray());
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, view?.Scroll)) return;
        OnTableScrollChanged(e);
        QueueLoadCheck();
    }

    private protected void QueueLoadCheck()
    {
        if (loadPending || !CanRequestMoreRows || !HasMoreItems || IsLoading || LoadMoreCommand is null) return;
        loadPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            loadPending = false;
            if (!CanRequestMoreRows || !IsEffectivelyVisible || !((ILogical)this).IsAttachedToLogicalTree || !HasMoreItems || IsLoading ||
                requestedRowCount == VisibleRowCount || view?.Scroll is not ScrollViewer scroll || scroll.Viewport.Height <= 0 ||
                GetVerticalExtent(scroll) - scroll.Offset.Y - scroll.Viewport.Height > 24 ||
                LoadMoreCommand is not { } command || !command.CanExecute(null)) return;

            requestedRowCount = VisibleRowCount;
            command.Execute(null);
        }, DispatcherPriority.Background);
    }
}
