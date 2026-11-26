using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class HoverCardViewModel : GalleryViewModel
{
    public HoverCardViewModel()
    {
        OpenPreview = ReactiveCommand.Create(() => { IsPreviewOpen = true; });
        ClosePreview = ReactiveCommand.Create(() => { IsPreviewOpen = false; });
        ToggleFollow = ReactiveCommand.Create(() =>
        {
            isFollowing = !isFollowing;
            this.RaisePropertyChanged(nameof(FollowLabel));
        });
    }

    private bool isFollowing;

    public bool IsPreviewOpen
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PreviewStatus));
        }
    }

    public ICommand OpenPreview { get; }
    public ICommand ClosePreview { get; }
    public ICommand ToggleFollow { get; }
    public Uri UsageProfileUri { get; } = new("https://avaloniaui.net/");
    public string UsageProfileName => Text("HoverCardUsageProfileName");
    public string UsageProfileDomain => UsageProfileUri.Host;
    public string UsageProfileDescription => Text("HoverCardUsageProfileDescription");
    public string FollowLabel => Text(isFollowing ? "HoverCardFollowing" : "HoverCardFollow");
    public string PreviewStatus => Text(IsPreviewOpen ? "HoverCardPreviewOpen" : "HoverCardPreviewClosed");
}
