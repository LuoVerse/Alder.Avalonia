using System.Globalization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DateFieldViewModel : GalleryViewModel
{
    private static readonly DateOnly InitialDate = new(2026, 11, 23);

    public CultureInfo GermanCulture { get; } = CultureInfo.GetCultureInfo("de-DE");
    public DateOnly Minimum { get; } = new(2026, 11, 1);
    public DateOnly Example { get; } = new(2026, 11, 23);

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

    public string Summary => Date?.ToString("d", DisplayCulture) ?? Text("NoDateSelected");
    public void Clear() => Date = null;
    public void Reset() => Date = InitialDate;
}
