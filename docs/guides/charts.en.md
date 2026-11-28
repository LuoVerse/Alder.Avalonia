# Charts

[简体中文](charts.md) · **English**

Charts display data series against categories. Map your application data to the chart model and bind the result to the control. The line chart below has two series. Each value corresponds to the month at the same index, and `null` marks a missing value.

```csharp
using Alder.Avalonia.Controls;

var chart = new AlderLineChart
{
    Height = 180,
    Categories = new[] { "Jan", "Feb", "Mar" },
    Series = new[]
    {
        new AlderChartSeries("Visits", new double?[] { 120, null, 182 }),
        new AlderChartSeries("Sign-ups", new double?[] { 12, 19, 24 })
    },
    ShowTooltip = true
};
```

With MVVM, bind `Categories`, `Series`, and related properties to the ViewModel. When rerunning a query or changing the date range, assign new arrays or lists and raise property change notifications. Use `BrushResourceKey` to reference a theme color or set `Brush` on the series to supply a custom brush.

| Chart | Data model and options |
| --- | --- |
| Line, Area, Bar | Set `Categories` and `Series`. Use `AlderChartSeries` to provide names and nullable values. Configure markers, fills, orientation, and stacking where the control supports them. [Line](../../samples/Alder.Avalonia.Gallery/Views/LineChartExample.axaml) · [Area](../../samples/Alder.Avalonia.Gallery/Views/AreaChartExample.axaml) · [Bar](../../samples/Alder.Avalonia.Gallery/Views/BarChartExample.axaml) |
| Composed | Mix bar, line, and area series on the same categories with `AlderChartSeries.Kind`. Set `UseSecondaryAxis` on the series that need a second axis. [Example](../../samples/Alder.Avalonia.Gallery/Views/ComposedChartExample.axaml) |
| Pie | Set `Slices` to a collection of `AlderChartSlice`. Use `InnerRadius` for a doughnut chart and provide `InnerSlices` to show two rings. [Example](../../samples/Alder.Avalonia.Gallery/Views/PieChartExample.axaml) |
| Radar | Set `Categories` to define the dimensions and `Series` for the values. Use the same `Maximum` for datasets you want to compare. [Example](../../samples/Alder.Avalonia.Gallery/Views/RadarChartExample.axaml) |
| Radial | Set `Rings` to a collection of `AlderRadialSeries`. Each series can specify its own current value and maximum. [Example](../../samples/Alder.Avalonia.Gallery/Views/RadialChartExample.axaml) |
| Tooltip | Use `TooltipTemplate` to render `AlderChartTooltipData`. The template can read `Label`, `Entries`, and `Total`. [Example](../../samples/Alder.Avalonia.Gallery/Views/ChartTooltipExample.axaml) |

## Saving and restoring chart settings

Save the date range, metric IDs, aggregation interval, and the user's chart type. If you need offline replay, also save categories and raw values, including `null` entries. When the page reopens, query the data using those settings or rebuild `AlderChartSeries`, `AlderChartSlice`, or `AlderRadialSeries` from the snapshot. The view and current data determine the theme brushes, templates, and hover tooltips. [Gallery data mapping](../../samples/Alder.Avalonia.Gallery/ViewModels/ChartsViewModel.cs)
