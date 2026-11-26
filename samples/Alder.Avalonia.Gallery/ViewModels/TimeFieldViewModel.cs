using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TimeFieldViewModel : GalleryViewModel
{
    private static readonly TimeOnly InitialTime = new(14, 30);

    public TimeOnly Example { get; } = new(14, 35, 42);
    public TimeOnly Opens { get; } = new(9, 0);
    public TimeOnly Closes { get; } = new(17, 0);

    public TimeOnly? Time
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = InitialTime;

    public string Summary => Time?.ToString("t", DisplayCulture) ?? Text("NoTimeSelected");
    public void Clear() => Time = null;
    public void Reset() => Time = InitialTime;
}
