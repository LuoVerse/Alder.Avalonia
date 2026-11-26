using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies whether a column scrolls or stays pinned at a logical edge.</summary>
public enum TableColumnPin
{
    /// <summary>Lets the column scroll horizontally with the table.</summary>
    None,
    /// <summary>Pins the column to the logical leading edge.</summary>
    Start,
    /// <summary>Pins the column to the logical trailing edge.</summary>
    End
}
/// <summary>Specifies the direction in which a table column is ordered.</summary>
public enum TableSortDirection
{
    /// <summary>Orders values from lowest to highest.</summary>
    Ascending,
    /// <summary>Orders values from highest to lowest.</summary>
    Descending
}
/// <summary>Identifies the column and direction of the current sort.</summary>
/// <param name="ColumnKey">The key of the sorted column.</param>
/// <param name="Direction">The active sort direction.</param>
public sealed record AlderTableSort(string ColumnKey, TableSortDirection Direction);

/// <summary>A typed-binding column with an optional sort key and local comparison.</summary>
public sealed class AlderTableColumn : TableViewColumn
{
    /// <summary>Identifies the <see cref="Pin"/> Avalonia property.</summary>
    public static readonly StyledProperty<TableColumnPin> PinProperty =
        AvaloniaProperty.Register<AlderTableColumn, TableColumnPin>(nameof(Pin));
    /// <summary>Identifies the <see cref="Key"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<AlderTableColumn, string?>(nameof(Key));
    /// <summary>Identifies the <see cref="CanSort"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> CanSortProperty =
        AvaloniaProperty.Register<AlderTableColumn, bool>(nameof(CanSort));
    /// <summary>Identifies the <see cref="SortComparison"/> Avalonia property.</summary>
    public static readonly StyledProperty<Comparison<object>?> SortComparisonProperty =
        AvaloniaProperty.Register<AlderTableColumn, Comparison<object>?>(nameof(SortComparison));
    /// <summary>Identifies the <see cref="SortDirection"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderTableColumn, TableSortDirection?> SortDirectionProperty =
        AvaloniaProperty.RegisterDirect<AlderTableColumn, TableSortDirection?>(nameof(SortDirection), column => column.SortDirection);

    /// <summary>Fixes this column to a logical edge when displayed in an AlderDataGrid.</summary>
    public TableColumnPin Pin { get => GetValue(PinProperty); set => SetValue(PinProperty, value); }
    /// <summary>The stable key used to identify this column in sort and tree configuration.</summary>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    /// <summary>Whether the header can request a sort on this column.</summary>
    public bool CanSort { get => GetValue(CanSortProperty); set => SetValue(CanSortProperty, value); }
    /// <summary>An optional comparison function for ordering rows by this column.</summary>
    public Comparison<object>? SortComparison { get => GetValue(SortComparisonProperty); set => SetValue(SortComparisonProperty, value); }
    /// <summary>The active direction displayed by this column's header, or null for an unsorted column.</summary>
    public TableSortDirection? SortDirection { get => field; internal set => SetAndRaise(SortDirectionProperty, ref field, value); }

    internal bool CanInvokeSort => CanSort && !string.IsNullOrEmpty(Key);
}
