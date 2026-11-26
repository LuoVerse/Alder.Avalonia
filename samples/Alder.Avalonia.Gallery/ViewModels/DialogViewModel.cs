using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Views;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DialogViewModel : GalleryViewModel
{
    public DialogViewModel()
    {
        OpenUsageCommand = ReactiveCommand.CreateFromTask(OpenUsageAsync);
        OpenModalCommand = ReactiveCommand.CreateFromTask<DialogPlacement>(OpenModalAsync);
        OpenWindowCommand = ReactiveCommand.CreateFromTask(OpenWindowAsync);
        OpenPresentationCommand = ReactiveCommand.CreateFromTask<object>(OpenPresentationAsync);
        OpenLocalCommand = ReactiveCommand.CreateFromTask(OpenLocalAsync);
    }

    public ICommand OpenUsageCommand { get; }
    public ICommand OpenModalCommand { get; }
    public ICommand OpenWindowCommand { get; }
    public ICommand OpenPresentationCommand { get; }
    public ICommand OpenLocalCommand { get; }

    public string ProjectName
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ProjectSummary));
        }
    } = "Alder";

    public string ProjectSummary => Format("DialogCurrentProject", ProjectName);

    public DialogResult? ModalResult
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ModalFeedback));
        }
    }

    public DialogResult? WindowResult
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(WindowFeedback));
        }
    }

    public string? ModalFeedback => ResultText(ModalResult);
    public string? WindowFeedback => ResultText(WindowResult);

    private Task OpenUsageAsync()
    {
        var continueButton = OverlaySamples.CloseButton(Text("OverlayContinue"));
        continueButton.FullWidth = true;
        continueButton.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        return Dialog.ShowModalAsync(Text("DialogUsageDescription"), new DialogOptions
        {
            Title = Text("DialogUsageHeading"),
            Icon = Application.Current?.FindResource("AlderIconCircleDashed") as Geometry,
            MaxWidth = 360,
            Footer = continueButton
        });
    }

    private async Task OpenModalAsync(DialogPlacement placement)
    {
        var editor = new ProjectEditorViewModel(ProjectName);
        ModalResult = await Dialog.ShowModalAsync<ProjectEditorView>(editor, CreateOptions() with { Placement = placement });
        if (ModalResult == DialogResult.OK) ProjectName = editor.ProjectName;
    }

    private async Task OpenWindowAsync()
    {
        var editor = new ProjectEditorViewModel(ProjectName);
        WindowResult = await Dialog.ShowWindowAsync<ProjectEditorView>(editor, CreateOptions());
        if (WindowResult == DialogResult.OK) ProjectName = editor.ProjectName;
    }

    private DialogOptions CreateOptions() => new()
    {
        Title = Text("DialogEditProject"),
        FlowDirection = DisplayCulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        Size = DialogSize.Small
    };

    private Task OpenPresentationAsync(object scenario)
    {
        object content = Text("DrawerWorkspaceDescription");
        var options = PresentationOptions();
        if (scenario is DialogScrollMode mode)
        {
            content = OverlaySamples.Activity(GalleryLocalization.Current);
            options = options with { ScrollMode = mode, Placement = DialogPlacement.Top };
        }
        else
        {
            options = scenario switch
            {
                DialogSize size => options with { Size = size },
                DialogBackdrop backdrop => options with { Backdrop = backdrop },
                "Gradient" => options with { BackdropBrush = OverlaySamples.Gradient() },
                "Motion" => options with { Animation = OverlaySamples.Motion },
                "Locked" => options with { CanLightDismiss = false, CanEscapeDismiss = false, ShowCloseButton = false },
                "CustomClose" => options with
                {
                    ShowCloseButton = false,
                    Footer = OverlaySamples.CloseButton(Text("Close"), DialogResult.Cancel)
                },
                "IconTrigger" => options with { Icon = Application.Current?.FindResource("AlderIconGear") as Geometry },
                _ => options
            };
        }
        return Dialog.ShowModalAsync(content, options);
    }

    private Task OpenLocalAsync() =>
        Dialog.ShowModalAsync(Text("DrawerWorkspaceDescription"),
            PresentationOptions() with { Size = DialogSize.ExtraSmall }, hostId: "sample-overlay");

    private DialogOptions PresentationOptions() => new()
    {
        Title = Text("DrawerWorkspaceTitle"), Buttons = DialogButtons.OKCancel, Placement = DialogPlacement.Center
    };

    private string? ResultText(DialogResult? result) => result is { } value
        ? Format("DialogResultFeedback", Text("Dialog." + value))
        : null;
}

public sealed class ProjectEditorViewModel : GalleryViewModel, IDialogContext
{
    public ProjectEditorViewModel(string projectName)
    {
        ProjectName = projectName;
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => RequestClose?.Invoke(this, DialogResult.Cancel));
    }

    public event EventHandler<DialogResult>? RequestClose;
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string ProjectName
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanSave));
        }
    } = "";

    public bool CanSave => !string.IsNullOrWhiteSpace(ProjectName);

    private void Save()
    {
        if (!CanSave) return;
        ProjectName = ProjectName.Trim();
        RequestClose?.Invoke(this, DialogResult.OK);
    }
}
