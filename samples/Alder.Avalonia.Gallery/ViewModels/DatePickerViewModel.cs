using System.Globalization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DatePickerViewModel : GalleryViewModel
{
    private static readonly DateOnly InitialDate = new(2026, 11, 23);
    private static readonly TimeOnly InitialTime = new(14, 30);

    public CultureInfo ThaiCulture { get; } = CultureInfo.GetCultureInfo("th-TH");
    public DateOnly Example { get; } = new(2026, 11, 23);
    public DateOnly Minimum { get; } = new(2026, 11, 1);
    public Func<DateOnly, bool> Weekends { get; } = date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public DateOnly? Date
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = InitialDate;

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

    public string Summary => Date is { } date && Time is { } time
        ? Format("DateAtTime", date.ToString("d", DisplayCulture), time.ToString("t", DisplayCulture))
        : Text("ChooseADateAndATime");
    public void Clear() => Date = null;

    public void Reset()
    {
        Date = InitialDate;
        Time = InitialTime;
    }
}
