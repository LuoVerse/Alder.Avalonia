using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class CheckboxButtonGroupViewModel : GalleryViewModel
{
    public IReadOnlyList<string> SelectedAddOns
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedAddOnsText));
        }
    } = ["backups"];

    public string SelectedAddOnsText => Format("CardSelectedFeatures",
        SelectedAddOns.Count == 0 ? Text("CardNoneSelected") : string.Join(", ", SelectedAddOns.Select(key => key switch
        {
            "backups" => Text("CardBackups"),
            "monitoring" => Text("CardMonitoring"),
            "support" => Text("CardSupport"),
            _ => key
        })));
}
