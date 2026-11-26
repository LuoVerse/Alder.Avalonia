using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TimelineViewModel : GalleryViewModel
{
    private string? incidentDraft;

    public TimelineViewModel()
    {
        Post = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(IncidentDraft))
                Toast.Show(new ToastOptions(IncidentDraft.Trim()));
        });
        OpenReference = ReactiveCommand.Create<string>(key => Toast.Show(new ToastOptions(Text(key))));
        CloseHistory = ReactiveCommand.Create(() => IsHistoryVisible = false);
        ResetHistory = ReactiveCommand.Create(() => IsHistoryVisible = true);
    }

    public ICommand Post { get; }
    public ICommand OpenReference { get; }
    public ICommand CloseHistory { get; }
    public ICommand ResetHistory { get; }

    public string IncidentDraft
    {
        get => incidentDraft ?? Text("ProTimelineIncidentDraft");
        set => this.RaiseAndSetIfChanged(ref incidentDraft, value);
    }

    public bool IsHistoryVisible { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = true;
}
