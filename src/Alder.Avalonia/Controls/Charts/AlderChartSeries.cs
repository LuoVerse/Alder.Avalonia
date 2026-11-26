using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A named series whose values correspond to the chart's categories. Null values represent gaps.</summary>
public sealed record AlderChartSeries(string Name, IReadOnlyList<double?> Values, IBrush? Brush = null, string? BrushResourceKey = null)
{
    /// <summary>The series renderer used by a composed chart.</summary>
    public AlderChartSeriesKind Kind { get; init; }
    /// <summary>Whether line strokes use a dash pattern.</summary>
    public bool IsDashed { get; init; }
    /// <summary>Whether the renderer draws a marker at each plotted value.</summary>
    public bool ShowDots { get; init; }
    /// <summary>Connects observations across missing categories while leaving the missing values absent.</summary>
    public bool ConnectGaps { get; init; }
    /// <summary>Whether a composed chart scales the series against its secondary axis.</summary>
    public bool UseSecondaryAxis { get; init; }
    /// <summary>An optional numeric format for values belonging to this series.</summary>
    public string? ValueFormat { get; init; }
}

/// <summary>Selects how an individual series is drawn in a composed Cartesian chart.</summary>
public enum AlderChartSeriesKind
{
    /// <summary>Draws the series as connected points.</summary>
    Line,
    /// <summary>Fills the area between the series and its baseline.</summary>
    Area,
    /// <summary>Draws the series as bars.</summary>
    Bar
}

/// <summary>Pairs one chart tooltip label and formatted value with its series brush.</summary>
/// <param name="Name">The label of the series or segment.</param>
/// <param name="Value">The formatted value to display.</param>
/// <param name="Brush">The brush for its indicator.</param>
public sealed record AlderChartTooltipEntry(string Name, string Value, IBrush Brush);

/// <summary>The data context supplied to a chart's tooltip template.</summary>
public sealed record AlderChartTooltipData(string Label, IReadOnlyList<AlderChartTooltipEntry> Entries, string Total);
