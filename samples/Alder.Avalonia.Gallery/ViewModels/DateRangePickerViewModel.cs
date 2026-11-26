using System.Globalization;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DateRangePickerViewModel : GalleryViewModel
{
    private static readonly AlderDateRange InitialDates = new(new(2026, 11, 28), new(2026, 12, 4));

    public CultureInfo GermanCulture { get; } = CultureInfo.GetCultureInfo("de-DE");
    public DateOnly Minimum { get; } = new(2026, 11, 1);
    public DateOnly Maximum { get; } = new(2027, 1, 31);
    public AlderDateRange Example { get; } = new(new(2026, 11, 23), new(2026, 11, 28));

    public AlderDateRange? Dates
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = InitialDates;

    public string Summary => Dates is { } range
        ? Format("DaysSelected", range.End.DayNumber - range.Start.DayNumber + 1)
        : Text("NoDatesSelected");
    public void Clear() => Dates = null;
    public void Reset() => Dates = InitialDates;
}
