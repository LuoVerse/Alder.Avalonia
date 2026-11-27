using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class BloomMenuViewModel : GalleryViewModel
{
    private string? selected;

    public BloomMenuViewModel() => Select = ReactiveCommand.Create<string>(key =>
    {
        selected = key;
        this.RaisePropertyChanged(nameof(Feedback));
    });

    public ICommand Select { get; }
    public string TriggerLabel => Text("NavDemoCreate");
    public Geometry? TriggerIcon { get; } = Application.Current?.TryFindResource("AlderIconPlus", out var resource) == true
        ? resource as Geometry
        : null;
    public string? Feedback => selected is null ? null : Format("SelectedAction", Text(selected));
}
