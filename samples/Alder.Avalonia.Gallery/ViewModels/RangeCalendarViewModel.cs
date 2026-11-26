using Alder.Avalonia.Controls;
using System.Globalization;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class RangeCalendarViewModel : GalleryViewModel
{
    private static readonly AlderDateRange InitialStay = new(new(2026, 11, 29), new(2026, 12, 4));

    public DateOnly Minimum { get; } = new(2026, 11, 1);
    public DateOnly Maximum { get; } = new(2027, 2, 28);
    public DateOnly PreviewDate { get; } = new(2026, 11, 23);
    public AlderDateRange PreviewRange { get; } = new(new(2026, 11, 7), new(2026, 11, 14));
    public DateOnly? FocusedExample { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new DateOnly(2026, 11, 18);
    public CultureInfo ThaiCulture { get; } = CultureInfo.GetCultureInfo("th-TH");
    public Func<DateOnly, IBrush?> Indicator { get; } = date => date.Day is 10 or 18 or 25 ? Brushes.DodgerBlue : null;
    public Func<DateOnly, bool> Maintenance { get; } = date => date == new DateOnly(2026, 11, 28);
    public Func<DateOnly, DateOnly, bool> StayLimit { get; } = (anchor, end) => Math.Abs(end.DayNumber - anchor.DayNumber) > 14;

    public AlderDateRange? Stay
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = InitialStay;

    public string Summary => Stay is { } range
        ? $"{range.Start.ToString("d", DisplayCulture)} – {range.End.ToString("d", DisplayCulture)}"
        : Text("ChooseAStartAndAnEndDate");
    public void Clear() => Stay = null;
    public void Reset() => Stay = InitialStay;
}
