using System.Globalization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class RatingViewModel : GalleryViewModel
{
    public CultureInfo Culture => DisplayCulture;

    public double Selected
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedCaption));
        }
    } = 3;

    public double Custom
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CustomCaption));
        }
    }

    public string SelectedCaption => Format(Selected == 1 ? "RatingOneStar" : "RatingStars", Selected);
    public string CustomCaption => Custom == 0 ? Text("RatingNoRating") : Format(Custom == 1 ? "RatingOneStar" : "RatingStars", Custom);
}
