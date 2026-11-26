using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class AccordionViewModel : GalleryViewModel
{
    private static readonly string[] Keys = ["account", "appearance", "alerts"];

    public IReadOnlyList<string> ExpandedKeys
    {
        get => field;
        set
        {
            if (field.SequenceEqual(value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ExpandedSummary));
        }
    } = [Keys[0]];

    public ICommand Previous { get; }
    public ICommand Next { get; }

    public string ExpandedSummary => ExpandedKeys.FirstOrDefault() switch
    {
        "account" => Format("AccordionExpanded", Text("AccordionAccount")),
        "appearance" => Format("AccordionExpanded", Text("AccordionAppearance")),
        "alerts" => Format("AccordionExpanded", Text("AccordionAlerts")),
        _ => Text("AccordionNone")
    };

    public AccordionViewModel()
    {
        Previous = ReactiveCommand.Create(() => Step(-1));
        Next = ReactiveCommand.Create(() => Step(1));
    }

    private void Step(int direction)
    {
        int index = Array.IndexOf(Keys, ExpandedKeys.FirstOrDefault());
        if (index < 0) index = direction > 0 ? -1 : 0;
        ExpandedKeys = [Keys[(index + direction + Keys.Length) % Keys.Length]];
    }
}
