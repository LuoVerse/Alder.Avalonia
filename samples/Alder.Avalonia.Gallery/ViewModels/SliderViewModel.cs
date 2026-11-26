using System.Globalization;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SliderViewModel : GalleryViewModel
{
    public double Volume
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(VolumeLabel));
            this.RaisePropertyChanged(nameof(PriceLabel));
        }
    } = 30;

    public double Stepped
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SteppedLabel));
        }
    } = 40;

    public double Start
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(StartLabel));
        }
    } = 20;

    public double End
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(EndLabel));
        }
    } = 75;

    public double Vertical
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(VerticalLabel));
        }
    } = 60;

    public string VolumeLabel => Volume.ToString("F0", DisplayCulture);
    public string SteppedLabel => (Stepped / 100).ToString("P0", DisplayCulture);
    public string VerticalLabel => (Vertical / 100).ToString("P0", DisplayCulture);
    public string StartLabel => Start.ToString("C0", DisplayCulture);
    public string EndLabel => End.ToString("C0", DisplayCulture);
    public CultureInfo MarkCulture => DisplayCulture;
    public string PriceLabel => Volume.ToString("C2", DisplayCulture);

    public double Fraction
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FractionLabel));
        }
    } = .4;

    public string FractionLabel => Fraction.ToString("0.0", DisplayCulture);
    public IReadOnlyList<SliderMark> VolumeMarks =>
    [
        new() { Value = 0, Label = Text("SliderLevelOff") },
        new() { Value = 20, Label = Text("SliderLevelQuiet") },
        new() { Value = 65, Label = Text("SliderLevelLoud") },
        new() { Value = 100, Label = Text("SliderLevelMax") }
    ];
}
