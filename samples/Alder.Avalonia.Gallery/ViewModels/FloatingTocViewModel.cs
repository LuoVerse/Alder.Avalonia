using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record TocDemoSection(string Key, string Title, string Body);

public sealed class FloatingTocViewModel : GalleryViewModel
{
    private static readonly (string Key, string Label)[] Sections =
    [
        ("overview", "TocOverview"), ("setup", "TocSetup"), ("quick-start", "TocQuickStart"),
        ("configuration", "TocConfiguration"), ("api", "TocApi"), ("troubleshooting", "TocTroubleshooting")
    ];

    private static readonly (string Key, string Label, int Level)[] Hierarchy =
    [
        ("principles", "TocPrinciples", 1), ("composition", "TocComposition", 2),
        ("accessibility", "TocAccessibility", 2), ("setup", "TocSetup", 1),
        ("install", "TocInstall", 2), ("workspace", "TocWorkspace", 2),
        ("components", "TocComponents", 1), ("layout", "TocLayout", 2),
        ("interactive", "TocInteractive", 2), ("buttons", "TocButtons", 3),
        ("forms", "TocForms", 3), ("data", "NavGroupDataDisplay", 1),
        ("tables", "Table", 2), ("theming", "TocTheming", 1),
        ("tokens", "TocTokens", 2), ("dark", "TocDark", 2),
        ("advanced", "TocAdvanced", 1), ("changelog", "TocChangelog", 1)
    ];

    public FloatingTocViewModel() => Toggle = ReactiveCommand.Create(() => { IsOpen = !IsOpen; });

    public ICommand Toggle { get; }
    public bool IsOpen
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Status));
        }
    }

    public string Status => Text(IsOpen ? "TocIsOpen" : "TocIsClosed");
    public IReadOnlyList<AlderTocEntry> Entries => Sections.Select(section => new AlderTocEntry(section.Key, Text(section.Label))).ToArray();
    public IReadOnlyList<AlderTocEntry> NestedEntries => Hierarchy.Select(entry => new AlderTocEntry(entry.Key, Text(entry.Label), entry.Level)).ToArray();
    public IReadOnlyList<AlderTocEntry> ManyEntries => Enumerable.Range(1, 80).Select(n => new AlderTocEntry($"part-{n}", Format("TocSectionNumber", n))).ToArray();
    public IReadOnlyList<TocDemoSection> Page => Sections.Select(section => new TocDemoSection(section.Key, Text(section.Label), Text(section.Label + "Body"))).ToArray();
}
