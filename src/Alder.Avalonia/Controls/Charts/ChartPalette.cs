using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal static class ChartPalette
{
    private static readonly string[] Keys =
        ["AlderChartSeries1", "AlderChartSeries2", "AlderChartSeries3", "AlderChartSeries4", "AlderChartSeries5"];
    private static readonly IBrush[] Fallbacks =
        [Brushes.DodgerBlue, Brushes.MediumPurple, Brushes.LightSeaGreen, Brushes.DarkOrange, Brushes.IndianRed];

    internal static IBrush Resolve(StyledElement element, int index, string? key) =>
        element.TryFindResource(key ?? Keys[index % Keys.Length], element.ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush : Fallbacks[index % Fallbacks.Length];
}
