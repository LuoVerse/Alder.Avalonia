using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class PopoverViewModel : GalleryViewModel
{
    public string RenderedDescription => Text("OverlayPopoverRenderBody");

    public bool IncludeArchived
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsFollowing
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FollowLabel));
        }
    }

    public string FollowLabel => Text(IsFollowing ? "OverlayProfileFollowing" : "OverlayProfileFollow");
}
