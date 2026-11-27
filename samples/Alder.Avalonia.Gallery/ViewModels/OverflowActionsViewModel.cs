using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class OverflowActionsViewModel : GalleryViewModel
{
    public OverflowActionsViewModel() =>
        Run = ReactiveCommand.Create<string>(action => LastAction = new GalleryMessage(action));

    public bool IsExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool BlurSurface
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SurfaceBackdropBlurRadius));
        }
    }

    public double SurfaceBackdropBlurRadius => BlurSurface ? 16 : 0;
    public ICommand Run { get; }
    public string? Feedback => LastAction is { } action ? Format("SelectedAction", action) : null;

    private GalleryMessage? LastAction
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Feedback));
        }
    }
}
