using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record AvatarExamplePerson(string Name, string Initials, IImage? Photo);

public sealed class AvatarViewModel : GalleryViewModel
{
    public IReadOnlyList<AvatarExamplePerson> People { get; } =
    [
        new("John Doe", "JD", Load("Card/blue")),
        new("Kate Wilson", "KW", Load("Avatar/green")),
        new("Emily Chen", "EC", Load("Avatar/purple")),
        new("Michael Brown", "MB", Load("Avatar/orange")),
        new("Olivia Davis", "OD", Load("Card/red"))
    ];

    public IReadOnlyList<AvatarExamplePerson> StackPeople { get; }
    public IReadOnlyList<AvatarExamplePerson> OverlapPeople { get; }

    public AvatarViewModel()
    {
        StackPeople = People.Take(4).ToArray();
        OverlapPeople = [People[0], new("Alex Brooks", "AB", null), People[2], new("Sofia Martinez", "SM", null)];
    }

    private static Bitmap Load(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/{name}.jpg"));
        return new Bitmap(stream);
    }
}
