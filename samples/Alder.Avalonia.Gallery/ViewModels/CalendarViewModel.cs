using System.Globalization;
using Avalonia.Media;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CalendarViewModel : GalleryViewModel
{
    public CultureInfo ThaiCulture { get; } = CultureInfo.GetCultureInfo("th-TH");
    public DateOnly MonthStart { get; } = new(2026, 11, 1);
    public DateOnly MonthEnd { get; } = new(2027, 1, 31);
    public DateOnly Example { get; } = new(2026, 11, 23);
    public DateOnly? FocusedExample { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new DateOnly(2026, 11, 18);
    public Func<DateOnly, bool> Unavailable { get; } = date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public Func<DateOnly, IBrush?> Indicator { get; } = date => date.Day is 10 or 18 or 25 ? Brushes.DodgerBlue : null;

    public DateOnly? Date
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = new DateOnly(2026, 11, 23);

    public AlderCalendarView View { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string Summary => Date?.ToString("D", DisplayCulture) ?? Text("NoDateSelected");

    public void ShowMonth() => View = AlderCalendarView.Month;
    public void ShowWeek() => View = AlderCalendarView.Week;
    public void ShowDay() => View = AlderCalendarView.Day;
}
