using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;


/// <summary>The position of the metric's chart.</summary>
public enum KpiChartPlacement
{
    /// <summary>Below the metric.</summary>
    Bottom,
    /// <summary>Alongside the metric when room permits.</summary>
    Inline
}

/// <summary>Composes a metric, trend, optional progress, chart, and supplementary content.</summary>
public sealed class AlderKpi : ContentControl
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlderKpi, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<AlderKpi, double?>(nameof(Value));
    /// <summary>Identifies the <see cref="ValueFormat"/> property.</summary>
    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<AlderKpi, string>(nameof(ValueFormat), "N0");
    /// <summary>Identifies the <see cref="Prefix"/> property.</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<AlderKpi, string?>(nameof(Prefix));
    /// <summary>Identifies the <see cref="Suffix"/> property.</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<AlderKpi, string?>(nameof(Suffix));
    /// <summary>Identifies the <see cref="MissingValue"/> property.</summary>
    public static readonly StyledProperty<string> MissingValueProperty =
        AvaloniaProperty.Register<AlderKpi, string>(nameof(MissingValue), "\u2014");
    /// <summary>Identifies the <see cref="Culture"/> property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<AlderKpi, CultureInfo?>(nameof(Culture));
    /// <summary>Identifies the <see cref="Trend"/> property.</summary>
    public static readonly StyledProperty<string?> TrendProperty =
        AvaloniaProperty.Register<AlderKpi, string?>(nameof(Trend));
    /// <summary>Identifies the <see cref="TrendColor"/> property.</summary>
    public static readonly StyledProperty<AlderStatusColor> TrendColorProperty =
        AvaloniaProperty.Register<AlderKpi, AlderStatusColor>(nameof(TrendColor));
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderKpi, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<AlderKpi, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="Chart"/> property.</summary>
    public static readonly StyledProperty<object?> ChartProperty =
        AvaloniaProperty.Register<AlderKpi, object?>(nameof(Chart));
    /// <summary>Identifies the <see cref="ChartTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ChartTemplateProperty =
        AvaloniaProperty.Register<AlderKpi, IDataTemplate?>(nameof(ChartTemplate));
    /// <summary>Identifies the <see cref="ChartPlacement"/> property.</summary>
    public static readonly StyledProperty<KpiChartPlacement> ChartPlacementProperty =
        AvaloniaProperty.Register<AlderKpi, KpiChartPlacement>(nameof(ChartPlacement));
    /// <summary>Identifies the <see cref="Progress"/> property.</summary>
    public static readonly StyledProperty<double?> ProgressProperty =
        AvaloniaProperty.Register<AlderKpi, double?>(nameof(Progress));
    /// <summary>Identifies the <see cref="Footer"/> property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderKpi, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<AlderKpi, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="ShowSeparator"/> property.</summary>
    public static readonly StyledProperty<bool> ShowSeparatorProperty =
        AvaloniaProperty.Register<AlderKpi, bool>(nameof(ShowSeparator), true);

    /// <summary>Gets or sets the title.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Gets or sets the value.</summary>
    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Gets or sets the value format.</summary>
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    /// <summary>Gets or sets the prefix.</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <summary>Gets or sets the suffix.</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <summary>Gets or sets the missing value.</summary>
    public string MissingValue { get => GetValue(MissingValueProperty); set => SetValue(MissingValueProperty, value); }
    /// <summary>Gets or sets the culture.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>Gets or sets the trend.</summary>
    public string? Trend { get => GetValue(TrendProperty); set => SetValue(TrendProperty, value); }
    /// <summary>Gets or sets the trend color.</summary>
    public AlderStatusColor TrendColor { get => GetValue(TrendColorProperty); set => SetValue(TrendColorProperty, value); }
    /// <summary>Gets or sets the icon.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Gets or sets the actions.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>Gets or sets the chart.</summary>
    public object? Chart { get => GetValue(ChartProperty); set => SetValue(ChartProperty, value); }
    /// <summary>Gets or sets the chart template.</summary>
    public IDataTemplate? ChartTemplate { get => GetValue(ChartTemplateProperty); set => SetValue(ChartTemplateProperty, value); }
    /// <summary>Gets or sets the chart placement.</summary>
    public KpiChartPlacement ChartPlacement { get => GetValue(ChartPlacementProperty); set => SetValue(ChartPlacementProperty, value); }
    /// <summary>Gets or sets the progress.</summary>
    public double? Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    /// <summary>Gets or sets the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>Gets or sets the footer template.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>Gets or sets the show separator.</summary>
    public bool ShowSeparator { get => GetValue(ShowSeparatorProperty); set => SetValue(ShowSeparatorProperty, value); }

    /// <summary>Identifies the formatted read-only value property.</summary>
    public static readonly DirectProperty<AlderKpi, string> DisplayValueProperty =
        AvaloniaProperty.RegisterDirect<AlderKpi, string>(nameof(DisplayValue), owner => owner.DisplayValue);

    /// <summary>The formatted metric or placeholder for a missing observation.</summary>
    public string DisplayValue
    {
        get => field;
        private set => SetAndRaise(DisplayValueProperty, ref field, value);
    } = "—";

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == ValueFormatProperty || change.Property == PrefixProperty ||
            change.Property == SuffixProperty || change.Property == MissingValueProperty || change.Property == CultureProperty)
        {
            var culture = Culture ?? CultureInfo.CurrentCulture;
            DisplayValue = Value is { } value && double.IsFinite(value)
                ? string.Concat(Prefix, value.ToString(ValueFormat, culture), Suffix)
                : MissingValue;
        }
    }
}

/// <summary>A shared card that wraps its metrics into fewer columns as the available width decreases.</summary>
public sealed class AlderKpiGroup : ItemsControl
{
    /// <summary>Identifies the <see cref="Orientation"/> property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderKpiGroup, Orientation>(nameof(Orientation), Orientation.Horizontal);
    /// <summary>Identifies the <see cref="Columns"/> property.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<AlderKpiGroup, int>(nameof(Columns), 3, validate: value => value > 0);
    /// <summary>Identifies the <see cref="MinColumnWidth"/> property.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<AlderKpiGroup, double>(nameof(MinColumnWidth), 188, validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="ShowDividers"/> property.</summary>
    public static readonly StyledProperty<bool> ShowDividersProperty =
        AvaloniaProperty.Register<AlderKpiGroup, bool>(nameof(ShowDividers), true);

    /// <summary>Gets or sets the orientation.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Gets or sets the columns.</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    /// <summary>Gets or sets the min column width.</summary>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    /// <summary>Whether to draw separators between metrics on the shared card.</summary>
    public bool ShowDividers { get => GetValue(ShowDividersProperty); set => SetValue(ShowDividersProperty, value); }

    /// <summary>Initializes a metric group.</summary>
    public AlderKpiGroup() => UpdatePanel();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty || change.Property == ColumnsProperty ||
            change.Property == MinColumnWidthProperty || change.Property == ShowDividersProperty)
            UpdatePanel();
    }

    private void UpdatePanel() => SetCurrentValue(ItemsPanelProperty, new FuncTemplate<Panel?>(() => new DisplayGroupPanel
    {
        MaxColumns = Orientation == Orientation.Vertical ? 1 : Columns,
        MinimumColumnWidth = MinColumnWidth,
        Gap = 1,
        ShowDividers = ShowDividers
    }));
}
