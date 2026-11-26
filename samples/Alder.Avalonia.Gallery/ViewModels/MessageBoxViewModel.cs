using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class MessageBoxViewModel : OverlayViewModel
{
    public MessageBoxViewModel()
    {
        OpenUsageCommand = ReactiveCommand.CreateFromTask(OpenUsageAsync);
        OpenMessageCommand = ReactiveCommand.CreateFromTask<MessageBoxStatus>(OpenMessageAsync);
        OpenPresentationCommand = ReactiveCommand.CreateFromTask<object>(OpenPresentationAsync);
        OpenLocalCommand = ReactiveCommand.CreateFromTask(OpenLocalAsync);
    }

    public ICommand OpenUsageCommand { get; }
    public ICommand OpenMessageCommand { get; }
    public ICommand OpenPresentationCommand { get; }
    public ICommand OpenLocalCommand { get; }

    private async Task OpenUsageAsync()
    {
        Result = await MessageBox.ShowAsync(Text("MessageBoxUsageDescription"), new MessageBoxOptions
        {
            Title = Text("MessageBoxUsageHeading"),
            Status = MessageBoxStatus.Danger,
            MaxWidth = 400,
            Buttons = DialogButtons.OKCancel,
            PrimaryText = Text("MessageBoxUsageTrigger"),
            ShowCloseButton = true,
            IsDestructive = true
        });
    }

    private async Task OpenMessageAsync(MessageBoxStatus status)
    {
        bool destructive = status == MessageBoxStatus.Danger;
        string key = "MessageBox" + status;
        Result = await MessageBox.ShowAsync(Text(key + "Body"), new MessageBoxOptions
        {
            Title = Text(key + "Title"),
            Status = status,
            Buttons = destructive ? DialogButtons.OKCancel : DialogButtons.OK,
            PrimaryText = destructive ? Text("MessageBoxDelete") : null,
            ShowCloseButton = destructive,
            IsDestructive = destructive
        });
    }

    private async Task OpenPresentationAsync(object scenario)
    {
        var options = PresentationOptions();
        options = scenario switch
        {
            DialogSize size => options with { Size = size },
            DialogPlacement placement => options with { Placement = placement },
            DialogBackdrop backdrop => options with { Backdrop = backdrop },
            "Gradient" => options with { BackdropBrush = OverlaySamples.Gradient() },
            "Motion" => options with { Animation = OverlaySamples.Motion },
            "LightDismiss" => options with { CanLightDismiss = true, CanEscapeDismiss = true, ShowCloseButton = true },
            "CustomIcon" => options with { Icon = Application.Current?.FindResource("AlderIconBell") as Geometry },
            "CustomClose" => options with { Footer = OverlaySamples.CloseButton(Text("Close"), DialogResult.Cancel) },
            _ => options
        };
        Result = await MessageBox.ShowAsync(Text("MessageBoxDefaultBody"), options);
    }

    private async Task OpenLocalAsync()
    {
        Result = await MessageBox.ShowAsync(Text("MessageBoxDefaultBody"),
            PresentationOptions() with { Size = DialogSize.ExtraSmall }, hostId: "sample-overlay");
    }

    private MessageBoxOptions PresentationOptions() => new()
    {
        Title = Text("MessageBoxDefaultTitle"),
        Status = MessageBoxStatus.Accent,
        Placement = DialogPlacement.Center
    };
}
