namespace Alder.Avalonia.Controls;

/// <summary>Identifies the catalog used by an approval or copy recipient.</summary>
public enum WorkflowAssigneeKind
{
    /// <summary>Identifies an item from the role catalog.</summary>
    Role,
    /// <summary>Identifies an item from the user catalog.</summary>
    Person
}

/// <summary>A selectable approval assignee, identified by the host system's stable ID.</summary>
public sealed record WorkflowAssigneeOption(string Id, string DisplayName)
{
    internal object? SourceItem { get; init; }
    internal object PickerItem => SourceItem ?? this;
    internal bool IsUnavailable { get; init; }
}

/// <summary>A saved selection. The label remains available when the directory entry is missing.</summary>
/// <param name="Kind">The catalog containing the entry.</param>
/// <param name="Id">The stable entry ID, or null for a draft without a selection.</param>
/// <param name="Label">The most recently known display label.</param>
public sealed record AlderWorkflowAssignee(WorkflowAssigneeKind Kind, string? Id = null, string? Label = null);

/// <summary>Supplies the external role and person catalogs used by the workflow.</summary>
internal sealed class WorkflowAssigneeSources
{
    public WorkflowAssigneeSources(IEnumerable<WorkflowAssigneeOption> roles, IEnumerable<WorkflowAssigneeOption> people)
    {
        Roles = Snapshot(roles, nameof(roles));
        People = Snapshot(people, nameof(people));
    }

    public IReadOnlyList<WorkflowAssigneeOption> Roles { get; }
    public IReadOnlyList<WorkflowAssigneeOption> People { get; }

    internal IReadOnlyList<WorkflowAssigneeOption> For(WorkflowAssigneeKind kind) => kind == WorkflowAssigneeKind.Role ? Roles : People;

    internal WorkflowAssigneeOption? Find(WorkflowAssigneeKind kind, string? id) =>
        For(kind).FirstOrDefault(option => option.Id == id);

    internal WorkflowAssigneeOption? Find(AlderWorkflowAssignee selection) => Find(selection.Kind, selection.Id);

    internal string Name(AlderWorkflowAssignee selection) => Find(selection)?.DisplayName ?? selection.Label ?? selection.Id ?? "";

    private static IReadOnlyList<WorkflowAssigneeOption> Snapshot(IEnumerable<WorkflowAssigneeOption> source, string parameter)
    {
        ArgumentNullException.ThrowIfNull(source, parameter);
        var options = source.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (option is null || string.IsNullOrWhiteSpace(option.Id) || string.IsNullOrWhiteSpace(option.DisplayName) || !ids.Add(option.Id))
                throw new ArgumentException("审批目录中的每一项都需要唯一的 ID 和有效的显示名称。", parameter);
        }
        return Array.AsReadOnly(options);
    }
}
