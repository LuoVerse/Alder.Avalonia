using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record TableExampleRow(int Id, string Name, string Role, string Status, string Email,
    AlderStatusColor StatusColor = AlderStatusColor.Default, IReadOnlyList<TableExampleRow>? Children = null)
{
    public Bitmap? Avatar { get; init; }
    public string WorkerId => $"#{Id}";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0]));
}

public sealed class TableViewModel : GalleryViewModel
{
    private static readonly (string Name, string Role, string Email)[] People =
    [
        ("Kate Moore", "CEO", "kate@acme.com"),
        ("John Smith", "CTO", "john@acme.com"),
        ("Sara Johnson", "CMO", "sara@acme.com"),
        ("Michael Brown", "CFO", "michael@acme.com"),
        ("Emily Davis", "Product Manager", "emily@acme.com"),
        ("Davis Wilson", "Lead Designer", "davis@acme.com"),
        ("Alex Kim", "Engineering", "alex@acme.com"),
        ("Riley Hall", "Operations", "riley@acme.com"),
        ("Taylor Lee", "Engineering", "taylor@acme.com"),
        ("Jordan Reed", "Design", "jordan@acme.com"),
        ("Morgan Lane", "Support", "morgan@acme.com"),
        ("Sam Rivera", "Operations", "sam@acme.com")
    ];

    public Func<object, object> RowKey { get; } = row => ((TableExampleRow)row).Id;
    public Func<object, IEnumerable?> RowChildren { get; } = row => ((TableExampleRow)row).Children;
    public Comparison<object> CompareName { get; } = (a, b) => Compare(a, b, row => row.Name);
    public Comparison<object> CompareRole { get; } = (a, b) => Compare(a, b, row => row.Role);
    public Comparison<object> CompareStatus { get; } = (a, b) => Compare(a, b, row => row.Status);
    public Comparison<object> CompareEmail { get; } = (a, b) => Compare(a, b, row => row.Email);
    public Comparison<object> CompareId { get; } = (a, b) => ((TableExampleRow)a).Id.CompareTo(((TableExampleRow)b).Id);
    public IReadOnlyList<TableExampleRow> BasicRows => Enumerable.Range(0, 4).Select(Person).ToArray();
    public IReadOnlyList<TableExampleRow> AllRows => Enumerable.Range(0, People.Length).Select(Person).ToArray();
    public IReadOnlyList<TableExampleRow> WideRows => AllRows.Take(5).ToArray();
    public IReadOnlyList<TableExampleRow> CustomRows => Enumerable.Range(0, 3).Select(Person).ToArray();
    public IReadOnlyList<TableExampleRow> EmptyRows { get; } = [];
    public IReadOnlyList<TableExampleRow> VirtualRows => Enumerable.Range(1, 10_000)
        .Select(index => new TableExampleRow(index, Format("TableRecord", index), Text("TableEngineer"),
            Text("StatusActive"), $"person{index}@example.com")).ToArray();

    public IReadOnlyList<TableExampleRow> TreeRows =>
    [
        FileRow(101, "TableDocuments", 2025, 10, 20,
        [
            FileRow(102, "TableProject", 2025, 8, 2,
            [
                FileRow(103, "TableWeeklyReport", 2025, 7, 10),
                FileRow(104, "TableBudget", 2025, 8, 20)
            ])
        ]),
        FileRow(105, "TablePhotos", 2026, 2, 3,
        [
            FileRow(106, "TableImageOne", 2026, 1, 23),
            FileRow(107, "TableImageTwo", 2026, 2, 3)
        ])
    ];

    private TableExampleRow FileRow(int id, string key, int year, int month, int day,
        IReadOnlyList<TableExampleRow>? children = null) =>
        new(id, Text(key), Text(children is null ? "TableFile" : "TableDirectory"),
            new DateOnly(year, month, day).ToString("d", DisplayCulture), "", Children: children);

    private static readonly (int Id, string Role, string Avatar)[] WorkerDetails =
    [
        (4586932, "TableRoleCeo", "Card/red.jpg"),
        (5273849, "TableRoleCto", "Avatar/green.jpg"),
        (7492836, "TableRoleCmo", "Card/blue.jpg"),
        (8293746, "TableRoleCfo", "Avatar/purple.jpg"),
        (1234567, "TableProductManager", "Avatar/orange.jpg")
    ];

    private readonly Lazy<Bitmap[]> workerAvatars = new(() => WorkerDetails.Select(worker =>
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/{worker.Avatar}"));
        return Bitmap.DecodeToWidth(stream, 64);
    }).ToArray());

    public IReadOnlyList<TableExampleRow> WorkerRows => WorkerDetails.Select((worker, index) =>
    {
        var person = Person(index);
        return person with
        {
            Id = worker.Id,
            Role = Text(worker.Role),
            Status = index == 4 ? Text("StatusInactive") : person.Status,
            StatusColor = index == 4 ? AlderStatusColor.Danger : person.StatusColor,
            Avatar = workerAvatars.Value[index]
        };
    }).ToArray();
    public AlderTableSort InitialWorkerSort { get; } = new("name", TableSortDirection.Ascending);

    public string SelectionSummary => Format("TableSelectedSummary", SelectedKeys?.Count switch
    {
        4 => Text("TableSelectionAll"),
        null or 0 => Text("FeedbackNone"),
        _ => string.Join(", ", SelectedKeys.OfType<int>().Order().Select(key => key.ToString(DisplayCulture)))
    });

    public IReadOnlyCollection<object>? SelectedKeys
    {
        get => field;
        set
        {
            if (ReferenceEquals(field, value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectionSummary));
        }
    }

    public IReadOnlyCollection<object>? ExpandedKeys
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [101];

    public int Page
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PagedRows));
        }
    } = 1;

    public IReadOnlyList<TableExampleRow> PagedRows => AllRows.Skip((Page - 1) * 4).Take(4).ToArray();

    public AlderTableSort? ControlledSort
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ControlledRows));
        }
    }

    public string? Filter
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ControlledRows));
        }
    }

    public IReadOnlyList<TableExampleRow> ControlledRows
    {
        get
        {
            IEnumerable<TableExampleRow> rows = AllRows;
            if (!string.IsNullOrWhiteSpace(Filter))
                rows = rows.Where(row => row.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase));
            if (ControlledSort is { } sort)
            {
                Func<TableExampleRow, string> key = sort.ColumnKey == "role" ? row => row.Role : row => row.Name;
                rows = sort.Direction == TableSortDirection.Ascending ? rows.OrderBy(key) : rows.OrderByDescending(key);
            }
            return rows.ToArray();
        }
    }

    public ObservableCollection<TableExampleRow> AsyncRows { get; } = [];
    public bool HasMoreRows => AsyncRows.Count < 24;
    public bool IsLoading { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand LoadMore { get; }
    public ICommand CopyWorkerIdCommand { get; }
    public ICommand ResetTree { get; }
    public ICommand ResetControlled { get; }

    public TableViewModel()
    {
        foreach (int index in Enumerable.Range(0, 6)) AsyncRows.Add(Person(index));
        LoadMore = ReactiveCommand.CreateFromTask(LoadMoreAsync);
        CopyWorkerIdCommand = ReactiveCommand.CreateFromTask<Control>(CopyWorkerIdAsync);
        ResetTree = ReactiveCommand.Create(() => { ExpandedKeys = [101]; });
        ResetControlled = ReactiveCommand.Create(() =>
        {
            Filter = null;
            ControlledSort = null;
        });
        PropertyChanged += (_, change) =>
        {
            if (change.PropertyName != string.Empty) return;
            for (int index = 0; index < AsyncRows.Count; index++) AsyncRows[index] = Person(index);
        };
    }

    private async Task CopyWorkerIdAsync(Control source)
    {
        if (source.DataContext is not TableExampleRow row ||
            TopLevel.GetTopLevel(source)?.Clipboard is not { } clipboard) return;

        try
        {
            await clipboard.SetTextAsync(row.WorkerId);
        }
        catch (Exception error) when (error is InvalidOperationException or ExternalException)
        {
            Toast.Show(new ToastOptions(Text("ClipboardIsUnavailable")) { Variant = ToastVariant.Warning });
        }
    }

    private async Task LoadMoreAsync()
    {
        if (IsLoading || !HasMoreRows) return;
        IsLoading = true;
        try
        {
            await Task.Delay(350);
            int first = AsyncRows.Count;
            for (int index = first; index < Math.Min(first + 6, 24); index++) AsyncRows.Add(Person(index));
            this.RaisePropertyChanged(nameof(HasMoreRows));
        }
        finally { IsLoading = false; }
    }

    private TableExampleRow Person(int index)
    {
        var (name, role, email) = People[index % People.Length];
        string localizedRole = role.Length == 3 && role.All(char.IsUpper)
            ? role : Text("Table" + role.Replace(" ", "", StringComparison.Ordinal));
        bool onLeave = index % 5 == 2;
        return new(index + 1, name, localizedRole, Text(onLeave ? "TableOnLeave" : "StatusActive"),
            email, onLeave ? AlderStatusColor.Warning : AlderStatusColor.Success);
    }

    private static int Compare(object first, object second, Func<TableExampleRow, string> selector) =>
        StringComparer.CurrentCultureIgnoreCase.Compare(selector((TableExampleRow)first), selector((TableExampleRow)second));
}
