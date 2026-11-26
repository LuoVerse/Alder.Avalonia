using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Combines line, area, and bar series with optional secondary-axis values.</summary>
public sealed class AlderComposedChart : AlderCartesianChart
{
    /// <summary>Identifies the <see cref="IsSmooth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSmoothProperty = AlderLineChart.IsSmoothProperty.AddOwner<AlderComposedChart>();
    /// <summary>Identifies the <see cref="SecondaryAxisValueFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SecondaryAxisValueFormatProperty =
        AvaloniaProperty.Register<AlderComposedChart, string?>(nameof(SecondaryAxisValueFormat));
    /// <summary>Identifies the <see cref="SecondaryAxisMaximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double?> SecondaryAxisMaximumProperty =
        AvaloniaProperty.Register<AlderComposedChart, double?>(nameof(SecondaryAxisMaximum),
            validate: value => value is null || double.IsFinite(value.Value));
    /// <summary>Identifies the <see cref="MaxBarWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxBarWidthProperty = AlderBarChart.MaxBarWidthProperty.AddOwner<AlderComposedChart>();

    /// <summary>Whether line and area series use curves between adjacent points.</summary>
    public bool IsSmooth { get => GetValue(IsSmoothProperty); set => SetValue(IsSmoothProperty, value); }
    /// <summary>The numeric format string used on the secondary value axis.</summary>
    public string? SecondaryAxisValueFormat
    {
        get => GetValue(SecondaryAxisValueFormatProperty);
        set => SetValue(SecondaryAxisValueFormatProperty, value);
    }
    /// <summary>Includes this value in the right axis domain even when the data falls below it.</summary>
    public double? SecondaryAxisMaximum
    {
        get => GetValue(SecondaryAxisMaximumProperty);
        set => SetValue(SecondaryAxisMaximumProperty, value);
    }
    /// <summary>The max bar width in device-independent pixels.</summary>
    public double MaxBarWidth { get => GetValue(MaxBarWidthProperty); set => SetValue(MaxBarWidthProperty, value); }

    static AlderComposedChart() => AffectsRender<AlderComposedChart>(IsSmoothProperty, MaxBarWidthProperty);

    private protected override string? SecondaryAxisFormat => SecondaryAxisValueFormat ?? "#,0.##";
    private protected override double? SecondaryMaximum => SecondaryAxisMaximum;
    private protected override bool UsesBands => Series.Any(series => series.Kind == AlderChartSeriesKind.Bar);
    private protected override bool HasPointSeries => Series.Any(series => series.Kind is AlderChartSeriesKind.Area or AlderChartSeriesKind.Line);
    private protected override bool StackSeries(int seriesIndex) => IsStacked &&
        Series[seriesIndex] is { Kind: AlderChartSeriesKind.Bar, UseSecondaryAxis: false };

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SecondaryAxisValueFormatProperty || change.Property == SecondaryAxisMaximumProperty)
            ResetLayout();
    }

    private protected override void DrawSeries(DrawingContext context, ChartLayout chart)
    {
        using (context.PushClip(new Rect(chart.Plot.X - .5, chart.Plot.Y - .5,
            (chart.Plot.Width + 1) * RevealProgress, chart.Plot.Height + 1)))
        {
            for (int i = 0; i < Series.Count; i++)
                if (Series[i].Kind == AlderChartSeriesKind.Area)
                    DrawAreaSeries(context, chart, i, IsSmooth, false);
        }

        var bars = Enumerable.Range(0, Series.Count).Where(i => Series[i].Kind == AlderChartSeriesKind.Bar).ToArray();
        if (bars.Length > 0)
        {
            double barProgress = 1 - Math.Pow(1 - Math.Min(1, RevealProgress / .36), 2);
            DrawBarSeries(context, chart, bars, IsStacked, MaxBarWidth, 4, false, barProgress);
        }

        for (int i = 0; i < Series.Count; i++)
            if (Series[i].Kind == AlderChartSeriesKind.Line)
                DrawLineSeries(context, chart, i, IsSmooth, RevealProgress);
    }

    private protected override void DrawCursor(DrawingContext context, ChartLayout chart, int index) =>
        DrawDashedCursor(context, chart, index);

    private protected override void DrawActivePoints(DrawingContext context, ChartLayout chart, int index) =>
        DrawSeriesPoints(context, chart, index, ignoreBars: true);
}
