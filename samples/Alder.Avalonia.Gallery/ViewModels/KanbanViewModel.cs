using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record KanbanTaskSample(string Title, string Description, double Progress, string Label, IImage? Artwork,
    string Initials, AlderStatusColor Status, bool ShowProgress)
{
    public bool HasDescription => Description.Length > 0;
    public string ProgressText => (Progress / 100).ToString("P0");
    public AvatarColor AssigneeColor => Status switch
    {
        AlderStatusColor.Accent => AvatarColor.Accent,
        AlderStatusColor.Success => AvatarColor.Success,
        AlderStatusColor.Warning => AvatarColor.Warning,
        AlderStatusColor.Danger => AvatarColor.Danger,
        _ => AvatarColor.Default
    };
}

public sealed class KanbanColumnSample
{
    public KanbanColumnSample(string key, string title, Color tint, IEnumerable<KanbanTaskSample> cards, Func<KanbanTaskSample>? create, string? addLabel)
    {
        Key = key;
        Title = title;
        Indicator = new SolidColorBrush(tint);
        Tasks = new(cards);
        AddLabel = addLabel;
        Add = create is null ? null : ReactiveCommand.Create(() => Tasks.Add(create()));
    }

    public string Key { get; }
    public string Title { get; }
    public IBrush Indicator { get; }
    public ObservableCollection<KanbanTaskSample> Tasks { get; }
    public string? AddLabel { get; }
    public ICommand? Add { get; }
}

public sealed class KanbanBoardSample
{
    public KanbanBoardSample(params KanbanColumnSample[] columns)
    {
        Columns = columns;
        Move = ReactiveCommand.Create<KanbanMoveRequest>(ApplyMove);
    }

    public IReadOnlyList<KanbanColumnSample> Columns { get; }
    public ICommand Move { get; }

    private void ApplyMove(KanbanMoveRequest request)
    {
        var source = Columns.FirstOrDefault(column => Equals(column.Key, request.SourceColumn));
        var target = Columns.FirstOrDefault(column => Equals(column.Key, request.TargetColumn));
        if (source is null || target is null || request.Item is not KanbanTaskSample task) return;
        int index = source.Tasks.IndexOf(task);
        if (index < 0) return;
        int insertion = Math.Clamp(request.TargetIndex, 0, target.Tasks.Count - (source == target ? 1 : 0));
        if (source == target) source.Tasks.Move(index, insertion);
        else
        {
            source.Tasks.RemoveAt(index);
            target.Tasks.Insert(insertion, task);
        }
    }
}

public sealed class KanbanViewModel : GalleryViewModel
{
    public KanbanViewModel()
    {
        IImage[] artwork = [Load("cherries.jpeg"), Load("oranges.jpeg"), Load("robot1.jpeg")];
        Usage = CreateUsage(artwork, includeAllColumns: true);
        Notion = CreateNotion();
        Project = CreateProject();
        Compact = CreateUsage(artwork, includeAllColumns: false);
        Regular = CreateUsage(artwork, includeAllColumns: false);
        Spacious = CreateUsage(artwork, includeAllColumns: false);
    }

    public KanbanBoardSample Usage { get; }
    public KanbanBoardSample Notion { get; }
    public KanbanBoardSample Project { get; }
    public KanbanBoardSample Compact { get; }
    public KanbanBoardSample Regular { get; }
    public KanbanBoardSample Spacious { get; }

    private KanbanBoardSample CreateUsage(IReadOnlyList<IImage> artwork, bool includeAllColumns)
    {
        List<KanbanTaskSample> open =
        [
            Card("UsageTask1", Text("ProKanbanUsageTask1Detail"), "TagLowPriority", "A", AlderStatusColor.Success, 0, 0),
            Card("UsageTask2", Text("ProKanbanUsageTask2Detail"), "TagHighPriority", "L", AlderStatusColor.Danger),
            Card("UsageTask3", Text("ProKanbanUsageTask3Detail"), "TagFeature", "O", AlderStatusColor.Accent, 20)
        ];
        if (includeAllColumns)
            open.Add(Card("UsageTask4", Text("ProKanbanUsageTask4Detail"), "TagBug", "D", AlderStatusColor.Danger));

        var columns = new List<KanbanColumnSample>
        {
            Column("UsageColumn1", "#30A96E", false, open.ToArray()),
            Column("UsageColumn2", "#D88C25", false,
                Card("UsageTask5", Text("ProKanbanUsageTask5Detail"), "TagDesign", "D", AlderStatusColor.Accent, 45, 1),
                Card("UsageTask6", Text("ProKanbanUsageTask6Detail"), "TagBug", "M", AlderStatusColor.Danger, 70, -1))
        };
        if (includeAllColumns)
            columns.Add(Column("UsageColumn3", "#BD525C", false,
                Card("UsageTask7", Text("ProKanbanUsageTask7Detail"), "TagDone", "N", AlderStatusColor.Success, 100, 2),
                Card("UsageTask8", Text("ProKanbanUsageTask8Detail"), "TagFeature", "O", AlderStatusColor.Accent, 100, -1)));
        return new(columns.ToArray());

        KanbanTaskSample Card(string key, string detail, string tag, string avatar, AlderStatusColor color,
            double? progress = null, int image = -1) =>
            MakeCard(key, detail, tag, avatar, color, progress, image < 0 ? null : artwork[image]);
    }

    private KanbanBoardSample CreateNotion() => new(
        Column("NotionColumn1", "#2E88BD", false,
                MakeCard("NotionTask1", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask2", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask3", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning),
                MakeCard("NotionTask4", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning),
                MakeCard("NotionTask5", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning),
                MakeCard("NotionTask6", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning),
                MakeCard("NotionTask7", Text("ProKanbanNotionTrack"), "TagMedium", "M", AlderStatusColor.Warning)),
        Column("NotionColumn2", "#D88C25", false,
                MakeCard("NotionTask8", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask9", Text("ProKanbanNotionTrack"), "TagLow", "S", AlderStatusColor.Success),
                MakeCard("NotionTask10", Text("ProKanbanNotionTrack"), "TagLow", "S", AlderStatusColor.Success),
                MakeCard("NotionTask11", Text("ProKanbanNotionTrack"), "TagLow", "S", AlderStatusColor.Success)),
        Column("NotionColumn3", "#BD525C", false,
                MakeCard("NotionTask12", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask13", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask14", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask15", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask16", Text("ProKanbanNotionTrack"), "TagHigh", "D", AlderStatusColor.Danger),
                MakeCard("NotionTask17", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning),
                MakeCard("NotionTask18", Text("ProKanbanNotionTrack"), "TagMedium", "D", AlderStatusColor.Warning)),
        Column("NotionColumn4", "#30A96E", false,
                MakeCard("NotionTask19", Text("ProKanbanNotionTrack"), "TagHigh", "E", AlderStatusColor.Danger),
                MakeCard("NotionTask20", Text("ProKanbanNotionTrack"), "TagMedium", "S", AlderStatusColor.Warning)));

    private KanbanBoardSample CreateProject() => new(
        Column("ProjectColumn1", "#838491", true,
                MakeCard("ProjectTask1", "", "TagResearch", "S", AlderStatusColor.Default),
                MakeCard("ProjectTask2", "", "TagEngineering", "A", AlderStatusColor.Default)),
        Column("ProjectColumn2", "#2E88BD", true,
                MakeCard("ProjectTask3", Text("ProKanbanProjectTask3Detail"), "TagUX", "E", AlderStatusColor.Default),
                MakeCard("ProjectTask4", Text("ProKanbanProjectTask4Detail"), "TagEngineering", "J", AlderStatusColor.Default)),
        Column("ProjectColumn3", "#D88C25", true,
                MakeCard("ProjectTask5", Text("ProKanbanProjectTask5Detail"), "TagUX", "M", AlderStatusColor.Default, 43),
                MakeCard("ProjectTask6", "", "TagEngineering", "J", AlderStatusColor.Default, 60),
                MakeCard("ProjectTask7", "", "TagResearch", "S", AlderStatusColor.Default)),
        Column("ProjectColumn4", "#8365BD", true,
                MakeCard("ProjectTask8", "", "TagUX", "S", AlderStatusColor.Default),
                MakeCard("ProjectTask9", "", "TagEngineering", "J", AlderStatusColor.Default),
                MakeCard("ProjectTask10", "", "TagDocs", "S", AlderStatusColor.Default)),
        Column("ProjectColumn5", "#30A96E", true,
                MakeCard("ProjectTask11", "", "TagEngineering", "M", AlderStatusColor.Default),
                MakeCard("ProjectTask12", "", "TagEngineering", "E", AlderStatusColor.Default),
                MakeCard("ProjectTask13", "", "TagUX", "A", AlderStatusColor.Default)));

    private KanbanColumnSample Column(string key, string color, bool readOnly, params KanbanTaskSample[] cards) =>
        new(key, Text($"ProKanban{key}"), Color.Parse(color), cards, readOnly ? null : CreateTask,
            readOnly ? null : Text(key.StartsWith("Notion", StringComparison.Ordinal) ? "ProKanbanNewTask" : "ProKanbanAdd"));

    private KanbanTaskSample CreateTask() =>
        new(Text("ProKanbanUntitled"), Text("ProKanbanUntitledDetail"), 0, Text("ProKanbanTagTask"),
            null, "A", AlderStatusColor.Default, false);

    private KanbanTaskSample MakeCard(string key, string detail, string tag, string avatar, AlderStatusColor color,
        double? progress = null, IImage? image = null) =>
        new(Text($"ProKanban{key}"), detail, progress ?? 0, Text($"ProKanban{tag}"), image, avatar, color, progress.HasValue);

    private static Bitmap Load(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Card/{name}"));
        return Bitmap.DecodeToWidth(stream, 420);
    }
}
