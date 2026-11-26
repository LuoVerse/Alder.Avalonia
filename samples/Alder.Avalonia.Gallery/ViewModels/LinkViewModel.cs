using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class LinkViewModel : GalleryViewModel
{
    public Uri Documentation { get; } = new("https://docs.avaloniaui.net/");
    public ICommand Activate { get; }

    public string? Status
    {
        get => Activated ? Text("LinkActivated") : null;
    }

    private bool Activated
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(Status));
    }

    public LinkViewModel() => Activate = ReactiveCommand.Create(() => { Activated = true; });
}
