using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CellSwitchViewModel : GalleryViewModel
{
    public bool? Animations
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(AnimationsResult));
        }
    } = true;

    public string AnimationsResult => Format("CellAnimationsResult", Text(Animations == true ? "SwitchStateOn" : "SwitchStateOff"));
}
