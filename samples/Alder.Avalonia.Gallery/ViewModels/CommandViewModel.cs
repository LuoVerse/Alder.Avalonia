using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Views;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

internal enum CommandDemo { Usage, Launcher, Toolbar, Clean, Minimal, Split, SearchTerms }

public sealed class CommandViewModel : GalleryViewModel
{
    private string? lastAction;
    private readonly ICommand runAction;

    public CommandViewModel()
    {
        runAction = ReactiveCommand.Create<string>(key =>
        {
            lastAction = key;
            this.RaisePropertyChanged(nameof(Status));
        });
        OpenPaletteCommand = ReactiveCommand.CreateFromTask<Control>(OpenPaletteAsync);
    }

    public ICommand OpenPaletteCommand { get; }
    public KeyGesture LauncherGesture { get; } = new(Key.K, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
    public string LauncherShortcut => OperatingSystem.IsMacOS() ? "⌘ K" : "Ctrl K";
    public string Status => lastAction is null ? "" : Display(new GalleryMessage("CommandExecuted", new GalleryMessage(lastAction)));

    private async Task OpenPaletteAsync(Control trigger)
    {
        var palette = CommandPaletteSamples.Create(trigger, this);
        var backdrop = trigger.Tag is DialogBackdrop selectedBackdrop ? selectedBackdrop : DialogBackdrop.Opaque;
        if (await palette.ShowAsync(trigger, backdrop) == DialogResult.OK && Status.Length > 0)
            Toast.Show(new ToastOptions(Status) { Duration = TimeSpan.FromSeconds(2) });
    }

    public IReadOnlyList<AlderCommandGroup> Groups =>
    [
        new()
        {
            Heading = Text("CommandWorkspace"),
            Items =
            [
                Action("CommandNewNote", "AlderIconPlus", shortcut: OperatingSystem.IsMacOS() ? "⌘ N" : "Ctrl N"),
                new AlderCommandItem
                {
                    Label = Text("CommandOpenRecent"), Icon = Icon("AlderIconMagnifier"),
                    Groups = [new() { Items = [Action("CommandRoadmap"), Action("CommandReleaseNotes")] }]
                },
                Action("CommandArchived", enabled: false)
            ]
        },
        new()
        {
            Heading = Text("CommandPreferences"),
            Items =
            [
                new AlderCommandItem
                {
                    Label = Text("CommandAppearance"), Icon = Icon("AlderIconGear"),
                    Groups = [new() { Items = [Action("CommandLightTheme"), Action("CommandDarkTheme")] }]
                },
                Action("CommandKeyboardShortcuts")
            ]
        }
    ];

    internal static readonly string[] SearchCategories =
        ["CommandProjects", "CommandTasks", "CommandPeople", "CommandDocuments", "CommandChannels"];

    internal IReadOnlyList<AlderCommandGroup> UsageGroups =>
    [
        Group("CommandActions",
            Action("CommandNewDocument", "AlderIconPlus"),
            Action("CommandNewFolder", "AlderIconArchive"),
            Action("CommandAssignToMe", "AlderIconCheck")),
        Group("Settings",
            Action("CommandPreferences", "AlderIconGear"),
            Page("CommandAppearance", "AlderIconSun", Action("CommandLightTheme"), Action("CommandDarkTheme")),
            Action("CommandKeyboardShortcuts"))
    ];

    internal IReadOnlyList<AlderCommandGroup> LauncherGroups =>
    [
        Group("CommandSuggestions",
            Action("CommandDesignStudio", "AlderIconPencil", description: "CommandApplication"),
            Action("CommandProjectTracker", "AlderIconChart", description: "CommandApplication"),
            Action("CommandTeamChat", "AlderIconEnvelope", description: "CommandApplication"),
            Action("CommandVideoLibrary", "AlderIconEye", description: "CommandApplication"),
            Action("CommandNotes", "AlderIconBookmark", description: "CommandApplication")),
        Group("CommandTools",
            Action("CommandClipboardHistory", "AlderIconCopy"),
            Action("CommandImportExtension", "AlderIconDownload"),
            Action("CommandManageExtensions", "AlderIconGear"))
    ];

    internal IReadOnlyList<AlderCommandGroup> LauncherActions(AlderCommandItem? active) =>
    [
        new AlderCommandGroup
        {
            Heading = active?.Label,
            Items = [active ?? Action("CommandOpenActive"), Action("CommandPinFavorite", "AlderIconBookmark"),
                Action("CommandCopyName", "AlderIconCopy")]
        }
    ];

    internal IReadOnlyList<AlderCommandGroup> ToolbarGroups =>
    [
        Group(null, Action("CommandEnvironment", "AlderIconGlobe", detail: "staging / preview")),
        Group("CommandCollaboration",
            Action("CommandFeedback", "AlderIconEnvelope", shortcut: "C"),
            Action("CommandNotifications", "AlderIconBell", shortcut: "N"),
            Action("CommandFeatureFlags", "AlderIconGear", shortcut: "F"),
            Action("CommandSharePreview", "AlderIconArrowUpRight", shortcut: "S"),
            Page("CommandSwitchBranch", null, Action("CommandMainBranch"), Action("CommandPreviewBranch"))),
        Group("CommandDevelopment",
            Action("CommandViewLogs", "AlderIconChart", shortcut: "L"),
            Action("CommandTracing", "AlderIconEye", shortcut: "T"))
    ];

    internal IReadOnlyList<AlderCommandGroup> CleanGroups =>
    [
        Group("CommandProjects",
            Page("CommandSearchProjects", "AlderIconMagnifier", Action("CommandRoadmap"), Action("CommandReleaseNotes"), Action("CommandProjectTracker")),
            Action("CommandCreateProject", "AlderIconPlus")),
        Group("Teams",
            Page("CommandSearchTeams", "AlderIconMagnifier", Action("CommandDesignTeam"), Action("CommandEngineeringTeam")),
            Action("CommandCreateTeam", "AlderIconPlus")),
        Group("CommandHelp",
            Page("CommandSearchDocs", "AlderIconMagnifier", Action("CommandQuickStart"), Action("CommandKeyboardShortcuts")),
            Action("CommandFeedback", "AlderIconEnvelope"), Action("CommandContactSupport"))
    ];

    internal IReadOnlyList<AlderCommandGroup> MinimalGroups =>
    [
        Group(null,
            Page("CommandAssignTo", null, Action("CommandAssignToMe"), Action("CommandDesignTeam")),
            Action("CommandAssignToMe", shortcut: "I"),
            Page("CommandChangeStatus", null, Action("CommandInProgress"), Action("CommandDone")),
            Action("CommandChangePriority", shortcut: "P"),
            Action("CommandChangeLabels", shortcut: "L"),
            Action("CommandRemoveLabel", shortcut: "⇧ L"),
            Action("CommandSetDueDate", shortcut: "⇧ D"))
    ];

    internal IReadOnlyList<AlderCommandGroup> SplitGroups =>
    [
        Group("CommandComponents",
            Action("Button", description: "CommandButtonPreview"),
            Action("Input", description: "CommandInputPreview"),
            Action("CommandRadio", description: "CommandRadioPreview"),
            Action("Chip", description: "CommandChipPreview"),
            Action("Slider", description: "CommandSliderPreview"),
            Action("Avatar", description: "CommandAvatarPreview"),
            Action("Switch", description: "CommandSwitchPreview"))
    ];

    internal IReadOnlyList<AlderCommandGroup> SearchGroups(ISet<string> categories)
    {
        var prompts = new List<AlderCommandItem>();
        var results = new List<AlderCommandItem>();
        if (categories.Contains("CommandProjects"))
        {
            prompts.Add(Action("CommandDraftBrief", "AlderIconPencil", keywords: "project brief draft"));
            results.Add(Action("CommandRoadmap", "AlderIconBookmark", keywords: "project plan roadmap"));
        }
        if (categories.Contains("CommandTasks"))
        {
            prompts.Add(Action("CommandCreateTask", "AlderIconPlus", keywords: "team task assign"));
            results.Add(Action("CommandRecentActivity", "AlderIconClock", keywords: "task activity recent"));
        }
        if (categories.Contains("CommandPeople"))
        {
            prompts.Add(Action("CommandScheduleMeeting", "AlderIconCalendar", keywords: "people team meeting"));
            results.Add(Action("CommandDirectory", "AlderIconMagnifier", keywords: "people team directory"));
        }
        if (categories.Contains("CommandDocuments"))
        {
            prompts.Add(Action("CommandSummarizeProgress", "AlderIconChart", keywords: "document progress summary"));
            results.Add(Action("CommandReleaseNotes", "AlderIconBookmark", keywords: "document release notes"));
        }
        if (categories.Contains("CommandChannels"))
        {
            results.Add(Action("CommandWorkspaceSettings", "AlderIconGear", keywords: "channel workspace settings"));
        }
        return [Group("CommandSuggestedPrompts", prompts.ToArray()), Group("CommandResults", results.ToArray())];
    }

    private AlderCommandGroup Group(string? heading, params AlderCommandItem[] items) =>
        new() { Heading = heading is null ? null : Text(heading), Items = items };

    private AlderCommandItem Page(string key, string? icon, params AlderCommandItem[] children) =>
        new() { Label = Text(key), Icon = Icon(icon), Groups = [Group(null, children)] };

    private AlderCommandItem Action(string key, string? icon = null, bool enabled = true, string? shortcut = null,
        string? description = null, string? detail = null, string? keywords = null) => new()
    {
        Label = Text(key), Icon = Icon(icon), IsEnabled = enabled, Shortcut = shortcut,
        Description = description is null ? detail : Text(description), Keywords = keywords,
        Command = runAction, CommandParameter = key
    };

    private static Geometry? Icon(string? key) =>
        key is not null && Application.Current?.TryFindResource(key, out var value) == true ? value as Geometry : null;
}
