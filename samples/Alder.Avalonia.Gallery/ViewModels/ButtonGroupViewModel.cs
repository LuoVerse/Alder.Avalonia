using System.Windows.Input;
using Alder.Avalonia.Gallery.Localization;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ButtonGroupViewModel : GalleryViewModel
{
    public string Status => Display(StatusMessage);

    private GalleryMessage StatusMessage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value, nameof(Status));
    } = "TryAGroupedButtonOrChooseAMergeMethod";

    private string MergeMethod { get; set; } = "CreateAMergeCommit";
    private int Page { get; set; } = 1;

    public ICommand Report { get; }
    public ICommand Merge { get; }
    public ICommand ChooseMergeMethod { get; }
    public ICommand StepPage { get; }

    public ButtonGroupViewModel()
    {
        Report = ReactiveCommand.Create<string>(action => StatusMessage = new GalleryMessage("SelectedAction", new GalleryMessage(action)));
        Merge = ReactiveCommand.Create(() => StatusMessage = new GalleryMessage("MergePreview", new GalleryMessage(MergeMethod)));
        ChooseMergeMethod = ReactiveCommand.Create<string>(method =>
        {
            MergeMethod = method;
            StatusMessage = new GalleryMessage("MergeMethod", new GalleryMessage(method));
        });
        StepPage = ReactiveCommand.Create<string>(direction =>
        {
            Page = Math.Clamp(Page + (direction == "-1" ? -1 : 1), 1, 9);
            StatusMessage = new GalleryMessage("PageOf", Page, 9);
        });
    }
}
