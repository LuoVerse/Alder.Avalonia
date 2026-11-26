using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SegmentViewModel : GalleryViewModel
{
    public object? ThemeKey
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PreviewTheme));
        }
    } = "system";

    public ThemeVariant PreviewTheme => ThemeKey switch
    {
        "light" => ThemeVariant.Light,
        "dark" => ThemeVariant.Dark,
        _ => Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark
            ? ThemeVariant.Dark : ThemeVariant.Light
    };

    public object? SelectedViewKey
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedViewText));
        }
    } = "analytics";

    public string SelectedViewText => Format("SegmentSelected", SelectedViewKey ?? "");
}
