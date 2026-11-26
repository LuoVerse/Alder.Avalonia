using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CellSelectViewModel : GalleryViewModel
{
    public CellSelectViewModel()
    {
        SelectedTheme = Themes[0];
        SelectedIcons = IconSets[0];
    }

    public IReadOnlyList<CellSelectChoice> Themes { get; } =
        [new("Default"), new("NavDemoDark"), new("NavDemoSystem")];

    public IReadOnlyList<CellSelectChoice> Languages { get; } =
        [new("CellEnglish"), new("CellSpanish"), new("CellFrench")];

    public IReadOnlyList<CellSelectChoice> FontSizes { get; } =
        [new("CellSmall"), new("CellMedium"), new("CellLarge")];

    public IReadOnlyList<CellSelectChoice> Fonts { get; } =
    [
        new("CellInter", new FontFamily("fonts:Inter#Inter")),
        new("CellRoboto", new FontFamily("Roboto, Arial")),
        new("NavDemoSystem", FontFamily.Default),
        new("CellGeorgia", new FontFamily("Georgia, Times New Roman"))
    ];

    public IReadOnlyList<CellSelectChoice> IconSets { get; } =
    [
        new("CellGravity", icon: Geometry.Parse("F0 M8 1A7 7 0 1 1 8 15A7 7 0 1 1 8 1Z M8 2.4A5.6 5.6 0 1 0 8 13.6A5.6 5.6 0 1 0 8 2.4Z M5.3 6A.8 .8 0 1 0 6.9 6A.8 .8 0 1 0 5.3 6Z M9.1 6A.8 .8 0 1 0 10.7 6A.8 .8 0 1 0 9.1 6Z M5.6 8.8C6.7 10.7 9.3 10.7 10.4 8.8L11.4 9.5C9.9 12.1 6.1 12.1 4.6 9.5Z")),
        new("CellHeroicons", icon: Geometry.Parse("F0 M8 1L14 3.5V7C14 10.6 11.5 13.6 8 15C4.5 13.6 2 10.6 2 7V3.5Z M8 2.5L3.4 4.4V7C3.4 9.8 5.3 12.2 8 13.5C10.7 12.2 12.6 9.8 12.6 7V4.4Z M5.1 7.7L6 6.8L7.5 8.3L10.1 5.7L11 6.6L7.5 10.2Z")),
        new("CellLucide", icon: Geometry.Parse("F0 M8 1A7 7 0 1 1 8 15A7 7 0 1 1 8 1Z M8 2.3A5.7 5.7 0 1 0 8 13.7A5.7 5.7 0 1 0 8 2.3Z M2 7.4H14V8.6H2Z M7.3 2.5H8.7V13.5H7.3Z"))
    ];

    public CellSelectChoice? SelectedTheme
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ThemeResult));
        }
    }

    public CellSelectChoice? SelectedIcons { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    public string ThemeResult => Format("CellSelectedResult", SelectedTheme?.Title ?? string.Empty);
}

public sealed class CellSelectChoice(string label, FontFamily? font = null, Geometry? icon = null) : GalleryViewModel
{
    public string Title => Text(label);
    public FontFamily Font { get; } = font ?? FontFamily.Default;
    public Geometry? Icon { get; } = icon;
    public override string ToString() => Title;
}
