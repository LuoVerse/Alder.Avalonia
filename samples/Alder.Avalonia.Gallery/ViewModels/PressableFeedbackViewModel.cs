using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class PressableFeedbackViewModel : GalleryViewModel
{
    private bool notifyEnabled;
    private int holdCount;
    private int progressCount;

    public PressableFeedbackViewModel()
    {
        Notify = ReactiveCommand.Create(() =>
        {
            notifyEnabled = !notifyEnabled;
            this.RaisePropertyChanged(nameof(NotifyCaption));
        });
        CompleteHold = ReactiveCommand.Create(() =>
        {
            holdCount++;
            this.RaisePropertyChanged(nameof(HoldCaption));
        });
        CompleteProgress = ReactiveCommand.Create(() =>
        {
            progressCount++;
            this.RaisePropertyChanged(nameof(ProgressCaption));
        });
    }

    public ICommand Notify { get; }
    public ICommand CompleteHold { get; }
    public ICommand CompleteProgress { get; }
    public string NotifyCaption => Text(notifyEnabled ? "PressableFeedbackNotified" : "PressableFeedbackNotify");
    public string HoldCaption => Format("PressableFeedbackCount", holdCount);
    public string ProgressCaption => Format("PressableFeedbackCount", progressCount);
}
