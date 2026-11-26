using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Views;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class DrawerViewModel : OverlayViewModel
{
    private string projectName = "Alder";

    public DrawerViewModel()
    {
        OpenUsageCommand = ReactiveCommand.CreateFromTask(OpenUsageAsync);
        OpenDrawerCommand = ReactiveCommand.CreateFromTask<DrawerPlacement>(OpenDrawerAsync);
        OpenScenarioCommand = ReactiveCommand.CreateFromTask<object>(OpenScenarioAsync);
    }

    public ICommand OpenUsageCommand { get; }
    public ICommand OpenDrawerCommand { get; }
    public ICommand OpenScenarioCommand { get; }
    public IReadOnlyList<ControlledDrawerViewModel> ControlledDrawers { get; } =
    [
        new("DrawerControlledPropertyHeading", "DrawerControlledPropertyHint", "DrawerControlledPropertyTitle", "DrawerControlledPropertyBody"),
        new("DrawerControlledCommandsHeading", "DrawerControlledCommandsHint", "DrawerControlledCommandsTitle", "DrawerControlledCommandsBody")
    ];

    private async Task OpenUsageAsync()
    {
        Result = await Drawer.ShowAsync(Text("DrawerUsageDescription"), new DrawerOptions
        {
            Title = Text("DrawerUsageHeading"),
            Placement = DrawerPlacement.Right,
            ShowHandle = false,
            ShowCloseButton = false,
            Buttons = DialogButtons.OKCancel,
            PrimaryText = Text("OverlayConfirm")
        });
    }

    private async Task OpenDrawerAsync(DrawerPlacement placement)
    {
        Result = await Drawer.ShowAsync(Text("DrawerWorkspaceDescription"), new DrawerOptions
        {
            Placement = placement,
            Title = Text("DrawerWorkspaceTitle"),
            PrimaryText = Text("DrawerApply"),
            Buttons = DialogButtons.OKCancel,
            Backdrop = placement == DrawerPlacement.Right ? DialogBackdrop.Blur : DialogBackdrop.Opaque
        });
    }

    private async Task OpenScenarioAsync(object scenario)
    {
        var options = new DrawerOptions
        {
            Title = Text("DrawerWorkspaceTitle"), Placement = DrawerPlacement.Right, Buttons = DialogButtons.OKCancel
        };
        object content = Text("DrawerWorkspaceDescription");
        switch (scenario)
        {
            case DialogBackdrop backdrop:
                options = options with { Backdrop = backdrop };
                break;
            case "Locked":
                options = options with { CanEscapeDismiss = false, CanLightDismiss = false, ShowCloseButton = false };
                break;
            case "Scrollable":
                content = OverlaySamples.Activity(GalleryLocalization.Current);
                break;
            case "Form":
                var editor = new ProjectEditorViewModel(projectName);
                Result = await Drawer.ShowAsync<ProjectEditorView>(editor,
                    options with { Title = Text("DialogEditProject"), Buttons = DialogButtons.None });
                if (Result == DialogResult.OK) projectName = editor.ProjectName;
                return;
            case "Navigation":
                content = OverlaySamples.Navigation(GalleryLocalization.Current);
                options = options with { Title = Text("OverlayNavigation"), Buttons = DialogButtons.None };
                break;
            case "CustomClose":
                options = options with
                {
                    ShowCloseButton = false, Footer = OverlaySamples.CloseButton(Text("Close"), DialogResult.Cancel)
                };
                break;
        }
        Result = await Drawer.ShowAsync(content, options);
    }
}

public sealed class ControlledDrawerViewModel : GalleryViewModel, IDialogContext
{
    private static readonly FuncDataTemplate<ControlledDrawerViewModel> BodyTemplate = new((model, _) =>
        new AlderTextBlock { Text = model?.Body, FontSize = 14, LineHeight = 20, TextWrapping = TextWrapping.Wrap });

    private readonly string headingKey;
    private readonly string hintKey;
    private readonly string titleKey;
    private readonly string bodyKey;

    public ControlledDrawerViewModel(string headingKey, string hintKey, string titleKey, string bodyKey)
    {
        this.headingKey = headingKey;
        this.hintKey = hintKey;
        this.titleKey = titleKey;
        this.bodyKey = bodyKey;
        OpenCommand = ReactiveCommand.CreateFromTask(() => SetOpenAsync(true));
        ToggleCommand = ReactiveCommand.CreateFromTask(() => SetOpenAsync(!IsOpen));
        CloseCommand = ReactiveCommand.Create(RequestDismiss);
    }

    public string Heading => Text(headingKey);
    public string Hint => Text(hintKey);
    public string Body => Text(bodyKey);

    public bool IsOpen
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(StateText));
        }
    }

    public string StateText => Text(IsOpen ? "DrawerControlledOpen" : "DrawerControlledClosed");
    public ICommand OpenCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand CloseCommand { get; }
    public event EventHandler<DialogResult>? RequestClose;

    private async Task SetOpenAsync(bool open)
    {
        if (open == IsOpen) return;
        if (!open)
        {
            RequestDismiss();
            return;
        }

        IsOpen = true;
        try
        {
            await Drawer.ShowAsync(this, new DrawerOptions
            {
                Title = Text(titleKey), Placement = DrawerPlacement.Right, ShowHandle = false,
                ContentTemplate = BodyTemplate,
                Footer = new AlderButton
                {
                    Content = Text("Close"), Variant = ButtonVariant.Secondary,
                    HorizontalAlignment = HorizontalAlignment.Right, Command = CloseCommand
                }
            });
        }
        finally
        {
            IsOpen = false;
        }
    }

    private void RequestDismiss() => RequestClose?.Invoke(this, DialogResult.Cancel);
}
