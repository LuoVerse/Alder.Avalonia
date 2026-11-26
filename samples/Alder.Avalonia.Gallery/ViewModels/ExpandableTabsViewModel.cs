using System.Windows.Input;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ExpandableTabAction(string labelKey, string iconKey, ICommand command) : GalleryViewModel
{
    public string Caption => Text(labelKey);
    public string IconKey => iconKey;
    public ICommand Activate => command;
}

public sealed class ExpandableTabMenu(IReadOnlyList<ExpandableTabAction> actions)
{
    public IReadOnlyList<ExpandableTabAction> Actions { get; } = actions;
}

public sealed class ExpandableTabsViewModel : GalleryViewModel
{
    public int SelectedIndex
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = -1;

    public ExpandableTabAction? LastAction
    {
        get => field;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ActionFeedback));
        }
    }

    public string? ActionFeedback => LastAction is { } action ? Format("SelectedAction", action.Caption) : null;
    public ExpandableTabMenu Project { get; }
    public ExpandableTabMenu Inbox { get; }
    public ExpandableTabMenu Automation { get; }
    public ExpandableTabMenu Library { get; }
    public ExpandableTabMenu Status { get; }

    public ExpandableTabsViewModel()
    {
        var activate = ReactiveCommand.Create<ExpandableTabAction>(action => { LastAction = action; });
        Project = Menu(("TabOverview", "AlderIconEye"), ("ExpandableReleaseNotes", "AlderIconCopy"),
            ("ExpandableChecklist", "AlderIconCheck"), ("Calendar", "AlderIconCalendar"), ("ExpandableBuilds", "AlderIconGear"));
        Inbox = Menu(("ExpandableMessages", "AlderIconEnvelope"), ("ExpandableRequests", "AlderIconEllipsis"),
            ("AccordionAlerts", "AlderIconBell"));
        Automation = Menu(("ExpandableWorkflows", "AlderIconGear"), ("ExpandableRuns", "AlderIconClock"),
            ("ExpandableRetry", "AlderIconRedo"));
        Library = Menu(("ExpandableDocuments", "AlderIconCopy"), ("ExpandableAssets", "AlderIconBookmark"),
            ("ExpandableDownloads", "AlderIconDownload"), ("ExpandableArchive", "AlderIconArchive"));
        Status = Menu(("ExpandableHealth", "AlderIconHeart"), ("ExpandableMetrics", "AlderIconChart"),
            ("ExpandableIncidents", "AlderIconWarning"));

        ExpandableTabMenu Menu(params (string Label, string Icon)[] actions) =>
            new(actions.Select(action => new ExpandableTabAction(action.Label, action.Icon, activate)).ToArray());
    }
}
