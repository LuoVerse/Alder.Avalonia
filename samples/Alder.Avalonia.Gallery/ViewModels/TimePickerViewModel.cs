using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TimePickerViewModel : GalleryViewModel
{
    public TimeOnly? StartTime { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new TimeOnly(9, 30);
    public TimeOnly? Reminder { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new TimeOnly(14, 15, 20);
    public TimeOnly? FormattedTime { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new TimeOnly(14, 15, 20);
    public TimeOnly Minimum { get; } = new(8, 0);
    public TimeOnly Maximum { get; } = new(18, 0);
}
