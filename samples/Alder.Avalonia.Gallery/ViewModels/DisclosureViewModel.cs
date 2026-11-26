using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DisclosureViewModel : GalleryViewModel
{
    public bool IsPreviewExpanded
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsShippingExpanded
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ShippingAction));
        }
    }

    public string ShippingAction => Text(IsShippingExpanded ? "DisclosureHideShipping" : "DisclosureShowShipping");

    public ICommand ClosePreview { get; }
    public ICommand ToggleShipping { get; }
    public ICommand ResetPreview { get; }
    public ICommand ResetShipping { get; }

    public string? PreviewName { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    public DisclosureViewModel()
    {
        ClosePreview = ReactiveCommand.Create(() => { IsPreviewExpanded = false; });
        ToggleShipping = ReactiveCommand.Create(() => { IsShippingExpanded = !IsShippingExpanded; });
        ResetPreview = ReactiveCommand.Create(() =>
        {
            PreviewName = null;
            IsPreviewExpanded = true;
        });
        ResetShipping = ReactiveCommand.Create(() => { IsShippingExpanded = false; });
    }
}
