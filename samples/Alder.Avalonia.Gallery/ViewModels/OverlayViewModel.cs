using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public abstract class OverlayViewModel : GalleryViewModel
{
    public DialogResult? Result
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Feedback));
        }
    }

    public string? Feedback => Result is { } result
        ? Format("DialogResultFeedback", Text("Dialog." + result))
        : null;
}
