using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class FormSelectViewModel : GalleryViewModel
{
    private static readonly string[] StatusKeys = ["FormTodo", "FormInProgress", "FormDone", "FormCancelled"];

    public IReadOnlyList<AlderPickerOption> Statuses => Options(StatusKeys);
    public IReadOnlyList<AlderPickerOption> Priorities => Options("FormLow", "FormMedium", "FormHigh", "FormCritical");
    public IReadOnlyList<AlderPickerOption> States => Options("PickerCalifornia", "PickerTexas", "PickerFlorida", "PickerNewYork", "PickerIllinois");
    public IReadOnlyList<AlderPickerOption> Countries => Options("FormUnitedStates", "FormCanada", "FormMexico", "FormJapan");
    public IReadOnlyList<AlderPickerOption> Roles => Options("FormAdministrator", "ProDisplayEditor", "ProDisplayViewer");

    public IReadOnlyList<AlderPickerOption> Departments =>
    [
        new() { Text = Text("FormFrontend"), Group = Text("FormEngineering") },
        new() { Text = Text("FormBackend"), Group = Text("FormEngineering") },
        new() { Text = Text("FormDevOps"), Group = Text("FormEngineering") },
        new() { Text = Text("FormSalesRep"), Group = Text("FormSales") },
        new() { Text = Text("FormAccountManager"), Group = Text("FormSales") },
        new() { Text = Text("FormCustomerSupport"), Group = Text("FormOperations") }
    ];

    public IReadOnlyList<AlderPickerOption> Animals =>
    [
        new() { Text = Text("FormDog") }, new() { Text = Text("FormCat"), IsEnabled = false },
        new() { Text = Text("FormBird") }, new() { Text = Text("FormKangaroo"), IsEnabled = false },
        new() { Text = Text("FormElephant") }
    ];

    public int StateIndex
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(StateSummary));
        }
    }

    public string StateSummary => StateIndex >= 0 && StateIndex < States.Count
        ? Format("CellSelectedResult", States[StateIndex].Text) : string.Empty;

    private IReadOnlyList<AlderPickerOption> Options(params string[] labels) =>
        labels.Select(label => new AlderPickerOption { Text = Text(label) }).ToArray();
}
