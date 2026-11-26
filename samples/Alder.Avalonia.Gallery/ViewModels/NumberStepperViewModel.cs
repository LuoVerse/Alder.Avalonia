using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NumberStepperViewModel : GalleryViewModel
{
    public NumberStepperViewModel() => Reset = ReactiveCommand.Create(() => Value = 5);
    public ICommand Reset { get; }
    public decimal? Value
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Summary));
        }
    } = 5;
    public string Summary => Format("FormValueSummary", Value);
}
