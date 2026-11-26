using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Plots Cartesian series as grouped or stacked bars in either orientation.</summary>
public sealed class AlderBarChart : AlderCartesianChart
{
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderBarChart, Orientation>(nameof(Orientation), Orientation.Vertical);
    /// <summary>Identifies the <see cref="BarCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> BarCornerRadiusProperty =
        AvaloniaProperty.Register<AlderBarChart, double>(nameof(BarCornerRadius), 5,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Identifies the <see cref="MaxBarWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxBarWidthProperty =
        AvaloniaProperty.Register<AlderBarChart, double>(nameof(MaxBarWidth), double.PositiveInfinity,
            validate: value => value > 0);
    /// <summary>Identifies the <see cref="RoundBarBase"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> RoundBarBaseProperty =
        AvaloniaProperty.Register<AlderBarChart, bool>(nameof(RoundBarBase));

    /// <summary>Whether categories are drawn along the horizontal or vertical axis.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The radius of the rounded bar corners in device-independent pixels.</summary>
    public double BarCornerRadius { get => GetValue(BarCornerRadiusProperty); set => SetValue(BarCornerRadiusProperty, value); }

    /// <summary>The maximum thickness of a bar, perpendicular to its value axis.</summary>
    public double MaxBarWidth { get => GetValue(MaxBarWidthProperty); set => SetValue(MaxBarWidthProperty, value); }
    /// <summary>Whether to round the corners where a bar meets its baseline.</summary>
    public bool RoundBarBase { get => GetValue(RoundBarBaseProperty); set => SetValue(RoundBarBaseProperty, value); }

    private protected override TimeSpan RevealDuration => TimeSpan.FromMilliseconds(400);
    private protected override bool IsHorizontal => Orientation == Orientation.Horizontal;
    private protected override bool UsesBands => true;
    private protected override bool HasPointSeries => false;

    static AlderBarChart() => AffectsRender<AlderBarChart>(BarCornerRadiusProperty, MaxBarWidthProperty, RoundBarBaseProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
            ResetLayout();
    }

    private protected override void DrawSeries(DrawingContext context, ChartLayout chart) =>
        DrawBarSeries(context, chart, Enumerable.Range(0, Series.Count).ToArray(), IsStacked,
            MaxBarWidth, BarCornerRadius, RoundBarBase, RevealProgress);

    private protected override void DrawCursor(DrawingContext context, ChartLayout chart, int index)
    {
        if (index < 0 || ActiveData is null || CursorBrush is not { } brush)
            return;
        var plot = chart.Plot;
        double band = chart.CategorySpacing;
        double start = chart.Category(index) - band / 2;
        var rect = IsHorizontal ? new Rect(plot.Left, start, plot.Width, band)
            : new Rect(start, plot.Top, band, plot.Height);
        context.DrawRectangle(brush, null, rect, 3, 3);
    }
}
