using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class FileTreeViewModel : GalleryViewModel
{
    private readonly AvaloniaList<object> selectedFiles = [];
    private readonly AvaloniaList<object> dragSelection = [];
    private readonly AvaloniaList<AlderFileTreeNode> reviewSource = ReviewRepository();

    public FileTreeViewModel()
    {
        selectedFiles.CollectionChanged += (_, _) => this.RaisePropertyChanged(nameof(SelectedSummary));
        Move = ReactiveCommand.Create<AlderFileTreeMove>(MoveFile);
        ResetMoves = ReactiveCommand.Create(() =>
        {
            dragSelection.Clear();
            DragFiles = Source(includeModal: true);
        });
        ResetSelection = ReactiveCommand.Create(selectedFiles.Clear);
        ResetReview = ReactiveCommand.Create(() =>
        {
            ReviewQuery = null;
            IncludeJsonc = IncludeCsharp = IncludeAxaml = true;
            IncludeDeleted = IncludeViewed = false;
            ReviewSelection = null;
        });
    }

    public AvaloniaList<AlderFileTreeNode> MainFiles { get; } = Repository();
    public AvaloniaList<AlderFileTreeNode> ReducedFiles { get; } = Repository();
    public AvaloniaList<AlderFileTreeNode> IconFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> IndicatorFiles { get; } = CompactSource();
    public AvaloniaList<AlderFileTreeNode> SmallFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> MediumFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> LargeFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> AlwaysFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> HoverFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> HiddenFiles { get; } = Source();
    public AvaloniaList<AlderFileTreeNode> MultiFiles { get; } =
    [
        Folder("src", Folder("components", File("Button.axaml"), File("Card.axaml"), File("Dialog.axaml")),
            ClosedFolder("utils", File("Helpers.cs"))),
        File("README.md")
    ];
    public AvaloniaList<AlderFileTreeNode> DragFiles { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = Source(includeModal: true);
    public AvaloniaList<AlderFileTreeNode> DynamicFiles { get; } =
    [
        Folder("samples", Folder("Gallery", File("App.axaml"), File("MainWindow.axaml"), File("Gallery.csproj")),
            ClosedFolder("Desktop", File("Program.cs"), File("Desktop.csproj"))),
        Folder("src", Folder("Alder.Avalonia", File("AlderTheme.axaml"), File("Alder.Avalonia.csproj"))),
        File("README.md"), File("AGENTS.md"), File("Alder.Avalonia.slnx")
    ];

    public IList SelectedFiles => selectedFiles;
    public IList DragSelection => dragSelection;
    public string SelectedSummary => Format("FileTreeSelected", selectedFiles.Count == 0 ? Text("FeedbackNone") :
        string.Join(", ", selectedFiles.OfType<AlderFileTreeNode>().Select(file => file.Title)));
    public ICommand Move { get; }
    public ICommand ResetMoves { get; }
    public ICommand ResetSelection { get; }
    public ICommand ResetReview { get; }
    public AlderFileTreeNode? ReviewSelection { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    public string? ReviewQuery { get => field; set => SetReviewFilter(ref field, value); }
    public bool IncludeJsonc { get => field; set => SetReviewFilter(ref field, value); } = true;
    public bool IncludeCsharp { get => field; set => SetReviewFilter(ref field, value); } = true;
    public bool IncludeAxaml { get => field; set => SetReviewFilter(ref field, value); } = true;
    public bool IncludeDeleted { get => field; set => SetReviewFilter(ref field, value); }
    public bool IncludeViewed { get => field; set => SetReviewFilter(ref field, value); }

    public IReadOnlyList<AlderFileTreeNode> ReviewFiles => FilterReview(reviewSource).ToArray();

    private void SetReviewFilter<T>(ref T storage, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        this.RaiseAndSetIfChanged(ref storage, value, property);
        ReviewSelection = null;
        this.RaisePropertyChanged(nameof(ReviewFiles));
    }

    private IEnumerable<AlderFileTreeNode> FilterReview(IEnumerable<AlderFileTreeNode> nodes)
    {
        var query = ReviewQuery?.Trim();
        foreach (var node in nodes)
        {
            if (!node.IsFolder)
            {
                if ((IncludeDeleted || node.Title != "LegacySession.cs") &&
                    (IncludeViewed || node.Title != "WorkspaceAvatar.axaml") &&
                    (string.IsNullOrEmpty(query) || node.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                    (Path.GetExtension(node.Title) switch { ".jsonc" => IncludeJsonc, ".cs" => IncludeCsharp, ".axaml" => IncludeAxaml, _ => false }))
                    yield return node;
                continue;
            }

            if (node.Children.Count == 0)
            {
                if (string.IsNullOrEmpty(query) && IncludeJsonc && IncludeCsharp && IncludeAxaml) yield return node;
                continue;
            }

            var children = FilterReview(node.Children).ToArray();
            if (children.Length > 0) yield return Folder(node.Title, children);
        }
    }

    private static AlderFileTreeNode File(string title)
    {
        string resource = Path.GetExtension(title) switch
        {
            ".cs" => "AlderFileTreeSourceIcon",
            ".axaml" => "AlderFileTreeMarkupIcon",
            ".json" or ".csproj" or ".props" or ".slnx" => "AlderFileTreeConfigIcon",
            ".md" or ".jsonc" => "AlderFileTreeTextIcon",
            _ when title == ".env" => "AlderIconGear",
            _ => "AlderFileTreeFileIcon"
        };
        var icon = Application.Current?.TryFindResource(resource, out var value) == true ? value as Geometry : null;
        return new(title, title) { Icon = icon };
    }

    private static AlderFileTreeNode Folder(string title, params AlderFileTreeNode[] children)
    {
        var folder = new AlderFileTreeNode(title, title) { IsExpanded = children.Length > 0, IsFolder = true };
        folder.Children.AddRange(children);
        return folder;
    }

    private static AlderFileTreeNode ClosedFolder(string title, params AlderFileTreeNode[] children)
    {
        var folder = Folder(title, children);
        folder.IsExpanded = false;
        return folder;
    }

    private static AvaloniaList<AlderFileTreeNode> Repository() =>
    [
        Folder("samples",
            Folder("Gallery", File("Alder.Avalonia.Gallery.csproj"), File("App.axaml"),
                ClosedFolder("Views", Folder("Pages", File("HomeView.axaml"), File("SettingsView.axaml")))),
            Folder("Desktop", File("Desktop.csproj"), ClosedFolder("src", File("Program.cs"), File("AppBuilder.cs")))),
        Folder("src", Folder("Alder.Avalonia", File("Alder.Avalonia.csproj"), ClosedFolder("Themes", File("AlderTheme.axaml")))),
        Folder(".config", ClosedFolder("skills", File("README.md"))),
        File("README.md"), File("AGENTS.md"), File("Alder.Avalonia.slnx"), File("Directory.Build.props")
    ];

    private static AvaloniaList<AlderFileTreeNode> Source(bool includeModal = false)
    {
        AlderFileTreeNode[] components = includeModal
            ? [File("Button.axaml"), File("Card.axaml"), File("Dialog.axaml")]
            : [File("Button.axaml"), File("Card.axaml"), File("ButtonStyles.axaml")];
        var files = new AvaloniaList<AlderFileTreeNode>
        {
            Folder("src", Folder("components", components), Folder("utils", File("ControlComposition.cs"), File("ClassNames.cs")), File("Program.cs")),
            File("Alder.Demo.csproj"), File("Directory.Build.props"), File("README.md")
        };
        if (!includeModal) files.Add(File(".env"));
        return files;
    }

    private static AvaloniaList<AlderFileTreeNode> CompactSource() =>
    [
        Folder("src", Folder("components", File("Button.axaml"), File("Card.axaml")), File("Program.cs")),
        File("Alder.Demo.csproj"), File("README.md")
    ];

    private static AvaloniaList<AlderFileTreeNode> ReviewRepository() =>
    [
        Folder("apps",
            Folder("api",
                Folder("src", Folder("lib"), Folder("routes"),
                    Folder("tests",
                        Folder("middleware", File("SessionAuthTests.cs")),
                        Folder("routes", File("AccessKeyTests.cs")),
                        Folder("services", File("WorkspaceServiceTests.cs")),
                        Folder("utils", File("NotificationTests.cs"))),
                    File("Session.cs"), File("Program.cs"), File("Schema.cs"), File("LegacySession.cs")),
                File("runtime.jsonc")),
            Folder("Gallery",
                Folder("Views", Folder("Workspace"), Folder("Invitation")),
                Folder("components",
                    Folder("workspace", File("WorkspaceSidebar.axaml"), File("WorkspaceAvatar.axaml")),
                    Folder("access-keys", File("CreateAccessKeyDialog.axaml")),
                    Folder("teams", File("TeamNameEditor.axaml"))),
                Folder("lib", File("AccessKeyClient.cs"))))
    ];

    private static AvaloniaList<AlderFileTreeNode>? FindList(AvaloniaList<AlderFileTreeNode> items, AlderFileTreeNode target)
    {
        if (items.Contains(target)) return items;
        foreach (var item in items)
            if (FindList(item.Children, target) is { } found) return found;
        return null;
    }

    private void MoveFile(AlderFileTreeMove request)
    {
        if (request.Target is not AlderFileTreeNode target || FindList(DragFiles, target) is not { } parent) return;
        var moving = request.Items.OfType<AlderFileTreeNode>()
            .Select(item => (Item: item, Source: FindList(DragFiles, item))).ToArray();
        if (moving.Length == 0 || moving.Any(entry => entry.Source is null || entry.Item == target ||
            FindList(entry.Item.Children, target) is not null)) return;

        var destination = request.Position == FileTreeDropPosition.Into ? target.Children : parent;
        if (request.Position == FileTreeDropPosition.Into && !target.IsFolder) return;
        int index = request.Position == FileTreeDropPosition.Into ? destination.Count :
            destination.IndexOf(target) + (request.Position == FileTreeDropPosition.After ? 1 : 0);
        index -= moving.Count(entry => entry.Source == destination && destination.IndexOf(entry.Item) < index);
        var selected = dragSelection.ToArray();
        if (moving is [{ Item: var item, Source: var source }] && source == destination)
        {
            int oldIndex = destination.IndexOf(item);
            if (index != oldIndex) destination.Move(oldIndex, index);
        }
        else
        {
            foreach (var entry in moving) entry.Source!.Remove(entry.Item);
            destination.InsertRange(index, moving.Select(entry => entry.Item));
        }
        if (request.Position == FileTreeDropPosition.Into) target.IsExpanded = true;
        dragSelection.AddRange(selected.Where(item => !dragSelection.Contains(item)).ToArray());
    }
}
