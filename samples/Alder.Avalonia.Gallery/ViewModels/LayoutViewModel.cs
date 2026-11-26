using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class LayoutViewModel : GalleryViewModel
{
    public string? InputText
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Notes
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}
