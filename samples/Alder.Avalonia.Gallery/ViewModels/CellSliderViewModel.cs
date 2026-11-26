using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CellSliderViewModel : GalleryViewModel
{
    public double Spacing
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SpacingResult));
        }
    } = 0.50;

    public string SpacingResult => Format("CellValueResult", Spacing);
}
