using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ToastViewModel : GalleryViewModel
{
    private readonly ICommand undo;
    private int stackSequence;

    public ToastViewModel()
    {
        undo = ReactiveCommand.Create(() => Toast.Show(new ToastOptions(Text("ToastRestored")) { Variant = ToastVariant.Success }));
        ShowUsageCommand = ReactiveCommand.Create(ShowUsage);
        ShowVariant = ReactiveCommand.CreateFromTask<ToastVariant>(ShowVariantAsync);
        ShowPlacement = ReactiveCommand.Create<ToastPlacement>(placement => { Show("ToastDefault", ToastVariant.Default, placement); });
        ShowStack = ReactiveCommand.Create(() =>
        {
            Show("ToastDefault", ToastVariant.Default);
            Show("ToastWarning", ToastVariant.Warning);
            Show("ToastSuccess", ToastVariant.Success);
        });
        ShowAction = ReactiveCommand.Create(() => Toast.Show(CreateArchiveNotice()));
        ShowSimpleCommand = ReactiveCommand.Create(() => Toast.Show(DefaultNotice() with { ShowIndicator = false, Description = null }));
        ShowCustomIconCommand = ReactiveCommand.Create(ShowCustomIcon);
        ShowCustomContentCommand = ReactiveCommand.Create<IDataTemplate>(ShowCustomContent);
        ShowExpandedCommand = ReactiveCommand.Create<TopLevel>(owner => ShowNamedStack(owner, "ToastExpanded", expanded: true));
        ShowIsolatedCommand = ReactiveCommand.Create<TopLevel>(owner => ShowNamedStack(owner, "ToastIsolated", expanded: false));
        AddToLimitedStackCommand = ReactiveCommand.Create<TopLevel>(AddToLimitedStack);
        ObserveCloseCommand = ReactiveCommand.CreateFromTask(ObserveCloseAsync);
        ShowPendingFailureCommand = ReactiveCommand.CreateFromTask(() => ShowPendingAsync(failed: true));
        ShowCompactCommand = ReactiveCommand.Create<TopLevel>(ShowCompact);
    }

    public ToastCloseReason? LastClose
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CloseFeedback));
        }
    }

    public string? CloseFeedback => LastClose is { } reason ? Format("OverlayToastClosed", Text("OverlayToastReason" + reason)) : null;

    public ICommand ShowUsageCommand { get; }
    public ICommand ShowVariant { get; }
    public ICommand ShowPlacement { get; }
    public ICommand ShowStack { get; }
    public ICommand ShowAction { get; }
    public ICommand ShowSimpleCommand { get; }
    public ICommand ShowCustomIconCommand { get; }
    public ICommand ShowCustomContentCommand { get; }
    public ICommand ShowExpandedCommand { get; }
    public ICommand ShowIsolatedCommand { get; }
    public ICommand AddToLimitedStackCommand { get; }
    public decimal? StackLimit
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 5;
    public ICommand ObserveCloseCommand { get; }
    public ICommand ShowPendingFailureCommand { get; }
    public ICommand ShowCompactCommand { get; }

    private void ShowUsage()
    {
        ToastHandle? toast = null;
        toast = Toast.Show(new ToastOptions(Text("ToastUsageHeading"))
        {
            Description = Text("ToastUsageDescription"),
            Indicator = Application.Current?.FindResource("AlderIconAvatarPerson") as Geometry,
            ActionText = Text("ToastUsageDismiss"),
            Action = ReactiveCommand.Create(() => toast?.Close())
        });
    }

    private ToastOptions CreateArchiveNotice(string? description = null) => new(Text("ToastArchived"))
    {
        Variant = ToastVariant.Accent, ActionText = Text("ToastUndo"), Action = undo,
        Duration = Timeout.InfiniteTimeSpan, Description = description
    };

    private async Task ShowPendingAsync(bool failed)
    {
        var toast = Toast.Show(new ToastOptions(Text("ToastLoading"))
        {
            Variant = ToastVariant.Loading,
            Description = Text("ToastLoadingDescription"),
            Duration = Timeout.InfiniteTimeSpan
        });
        await Task.Delay(1200);
        var key = failed ? "ToastDanger" : "ToastSuccess";
        toast.TryUpdate(new ToastOptions(Text(key))
        {
            Variant = failed ? ToastVariant.Danger : ToastVariant.Success,
            Description = Text(key + "Description")
        });
    }

    private async Task ShowVariantAsync(ToastVariant variant)
    {
        if (variant != ToastVariant.Loading)
        {
            Show("Toast" + variant, variant);
            return;
        }

        await ShowPendingAsync(failed: false);
    }

    private void Show(string key, ToastVariant variant, ToastPlacement? placement = null)
        => Toast.Show(new ToastOptions(Text(key))
        {
            Variant = variant, Placement = placement, Description = Text(key + "Description")
        });

    private ToastOptions DefaultNotice() => new(Text("ToastDefault"))
    {
        Description = Text("ToastDefaultDescription")
    };

    private void ShowCustomIcon()
    {
        if (Application.Current?.TryFindResource("AlderIconBell", out var icon) == true)
            Toast.Show(DefaultNotice() with { Indicator = icon as Geometry, Variant = ToastVariant.Accent });
    }

    private void ShowCustomContent(IDataTemplate template) =>
        Toast.Show(DefaultNotice() with
        {
            Content = new ToastMessageDemo(Text("OverlayToastCustom"), Text("OverlayToastCustomBody")),
            ContentTemplate = template
        });

    private void ShowNamedStack(TopLevel owner, string hostId, bool expanded)
    {
        var host = Toast.Register(owner, hostId);
        host.Placement = expanded ? ToastPlacement.Bottom : ToastPlacement.TopEnd;
        host.SafePadding = new Thickness(16, 52, 16, 16);
        host.IsExpanded = expanded;
        for (int index = 1; index <= 3; index++)
            Toast.Show(new ToastOptions(Format("SheetActivityItem", index)) { Variant = ToastVariant.Accent }, owner, hostId);
    }

    private void AddToLimitedStack(TopLevel owner)
    {
        const string hostId = "ToastLimited";
        var host = Toast.Register(owner, hostId);
        host.MaxVisible = (int)Math.Clamp(StackLimit ?? 5, 1, 8);
        Toast.Show(new ToastOptions(Format("SheetActivityItem", ++stackSequence))
            { Duration = TimeSpan.FromSeconds(8) }, owner, hostId);
    }

    private async Task ObserveCloseAsync()
    {
        LastClose = null;
        LastClose = await Toast.Show(DefaultNotice()).Completion;
    }

    private void ShowCompact(TopLevel owner)
    {
        const string hostId = "ToastCompact";
        var host = Toast.Register(owner, hostId);
        host.ToastWidth = 320;
        host.Placement = ToastPlacement.BottomEnd;
        Toast.Show(CreateArchiveNotice(Text("OverlayToastCompactBody")), owner, hostId);
    }
}

public sealed record ToastMessageDemo(string Title, string Detail);
