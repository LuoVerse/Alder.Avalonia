namespace Alder.Avalonia.Gallery.ViewModels;

internal sealed class LocalizedNavigationBinding : GalleryViewModel
{
    public NavigationBindingViewModel Model { get; }

    public LocalizedNavigationBinding()
    {
        Model = new((key, _) => Text(key));
        PropertyChanged += (_, _) => Model.RefreshLanguage();
    }
}
