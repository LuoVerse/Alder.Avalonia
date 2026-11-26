namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TrendChipViewModel : GalleryViewModel
{
    public string Increase => .033.ToString("+0.0%;-0.0%;0.0%", DisplayCulture);
    public string Decrease => (-.021).ToString("+0.0%;-0.0%;0.0%", DisplayCulture);
    public string Steady => 0d.ToString("0.0%", DisplayCulture);
}
