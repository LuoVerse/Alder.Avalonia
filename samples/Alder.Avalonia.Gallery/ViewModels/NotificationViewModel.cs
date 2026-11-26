using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NotificationViewModel : GalleryViewModel
{
    private GalleryMessage? result;

    public NotificationViewModel()
    {
        Save = ReactiveCommand.Create(() =>
        {
            int enabled = (EmailAlerts ? 1 : 0) + (ActivityUpdates ? 1 : 0) + (WeeklyReport ? 1 : 0);
            result = new GalleryMessage("SwitchSaved", enabled);
            this.RaisePropertyChanged(nameof(Feedback));
        });
    }

    public bool MentionsEnabled
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool DigestEnabled
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool EmailAlerts { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
    public bool ActivityUpdates { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool WeeklyReport { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public ICommand Save { get; }
    public string? Feedback => Display(result);
}
