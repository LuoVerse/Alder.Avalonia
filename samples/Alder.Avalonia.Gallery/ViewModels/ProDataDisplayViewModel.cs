using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record DisplayExampleFile(string Name, string Detail, string Size, bool IsAvailable, bool IsFolder = false);
public sealed record WidgetTeamMember(string Name, string Role, string Status, string Email);
public sealed record WidgetUsageMetric(string Label, string Amount, double Progress, FeedbackColor Color = FeedbackColor.Accent);

public sealed class ProDataDisplayViewModel : GalleryViewModel
{
    public ProDataDisplayViewModel()
    {
        Report = ReactiveCommand.Create<string>(key => Toast.Show(new ToastOptions(Text(key))));
        OpenFile = ReactiveCommand.Create<DisplayExampleFile>(file => Toast.Show(new ToastOptions(file.Name)));
        ClearFiles = ReactiveCommand.Create(SelectedFiles.Clear);
        DownloadFiles = ReactiveCommand.Create(() => Toast.Show(new ToastOptions(Text("ProListDownload"))));
        ChooseItemCardLanguage = ReactiveCommand.Create<string>(key =>
        {
            itemCardLanguage = key;
            this.RaisePropertyChanged(nameof(ItemCardLanguage));
        });
        SelectedFiles.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasSelectedFiles));
            this.RaisePropertyChanged(nameof(SelectedFileCount));
            this.RaisePropertyChanged(nameof(SelectedFilesLabel));
        };
    }

    public ICommand Report { get; }
    public ICommand OpenFile { get; }
    public ICommand ClearFiles { get; }
    public ICommand DownloadFiles { get; }
    public ICommand ChooseItemCardLanguage { get; }
    private string itemCardLanguage = "ProDisplayEnglish";
    public string ItemCardLanguage => Text(itemCardLanguage);
    public ObservableCollection<object> SelectedFiles { get; } = [];
    public bool HasSelectedFiles => SelectedFiles.Count > 0;
    public int SelectedFileCount => SelectedFiles.Count;
    public string SelectedFilesLabel => Format("ProDisplaySelectedFiles", SelectedFiles.Count);
    public Func<object, bool> CanOpenFile { get; } = item => item is DisplayExampleFile { IsAvailable: true };
    public bool Notifications { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
    public bool ProductNews { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string Email { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "studio@example.com";

    public IReadOnlyList<string> Languages => [Text("ProDisplayEnglish"), Text("ProDisplayChinese"), Text("ProDisplaySpanish")];
    public IReadOnlyList<string> Permissions => [Text("ProDisplayViewer"), Text("ProDisplayEditor"), Text("ProDisplayAdmin")];

    public IReadOnlyList<DisplayExampleFile> Files =>
    [
        new("Design notes.md", Text("ProDisplayUpdatedToday"), "18 KB", true),
        new("Assets", Text("ProDisplayUpdatedYesterday"), "4.2 MB", true),
        new("release.json", Text("ProDisplayUpdatedWeek"), "8 KB", false),
        new("Archive", Text("ProDisplayUpdatedWeek"), "1.1 MB", true)
    ];

    public IReadOnlyList<DisplayExampleFile> NavigationFiles =>
    [
        new(Text("ProListDocuments"), Text("ProListUpdatedTwoDays"), "", true, true),
        new(Text("ProListPhotos"), Text("ProListUpdatedOneWeek"), "", true, true),
        new("README.md", Text("ProListUpdatedThreeHours"), "", true),
        new("package.json", Text("ProDisplayUpdatedYesterday"), "", true),
        new("src", Text("ProListUpdatedNow"), "", true, true),
        new(".gitignore", Text("ProListUpdatedTwoWeeks"), "", true)
    ];

    public IReadOnlyList<DisplayExampleFile> SecondaryFiles => NavigationFiles.Take(5).ToArray();

    public IReadOnlyList<DisplayExampleFile> DisabledFiles =>
    [
        new(Text("ProListDocuments"), "", "", true, true),
        new("Budget.xlsx", "", "", false),
        new("Archived.zip", "", "", false),
        new(Text("ProListPhotos"), "", "", true, true),
        new("Old backup.tar", "", "", false),
        new("README.md", "", "", true)
    ];

    public IReadOnlyList<DisplayExampleFile> ActionFiles =>
    [
        new(Text("ProListDesignSystem"), Text("ProListUpdatedTwoDays"), "", true, true),
        ..NavigationFiles.Skip(1),
        new("tsconfig.json", Text("ProListUpdatedThreeDays"), "", true),
        new("node_modules", Text("ProListUpdatedOneDay"), "", true, true)
    ];

    public IReadOnlyList<WidgetTeamMember> Team =>
    [
        new("Kate Moore", "CEO", Text("ProItemCardActiveDevice"), "kate@acme.com"),
        new("John Smith", "CTO", Text("ProItemCardActiveDevice"), "john@acme.com"),
        new("Sara Johnson", "CMO", Text("ProWidgetOnLeave"), "sara@acme.com"),
        new("Michael Brown", "CFO", Text("ProItemCardActiveDevice"), "michael@acme.com")
    ];

    public IReadOnlyList<string> MonthLabels => Enumerable.Range(1, 12)
        .Select(month => DisplayCulture.DateTimeFormat.GetAbbreviatedMonthName(month)).ToArray();

    public IReadOnlyList<string> HalfYearLabels => MonthLabels.Take(6).ToArray();

    public IReadOnlyList<AlderChartSeries> MonthlySales =>
        [new(Text("ProWidgetMonthlySales"), [18, 32, 28, 45, 38, 52, 42, 55, 48, 60, 53, 58], BrushResourceKey: "AlderChartSeries1")];

    public IReadOnlyList<AlderChartSeries> DashboardRevenue =>
        [new(Text("ProDisplayRevenue"), [4200, 5800, 4900, 7200, 6100, 8400, 7800, 9200, 8600, 10200, 9800, 11500],
            BrushResourceKey: "AlderChartSeries1") { ValueFormat = "C0" }];

    public IReadOnlyList<AlderChartSeries> DashboardTraffic =>
    [
        new(Text("ProDisplayOrganic"), [2000, 5000, 8000, 7000, 9500, 8000], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ProDisplayPaid"), [1000, 3000, 5000, 6000, 4000, 5500], BrushResourceKey: "AlderChartSeries2")
    ];

    public IReadOnlyList<AlderChartSeries> WidgetTrafficSources =>
    [
        new(Text("ProDisplayOrganic"), [2000, 15000, 8000, 14000, 15000, 8000, 18000, 18000, 20000, 17000, 22000, 15000],
            BrushResourceKey: "AlderChartSeries1"),
        new(Text("ProWidgetPaidAds"), [1000, 10000, 12000, 14000, 8000, 9000, 12000, 10000, 5000, 12000, 18000, 9000],
            BrushResourceKey: "AlderChartSeries2")
    ];

    public IReadOnlyList<AlderChartSeries> WidgetRequests =>
        [new(Text("ProWidgetRequests"), [680, 1150, 1470, 1130, 560, 470, 960, 1200, 1120, 1060, 780, 930, 950, 1050, 1740,
            940, 1570, 1250, 930, 1280, 1180, 1320, 950, 980, 680, 510, 960, 860, 630, 380], BrushResourceKey: "AlderChartSeries1")];

    public IReadOnlyList<double> TokenTicks { get; } = [0, 35000, 70000, 105000, 140000];
    public IReadOnlyList<double> RevenueTicks { get; } = [0, 3000, 6000, 9000, 12000];
    public IReadOnlyList<double> DashboardTrafficTicks { get; } = [0, 2500, 5000, 7500, 10000];
    public IReadOnlyList<double> TrafficSourceTicks { get; } = [0, 5500, 11000, 16500, 22000];
    public IReadOnlyList<double> SalesTicks { get; } = [0, 15, 30, 45, 60];
    public IReadOnlyList<double> RequestTicks { get; } = [0, 450, 900, 1350, 1800];
    public IReadOnlyList<double> SparklineTicks { get; } = [0, 60];

    public IReadOnlyList<AlderChartSlice> BrowserUsage =>
    [
        new("Chrome", 62, BrushResourceKey: "AlderChartSeries4"),
        new("Safari", 19, BrushResourceKey: "AlderChartSeries1"),
        new("Firefox", 10, BrushResourceKey: "AlderChartSeries3"),
        new("Edge", 9, BrushResourceKey: "AlderChartSeries2")
    ];

    public IReadOnlyList<WidgetUsageMetric> WidgetUsage =>
    [
        new(Text("ProWidgetTotalRequests"), "33.1K", 35, FeedbackColor.Default),
        new(Text("ProWidgetInputTokens"), "98.2M", 65),
        new(Text("ProWidgetOutputTokens"), "59M", 40),
        new(Text("ProWidgetTotalSpend"), "$149.61", 75)
    ];

    public IReadOnlyList<AlderChartSeries> WidgetRevenueSparkline =>
        [new(Text("ProKpiTotalRevenue"), RisingSparkline, BrushResourceKey: "AlderChartSeries1")];

    public IReadOnlyList<AlderChartSeries> WidgetBounceSparkline =>
        [new(Text("ProKpiBounceRate"), [45.5, 42, 43.4, 38.5, 40.6, 36.4, 35, 33.6, 31.5, 29.4, 30.8, 28],
            BrushResourceKey: "AlderDanger")];

    public IReadOnlyList<AlderChartSeries> WidgetActiveSparkline =>
        [new(Text("ProKpiActiveUsers"), RisingSparkline, BrushResourceKey: "AlderStatusSuccess")];

    public double WidgetActiveUsersDisplayValue =>
        100_000d / (DisplayCulture.TwoLetterISOLanguageName == "zh" ? 10_000 : 1_000);

    private static readonly IReadOnlyList<double?> RisingSparkline = [21, 24.5, 19.6, 29.4, 26.6, 31.5, 35, 33.6, 38.5, 42, 40.6, 45.5];

    public IReadOnlyList<string> Categories => Enumerable.Range(1, 8)
        .Select(day => new DateTime(2025, 10, day).ToString("M", DisplayCulture)).ToArray();

    public IReadOnlyList<AlderChartSeries> Revenue =>
        [new(Text("ProDisplayRevenue"), [42, 51, 44, 68, 64, 72, 63, 87], BrushResourceKey: "AlderChartSeries1")];

    public IReadOnlyList<AlderChartSeries> Traffic =>
    [
        new(Text("ProDisplayOrganic"), [29, 50, 46, 57, 71, 65, 68, 76], BrushResourceKey: "AlderChartSeries1"),
        new(Text("ProDisplayPaid"), [18, 32, 21, 39, 32, 48, 44, 53], BrushResourceKey: "AlderChartSeries2")
    ];

    public IReadOnlyList<string> TokenDates => Enumerable.Range(1, 30)
        .Select(day => new DateTime(2025, 9, day).ToString("MM/dd", DisplayCulture)).ToArray();

    public IReadOnlyList<AlderChartSeries> Tokens =>
    [
        new(Text("ProDisplayInputTokensShort"), [35000, 80000, 130000, 140000, 15000, 50000, 65000, 70000, 60000, 40000, 55000,
            75000, 50000, 45000, 90000, 95000, 110000, 60000, 55000, 70000, 60000, 55000, 65000, 50000, 45000, 55000, 40000,
            35000, 30000, 28000], BrushResourceKey: "AlderChartSeries4"),
        new(Text("ProDisplayOutputTokensShort"), [22000, 35000, 48000, 50000, 5000, 30000, 35000, 40000, 45000, 15000, 28000,
            35000, 30000, 25000, 40000, 45000, 48000, 30000, 25000, 20000, 22000, 18000, 25000, 15000, 18000, 20000, 15000,
            12000, 10000, 8000],
            BrushResourceKey: "AlderChartSeries2")
    ];

    public IReadOnlyList<AlderChartSeries> Missing =>
        [new(Text("ProDisplayResponseTime"), [22, 19, null, 25, 28, null, 23, 20], BrushResourceKey: "AlderChartSeries2")];

    public IReadOnlyList<AlderChartSeries> Connected => [Missing[0] with { ConnectGaps = true }];

    public IReadOnlyList<AlderChartSlice> Breakdown =>
    [
        new(Text("ProDisplayOrganic"), 54, BrushResourceKey: "AlderChartSeries1"),
        new(Text("ProDisplayPaid"), 28, BrushResourceKey: "AlderChartSeries2"),
        new(Text("ProDisplayDirect"), 18, BrushResourceKey: "AlderChartSeries3")
    ];
}
