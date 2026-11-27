using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ExpandableActionBarViewModel : GalleryViewModel
{
    private static readonly string[] Actions =
        ["ExpandableActionSend", "Copy", "ActionBarExport", "ActionBarArchive", "ExpandableActionAlerts", "ExpandableActionSettings"];

    public ExpandableActionBarViewModel() =>
        Toggle = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });

    public bool IsExpanded
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ToggleLabel));
        }
    }

    public int SelectedIndex
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Feedback));
        }
    }

    public ICommand Toggle { get; }
    public string ToggleLabel => Text(IsExpanded ? "ExpandableActionCollapse" : "ExpandableActionExpand");
    public string? Feedback => SelectedIndex >= 0 && SelectedIndex < Actions.Length
        ? Format("SelectedAction", Text(Actions[SelectedIndex])) : null;
}
