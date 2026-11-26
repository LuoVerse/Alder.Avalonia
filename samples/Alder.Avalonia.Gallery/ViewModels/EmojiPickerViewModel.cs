using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class EmojiPickerViewModel : GalleryViewModel
{
    public ObservableCollection<string> Recents { get; } = [];

    public string? Selected
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "😀";

    private static readonly string[] CategoryIcons =
    [
        "M2 8 A6 6 0 1 1 14 8 A6 6 0 1 1 2 8 M5.5 6 H5.6 M10.4 6 H10.5 M5.5 9.5 Q8 12 10.5 9.5",
        "M5 4 A3 3 0 1 1 11 4 A3 3 0 1 1 5 4 M2 14 V13 Q2 9 6 9 H10 Q14 9 14 13 V14",
        "M3 13 Q2 7 6 5 Q9 3 14 2 Q14 7 12 10 Q9 14 3 13 M2 14 L10 6",
        "M8 6 C4 2 1 6 3 11 C4 15 6 15 8 13 C10 15 12 15 13 11 C15 6 12 2 8 6 M8 5 Q8 2 11 1",
        "M2 8 A6 6 0 1 1 14 8 A6 6 0 1 1 2 8 M2.5 6 L6 7 L8 5 L11 7 L10 10 L6 10 L5 13 M8 5 V2 M11 7 L14 6 M10 10 L12 12",
        "M2 8 A6 6 0 1 1 14 8 A6 6 0 1 1 2 8 M2 8 H14 M8 2 C4 4 4 12 8 14 C12 12 12 4 8 2",
        "M6 13 H10 M7 15 H9 M6 11 C6 9 3 8 3 5 A5 5 0 0 1 13 5 C13 8 10 9 10 11 Z",
        "M2 3 H7 V8 H2 Z M9 3 L14 8 H9 Z M2 12 A2 2 0 1 1 6 12 A2 2 0 1 1 2 12 M10 11 L14 14 M14 11 L10 14",
        "M3 15 V2 C7 -1 10 6 14 2 V10 C10 14 7 7 3 10"
    ];

    public IReadOnlyList<AlderEmojiCategory> CustomCategories { get; } = AlderEmojiPicker.DefaultCategories
        .Select((category, index) => category with { IconData = Geometry.Parse(CategoryIcons[index]) })
        .ToArray();
}
