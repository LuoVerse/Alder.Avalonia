using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.LogicalTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the marker used beside an entry in a chart tooltip.</summary>
public enum AlderChartTooltipIndicator
{
    /// <summary>Shows a circular series marker.</summary>
    Dot,
    /// <summary>Shows a short line in the series color.</summary>
    Line
}

/// <summary>Supplies a raw labeled value and optional brush for a standalone chart tooltip.</summary>
/// <param name="Name">The row label.</param>
/// <param name="Value">The raw numeric value.</param>
/// <param name="Brush">An optional indicator brush.</param>
/// <param name="BrushResourceKey">An optional resource key used to resolve the indicator brush.</param>
public sealed record AlderChartTooltipItem(string Name, double Value, IBrush? Brush = null, string? BrushResourceKey = null);

/// <summary>Formats and presents raw chart data as labeled rows with optional markers.</summary>
public sealed class AlderChartTooltip : TemplatedControl
{
    /// <summary>Identifies the <see cref="Data"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderChartTooltipData?> DataProperty =
        AvaloniaProperty.Register<AlderChartTooltip, AlderChartTooltipData?>(nameof(Data));
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AlderChartTooltip, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="ItemsSource"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderChartTooltipItem>> ItemsSourceProperty =
        AvaloniaProperty.Register<AlderChartTooltip, IReadOnlyList<AlderChartTooltipItem>>(nameof(ItemsSource), []);
    /// <summary>Identifies the <see cref="LabelFormatter"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<string, string>?> LabelFormatterProperty =
        AvaloniaProperty.Register<AlderChartTooltip, Func<string, string>?>(nameof(LabelFormatter));
    /// <summary>Identifies the <see cref="ValueFormatter"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<AlderChartTooltip, Func<double, string>?>(nameof(ValueFormatter));
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderChartTooltipIndicator> IndicatorProperty =
        AvaloniaProperty.Register<AlderChartTooltip, AlderChartTooltipIndicator>(nameof(Indicator));
    /// <summary>Identifies the <see cref="IsActive"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AlderChartTooltip, bool>(nameof(IsActive), true);
    /// <summary>Identifies the <see cref="ShowHeader"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<AlderChartTooltip, bool>(nameof(ShowHeader), true);

    /// <summary>Identifies the <see cref="Header"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderChartTooltip, string?> HeaderProperty =
        AvaloniaProperty.RegisterDirect<AlderChartTooltip, string?>(nameof(Header), tooltip => tooltip.Header);
    /// <summary>Identifies the <see cref="Rows"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderChartTooltip, IReadOnlyList<AlderChartTooltipRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<AlderChartTooltip, IReadOnlyList<AlderChartTooltipRow>>(nameof(Rows), tooltip => tooltip.Rows);
    /// <summary>Identifies the <see cref="HasContent"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderChartTooltip, bool> HasContentProperty =
        AvaloniaProperty.RegisterDirect<AlderChartTooltip, bool>(nameof(HasContent), tooltip => tooltip.HasContent);

    private string? header;
    private IReadOnlyList<AlderChartTooltipRow> rows = [];
    private bool hasContent;

    /// <summary>Preformatted tooltip data, as supplied by a chart. Use ItemsSource for numeric values.</summary>
    public AlderChartTooltipData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    /// <summary>The text that identifies the control or value.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>The raw values presented as rows in the tooltip.</summary>
    public IReadOnlyList<AlderChartTooltipItem> ItemsSource { get => GetValue(ItemsSourceProperty) ?? []; set => SetValue(ItemsSourceProperty, value); }
    /// <summary>A function that formats the tooltip's category label.</summary>
    public Func<string, string>? LabelFormatter { get => GetValue(LabelFormatterProperty); set => SetValue(LabelFormatterProperty, value); }
    /// <summary>A function that formats the row's numeric value.</summary>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }
    /// <summary>The optional indicator displayed alongside the main content.</summary>
    public AlderChartTooltipIndicator Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>Whether the tooltip is currently active and can be shown.</summary>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    /// <summary>Whether to display the formatted category header.</summary>
    public bool ShowHeader { get => GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }

    /// <summary>The formatted text displayed above the rows.</summary>
    public string? Header => header;
    /// <summary>The formatted entries and marker dimensions for the tooltip template.</summary>
    public IReadOnlyList<AlderChartTooltipRow> Rows => rows;
    /// <summary>Whether formatted tooltip rows are available.</summary>
    public bool HasContent => hasContent;

    /// <summary>Initializes a new instance of <see cref="AlderChartTooltip"/>.</summary>
    public AlderChartTooltip() => ActualThemeVariantChanged += (_, _) => UpdateContent();

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UpdateContent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty || change.Property == LabelProperty ||
            change.Property == ItemsSourceProperty || change.Property == LabelFormatterProperty ||
            change.Property == ValueFormatterProperty || change.Property == IndicatorProperty ||
            change.Property == IsActiveProperty || change.Property == ShowHeaderProperty)
            UpdateContent();
    }

    private void UpdateContent()
    {
        var label = Data?.Label ?? Label;
        var title = ShowHeader && !string.IsNullOrWhiteSpace(label) ? LabelFormatter?.Invoke(label) ?? label : null;
        var entries = Data?.Entries ?? ItemsSource.Where(i => double.IsFinite(i.Value))
            .Select((item, index) => new AlderChartTooltipEntry(item.Name,
                ValueFormatter?.Invoke(item.Value) ?? item.Value.ToString("0.##", CultureInfo.CurrentCulture),
                item.Brush ?? ChartPalette.Resolve(this, index, item.BrushResourceKey))).ToArray();
        bool line = Indicator == AlderChartTooltipIndicator.Line;
        SetAndRaise(HeaderProperty, ref header, title);
        SetAndRaise(RowsProperty, ref rows, entries.Select(entry => new AlderChartTooltipRow(entry, line ? 4 : 8, line ? 12 : 8)).ToArray());
        SetAndRaise(HasContentProperty, ref hasContent, IsActive && entries.Count > 0);
    }
}

/// <summary>Contains an entry and the measured marker dimensions to use in the tooltip.</summary>
/// <param name="Entry">The formatted data and indicator brush.</param>
/// <param name="MarkerWidth">The width of the marker.</param>
/// <param name="MarkerHeight">The height of the marker.</param>
public sealed record AlderChartTooltipRow(AlderChartTooltipEntry Entry, double MarkerWidth, double MarkerHeight);
