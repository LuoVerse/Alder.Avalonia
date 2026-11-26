using System.Windows.Input;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Views;
using Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SheetViewModel : OverlayViewModel
{
    private string projectName = "Alder";

    public SheetViewModel()
    {
        OpenEdgeCommand = ReactiveCommand.CreateFromTask<DrawerPlacement>(OpenEdgeAsync);
        OpenSnapCommand = ReactiveCommand.CreateFromTask(OpenSnapAsync);
        OpenNestedCommand = ReactiveCommand.CreateFromTask(OpenNestedAsync);
        OpenDetailsCommand = ReactiveCommand.CreateFromTask(OpenDetailsAsync);
        OpenScenarioCommand = ReactiveCommand.CreateFromTask<object>(OpenScenarioAsync);
    }

    public ICommand OpenEdgeCommand { get; }
    public ICommand OpenSnapCommand { get; }
    public ICommand OpenNestedCommand { get; }
    public ICommand OpenDetailsCommand { get; }
    public ICommand OpenScenarioCommand { get; }

    public string? SelectedProfession
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PickerFeedback));
        }
    }

    public string? PickerFeedback => SelectedProfession is { } key ? Format("OverlayProfessionSelected", Text(key)) : null;

    private async Task OpenEdgeAsync(DrawerPlacement placement)
    {
        Result = await Sheet.ShowAsync(Text("SheetDescription"), new SheetOptions
        {
            Title = Text("SheetHeading"), Placement = placement, Buttons = DialogButtons.OKCancel, MaxWidth = 420
        });
    }

    private async Task OpenSnapAsync()
    {
        await Sheet.ShowAsync(OverlaySamples.SnapDetails(GalleryLocalization.Current), new SheetOptions
        {
            Title = Text("SheetHeading"), IsDetached = true, Extent = 600,
            SnapPoints = [.38, .68], MaxWidth = 520, Backdrop = DialogBackdrop.Blur
        });
    }

    private async Task OpenNestedAsync()
    {
        await Sheet.ShowAsync(OverlaySamples.NestedSheet(GalleryLocalization.Current, OpenDetailsCommand), new SheetOptions
        {
            Title = Text("SheetHeading"), MaxWidth = 480, Extent = 270
        });
    }

    private async Task OpenDetailsAsync()
    {
        await Sheet.ShowAsync(Text("SheetDescription"), new SheetOptions
        {
            Title = Text("SheetDetails"), IsDetached = true, MaxWidth = 360, Buttons = DialogButtons.OK
        });
    }

    private async Task OpenScenarioAsync(object scenario)
    {
        var options = new SheetOptions
        {
            Title = Text("SheetHeading"), MaxWidth = 480, Buttons = DialogButtons.OKCancel
        };
        object content = Text("SheetDescription");
        switch (scenario)
        {
            case DialogBackdrop backdrop:
                options = options with { Backdrop = backdrop };
                break;
            case "Basic":
            case "Detached":
                bool detached = Equals(scenario, "Detached");
                if (!detached) content = Text("SheetUsageDescription");
                options = options with
                {
                    Title = Text(detached ? "SheetHeading" : "SheetUsageHeading"),
                    IsDetached = detached, MaxWidth = 420,
                    Footer = OverlaySamples.ConfirmationFooter(Text("Dialog.Cancel"), Text("OverlayConfirm"))
                };
                break;
            case "SnapSequential":
            case "SnapFade":
            case "HandleOnly":
            case "InitialSnap":
                options = options with
                {
                    SnapPoints = [240, .64, .82], MaxWidth = 520,
                    InitialSnapPoint = Equals(scenario, "InitialSnap") ? .64 : null,
                    FadeBackdropAfterLastSnap = Equals(scenario, "SnapFade"),
                    DragFromHandleOnly = Equals(scenario, "HandleOnly")
                };
                content = OverlaySamples.Activity(GalleryLocalization.Current);
                break;
            case "LockedSheet":
                options = options with { CanLightDismiss = false, CanEscapeDismiss = false, ShowCloseButton = false };
                break;
            case "Scrollable":
                content = OverlaySamples.Activity(GalleryLocalization.Current);
                options = options with { Extent = 340 };
                break;
            case "Form":
                var editor = new ProjectEditorViewModel(projectName);
                Result = await Sheet.ShowAsync<ProjectEditorView>(editor, options with
                {
                    Title = Text("DialogEditProject"), Buttons = DialogButtons.None,
                    Placement = DrawerPlacement.Right, Extent = 400, ShowHandle = false
                });
                if (Result == DialogResult.OK) projectName = editor.ProjectName;
                return;
            case "Picker":
                await OpenPickerAsync();
                return;
            case "CustomClose":
                options = options with { ShowCloseButton = false, Footer = OverlaySamples.CloseButton(Text("Close")) };
                break;
        }

        Result = await Sheet.ShowAsync(content, options);
    }

    private async Task OpenPickerAsync()
    {
        var content = OverlaySamples.ProfessionPicker(GalleryLocalization.Current, key => SelectedProfession = key);
        Result = await Sheet.ShowAsync(content, new SheetOptions
        {
            Title = Text("OverlayPicker"), SnapPoints = [355, .8], MaxWidth = 440, DragFromHandleOnly = true
        });
    }
}
