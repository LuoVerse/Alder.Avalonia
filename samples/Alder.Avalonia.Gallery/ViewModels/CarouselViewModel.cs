using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CarouselViewModel : GalleryViewModel
{
    public CarouselViewModel()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Alder.Avalonia.Gallery/Assets/Carousel/trail-shoe-views.png"));
        var sheet = new Bitmap(stream);
        int width = sheet.PixelSize.Width / 3;
        int height = sheet.PixelSize.Height / 2;
        string[] descriptions =
        [
            "CarouselPhotoSide", "CarouselPhotoFront", "CarouselPhotoRear",
            "CarouselPhotoTop", "CarouselPhotoOutsole", "CarouselPhotoHeel"
        ];
        Product = descriptions.Select((key, index) => new CarouselPhoto(new CroppedBitmap
        {
            Source = sheet,
            SourceRect = new PixelRect(width * (index % 3), height * (index / 3), width, height)
        }, key)).ToArray();
    }

    public IReadOnlyList<CarouselPhoto> Product { get; }
    public IReadOnlyList<CarouselTile> MultipleSlides { get; } = Tiles(8);
    public IReadOnlyList<CarouselTile> Slides { get; } = Tiles(5);

    public int ApiIndex
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ApiStatus));
        }
    }

    public string ApiStatus => Format("CarouselPosition", ApiIndex + 1, Slides.Count);

    private static CarouselTile[] Tiles(int count) =>
        Enumerable.Range(1, count).Select(number => new CarouselTile(number)).ToArray();
}

public sealed record CarouselTile(int Number);

public sealed class CarouselPhoto(CroppedBitmap image, string descriptionKey) : GalleryViewModel
{
    public CroppedBitmap Image { get; } = image;
    public string Description => Text(descriptionKey);
}
