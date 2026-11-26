using System.Globalization;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NumberValueViewModel : GalleryViewModel
{
    public CultureInfo Culture => DisplayCulture;
}
