using Alder.Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ImageViewViewModel : GalleryViewModel
{
    public ImageViewViewModel()
    {
        Photos =
        [
            Load("avocado.jpeg"),
            Load("cherries.jpeg"),
            Load("oranges.jpeg")
        ];
        LocalizePhotos();
        PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)) LocalizePhotos();
        };
    }

    public IReadOnlyList<AlderImageViewItem> Photos { get; }
    public IImage? SingleImage => Photos[0].Source;

    public IReadOnlyList<AlderImageViewTransition> ImageTransitions { get; } = Enum.GetValues<AlderImageViewTransition>();

    public AlderImageViewTransition ImageTransition
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = AlderImageViewTransition.CrossFade;

    public Func<object?, string?> ImageTransitionCaption => value =>
        value is AlderImageViewTransition transition ? Text($"ImageViewTransition{transition}") : null;

    public bool ReduceMotion
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool BlurToolbar
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ToolbarBackdropBlurRadius));
        }
    } = true;

    public double ToolbarBackdropBlurRadius => BlurToolbar ? 16 : 0;

    private void LocalizePhotos()
    {
        Describe(Photos[0], "ImageViewAvocado", "ImageViewAvocadoDescription");
        Describe(Photos[1], "ImageViewCherries", "ImageViewCherriesDescription");
        Describe(Photos[2], "ImageViewOranges", "ImageViewOrangesDescription");
    }

    private void Describe(AlderImageViewItem item, string title, string description)
    {
        item.Title = Text(title);
        item.Description = Text(description);
    }

    private static AlderImageViewItem Load(string file)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Card/{file}"));
        return new AlderImageViewItem { Source = new Bitmap(stream), FileName = file };
    }
}
