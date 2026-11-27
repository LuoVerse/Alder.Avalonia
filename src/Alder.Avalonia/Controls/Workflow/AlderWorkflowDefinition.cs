using System.Collections.Immutable;

namespace Alder.Avalonia.Controls;

/// <summary>An immutable workflow draft, including the graph, node settings, and canvas positions.</summary>
/// <param name="Id">The stable workflow identifier.</param>
/// <param name="Name">The workflow name.</param>
/// <param name="Nodes">The node definitions.</param>
/// <param name="Edges">The connections between nodes.</param>
public readonly record struct AlderWorkflowDefinition(Guid Id, string Name, ImmutableArray<AlderWorkflowNode> Nodes, ImmutableArray<AlderWorkflowEdge> Edges)
{
    /// <summary>Reads a workflow definition from JSON and upgrades supported older formats.</summary>
    public static AlderWorkflowDefinition FromJson(string json) => WorkflowPersistence.Parse(json);

    /// <summary>Writes the definition in Alder's versioned JSON format.</summary>
    public string ToJson() => WorkflowPersistence.Serialize(this);
}

/// <summary>One node in a workflow, with the settings that apply to its kind.</summary>
/// <param name="Id">The stable node identifier used by edges.</param>
/// <param name="Kind">The node's role in the graph.</param>
/// <param name="Name">The node's display name.</param>
/// <param name="X">The horizontal canvas position in DIPs.</param>
/// <param name="Y">The vertical canvas position in DIPs.</param>
/// <param name="Condition">The rule for a condition node.</param>
/// <param name="Assignee">The selection for a single approval node.</param>
/// <param name="Deadline">The deadline settings for an approval node.</param>
/// <param name="Signers">The individual signers of a countersign node.</param>
/// <param name="CopyRecipients">Recipients selected for a copy node.</param>
/// <param name="CopyRecipientKind">The directory used by a copy node.</param>
/// <param name="LegacyRecipients">An unresolved free-text recipient list from an older definition.</param>
public sealed record AlderWorkflowNode(Guid Id, WorkflowNodeKind Kind, string Name, double X, double Y,
    AlderWorkflowCondition? Condition = null, AlderWorkflowAssignee? Assignee = null, AlderWorkflowDeadline? Deadline = null,
    ImmutableArray<AlderWorkflowAssignee> Signers = default, ImmutableArray<AlderWorkflowAssignee> CopyRecipients = default,
    WorkflowAssigneeKind? CopyRecipientKind = null, string? LegacyRecipients = null);

/// <summary>A directed connection from a node's output to another node.</summary>
/// <param name="From">The origin node ID.</param>
/// <param name="Route">The output on the origin node.</param>
/// <param name="To">The destination node ID.</param>
public readonly record struct AlderWorkflowEdge(Guid From, WorkflowRoute Route, Guid To);

/// <summary>A comparison against a field from the host's condition catalog.</summary>
/// <param name="Operator">The comparison operation.</param>
/// <param name="FieldKey">The stable key of the request field.</param>
/// <param name="ValueKind">How the input and expected values are interpreted.</param>
/// <param name="Value">The comparison value in text form.</param>
public sealed record AlderWorkflowCondition(WorkflowConditionOperator Operator, string? FieldKey, WorkflowConditionValueKind ValueKind, string Value)
{
    /// <summary>Evaluates the comparison. Returns false when either value cannot be interpreted for the requested operation.</summary>
    public bool TryMatch(string? actual, out bool matches) => new ConditionRule(FieldKey, ValueKind, Operator, Value).TryMatch(actual, out matches);
}

/// <summary>The deadline and timeout behavior of an approval node.</summary>
/// <param name="Enabled">Whether the deadline applies.</param>
/// <param name="Hours">The deadline in hours.</param>
/// <param name="Action">What happens when the deadline passes.</param>
/// <param name="Escalation">The recipient when the timeout action escalates.</param>
public sealed record AlderWorkflowDeadline(bool Enabled, int Hours, WorkflowTimeoutAction Action, AlderWorkflowAssignee? Escalation = null);
