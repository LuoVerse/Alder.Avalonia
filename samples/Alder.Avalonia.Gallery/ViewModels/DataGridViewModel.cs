using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record DataGridPriority(string Text, AlderStatusColor Color)
{
    public override string ToString() => Text;
}

public sealed class DataGridDemoRow(int id, string name) : ReactiveObject
{
    public int Id { get; } = id;
    public string Name { get; set => this.RaiseAndSetIfChanged(ref field, value); } = name;
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => word[0]));
    public string Code { get; init; } = "";
    public string Reference { get; init; } = "";
    private static readonly string[] RequestIntervals =
        Enumerable.Range(1, 12).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray();
    public IReadOnlyList<string> RequestCategories => RequestIntervals;
    public IReadOnlyList<AlderChartSeries> RequestSeries { get; init; } = [];
    public string Detail { get; init; } = "";
    public string Role { get; init; } = "";
    public AlderStatusColor RoleColor { get; init; }
    public string SecondaryTag { get; init; } = "";
    public string CompanyMark => Name.Length == 0 ? "" : Name[..1];
    public double Capacity { get; init; }
    public FeedbackColor CapacityColor => Capacity < 40 ? FeedbackColor.Accent : FeedbackColor.Warning;
    public string Category { get; init; } = "";
    public string Region { get; init; } = "";
    public string Method { get; init; } = "";
    public string Date { get; init; } = "";
    public string Status { get; init; } = "";
    public AlderStatusColor Color { get; init; }
    public decimal Amount { get; init; }
    public decimal Fee { get; init; }
    public decimal Cost { get; init; }
    public int UnitsSold { get; init; }
    public int Rating { get; init; }
    public string PriceLabel => Amount.ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    public string CostLabel => Cost.ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    public string SalesLabel => UnitsSold.ToString("N0", CultureInfo.CurrentCulture);
    public string RatedStars => new('★', Rating);
    public string UnratedStars => new('★', 4 - Rating);
    public string RatingLabel => $"{Rating} / 4";
    public string AmountLabel => Amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public string FeeLabel => Fee.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public string NetLabel => (Amount - Fee).ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public int Count { get; init; }
    public string CountLabel => Count.ToString("N0", CultureInfo.CurrentCulture);
    public int Priority { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public decimal? Points { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool Enabled { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IReadOnlyList<DataGridPriority> Priorities { get; init; } = [];
    public IReadOnlyList<DataGridDemoRow>? Children { get; init; }
    public bool IsFolder => Children is not null;
    public ICommand? ViewAction { get; init; }
    public ICommand? EditAction { get; init; }
    public ICommand? DeleteAction { get; init; }
    public ICommand? CopyPaymentIdAction { get; init; }
}

public sealed class DataGridViewModel : GalleryViewModel
{
    private readonly record struct Person(string Name, string Job, string Role);
    private static readonly Person[] People =
    [
        new("Lindsay Walton", "Frontend", "Member"), new("Courtney Henry", "Designer", "Admin"),
        new("Tom Cook", "ProductDirector", "Member"), new("Whitney Francis", "Copywriter", "Admin"),
        new("Leonard Krasner", "SeniorDesigner", "Owner"), new("Floyd Miles", "PrincipalDesigner", "Member")
    ];

    private static readonly string[] Customers = ["Emma Wilson", "Isabella Nguyen", "Jackson Lee", "Liam Johnson", "Olivia Martin", "Sofia Davis", "William Kim"];
    private static readonly string[] Companies = ["LVMH", "Disney", "PayPal", "United Airlines", "Apple", "Microsoft", "Amazon", "Instagram", "Salesforce", "Walmart", "Nintendo", "SpaceX", "Google"];
    private static readonly string[] InvoiceCustomers = ["Acme Corp", "Globex", "Initech", "Umbrella Co", "Cyberdyne Systems", "Soylent Corp", "LexCorp", "Pied Piper", "Massive Dynamic"];
    private static readonly double?[][] ServerRequestHistory =
    [
        [80, 75, 77, 65, 71, 72, 76, 84, 79, 90, 96, 92],
        [57, 61, 67, 72, 73, 68, 64, 59, 68, 70, 66, 73],
        [70, 65, 70, 67, 73, 70, 69, 75, 81, 77, 73, 76],
        [52, 48, 54, 60, 58, 64, 68, 64, 67, 72, 77, 81],
        [84, 85, 77, 73, 80, 78, 70, 62, 59, 64, 62, 60],
        [65, 69, 67, 64, 72, 73, 68, 71, 69, 70, 66, 70],
        [76, 74, 82, 80, 70, 65, 62, 70, 74, 83, 76, 80],
        [70, 72, 67, 75, 84, 86, 83, 87, 92, 94, 89, 88]
    ];
    private const int InvoicePageSize = 4;
    private const int TeamPageSize = 10;
    private const int AsyncTotal = 50;
    private int invoicePage = 1;
    private int teamPage = 1;
    private int workerFilter;
    private int statusFilter;
    private int serverFilter;
    private int companyFilter;
    private object? stockFilter = "all";
    private string? teamSearch;
    private string? serverSearch;
    private string? companySearch;
    private string? productSearch;
    private AlderTableSort? teamSort;
    private AlderTableSort? serverSort;
    private AlderTableSort? companySort;
    private IReadOnlyCollection<object>? bulkSelection;
    private IReadOnlyCollection<object>? expandedKeys = [101];
    private bool isLoading;
    private bool showTeamCountry = true;
    private bool showTeamRole = true;
    private bool showServerRegion = true;
    private bool showServerCost = true;
    private readonly DataGridDemoRow[] products;

    private readonly ICommand copyPaymentId;

    public DataGridViewModel()
    {
        copyPaymentId = ReactiveCommand.CreateFromTask<Control>(CopyPaymentIdAsync);
        Reorder = ReactiveCommand.Create<DataGridReorderRequest>(MoveTask);
        LoadMore = ReactiveCommand.CreateFromTask(LoadMoreAsync);
        BulkActivate = ReactiveCommand.Create(() => UpdateEmployees("Active"));
        BulkRemove = ReactiveCommand.Create(() => UpdateEmployees(null));
        ClearSelection = ReactiveCommand.Create(() => BulkSelection = []);
        Employees = new(CreateEmployees());
        ResetBulk = ReactiveCommand.Create(() =>
        {
            BulkSelection = [];
            BulkSort = null;
            Employees.Clear();
            foreach (var row in CreateEmployees()) Employees.Add(row);
        });
        ResetTeam = ReactiveCommand.Create(() =>
        {
            TeamSelection = [];
            TeamSearch = null;
            WorkerFilter = 0;
            StatusFilter = 0;
            TeamPage = 1;
            TeamSort = null;
            ShowTeamCountry = true;
            ShowTeamRole = true;
        });
        ResetCompanies = ReactiveCommand.Create(() =>
        {
            CompanySelection = [];
            CompanyFilter = 0;
            CompanySearch = null;
            CompanySort = null;
        });
        ResetServers = ReactiveCommand.Create(() =>
        {
            ServerSelection = [];
            ServerFilter = 0;
            ServerSearch = null;
            ServerSort = null;
            ShowServerRegion = true;
            ShowServerCost = true;
        });
        ResetTree = ReactiveCommand.Create(() => { TreeSort = null; ExpandedKeys = [101]; });
        ResetProducts = ReactiveCommand.Create(() =>
        {
            ProductSelection = [];
            ProductSort = null;
            StockFilter = "all";
            ProductSearch = null;
        });
        ResetFeatures = ReactiveCommand.Create(RestoreFeatures);
        ResetReorder = ReactiveCommand.Create(RestoreTaskOrder);
        AddUser = Notice("AddUser");
        AddMember = Notice("AddMember");
        CreateProject = Notice("CreateProject");
        SortTeam = ReactiveCommand.Create(() => TeamSort = Flip(TeamSort, "name"));
        SortServers = ReactiveCommand.Create(() => ServerSort = Flip(ServerSort, "amount"));
        SortCompanies = ReactiveCommand.Create(() => CompanySort = Flip(CompanySort, "name"));
        Tasks = new ObservableCollection<DataGridDemoRow>(new[]
        {
            ("TaskAudit", 0, "Olivia", "InProgress"), ("TaskRate", 0, "Jackson", "ToDo"),
            ("TaskOnboarding", 1, "Isabella", "InProgress"), ("TaskMigration", 0, "William", "ToDo"),
            ("TaskTests", 2, "Sofia", "Done"), ("TaskProfiling", 1, "Liam", "ToDo"),
            ("TaskAccessibility", 1, "Emma", "InProgress"), ("TaskPipeline", 2, "Noah", "Done")
        }.Select((task, index) => new DataGridDemoRow(index + 1, L(task.Item1))
        {
            Role = Priorities[task.Item2].Text, RoleColor = Priorities[task.Item2].Color, Detail = task.Item3, Status = L(task.Item4),
            Color = task.Item4 == "Done" ? AlderStatusColor.Success : task.Item4 == "ToDo" ? AlderStatusColor.Default : AlderStatusColor.Warning
        }));
        Features = CreateFeatures().ToArray();
        foreach (var row in Features) row.PropertyChanged += (_, _) => this.RaisePropertyChanged(nameof(FeatureSummary));
        AsyncInvoices = new ObservableCollection<DataGridDemoRow>(Enumerable.Range(0, 8).Select(index => Invoice(index, true)));
        products = Enumerable.Range(0, 1000).Select(index => new DataGridDemoRow(index, F("ProductName", L(new[] { "Monitor", "Keyboard", "Headphones", "Camera", "Notebook", "Lamp" }[index % 6]), index + 1))
        {
            Code = $"SKU-{8400 + index}", Role = L(index % 2 == 0 ? "Electronics" : "Accessories"),
            Category = index % 5 == 0 ? "out" : index % 7 == 0 ? "low" : "in",
            Status = L(index % 5 == 0 ? "OutOfStock" : index % 7 == 0 ? "LowStock" : "InStock"),
            Color = index % 5 == 0 ? AlderStatusColor.Danger : index % 7 == 0 ? AlderStatusColor.Warning : AlderStatusColor.Success,
            Count = index % 5 == 0 ? 0 : index % 7 == 0 ? index % 9 : 16 + index * 7 % 630,
            Amount = 42 + index * 43.67m % 1900, Cost = 29 + index * 19.49m % 920,
            UnitsSold = index * 17 % 829, Rating = 2 + index % 3
        }).ToArray();
    }

    public Func<object, object> RowKey { get; } = row => ((DataGridDemoRow)row).Id;
    public Func<object, IEnumerable?> RowChildren { get; } = row => ((DataGridDemoRow)row).Children;

    public Comparison<object> CompareName { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "name");
    public Comparison<object> CompareCode { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "code");
    public Comparison<object> CompareAmount { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "amount");
    public Comparison<object> CompareStatus { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "status");
    public Comparison<object> CompareCount { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "count");
    public DataGridPriority[] Priorities =>
    [
        new(L("High"), AlderStatusColor.Danger),
        new(L("Medium"), AlderStatusColor.Warning),
        new(L("Low"), AlderStatusColor.Default)
    ];
    public string[] WorkerOptions => [L("WorkerType"), L("Employee"), L("Contractor")];
    public string[] StatusOptions => [L("Status"), L("Active"), L("Inactive")];
    public Comparison<object> CompareCost { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "cost");
    public Comparison<object> CompareSales { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "sales");
    public Comparison<object> CompareRating { get; } = (first, second) => Compare((DataGridDemoRow)first, (DataGridDemoRow)second, "rating");
    public string[] CompanyOptions => [L("AllCompanies"), L("Ecommerce"), L("Finance")];

    public DataGridDemoRow[] Users => People.Select((person, index) => new DataGridDemoRow(index, person.Name)
    {
        Detail = Email(person.Name), Role = L(person.Job), Status = L(person.Role), EditAction = Notice("Edit", person.Name)
    }).ToArray();

    public DataGridDemoRow[] Payments => Customers.Select((customer, index) =>
    {
        decimal amount = new decimal[] { 2450, 299, 59, 150, 1999, 450, 99 }[index];
        string status = index == 2 ? "Pending" : index == 3 ? "Refunded" : index == 6 ? "Failed" : "Succeeded";
        return new DataGridDemoRow(index, customer)
        {
            Detail = Email(customer), Code = "pay_1N3x" + new[] { "DR", "9M", "Q5", "12", "E9", "J7", "F4" }[index],
            Amount = amount, Fee = decimal.Round(amount * .03m, 2), Status = L(status), Color = StatusColor(status),
            Date = Date(new DateOnly(2025, 12, 7).AddDays(-4 * index)), Region = L(index % 2 == 0 ? "NorthAmerica" : "AsiaPacific"),
            Method = new[] { "Visa •••• 4242", "Visa •••• 1234", "Mastercard •••• 8888", "Mastercard •••• 9798", "Amex •••• 0018", "Visa •••• 7777", "Amex •••• 4219" }[index],
            Role = L(index % 2 == 0 ? "AnnualPlan" : "QuarterlyPlan"),
            CopyPaymentIdAction = copyPaymentId, ViewAction = Notice("Details", customer),
            EditAction = Notice("EditPayment", customer), DeleteAction = Notice("Delete", customer)
        };
    }).ToArray();

    public string? TeamSearch { get => teamSearch; set => SetTeamFilter(ref teamSearch, value); }
    public int WorkerFilter { get => workerFilter; set => SetTeamFilter(ref workerFilter, value); }
    public int StatusFilter { get => statusFilter; set => SetTeamFilter(ref statusFilter, value); }
    public int TeamPage { get => teamPage; set => Set(ref teamPage, value, [nameof(TeamRows), nameof(TeamSummary)]); }
    public AlderTableSort? TeamSort { get => teamSort; set => SetTeamFilter(ref teamSort, value); }
    public bool ShowTeamCountry { get => showTeamCountry; set => this.RaiseAndSetIfChanged(ref showTeamCountry, value); }
    public bool ShowTeamRole { get => showTeamRole; set => this.RaiseAndSetIfChanged(ref showTeamRole, value); }
    public int TeamPageCount => Math.Max(1, (TeamMatches.Count() + TeamPageSize - 1) / TeamPageSize);
    public string TeamCount => TeamMatches.Count().ToString(DisplayCulture);
    public string TeamSummary => PageSummary(TeamPage, TeamPageSize, TeamMatches.Count());
    public DataGridDemoRow[] TeamRows => Order(TeamMatches.Select(TeamMember), TeamSort).Skip((TeamPage - 1) * TeamPageSize).Take(TeamPageSize).ToArray();

    public bool ShowServerRegion { get => showServerRegion; set => this.RaiseAndSetIfChanged(ref showServerRegion, value); }
    public bool ShowServerCost { get => showServerCost; set => this.RaiseAndSetIfChanged(ref showServerCost, value); }
    public int ServerFilter { get => serverFilter; set => Set(ref serverFilter, value, [nameof(Servers), nameof(ServerCount)]); }
    public string? ServerSearch { get => serverSearch; set => Set(ref serverSearch, value, [nameof(Servers), nameof(ServerCount)]); }
    public AlderTableSort? ServerSort { get => serverSort; set => Set(ref serverSort, value, [nameof(Servers)]); }
    public int ServerCount => Servers.Length;
    public DataGridDemoRow[] Servers => Order(Enumerable.Range(0, 8)
        .Where(index => ServerFilter <= 0 || (index % 4 < 2 ? 1 : 2) == ServerFilter)
        .Select(Server).Where(row => Matches(row, ServerSearch)), ServerSort).ToArray();

    public int CompanyFilter { get => companyFilter; set => Set(ref companyFilter, value, [nameof(CompanyRows), nameof(CompanySummary)]); }
    public string? CompanySearch { get => companySearch; set => Set(ref companySearch, value, [nameof(CompanyRows), nameof(CompanySummary)]); }
    public AlderTableSort? CompanySort { get => companySort; set => Set(ref companySort, value, [nameof(CompanyRows), nameof(CompanySummary)]); }
    public string CompanySummary => F("ResultCount", CompanyRows.Length);
    public DataGridDemoRow[] CompanyRows => Order(Companies.Select(Company)
        .Where(row => (CompanyFilter <= 0 || row.Role == CompanyOptions[CompanyFilter]) && Matches(row, CompanySearch)), CompanySort).ToArray();

    public int InvoicePage { get => invoicePage; set => Set(ref invoicePage, value, [nameof(InvoiceRows), nameof(InvoiceSummary)]); }
    public DataGridDemoRow[] InvoiceRows => Enumerable.Range((InvoicePage - 1) * InvoicePageSize, InvoicePageSize).Select(index => Invoice(index, false)).ToArray();
    public string InvoiceSummary => PageSummary(InvoicePage, InvoicePageSize, 12);
    public ObservableCollection<DataGridDemoRow> AsyncInvoices { get; }
    public bool HasMoreInvoices => AsyncInvoices.Count < AsyncTotal;
    public string AsyncSummary => $"{AsyncInvoices.Count} / {AsyncTotal}";
    public bool IsLoading { get => isLoading; private set => this.RaiseAndSetIfChanged(ref isLoading, value); }

    public DataGridDemoRow[] TreeRows =>
    [
        File(101, "Documents", "3", new DateOnly(2025, 10, 20),
        [
            File(102, "ProjectAlpha", "2", new DateOnly(2025, 8, 2),
            [
                File(103, "README.md", "4 KB", new DateOnly(2025, 8, 1)),
                File(104, "budget.csv", "22 KB", new DateOnly(2025, 8, 2))
            ]),
            File(105, "Meeting Notes.md", "12 KB", new DateOnly(2025, 9, 14))
        ]),
        File(106, "Photos", "2", new DateOnly(2026, 2, 3),
        [
            File(107, "IMG_001.png", "228 KB", new DateOnly(2026, 2, 3)),
            File(108, "IMG_002.png", "356 KB", new DateOnly(2026, 2, 3))
        ]),
        File(109, "readme.txt", "4 KB", new DateOnly(2026, 3, 1))
    ];
    public IReadOnlyCollection<object>? ExpandedKeys { get => expandedKeys; set => this.RaiseAndSetIfChanged(ref expandedKeys, value); }
    public ObservableCollection<DataGridDemoRow> Tasks { get; }
    public DataGridDemoRow[] Features { get; }
    public string FeatureSummary => F("FeatureSummary", Features.Count(row => row.Enabled), Features.Length, Features.Sum(row => row.Points ?? 0));
    public ObservableCollection<DataGridDemoRow> Employees { get; }
    public IReadOnlyCollection<object>? BulkSelection { get => bulkSelection; set => Set(ref bulkSelection, value, [nameof(HasBulkSelection), nameof(BulkSummary)]); }
    public bool HasBulkSelection => BulkSelection is { Count: > 0 };
    public string BulkSummary => F("SelectedCount", BulkSelection?.Count ?? 0);
    public DataGridDemoRow[] EmptyProjects { get; } = [];

    public object? StockFilter { get => stockFilter; set => Set(ref stockFilter, value, [nameof(ProductRows), nameof(ProductSummary)]); }
    public string? ProductSearch { get => productSearch; set => Set(ref productSearch, value, [nameof(ProductRows), nameof(ProductSummary)]); }
    public DataGridDemoRow[] ProductRows => products.Where(row =>
        (StockFilter is null or "all" || Equals(row.Category, StockFilter)) && Matches(row, ProductSearch)).ToArray();
    public string ProductSummary
    {
        get
        {
            var rows = ProductRows;
            return F("ProductSummary", rows.Length, rows.Sum(row => (row.Amount - row.Cost) * row.UnitsSold), rows.Sum(row => row.UnitsSold));
        }
    }

    public ICommand AddUser { get; }
    public ICommand AddMember { get; }
    public ICommand CreateProject { get; }
    public ICommand SortTeam { get; }
    public ICommand SortServers { get; }
    public ICommand SortCompanies { get; }
    public ICommand Reorder { get; }
    public ICommand LoadMore { get; }
    public ICommand BulkActivate { get; }
    public ICommand BulkRemove { get; }
    public ICommand ClearSelection { get; }
    public ICommand ResetBulk { get; }
    public ICommand ResetTeam { get; }
    public ICommand ResetCompanies { get; }
    public ICommand ResetServers { get; }
    public ICommand ResetTree { get; }
    public ICommand ResetProducts { get; }
    public ICommand ResetFeatures { get; }
    public ICommand ResetReorder { get; }

    public IReadOnlyCollection<object>? TeamSelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IReadOnlyCollection<object>? ServerSelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IReadOnlyCollection<object>? CompanySelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IReadOnlyCollection<object>? ProductSelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public AlderTableSort? TreeSort { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public AlderTableSort? ProductSort { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public AlderTableSort? BulkSort { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    private IEnumerable<int> TeamMatches => Enumerable.Range(0, 100).Where(index =>
        (WorkerFilter <= 0 || (index % 3 == 0 ? 2 : 1) == WorkerFilter) &&
        (StatusFilter <= 0 || (index % 5 == 0 ? 2 : 1) == StatusFilter) &&
        (string.IsNullOrWhiteSpace(TeamSearch) || TeamName(index).Contains(TeamSearch.Trim(), StringComparison.CurrentCultureIgnoreCase) ||
         $"WRK-{7293027 + index}".Contains(TeamSearch.Trim(), StringComparison.OrdinalIgnoreCase)));

    private DataGridDemoRow TeamMember(int index)
    {
        var name = TeamName(index);
        string status = index % 5 == 0 ? "Inactive" : "Active";
        return new(index, name)
        {
            Code = $"WRK-{7293027 + index}", Reference = $"EXT-{(0x65F00000L + index * 70113):X8}", Detail = Email(name),
            Region = L(new[] { "Canada", "SouthKorea", "UK", "US", "Brazil", "Japan", "China" }[index % 7]),
            Role = L(index % 2 == 0 ? "Engineer" : "Designer"), Category = L(index % 3 == 0 ? "Contractor" : "Employee"),
            Method = L(index % 2 == 0 ? "Product" : "Engineering"), Date = Date(new DateOnly(2025, 9, 16).AddDays(-index * 3)),
            Status = L(status), Color = StatusColor(status), ViewAction = Notice("View", name),
            EditAction = Notice("Edit", name), DeleteAction = Notice("Delete", name)
        };
    }

    private static string TeamName(int index)
    {
        string[] first = ["Aaron", "Alex", "Alice", "Amara", "Anika", "Ben", "Daniel", "Ella", "James", "Maya"];
        string[] last = ["Carter", "Johnson", "Adams", "Okafor", "Cho", "Brown", "Wong", "Kim", "Singh", "Park"];
        return $"{first[index % first.Length]} {last[(index % first.Length + index / first.Length) % last.Length]}";
    }

    private DataGridDemoRow Server(int index)
    {
        string status = index % 4 < 2 ? "Active" : "Inactive";
        string code = $"#{4586930 + index}";
        double capacity = new[] { 20.01, 7.36, 48.25, 21.32, 73.12, 72.85, 15.18, 11.39 }[index];
        return new(index, code)
        {
            Code = code, Count = new[] { 25, 13, 27, 28, 6, 9, 28, 27 }[index],
            Status = L(status), Color = StatusColor(status), Region = new[] { "US-West 1", "US-East 2", "EU-Central 1", "AP-South 1", "US-West 2", "EU-West 1", "CA-Central 1", "AP-Northeast 1" }[index],
            Capacity = capacity, Detail = $"{capacity.ToString("N2", DisplayCulture)}%",
            Amount = new decimal[] { 830.04m, 752.72m, 517.60m, 544.83m, 412.27m, 350.53m, 775.45m, 198.05m }[index],
            RequestSeries = [new AlderChartSeries(L("Requests"), ServerRequestHistory[index], BrushResourceKey: "AlderAccent")]
        };
    }

    private DataGridDemoRow Company(string name, int index)
    {
        string category = index % 3 == 0 ? "Ecommerce" : index % 3 == 1 ? "Media" : "Finance";
        return new(index, name)
        {
            Role = L(category), RoleColor = category == "Ecommerce" ? AlderStatusColor.Success : category == "Finance" ? AlderStatusColor.Warning : AlderStatusColor.Accent,
            SecondaryTag = index % 2 == 0 ? L("B2C") : L("B2B"), Detail = name.ToLowerInvariant().Replace(' ', '-'),
            Date = L("NoContact"), Status = L("NoCommunication"), Count = 198135 + index * 124652,
            Reference = "@" + name.Replace(" ", "", StringComparison.Ordinal)
        };
    }

    private DataGridDemoRow File(int id, string name, string size, DateOnly date, IReadOnlyList<DataGridDemoRow>? children = null) =>
        new(id, children is null ? name : L(name))
        {
            Role = L(children is null ? "TextFile" : "Folder"), Color = children is null ? AlderStatusColor.Default : AlderStatusColor.Warning,
            Status = L(children is null ? "TextFile" : "Folder"), Detail = children is null ? size : F("Items", size),
            Date = Date(date), Children = children
        };

    private DataGridDemoRow Invoice(int index, bool asynchronous)
    {
        string status = index % 5 == (asynchronous ? 0 : 3) ? "Overdue" : index % 5 == (asynchronous ? 3 : 2) ? "Pending" : "Paid";
        return new(index, InvoiceCustomers[(index + (asynchronous ? 4 : 0)) % InvoiceCustomers.Length])
        {
            Code = $"INV-{index + (asynchronous ? 1000 : 1):D3}", Amount = asynchronous ? 389 + (9359 + index * 2264) % 9562 : new[] { 1250, 890, 2340, 560, 750, 1625 }[index % 6],
            Date = asynchronous ? Date(new DateOnly(2025, 1, 6).AddDays(index * 25)) : new DateOnly(2024, 3, 1).AddDays(index * 3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Status = L(status), Color = StatusColor(status)
        };
    }

    private DataGridDemoRow Employee(int index, string name, string role, string status) => new(index, name)
    {
        Detail = Email(name, "company.com"), Role = role.StartsWith('@') ? role[1..] : L(role), Status = L(status),
        Color = StatusColor(status), Date = Date(new DateOnly(2024, 6, 27).AddDays(-19 * index))
    };

    private IEnumerable<DataGridDemoRow> CreateEmployees() => new[]
    {
        ("Amara Okafor", "Support"), ("Elena Rodriguez", "HR"), ("James O'Brien", "Finance"), ("Luca Bianchi", "Engineering"),
        ("Marcus Chen", "Design"), ("Priya Patel", "HR"), ("Yuki Tanaka", "Product"), ("Matteo Rossi", "Engineering")
    }.Select((person, index) => Employee(index, person.Item1, person.Item2, index % 4 == 0 ? "Pending" : index == 6 ? "Inactive" : "Active"));

    private IEnumerable<DataGridDemoRow> CreateFeatures()
    {
        (string Name, int Priority, int Points, bool Enabled)[] initialFeatures =
        [
            ("DarkMode", 0, 8, true), ("CsvExport", 1, 3, true), ("KeyboardShortcuts", 2, 5, false),
            ("TwoFactor", 0, 13, true), ("BulkImport", 1, 8, false), ("AuditLog", 0, 5, true),
            ("Webhooks", 2, 3, false), ("CustomBranding", 1, 8, true)
        ];
        var priorities = Priorities;
        return initialFeatures.Select((item, index) => new DataGridDemoRow(index, L(item.Name))
        {
            Priorities = priorities, Priority = item.Priority, Points = item.Points, Enabled = item.Enabled
        });
    }

    private void RestoreFeatures()
    {
        foreach (var seed in CreateFeatures())
        {
            var row = Features[seed.Id];
            row.Name = seed.Name;
            row.Priority = seed.Priority;
            row.Points = seed.Points;
            row.Enabled = seed.Enabled;
        }
    }

    private void RestoreTaskOrder()
    {
        var initialOrder = Tasks.OrderBy(row => row.Id).ToArray();
        for (int index = 0; index < initialOrder.Length; index++)
        {
            int current = Tasks.IndexOf(initialOrder[index]);
            if (current != index) Tasks.Move(current, index);
        }
    }

    private void UpdateEmployees(string? status)
    {
        var selected = BulkSelection?.ToHashSet() ?? [];
        for (int i = Employees.Count - 1; i >= 0; i--)
        {
            var row = Employees[i];
            if (!selected.Contains(row.Id)) continue;
            if (status is null) Employees.RemoveAt(i);
            else Employees[i] = Employee(row.Id, row.Name, "@" + row.Role, status);
        }
        BulkSelection = [];
    }

    private void MoveTask(DataGridReorderRequest request)
    {
        int source = Tasks.ToList().FindIndex(row => Equals(row.Id, request.RowKey));
        int target = Tasks.ToList().FindIndex(row => Equals(row.Id, request.TargetKey));
        if (source < 0 || target < 0 || source == target) return;
        int destination = target + (request.Position == DataGridDropPosition.After ? 1 : 0);
        if (source < destination) destination--;
        Tasks.Move(source, destination);
    }

    private async Task LoadMoreAsync()
    {
        if (IsLoading || !HasMoreInvoices) return;
        IsLoading = true;
        try
        {
            await Task.Delay(350);
            int end = Math.Min(AsyncInvoices.Count + 8, AsyncTotal);
            for (int index = AsyncInvoices.Count; index < end; index++) AsyncInvoices.Add(Invoice(index, true));
            this.RaisePropertyChanged(nameof(HasMoreInvoices));
            this.RaisePropertyChanged(nameof(AsyncSummary));
        }
        finally { IsLoading = false; }
    }

    private static IEnumerable<DataGridDemoRow> Order(IEnumerable<DataGridDemoRow> rows, AlderTableSort? sort)
    {
        if (sort is null) return rows;
        var comparison = Comparer<DataGridDemoRow>.Create((first, second) => Compare(first, second, sort.ColumnKey));
        return sort.Direction == TableSortDirection.Ascending ? rows.Order(comparison) : rows.OrderDescending(comparison);
    }

    private static int Compare(DataGridDemoRow first, DataGridDemoRow second, string column) => column switch
    {
        "code" => StringComparer.OrdinalIgnoreCase.Compare(first.Code, second.Code),
        "amount" => first.Amount.CompareTo(second.Amount),
        "cost" => first.Cost.CompareTo(second.Cost),
        "sales" => first.UnitsSold.CompareTo(second.UnitsSold),
        "rating" => first.Rating.CompareTo(second.Rating),
        "count" => first.Count.CompareTo(second.Count),
        "status" => StringComparer.CurrentCultureIgnoreCase.Compare(first.Status, second.Status),
        _ => StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name)
    };

    private static AlderTableSort Flip(AlderTableSort? sort, string key) =>
        new(key, sort?.ColumnKey == key && sort.Direction == TableSortDirection.Ascending ? TableSortDirection.Descending : TableSortDirection.Ascending);
    private static bool Matches(DataGridDemoRow row, string? search) =>
        string.IsNullOrWhiteSpace(search) || row.Name.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) ||
        row.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Email(string name, string domain = "example.com") => $"{name.ToLowerInvariant().Replace("'", "", StringComparison.Ordinal).Replace(' ', '.')}@{domain}";

    private static AlderStatusColor StatusColor(string status) => status switch
    {
        "Succeeded" or "Active" or "Paid" => AlderStatusColor.Success,
        "Pending" => AlderStatusColor.Warning,
        "Overdue" or "Failed" => AlderStatusColor.Danger,
        _ => AlderStatusColor.Default
    };

    private async Task CopyPaymentIdAsync(Control source)
    {
        if (source.DataContext is not DataGridDemoRow row) return;
        if (TopLevel.GetTopLevel(source)?.Clipboard is { } clipboard)
        {
            try
            {
                await clipboard.SetTextAsync(row.Code);
                return;
            }
            catch (Exception error) when (error is InvalidOperationException or ExternalException)
            {
                // Report clipboard access errors through the same feedback used by the Table example.
            }
        }
        Toast.Show(new ToastOptions(Text("ClipboardIsUnavailable")) { Variant = ToastVariant.Warning });
    }

    private ICommand Notice(string verb, string? subject = null) => ReactiveCommand.Create(() =>
    {
        Toast.Show(new ToastOptions(subject is null ? L(verb) : F("ActionSummary", L(verb), subject)));
    });

    private string Date(DateOnly date) => date.ToString("MMM d, yyyy", DisplayCulture);
    private string PageSummary(int page, int size, int total) => F("PageSummary", total == 0 ? 0 : (page - 1) * size + 1, Math.Min(page * size, total), total);
    private string L(string key) => Text("DataGrid" + key);
    private string F(string key, params object?[] args) => Format("DataGrid" + key, args);

    private void SetTeamFilter<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        this.RaiseAndSetIfChanged(ref storage, value, property);
        TeamPage = 1;
        foreach (string dependent in new[] { nameof(TeamRows), nameof(TeamPageCount), nameof(TeamCount), nameof(TeamSummary) })
            this.RaisePropertyChanged(dependent);
    }

    private void Set<T>(ref T storage, T value, string[] dependents, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        this.RaiseAndSetIfChanged(ref storage, value, property);
        foreach (string dependent in dependents) this.RaisePropertyChanged(dependent);
    }
}
