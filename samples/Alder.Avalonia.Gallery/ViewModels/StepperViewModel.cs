using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Input.Platform;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class StepperViewModel : GalleryViewModel
{
    public StepperViewModel()
    {
        CopyTrackingNumberCommand = ReactiveCommand.CreateFromTask<IClipboard?>(CopyTrackingNumberAsync);
        ShowTrackingDetailsCommand = ReactiveCommand.Create<string>(key => Toast.Show(new ToastOptions(Text(key))));
    }

    public ICommand CopyTrackingNumberCommand { get; }
    public ICommand ShowTrackingDetailsCommand { get; }

    public string TrackingNumber => "AX 482 709 163";
    public string OrderLabel => Format("StepperOrderLabel", "AL-472891");
    public string TrackingLabel => Format("StepperTrackingLabel", TrackingNumber);

    public StepperControlState Horizontal { get; } = new();
    public StepperControlState Vertical { get; } = new();

    private async Task CopyTrackingNumberAsync(IClipboard? clipboard)
    {
        if (clipboard is null) return;

        await clipboard.SetTextAsync(TrackingNumber);
        Toast.Show(new ToastOptions(Text("StepperTrackingCopied")));
    }
}

public sealed class StepperControlState : ReactiveObject
{
    private int activeStep;

    public StepperControlState()
    {
        Previous = ReactiveCommand.Create(() => { ActiveStep = Math.Max(0, ActiveStep - 1); });
        Next = ReactiveCommand.Create(() => { ActiveStep = Math.Min(3, ActiveStep + 1); });
    }

    public int ActiveStep
    {
        get => activeStep;
        set
        {
            if (activeStep == value) return;
            this.RaiseAndSetIfChanged(ref activeStep, value);
            this.RaisePropertyChanged(nameof(CanPrevious));
            this.RaisePropertyChanged(nameof(CanNext));
        }
    }

    public bool CanPrevious => ActiveStep > 0;
    public bool CanNext => ActiveStep < 3;
    public ICommand Previous { get; }
    public ICommand Next { get; }
}
