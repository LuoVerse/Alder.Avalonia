using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class RadioButtonGroupViewModel : GalleryViewModel
{
    public string? Plan
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "pro";
}
