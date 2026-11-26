using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ToggleButtonViewModel : GalleryViewModel
{
    public IReadOnlyList<string> Layout
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = ["list"];

    public bool IsLiked
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectionStatus));
        }
    }

    public string SelectionStatus => Format("StatusFormat", new GalleryMessage(IsLiked ? "Selected" : "NotSelected"));
    public ICommand Reset { get; }

    public ToggleButtonViewModel() => Reset = ReactiveCommand.Create(() => { IsLiked = false; });
}
