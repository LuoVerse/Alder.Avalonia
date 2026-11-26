using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record HoloArtwork(string Name, IImage Source);

public sealed class HoloCardViewModel : GalleryViewModel
{
    private static readonly (string File, string Label)[] Art =
    [
        ("robot1.jpeg", "HoloArtAutomaton"), ("cherries.jpeg", "HoloArtCherries"),
        ("oranges.jpeg", "HoloArtCitrus"), ("avocado.jpeg", "HoloArtAvocado"),
        ("demo1.jpg", "HoloArtCactus"), ("demo2.jpg", "HoloArtPortrait"),
        ("neo1.jpeg", "HoloArtCeramic"), ("neo2.jpeg", "HoloArtCeramicProfile")
    ];

    private readonly Bitmap[] bitmaps = Art.Select(item => Load(item.File)).ToArray();

    public IReadOnlyList<HoloArtwork> ImageCards => CreateCards([0, 1, 2]);
    public IReadOnlyList<HoloArtwork> Collection => CreateCards([0, 1, 2, 3, 4, 5, 6, 7, 3, 2, 0, 5, 7, 1, 4, 6]);

    private HoloArtwork[] CreateCards(int[] indices) => indices.Select(i => new HoloArtwork(Text(Art[i].Label), bitmaps[i])).ToArray();

    private static Bitmap Load(string file)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Card/{file}"));
        return Bitmap.DecodeToWidth(stream, 440);
    }
}
