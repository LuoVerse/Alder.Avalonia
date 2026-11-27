using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Plots Cartesian values as lines with optional curve smoothing.</summary>
public sealed class AlderLineChart : AlderCartesianChart
{
    /// <summary>Identifies the <see cref="IsSmooth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSmoothProperty =
        AvaloniaProperty.Register<AlderLineChart, bool>(nameof(IsSmooth), true);

    /// <summary>Whether the plotted lines are curved between adjacent data points.</summary>
    public bool IsSmooth { get => GetValue(IsSmoothProperty); set => SetValue(IsSmoothProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSmoothProperty)
            InvalidateSeries();
    }

    private protected override void DrawSeries(DrawingContext context, ChartLayout chart)
    {
        for (int s = 0; s < Series.Count; s++)
            DrawLineSeries(context, chart, s, IsSmooth, RevealProgress);
    }

    private protected override void DrawCursor(DrawingContext context, ChartLayout chart, int index) =>
        DrawDashedCursor(context, chart, index);

    private protected override void DrawActivePoints(DrawingContext context, ChartLayout chart, int index) =>
        DrawSeriesPoints(context, chart, index);
}
