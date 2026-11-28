using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class MenuViewModel : GalleryViewModel
{
    public ICommand RunAction { get; }
    public string? LastAction => actionKey is { } key ? Text(key) : null;

    private string? actionKey;

    public MenuViewModel() => RunAction = ReactiveCommand.Create<string>(key =>
    {
        actionKey = key;
        this.RaisePropertyChanged(nameof(LastAction));
    });
}
