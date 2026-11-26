using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Layout;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

internal sealed record GalleryHomeLink(string PageKey, string? ExampleName = null, string? SectionName = null, bool ShowCode = false);

public sealed class GalleryHomeViewModel : GalleryViewModel
{
    private bool saved = true;

    public GalleryHomeViewModel()
    {
        Save = ReactiveCommand.Create(() => SetSaved(true));
        Cancel = ReactiveCommand.Create(() => SetSaved(false));
        Align = ReactiveCommand.Create<HorizontalAlignment>(value => Alignment = value);
    }

    public ICommand Save { get; }
    public ICommand Cancel { get; }
    public ICommand Align { get; }

    public double Progress { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = 64;
    public HorizontalAlignment Alignment { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = HorizontalAlignment.Left;
    public string FeedbackTitle => Text(saved ? "HomeSavedTitle" : "HomeCanceledTitle");
    public string FeedbackDescription => Text(saved ? "HomeSavedDescription" : "HomeCanceledDescription");
    public FeedbackColor FeedbackColor => saved ? FeedbackColor.Success : FeedbackColor.Default;

    private void SetSaved(bool value)
    {
        saved = value;
        this.RaisePropertyChanged(nameof(FeedbackTitle));
        this.RaisePropertyChanged(nameof(FeedbackDescription));
        this.RaisePropertyChanged(nameof(FeedbackColor));
    }
}
