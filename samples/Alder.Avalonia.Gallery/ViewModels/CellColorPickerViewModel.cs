using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CellColorPickerViewModel : GalleryViewModel
{
    public IReadOnlyList<AlderColorValue> BrandPalette { get; } =
    [
        AlderColorValue.Parse("#EF4444"), AlderColorValue.Parse("#F97316"), AlderColorValue.Parse("#EAB308"),
        AlderColorValue.Parse("#22C55E"), AlderColorValue.Parse("#06B6D4"), AlderColorValue.Parse("#3B82F6"),
        AlderColorValue.Parse("#8B5CF6"), AlderColorValue.Parse("#EC4899"), AlderColorValue.Parse("#F43F5E")
    ];

    public AlderColorValue Accent
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(AccentResult));
        }
    } = AlderColorValue.Parse("#3B82F6");

    public string AccentResult => Format("CellSelectedResult", Accent);
}
