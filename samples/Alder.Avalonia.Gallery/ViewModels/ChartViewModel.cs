using Alder.Avalonia.Controls;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ChartViewModel : GalleryViewModel
{
    public IReadOnlyList<string> Months => Enumerable.Range(1, 12).Select(DisplayCulture.DateTimeFormat.GetAbbreviatedMonthName).ToArray();
    public IReadOnlyList<string> HalfYear => Months.Take(6).ToArray();
    public IReadOnlyList<string> Quarters => Enumerable.Range(1, 4).Select(quarter => Format("ChartQuarterFormat", quarter)).ToArray();
    public IReadOnlyList<string> Days => Enumerable.Range(1, 7).Select(day => DisplayCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(day % 7))).ToArray();
    public IReadOnlyList<string> ProductNames =>
        [Text("ChartWidgets"), Text("ChartGadgets"), Text("ChartModules"), Text("ChartPlugins"), Text("ChartAddOns")];
    public string RevenueTotal => 226420.ToString("C0", DisplayCulture);
    public string UserTotal => 108463.ToString("N0", DisplayCulture);
    public string SessionTotal => 146813.ToString("N0", DisplayCulture);
    public string BounceRate => .418.ToString("P1", DisplayCulture);
    public string SalesTotal => 316.ToString("N0", DisplayCulture);

    public IReadOnlyList<AlderChartSeries> Revenue =>
    [
        new(Text("ChartRevenue"), [4600, 5950, 5250, 7100, 6400, 8350, 7800, 9100, 8710, 10500, 9700, 11350]),
    ];

    public IReadOnlyList<AlderChartSeries> Traffic =>
    [
        new(Text("ChartOrganic"), [2200, 5300, 7950, 6900, 9500, 7900, 11900, 11200, 14100, 13200, 15800, 14900]),
        new(Text("ChartPaid"), [1100, 3100, 5000, 5850, 4100, 5750, 7050, 6600, 8350, 9130, 10100, 9470]),
    ];

    public IReadOnlyList<AlderChartSeries> TrafficBreakdown =>
    [
        new(Text("ChartOrganic"), [2200, 5300, 7950, 6900, 9500, 7900, 11900, 11200, 14100, 13200, 15800, 14900], BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartPaid"), [1100, 3100, 5000, 5850, 4100, 5750, 7050, 6600, 8350, 9130, 10100, 9470], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartReferral"), [700, 1050, 1300, 1150, 1600, 1820, 2260, 2000, 2750, 2600, 3200, 3600], BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartDirect"), [450, 800, 1000, 900, 1340, 1650, 1500, 1860, 2000, 2390, 2260, 2800], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSeries> KpiRevenue =>
    [
        new(Text("ChartRevenue"), [36, 40, 34, 47, 45, 50, 55, 54, 62, 66, 65, 72]),
    ];

    public IReadOnlyList<AlderChartSeries> KpiChurn =>
    [
        new(Text("ChartChurn"), [70, 65, 68, 61, 62, 57, 56, 54, 52, 49, 51, 47], BrushResourceKey: "AlderDanger"),
    ];

    public IReadOnlyList<AlderChartSeries> KpiUsers =>
    [
        new(Text("ChartUsers"), [38, 42, 36, 47, 44, 51, 56, 54, 62, 68, 65, 72], BrushResourceKey: "GalleryChartSuccess"),
    ];

    public IReadOnlyList<AlderChartSeries> Sales =>
    [
        new(Text("ChartSales"), [19, 33, 27, 43, 39, 50, 45, 55, 49, 60, 54, 58]),
    ];

    public IReadOnlyList<AlderChartSeries> Channels =>
    [
        new(Text("ChartOnline"), [4200, 6100, 5200, 7400, 6500, 8200]),
        new(Text("ChartRetail"), [2700, 3400, 3200, 4300, 3900, 4700], BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartDirectSales"), [3100, 4100, 3650, 5200, 4600, 5900], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSeries> Plans =>
    [
        new(Text("ChartStarter"), [4100, 5200, 5900, 7100], BrushResourceKey: "AlderChartSeries2"),
        new(Text("ChartPro"), [7900, 9500, 12000, 14000], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartEnterprise"), [10500, 15300, 17800, 21950], BrushResourceKey: "AlderChartSeries4"),
    ];

    public IReadOnlyList<AlderChartSeries> Products =>
    [
        new(Text("ChartSales"), [1780, 1530, 1170, 940, 640]),
    ];

    public IReadOnlyList<AlderChartSeries> Activity =>
    [
        new(Text("ChartLow"), [110, 135, 170, 145, 155, 120, 150], BrushResourceKey: "AlderChartSeries2"),
        new(Text("ChartMedium"), [255, 289, 240, 275, 285, 225, 280], BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartHigh"), [170, 195, 160, 185, 190, 205, 220], BrushResourceKey: "AlderChartSeries4"),
    ];

    public IReadOnlyList<AlderChartSeries> Orders =>
    [
        new(Text("ChartCurrentWeek"), [126, 148, 173, 206, 175, 213, 187]),
        new(Text("ChartPreviousWeek"), [106, 135, 128, 142, 156, 168, 149], BrushResourceKey: "AlderChartSeries2"),
    ];
}
