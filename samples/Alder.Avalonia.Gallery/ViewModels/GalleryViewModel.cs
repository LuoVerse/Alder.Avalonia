using System.Globalization;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public abstract class GalleryViewModel : ReactiveObject
{
    private readonly GalleryLocalization localization = GalleryLocalization.Current;

    protected GalleryViewModel() => localization.Register(this);

    protected CultureInfo DisplayCulture => localization.Culture;
    protected string Text(string key) => localization.Text(key);
    protected string Display(GalleryMessage? message) => localization.Render(message);
    protected string Format(string key, params object?[] arguments) => Display(new(key, arguments));

    internal void RefreshLanguage() => this.RaisePropertyChanged(string.Empty);
}
