using System.Globalization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NumberFieldViewModel : GalleryViewModel
{
    public NumberFormatInfo UsFormat { get; } = CultureInfo.GetCultureInfo("en-US").NumberFormat;
    public NumberFormatInfo EuroFormat { get; } = CultureInfo.GetCultureInfo("de-DE").NumberFormat;
    public NumberFormatInfo KilogramFormat { get; } = new()
    {
        CurrencySymbol = "kg",
        CurrencyPositivePattern = 3,
        CurrencyNegativePattern = 8
    };

    public decimal? Width
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(WidthSummary));
        }
    } = 1024;

    public decimal? Quantity
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsQuantityInvalid));
        }
    } = 0;

    public bool IsQuantityInvalid => Quantity is not > 0;
    public string WidthSummary => Width is { } width ? Format("WidthInPixels", width) : Text("WidthIsEmpty");

    public void ResetWidth() => Width = 0;
    public void SetWideWidth() => Width = 2048;
}
