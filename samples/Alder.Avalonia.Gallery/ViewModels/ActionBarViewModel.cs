using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ActionBarViewModel : GalleryViewModel
{
    public ActionBarSelectionDemo Files { get; } = new(
    [
        new("ActionBarFileProposal", isFile: true),
        new("ActionBarFileBudget", isFile: true),
        new("ActionBarFileBrand", isFile: true),
        new("ActionBarFilePhoto", isFile: true),
        new("ActionBarFileNotes", isFile: true),
        new("ActionBarFileDocs", isFile: true)
    ]);

    public ActionBarSelectionDemo Employees { get; } = new(
    [
        Employee("Amina Cole", "amina.cole", "Sales", "Pending", new(2024, 6, 12)),
        Employee("Celia Ramos", "celia.ramos", "Product", "Active", new(2024, 1, 24)),
        Employee("Daniel Reed", "daniel.reed", "Support", "Active", new(2024, 4, 17)),
        Employee("Emil Berg", "emil.berg", "Engineering", "Active", new(2024, 7, 9)),
        Employee("Iris Chen", "iris.chen", "Design", "Pending", new(2024, 2, 6)),
        Employee("Mira Desai", "mira.desai", "Product", "Active", new(2024, 3, 22)),
        Employee("Noor Wallace", "noor.wallace", "Design", "Active", new(2024, 8, 13)),
        Employee("Ren Ito", "ren.ito", "Support", "Inactive", new(2024, 5, 3))
    ]);

    private static ActionBarRow Employee(string name, string account, string department, string status, DateOnly joined) =>
        new(name) { Email = account + "@alder.example", DepartmentKey = "ActionBar" + department,
            StatusKey = "ActionBar" + status, JoinedDate = joined };
}

public sealed class ActionBarSelectionDemo : GalleryViewModel
{
    private readonly ActionBarRow[] initialRows;
    private IReadOnlyCollection<object> initialSelection = [];
    private IReadOnlyCollection<object> selectedKeys = [];
    private GalleryMessage? feedback;
    private bool applyingSelection;

    public ActionBarSelectionDemo(IEnumerable<ActionBarRow> rows)
    {
        initialRows = rows.ToArray();
        Rows = new(initialRows);
        foreach (var row in Rows) row.PropertyChanged += RowSelectionChanged;
        Clear = ReactiveCommand.Create(() => { SelectedKeys = []; });
        Edit = ReactiveCommand.Create(() => Apply("ActionBarEditFeedback", remove: false));
        Export = ReactiveCommand.Create(() => Apply("ActionBarExportFeedback", remove: false));
        Archive = ReactiveCommand.Create(() => Apply("ActionBarArchiveFeedback", remove: true));
        Delete = ReactiveCommand.Create(() => Apply("ActionBarDeleteFeedback", remove: true));
        Reset = ReactiveCommand.Create(RestoreInitialState);
    }

    public ObservableCollection<ActionBarRow> Rows { get; }
    public ICommand Clear { get; }
    public ICommand Edit { get; }
    public ICommand Export { get; }
    public ICommand Archive { get; }
    public ICommand Delete { get; }
    public ICommand Reset { get; }

    public IReadOnlyCollection<object> SelectedKeys
    {
        get => selectedKeys;
        set
        {
            var keys = (value ?? []).ToHashSet();
            if (keys.SetEquals(selectedKeys)) return;
            this.RaiseAndSetIfChanged(ref selectedKeys, keys);
            applyingSelection = true;
            try
            {
                foreach (var row in Rows) row.IsSelected = keys.Contains(row);
            }
            finally { applyingSelection = false; }
            this.RaisePropertyChanged(nameof(Count));
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public int Count => SelectedKeys.Count;
    public bool HasSelection => Count > 0;
    public string Feedback => Display(feedback);
    public bool HasFeedback => feedback is not null;

    internal void SetInitialSelection(params ActionBarRow[] rows)
    {
        initialSelection = rows;
        SelectedKeys = initialSelection;
    }

    private void RowSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!applyingSelection && e.PropertyName == nameof(ActionBarRow.IsSelected))
            SelectedKeys = Rows.Where(row => row.IsSelected).Cast<object>().ToArray();
    }

    private void Apply(string message, bool remove)
    {
        if (!HasSelection) return;
        feedback = new GalleryMessage(message, Count);
        if (remove)
        {
            var removed = SelectedKeys.OfType<ActionBarRow>().ToArray();
            SelectedKeys = [];
            foreach (var row in removed)
            {
                row.PropertyChanged -= RowSelectionChanged;
                Rows.Remove(row);
            }
        }
        this.RaisePropertyChanged(nameof(Feedback));
        this.RaisePropertyChanged(nameof(HasFeedback));
    }

    private void RestoreInitialState()
    {
        SelectedKeys = [];
        for (int index = 0; index < initialRows.Length; index++)
        {
            var row = initialRows[index];
            if (index < Rows.Count && Rows[index] == row) continue;
            row.PropertyChanged += RowSelectionChanged;
            Rows.Insert(index, row);
        }
        SelectedKeys = initialSelection;
        feedback = null;
        this.RaisePropertyChanged(nameof(Feedback));
        this.RaisePropertyChanged(nameof(HasFeedback));
    }
}

public sealed class ActionBarRow(string title, bool isFile = false) : GalleryViewModel
{
    public string Name => isFile ? Text(title) : title;
    public string Email { get; init; } = "";
    public string DepartmentKey { get; init; } = "";
    public string StatusKey { get; init; } = "";
    public DateOnly JoinedDate { get; init; }
    public string Department => Text(DepartmentKey);
    public string Status => Text(StatusKey);
    public AlderStatusColor StatusColor => StatusKey switch
    {
        "ActionBarActive" => AlderStatusColor.Success,
        "ActionBarPending" => AlderStatusColor.Warning,
        _ => AlderStatusColor.Default
    };

    public string Joined => JoinedDate.ToString("d", DisplayCulture);
    public string Initials => isFile ? "" : string.Concat(title.Split(' ').Select(word => word[0]));
    public bool IsSelected { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
}
