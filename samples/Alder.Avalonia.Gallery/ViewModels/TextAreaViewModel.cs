using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TextAreaViewModel : GalleryViewModel
{
    public string? Announcement
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Count));
            this.RaisePropertyChanged(nameof(IsTooShort));
        }
    }

    public string Count => Format("CharacterCount", Announcement?.Length ?? 0);
    public bool IsTooShort => Announcement is { Length: > 0 } text && text.Trim().Length < 12;
    public void Clear() => Announcement = "";
}
