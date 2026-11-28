using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TreeViewExampleViewModel : GalleryViewModel
{
    public IReadOnlyList<TreeViewExampleNode> Nodes { get; } =
    [
        new("TreeViewProjects", new TreeViewExampleNode("TreeViewDesign", new TreeViewExampleNode("TreeViewResearch")),
            new TreeViewExampleNode("TreeViewEngineering")),
        new("TreeViewArchive", new TreeViewExampleNode("TreeView2025"))
    ];

    public TreeViewExampleNode? SelectedNode { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
}

public sealed class TreeViewExampleNode(string titleKey, params TreeViewExampleNode[] children) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public IReadOnlyList<TreeViewExampleNode> Children { get; } = children;
}
