using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Plots Cartesian series as filled areas with optional curve smoothing.</summary>
public sealed class AlderAreaChart : AlderCartesianChart
{
    /// <summary>Identifies the <see cref="IsSmooth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSmoothProperty =
        AvaloniaProperty.Register<AlderAreaChart, bool>(nameof(IsSmooth), true);

    /// <summary>Whether adjacent data points are connected with curves.</summary>
    public bool IsSmooth { get => GetValue(IsSmoothProperty); set => SetValue(IsSmoothProperty, value); }

    static AlderAreaChart() => AffectsRender<AlderAreaChart>(IsSmoothProperty);

    private protected override void DrawSeries(DrawingContext context, ChartLayout chart)
    {
        var plot = chart.Plot.Inflate(.5);
        using var reveal = context.PushClip(new Rect(plot.X, plot.Y, plot.Width * RevealProgress, plot.Height));
        for (int s = 0; s < chart.Spans.Length; s++)
            DrawAreaSeries(context, chart, s, IsSmooth, IsStacked);
    }

    private protected override void DrawCursor(DrawingContext context, ChartLayout chart, int index) =>
        DrawDashedCursor(context, chart, index);

    private protected override void DrawActivePoints(DrawingContext context, ChartLayout chart, int index) =>
        DrawSeriesPoints(context, chart, index);
}
