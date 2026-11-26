using System.Globalization;
using Alder.Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ChartsViewModel : GalleryViewModel
{
    public IReadOnlyList<string> Months => Enumerable.Range(1, 12).Select(DisplayCulture.DateTimeFormat.GetAbbreviatedMonthName).ToArray();
    public IReadOnlyList<string> HalfYear => Months.Take(6).ToArray();
    public IReadOnlyList<string> CommitDates => new[] { 13, 14, 15, 16, 17, 18, 19, 22, 25, 28, 31, 34, 37, 38, 40 }
        .Select(day => new DateTime(2025, 3, 1).AddDays(day - 1).ToString("MMM d", DisplayCulture)).ToArray();
    public IReadOnlyList<string> SkillAxes => [Text("ChartDesign"), Text("ChartFrontend"), Text("ChartBackend"),
        Text("ChartDevOps"), Text("ChartTesting"), Text("ChartLeadership")];
    public IReadOnlyList<string> PlatformAxes => [Text("ChartSpeed"), Text("ChartReliability"), Text("ChartSecurity"),
        Text("ChartUsability"), Text("ChartPerformance"), Text("ChartScalability")];
    public string Sessions => 214630.ToString("N0", DisplayCulture);
    public string RevenueValue => "US$" + 226420.ToString("N0", DisplayCulture);
    public string CustomerValue => 1278.ToString("N0", DisplayCulture);
    public string ChurnValue => .418.ToString("P1", DisplayCulture);
    public string StorageUsed => Format("ChartStorageDescription", 104, 128);
    public string RadialStorageUsed => Format("ChartStorageDescription", 90, 128);
    public string DeviceTotal => 4.56.ToString("0.#", DisplayCulture) + "K";
    public string CaloriesValue => Format("ChartCaloriesValue", 1670);
    public IReadOnlyList<string> TrafficLegend => PieTraffic.Select(slice => Format("ChartPieValueLabel", slice.Name, slice.Value)).ToArray();
    public IReadOnlyList<string> StorageLegend => PieStorage.Select(slice => Format("ChartPieStorageLabel", slice.Name, slice.Value)).ToArray();
    public IReadOnlyList<string> StorageRingValues => StorageRings.Select(item => Format("ChartGigabytesValue", item.Value)).ToArray();
    public string PlansTotal => 1170.ToString("N0", DisplayCulture);
    public string SalesValue => 472.ToString("N0", DisplayCulture);
    public string ExpensesValue => "US$" + 24380.ToString("N0", DisplayCulture);

    private object? portfolioRange = "month";
    public object? PortfolioRange
    {
        get => portfolioRange;
        set
        {
            if (Equals(value, portfolioRange))
                return;
            this.RaiseAndSetIfChanged(ref portfolioRange, value);
            this.RaisePropertyChanged(nameof(PortfolioSeries));
            this.RaisePropertyChanged(nameof(PortfolioValue));
            this.RaisePropertyChanged(nameof(PortfolioChange));
            this.RaisePropertyChanged(nameof(PortfolioCategories));
        }
    }

    private (double Start, double End, IReadOnlyList<double?> Track) PortfolioSnapshot => PortfolioRange switch
    {
        "hour" => (24610, 24806.23, new double?[] { 24610, 24617, 24596, 24642, 24712, 24658, 24728, 24806.23 }),
        "day" => (24120, 24806.23, new double?[] { 24120, 24332, 24305, 24480, 24421, 24554, 24631, 24806.23 }),
        "week" => (23855, 24806.23, new double?[] { 23855, 24040, 24065, 24352, 24211, 24388, 24524, 24806.23 }),
        "year" => (19650, 24806.23, new double?[] { 19650, 20400, 20120, 21220, 22083, 21896, 23483, 24806.23 }),
        "all" => (15400, 24806.23, new double?[] { 15400, 16235, 17140, 19122, 18976, 21380, 22491, 24806.23 }),
        _ => (23512, 24806.23, new double?[] { 23512, 23460, 23596, 23711, 23582, 23876, 24062, 23991, 24131, 24101, 24353, 24280, 24571, 24806.23 })
    };

    public IReadOnlyList<AlderChartSeries> PortfolioSeries => [new(Text("ChartPortfolio"), PortfolioSnapshot.Track)];
    public string PortfolioValue => Dollar(PortfolioSnapshot.End, 2);
    public string PortfolioChange
    {
        get
        {
            var (start, end, _) = PortfolioSnapshot;
            return Dollar(end - start, 2) + " (" + ((end - start) / start).ToString("P2", DisplayCulture) + ")";
        }
    }
    public IReadOnlyList<string> PortfolioCategories => Enumerable.Range(0, PortfolioSnapshot.Track.Count)
        .Select(i => new DateTime(2025, 11, 1, 9, 0, 0).AddMinutes(i * (PortfolioRange is "hour" ? 8 : 60))
            .ToString("t", DisplayCulture)).ToArray();

    public string TooltipJanuary => DisplayCulture.DateTimeFormat.GetMonthName(1);
    public string TooltipFebruary => DisplayCulture.DateTimeFormat.GetMonthName(2);
    public Func<string, string> LocalDateFormatter => label => DateTime.TryParseExact(label, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("D", DisplayCulture) : label;
    public Func<double, string> PortfolioFormatter => value => Dollar(value, value == Math.Truncate(value) ? 0 : 2);
    public Func<double, string> GroupedNumberFormatter => value => value.ToString("N0", DisplayCulture);
    public Func<double, string> ShadeFormatter => value => Text("ChartTooltipShade" + value.ToString("0", CultureInfo.InvariantCulture));

    public IReadOnlyList<AlderChartTooltipItem> FinancialRows =>
        [new(Text("ChartRevenue"), 12850), new(Text("ChartExpenses"), 8060)];
    public AlderChartTooltipData FinancialTooltip => new(TooltipJanuary,
    [
        new(Text("ChartRevenue"), Dollar(12850, 0), Brushes.DodgerBlue),
        new(Text("ChartExpenses"), Dollar(8060, 0), Brushes.RoyalBlue),
    ], "");
    public IReadOnlyList<AlderChartTooltipItem> MarketingRows =>
        [new(Text("ChartOrganic"), 15280), new(Text("ChartPaid"), 8910), new(Text("ChartReferral"), 3260)];
    public IReadOnlyList<AlderChartTooltipItem> SalesRows => [new(Text("ChartSales"), 472)];
    public IReadOnlyList<AlderChartTooltipItem> PortfolioRows =>
        [new(Text("ChartPortfolio"), 24806.23), new(Text("ChartBenchmark"), 21985)];
    public IReadOnlyList<AlderChartTooltipItem> PaletteRows =>
        [new("chart-1", 1, BrushResourceKey: "AlderChartSeries2"), new("chart-2", 2, BrushResourceKey: "AlderChartSeries3"),
         new("chart-3", 3, BrushResourceKey: "AlderChartSeries1"), new("chart-4", 4, BrushResourceKey: "AlderChartSeries4")];

    public IReadOnlyList<AlderChartSeries> TrafficTracks =>
    [
        new(Text("ChartOrganic"), [2300, 12400, 8100, 13800, 14600, 7900, 16800, 17700, 19400, 16100, 22300, 18100], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartPaid"), [1300, 7800, 10300, 12400, 9300, 9700, 11600, 10000, 6100, 12200, 16200, 11900], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSeries> TrafficColors =>
    [
        new(Text("ChartOrganic"), [2200, 5300, 7950, 6900, 9500, 7900, 11900, 11200, 14100, 13200, 15800, 14900], BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartPaid"), [1100, 3100, 5000, 5850, 4100, 5750, 7050, 6600, 8350, 9130, 10100, 9470], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartReferral"), [700, 1050, 1300, 1150, 1600, 1820, 2260, 2000, 2750, 2600, 3200, 3600], BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartDirect"), [450, 800, 1000, 900, 1340, 1650, 1500, 1860, 2000, 2390, 2260, 2800], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSeries> RevenueTrack =>
    [
        new(Text("ChartRevenue"), [4600, 5950, 5250, 7100, 6400, 8350, 7800, 9100, 8710, 10500, 9700, 11350], BrushResourceKey: "AlderChartSeries1") { ShowDots = true },
    ];

    public IReadOnlyList<AlderChartSeries> RevenueTrend =>
    [
        new(Text("ChartRevenue"), [4600, 5950, 5250, 7100, 6400, 8350, 7800, 9100, 8710, 10500, 9700, 11350], BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderChartSeries> Targets =>
    [
        new(Text("ChartTarget"), [5100, 5550, 6100, 6580, 7010, 7450, 7910, 8210, 8580, 9070, 9530, 10020], BrushResourceKey: "AlderChartSeries3") { IsDashed = true },
        new(Text("ChartActual"), [4600, 5950, 5250, 7100, 6400, 8350, 7800, 9100, 8710, 10500, 9700, 11350], BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderChartSeries> SparkRevenue =>
    [
        new(Text("ChartRevenue"), [36, 40, 34, 47, 45, 50, 55, 54, 62, 66, 65, 72], BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderChartSeries> SparkChurn =>
    [
        new(Text("ChartChurn"), [70, 65, 68, 61, 62, 57, 56, 54, 52, 49, 51, 47], BrushResourceKey: "AlderDanger"),
    ];

    public IReadOnlyList<AlderChartSeries> SparkCustomers =>
    [
        new(Text("ChartNewCustomers"), [38, 42, 36, 47, 44, 51, 56, 54, 62, 68, 65, 72], BrushResourceKey: "GalleryChartSuccess"),
    ];

    public IReadOnlyList<AlderChartSeries> RevenueOrdersSeries =>
    [
        new(Text("ChartRevenue"), [4900, 6650, 5800, 7900, 6600, 8920, 8170, 9700, 9140, 10850, 10400, 11500], BrushResourceKey: "AlderChartSeries1") { Kind = AlderChartSeriesKind.Bar, ValueFormat = "\"$\"#,0" },
        new(Text("ChartOrders"), [310, 450, 385, 518, 470, 585, 541, 661, 616, 755, 690, 822], BrushResourceKey: "AlderChartSeries2") { UseSecondaryAxis = true },
    ];

    public IReadOnlyList<AlderChartSeries> CodeShareSeries =>
    [
        new(Text("ChartIDE"), [2170, 1240, 3200, 13800, 13000, 920, 680, 430, 630, 5410, 8630, 3520, 11200, 21900, 17000], BrushResourceKey: "AlderChartSeries4") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartCLI"), [430, 280, 530, 1110, 1090, 115, 120, 80, 96, 430, 625, 560, 760, 1350, 920], BrushResourceKey: "AlderChartSeries3") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartCloudAgent"), [190, 140, 170, 530, 480, 78, 64, 45, 50, 320, 210, 220, 450, 735, 610], BrushResourceKey: "AlderChartSeries1") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartOther"), [190, 124, 160, 280, 210, 60, 63, 40, 43, 100, 225, 120, 240, 570, 410], BrushResourceKey: "AlderChartSeries2") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartAIShare"), [0.71, 0.66, 0.79, 0.91, 0.83, 0.63, 0.55, 0.49, 0.52, 0.71, 0.75, 0.69, 0.83, 0.91, 0.85], BrushResourceKey: "AlderMuted") { UseSecondaryAxis = true, ValueFormat = "0.#%" },
    ];

    public IReadOnlyList<AlderChartSeries> SessionsTargets =>
    [
        new(Text("ChartSessions"), [12500, 16400, 15300, 19200, 18600, 22500, 20700, 24100, 27800, 25600, 29300, 32400], BrushResourceKey: "AlderChartSeries1") { Kind = AlderChartSeriesKind.Area },
        new(Text("ChartTarget"), [14800, 15100, 16500, 17500, 18800, 19900, 20500, 22000, 22800, 24000, 25800, 27300], BrushResourceKey: "AlderChartSeries2") { IsDashed = true },
    ];

    public IReadOnlyList<AlderChartSeries> ImpressionsCtr =>
    [
        new(Text("ChartImpressions"), [44000, 51000, 47000, 57000, 53000, 68000, 62000, 76000, 72500, 81000, 78500, 92000], BrushResourceKey: "AlderChartSeries1") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartCtr"), [0.032, 0.041, 0.037, 0.043, 0.039, 0.049, 0.045, 0.053, 0.05, 0.061, 0.054, 0.062], BrushResourceKey: "AlderChartSeries4") { Kind = AlderChartSeriesKind.Area, UseSecondaryAxis = true, ValueFormat = "0.#%" },
    ];

    public IReadOnlyList<AlderChartSeries> Analytics =>
    [
        new(Text("ChartPageViews"), [8200, 11300, 9600, 14200, 12100, 16400, 14900, 18300, 17000, 20100, 19400, 22700], BrushResourceKey: "AlderChartSeries1") { Kind = AlderChartSeriesKind.Area },
        new(Text("ChartUsers"), [3700, 5100, 4600, 5900, 5200, 7100, 6500, 7800, 7300, 8250, 7900, 9200], BrushResourceKey: "AlderChartSeries4") { Kind = AlderChartSeriesKind.Bar },
        new(Text("ChartBounceRate"), [0.43, 0.39, 0.4, 0.36, 0.37, 0.33, 0.35, 0.31, 0.32, 0.29, 0.3, 0.27], BrushResourceKey: "AlderChartSeries2") { UseSecondaryAxis = true, ShowDots = true, ValueFormat = "0.#%" },
    ];

    public IReadOnlyList<AlderChartSeries> SkillSeries =>
    [
        new(Text("ChartScore"), [86, 94, 76, 71, 84, 68], BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderChartSeries> SkillDots =>
    [
        new(Text("ChartScore"), [86, 94, 76, 71, 84, 68], BrushResourceKey: "AlderChartSeries1") { ShowDots = true },
    ];

    public IReadOnlyList<AlderChartSeries> TeamSeries =>
    [
        new(Text("ChartTeamA"), [86, 79, 91, 75, 89, 74], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartTeamB"), [72, 90, 74, 85, 71, 88], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSeries> SprintSeries =>
    [
        new(Text("ChartCurrent"), [83, 75, 85, 68, 91, 77], BrushResourceKey: "AlderChartSeries4") { ShowDots = true },
        new(Text("ChartTarget"), [92, 89, 92, 86, 94, 90], BrushResourceKey: "AlderChartSeries2") { ShowDots = true },
    ];

    public IReadOnlyList<AlderChartSeries> DeviceSeries =>
    [
        new(Text("ChartDesktop"), [82, 66, 75, 63, 48, 79, 91], BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartMobile"), [66, 83, 60, 84, 91, 70, 60], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartTablet"), [43, 54, 49, 55, 62, 69, 52], BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> Browsers =>
    [
        new(Text("ChartChrome"), 58, BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartSafari"), 22, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartFirefox"), 12, BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartEdge"), 8, BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> PieTraffic =>
    [
        new(Text("ChartOrganic"), 4650, BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartDirect"), 3070, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartReferral"), 2250, BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartSocial"), 1480, BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> PieStorage =>
    [
        new(Text("ChartDocuments"), 39, BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartMedia"), 32, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartApps"), 19, BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartOther"), 14, BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> PieDevices =>
    [
        new(Text("ChartMobile"), 2690, BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartDesktop"), 1310, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartTablet"), 560, BrushResourceKey: "AlderChartSeries3"),
    ];

    public IReadOnlyList<AlderChartSlice> PieLastYear =>
    [
        new(Format("ChartQuarterFormat", 1), 18, BrushResourceKey: "AlderChartSeries4"),
        new(Format("ChartQuarterFormat", 2), 22, BrushResourceKey: "AlderChartSeries1"),
        new(Format("ChartQuarterFormat", 3), 23, BrushResourceKey: "AlderChartSeries3"),
        new(Format("ChartQuarterFormat", 4), 31, BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> PieThisYear =>
    [
        new(Format("ChartQuarterFormat", 1), 23, BrushResourceKey: "AlderChartSeries4"),
        new(Format("ChartQuarterFormat", 2), 28, BrushResourceKey: "AlderChartSeries1"),
        new(Format("ChartQuarterFormat", 3), 32, BrushResourceKey: "AlderChartSeries3"),
        new(Format("ChartQuarterFormat", 4), 38, BrushResourceKey: "AlderChartSeries2"),
    ];

    public IReadOnlyList<AlderChartSlice> PiePlans =>
    [
        new(Text("ChartEnterprise"), 365, BrushResourceKey: "AlderChartSeries4"),
        new(Text("ChartPro"), 495, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartStarter"), 310, BrushResourceKey: "AlderChartSeries3"),
    ];

    public IReadOnlyList<AlderRadialSeries> EnergyRings =>
    [
        new(Text("ChartCalories"), 1670, 2000, BrushResourceKey: "AlderChartSeries2"),
        new(Text("ChartSteps"), 8210, 10000, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartExercise"), 36, 120, BrushResourceKey: "AlderChartSeries4"),
    ];

    public IReadOnlyList<AlderRadialSeries> ActivityRing =>
    [
        new(Text("ChartActiveUsers"), 1410, 2600, BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderRadialSeries> StepsRing =>
    [
        new(Text("ChartSteps"), 7520, 10000, BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderRadialSeries> StorageRings =>
    [
        new(Text("ChartDocuments"), 43, 128, BrushResourceKey: "AlderChartSeries3"),
        new(Text("ChartMedia"), 29, 48, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ChartSystem"), 18, 18, BrushResourceKey: "AlderChartSeries4"),
    ];

    public IReadOnlyList<AlderRadialSeries> ConversionRing =>
    [
        new(Text("ChartConversion"), 74, 100, BrushResourceKey: "AlderChartSeries4"),
    ];

    public IReadOnlyList<AlderRadialSeries> EngagementRing =>
    [
        new(Text("ChartEngagement"), 79, 100, BrushResourceKey: "AlderChartSeries1"),
    ];

    public IReadOnlyList<AlderRadialSeries> BounceRing =>
    [
        new(Text("ChartBounceRate"), 36, 100, BrushResourceKey: "AlderChartSeries3"),
    ];

    public IReadOnlyList<AlderRadialSeries> ErrorRing =>
    [
        new(Text("ChartErrors"), 87, 100, BrushResourceKey: "AlderChartSeries2"),
    ];


    private string Dollar(double value, int decimals) =>
        "$" + value.ToString(decimals == 0 ? "N0" : "N2", DisplayCulture);
}
