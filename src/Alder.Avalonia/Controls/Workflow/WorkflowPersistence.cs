using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>Converts workflow definitions to and from the versioned JSON format used by the editor.</summary>
internal static class WorkflowPersistence
{
    private const int Version = 4;

    public static AlderWorkflowDefinition Capture(WorkflowGraph workflow, WorkflowAssigneeSources? sources = null)
    {
        var edges = ImmutableArray.CreateBuilder<AlderWorkflowEdge>();
        foreach (var node in workflow.Nodes)
            foreach (var (route, target) in node.Links.OrderBy(link => link.Key))
                edges.Add(new(node.Id, route, target.Id));
        return new(workflow.Id, workflow.Name, workflow.Nodes.Select(node => CaptureNode(node, sources)).ToImmutableArray(), edges.ToImmutable());
    }

    public static string Serialize(AlderWorkflowDefinition definition) =>
        JsonSerializer.Serialize(ToFile(definition), WorkflowJsonContext.Default.WorkflowFile);

    public static AlderWorkflowDefinition Parse(string json) =>
        Capture(ReadFile(JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowFile)));

    public static WorkflowGraph Restore(AlderWorkflowDefinition definition) => ReadFile(ToFile(definition));

    private static WorkflowFile ToFile(AlderWorkflowDefinition definition)
    {
        if (definition.Nodes.IsDefault || definition.Edges.IsDefault)
            throw new InvalidDataException("工作流文件缺少节点或连线集合。");

        return new WorkflowFile(Version, definition.Id, definition.Name, definition.Nodes.Select(node =>
            node is null ? null! : new WorkflowNodeFile(node.Id, node.Kind, node.Name, node.X, node.Y,
                Condition: node.Condition is { } condition ? new(condition.Operator, condition.FieldKey, condition.ValueKind, condition.Value) : null,
                Assignee: node.Assignee,
                Deadline: node.Deadline,
                Signers: node.Signers.IsDefault ? null : node.Signers.ToArray(),
                CopyRecipients: node.CopyRecipients.IsDefault ? null : node.CopyRecipients.ToArray(),
                LegacyRecipients: node.LegacyRecipients,
                CopyRecipientKind: node.CopyRecipientKind)).ToArray(),
            definition.Edges.Select(edge => new WorkflowEdgeFile(edge.From, edge.Route, edge.To)).ToArray());
    }

    private static WorkflowGraph ReadFile(WorkflowFile? file)
    {
        if (file is null || file.Id == Guid.Empty || file.Name is null || file.Nodes is null || file.Edges is null)
            throw new InvalidDataException("工作流文件缺少基本数据。");
        if (file.SchemaVersion is < 1 or > Version)
            throw new InvalidDataException($"不支持工作流文件版本 {file.SchemaVersion}。");

        var workflow = new WorkflowGraph { Id = file.Id, Name = file.Name };
        Dictionary<Guid, WorkflowNode> nodes = [];
        WorkflowNode? legacyEnd = null;
        foreach (var item in file.Nodes)
        {
            if (item is null || item.Id == Guid.Empty || item.Name is null || !Enum.IsDefined(item.Kind) ||
                !WorkflowCanvas.ContainsNodePosition(new Point(item.X, item.Y)) ||
                item.IsReturn && (file.SchemaVersion >= 4 || item.Kind != WorkflowNodeKind.End || legacyEnd is not null))
                throw new InvalidDataException("工作流文件包含无效的节点。");

            var node = ReadNode(item, file.SchemaVersion);
            if (!nodes.TryAdd(node.Id, node))
                throw new InvalidDataException("工作流文件包含重复的节点 ID。");
            workflow.Nodes.Add(node);
            if (item.IsReturn) legacyEnd = node;
        }
        if (workflow.Nodes.Count(node => node.Kind == WorkflowNodeKind.Start) > 1)
            throw new InvalidDataException("工作流只能有一个开始节点。");

        HashSet<(Guid, WorkflowRoute)> seen = [];
        foreach (var edge in file.Edges)
        {
            if (edge is null || !nodes.TryGetValue(edge.From, out var from) || !nodes.TryGetValue(edge.To, out var to) ||
                from == to || !seen.Add((edge.From, edge.Route)))
                throw new InvalidDataException("工作流文件包含无效或重复的连线。");

            if (edge.Route == WorkflowRoute.Reject && to.Kind != WorkflowNodeKind.End)
                throw new InvalidDataException("拒绝连线必须指向结束节点。");
            if (!from.EditableRoutes.Contains(edge.Route) || to.Kind == WorkflowNodeKind.Start ||
                to == legacyEnd && edge.Route != WorkflowRoute.Reject)
                throw new InvalidDataException("工作流文件中的连线与节点类型不匹配。");
            from.Links.Add(edge.Route, to);
        }
        if (file.SchemaVersion < 4) MigrateRejectLinks(workflow, legacyEnd);
        return workflow;
    }

    private static void MigrateRejectLinks(WorkflowGraph workflow, WorkflowNode? legacyEnd)
    {
        var ends = workflow.Nodes.Where(node => node.Kind == WorkflowNodeKind.End && node != legacyEnd).ToArray();
        if (legacyEnd is not null && ends.Length == 0)
        {
            if (legacyEnd.Name == "退回提交者") legacyEnd.Name = "流程结束";
            ends = [legacyEnd];
        }
        else if (legacyEnd is not null)
            workflow.Nodes.Remove(legacyEnd);

        foreach (var node in workflow.Nodes.Where(node => node.IsApproval))
        {
            if (node.Links.TryGetValue(WorkflowRoute.Reject, out var previous) && previous != legacyEnd) continue;
            if ((ends.Length == 1 ? ends[0] : FindUniqueEndAfterApproval(node)) is { } target)
                node.Links[WorkflowRoute.Reject] = target;
            else
                node.Links.Remove(WorkflowRoute.Reject);
        }
    }

    private static WorkflowNode? FindUniqueEndAfterApproval(WorkflowNode approval)
    {
        if (!approval.Links.TryGetValue(WorkflowRoute.Approve, out var next)) return null;
        Stack<WorkflowNode> queue = new([next]);
        HashSet<WorkflowNode> seen = [];
        WorkflowNode? end = null;
        while (queue.TryPop(out var node))
        {
            if (!seen.Add(node)) continue;
            if (node.Kind == WorkflowNodeKind.End)
            {
                if (end is not null) return null;
                end = node;
                continue;
            }
            foreach (var (route, target) in node.Links)
                if (route != WorkflowRoute.Reject) queue.Push(target);
        }
        return end;
    }

    private static AlderWorkflowNode CaptureNode(WorkflowNode node, WorkflowAssigneeSources? sources) => new(node.Id, node.Kind, node.Name, node.Position.X, node.Position.Y,
        Condition: node.Kind == WorkflowNodeKind.Condition ? new(node.Rule.Operator, node.Rule.FieldKey, node.Rule.ValueKind, node.Rule.ExpectedValue) : null,
        Assignee: node.Kind == WorkflowNodeKind.SingleApproval ? WithCurrentLabel(node.Assignee, sources) : null,
        Deadline: node.IsApproval ? new AlderWorkflowDeadline(node.HasDeadline, node.Hours, node.TimeoutAction,
            node.Kind == WorkflowNodeKind.SingleApproval ? WithCurrentLabel(node.Escalation, sources) : null) : null,
        Signers: node.Kind == WorkflowNodeKind.Countersign ? node.Signers.Select(signer => WithCurrentLabel(signer, sources)).ToImmutableArray() : default,
        CopyRecipients: node.Kind == WorkflowNodeKind.Copy ? node.Recipients.Select(recipient => WithCurrentLabel(recipient, sources)).ToImmutableArray() : default,
        LegacyRecipients: node.LegacyRecipients,
        CopyRecipientKind: node.Kind == WorkflowNodeKind.Copy ? node.CopyRecipientKind : null);

    private static AlderWorkflowAssignee WithCurrentLabel(AlderWorkflowAssignee reference, WorkflowAssigneeSources? sources) =>
        sources?.Find(reference) is { } option && option.DisplayName != reference.Label ? reference with { Label = option.DisplayName } : reference;

    private static WorkflowNode ReadNode(WorkflowNodeFile item, int version)
    {
        var node = new WorkflowNode(item.Kind, item.Name, new Point(item.X, item.Y)) { Id = item.Id };
        bool legacy = version < 3;
        if (legacy && (item.Signers is not null || item.CopyRecipients is not null || item.LegacyRecipients is not null || item.CopyRecipientKind is not null) ||
            !legacy && (item.SignerIds is not null || item.Recipients is not null))
            throw new InvalidDataException("审批或抄送数据与文件版本不匹配。");

        var signers = legacy ? item.SignerIds?.Select(id => new AlderWorkflowAssignee(WorkflowAssigneeKind.Person, id)).ToArray() : item.Signers;
        var recipients = legacy && item.Recipients is not null ? [] : item.CopyRecipients;
        var legacyRecipients = legacy ? item.Recipients : item.LegacyRecipients;
        if ((node.Kind == WorkflowNodeKind.Condition) != (item.Condition is not null) ||
            (node.Kind == WorkflowNodeKind.SingleApproval) != (item.Assignee is not null) ||
            (node.Kind == WorkflowNodeKind.Countersign) != (signers is not null) ||
            node.IsApproval != (item.Deadline is not null) ||
            (node.Kind == WorkflowNodeKind.Copy) != (recipients is not null) ||
            node.Kind != WorkflowNodeKind.Copy && (legacyRecipients is not null || item.CopyRecipientKind is not null))
            throw new InvalidDataException($"「{item.Name}」的配置与节点类型不匹配。");

        if (item.Condition is { } condition) node.Rule = ReadCondition(condition, version);
        if (item.Assignee is { } assignee)
        {
            CheckAssignee(assignee);
            node.Assignee = assignee;
        }
        if (signers is not null)
        {
            CheckSelections(signers, WorkflowAssigneeKind.Person);
            node.Signers = signers;
        }
        if (item.Deadline is { } deadline)
        {
            if (deadline.Hours is < 1 or > 720 || !Enum.IsDefined(deadline.Action) ||
                node.Kind == WorkflowNodeKind.Countersign && (deadline.Action == WorkflowTimeoutAction.Escalate || deadline.Escalation is not null))
                throw new InvalidDataException("审批时限配置无效。");
            node.HasDeadline = deadline.Enabled;
            node.Hours = deadline.Hours;
            node.TimeoutAction = deadline.Action;
            if (deadline.Escalation is { } escalation)
            {
                CheckAssignee(escalation);
                node.Escalation = escalation;
            }
        }
        if (recipients is not null)
        {
            var kind = item.CopyRecipientKind;
            if (kind is { } specified && !Enum.IsDefined(specified))
                throw new InvalidDataException("抄送对象类型无效。");
            CheckSelections(recipients, kind);
            node.Recipients = recipients;
            node.LegacyRecipients = string.IsNullOrWhiteSpace(legacyRecipients) ? null : legacyRecipients;
            if (kind is null && recipients.Length > 0)
            {
                var first = recipients[0].Kind;
                node.CopyRecipientKind = recipients.All(recipient => recipient.Kind == first) ? first : null;
            }
            else
                node.CopyRecipientKind = kind ?? WorkflowAssigneeKind.Person;
        }
        return node;
    }

    private static void CheckAssignee(AlderWorkflowAssignee assignee)
    {
        if (!Enum.IsDefined(assignee.Kind) || assignee.Id is { } id && string.IsNullOrWhiteSpace(id) ||
            assignee.Label is { } label && (assignee.Id is null || string.IsNullOrWhiteSpace(label)))
            throw new InvalidDataException("审批对象需要有效的目录类型和 ID。");
    }

    private static void CheckSelections(AlderWorkflowAssignee[] selections, WorkflowAssigneeKind? requiredKind = null)
    {
        HashSet<(WorkflowAssigneeKind, string)> keys = [];
        foreach (var reference in selections)
        {
            if (reference?.Id is not { } id || requiredKind.HasValue && reference.Kind != requiredKind || !keys.Add((reference.Kind, id)))
                throw new InvalidDataException("所选对象需要唯一且有效的类型和 ID。");
            CheckAssignee(reference);
        }
    }

    private static ConditionRule ReadCondition(ConditionFile item, int version)
    {
        ConditionRule rule;
        if (version == 1)
        {
            var number = item.Number?.ToString(CultureInfo.InvariantCulture) ?? "";
            var date = item.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var time = item.Time?.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
            rule = item.Field switch
            {
                LegacyConditionField.Amount when item.Number is not (< 0 or > 10_000_000) =>
                    new("Amount", WorkflowConditionValueKind.Number, item.Operator, number),
                LegacyConditionField.LeaveDuration when item.Number is not (< 0 or > 8_760) && item.Unit is { } unit && Enum.IsDefined(unit) =>
                    new("LeaveDuration", WorkflowConditionValueKind.Duration, item.Operator,
                        item.Number is null || unit == LegacyDurationUnit.Hour ? number : number + " 工作日"),
                LegacyConditionField.SubmittedAt => new("SubmittedAt", WorkflowConditionValueKind.DateTime, item.Operator,
                    (date + " " + time).Trim()),
                LegacyConditionField.RequestType when item.Type is { } text =>
                    new("RequestType", WorkflowConditionValueKind.Text, item.Operator, text),
                _ => throw new InvalidDataException("条件字段或阈值无效。")
            };
        }
        else
        {
            if (item.ValueKind is not { } kind || !Enum.IsDefined(kind) || item.Value is null ||
                item.FieldKey is { } key && string.IsNullOrWhiteSpace(key))
                throw new InvalidDataException("条件字段或比较值无效。");
            rule = new(item.FieldKey, kind, item.Operator, item.Value);
        }
        if (!rule.Operators.Contains(item.Operator))
            throw new InvalidDataException("条件字段不支持该比较方式。");
        return rule;
    }
}

internal sealed record WorkflowFile(int SchemaVersion, Guid Id, string Name, WorkflowNodeFile[] Nodes, WorkflowEdgeFile[] Edges);
internal sealed record WorkflowEdgeFile(Guid From, WorkflowRoute Route, Guid To);
internal sealed record WorkflowNodeFile(Guid Id, WorkflowNodeKind Kind, string Name, double X, double Y,
    ConditionFile? Condition = null, AlderWorkflowAssignee? Assignee = null, string[]? SignerIds = null,
    AlderWorkflowDeadline? Deadline = null, string? Recipients = null, AlderWorkflowAssignee[]? Signers = null,
    AlderWorkflowAssignee[]? CopyRecipients = null, string? LegacyRecipients = null, WorkflowAssigneeKind? CopyRecipientKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsReturn = false);
internal sealed record ConditionFile(WorkflowConditionOperator Operator, string? FieldKey = null, WorkflowConditionValueKind? ValueKind = null, string? Value = null,
    LegacyConditionField? Field = null, decimal? Number = null, LegacyDurationUnit? Unit = null,
    DateOnly? Date = null, TimeOnly? Time = null, string? Type = null);
internal enum LegacyConditionField { Amount, LeaveDuration, SubmittedAt, RequestType }
internal enum LegacyDurationUnit { Hour, Workday }
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(WorkflowFile))]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext;
