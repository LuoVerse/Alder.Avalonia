using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>The role of a node in the workflow graph.</summary>
public enum WorkflowNodeKind
{
    /// <summary>Accepts a new request.</summary>
    Start,
    /// <summary>Waits for one person or role.</summary>
    SingleApproval,
    /// <summary>Waits for all selected people.</summary>
    Countersign,
    /// <summary>Routes a request according to a comparison.</summary>
    Condition,
    /// <summary>Notifies the selected recipients.</summary>
    Copy,
    /// <summary>Terminates the workflow.</summary>
    End
}

/// <summary>An output on a workflow node.</summary>
public enum WorkflowRoute
{
    /// <summary>Continues to the next node.</summary>
    Next,
    /// <summary>The condition matched.</summary>
    Match,
    /// <summary>The condition did not match.</summary>
    Otherwise,
    /// <summary>The approval succeeded.</summary>
    Approve,
    /// <summary>The approval was rejected and leads directly to an end node.</summary>
    Reject
}

/// <summary>What happens when an approval exceeds its deadline.</summary>
public enum WorkflowTimeoutAction
{
    /// <summary>Notifies pending approvers and waits.</summary>
    Remind,
    /// <summary>Transfers a single approval to another person or role.</summary>
    Escalate,
    /// <summary>Terminates the workflow at the rejection destination.</summary>
    Return
}

internal sealed class WorkflowNode(WorkflowNodeKind kind, string name, Point position)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public WorkflowNodeKind Kind { get; } = kind;
    public string Name { get; set; } = name;
    public Point Position { get; set; } = position;
    public bool IsApproval => Kind is WorkflowNodeKind.SingleApproval or WorkflowNodeKind.Countersign;
    public AlderWorkflowAssignee Assignee { get; set; } = new(WorkflowAssigneeKind.Role);
    public AlderWorkflowAssignee[] Signers { get; set; } = [];
    public ConditionRule Rule { get; set; } = ConditionRule.Empty;
    public int Hours { get; set; } = 24;
    public bool HasDeadline { get; set; } = true;
    public WorkflowTimeoutAction TimeoutAction { get; set; }
    public AlderWorkflowAssignee Escalation { get; set; } = new(WorkflowAssigneeKind.Role);
    // Old mixed recipient lists need an explicit type before they can run.
    public WorkflowAssigneeKind? CopyRecipientKind { get; set; } = WorkflowAssigneeKind.Person;
    public AlderWorkflowAssignee[] Recipients { get; set; } = [];
    public string? LegacyRecipients { get; set; }
    public Dictionary<WorkflowRoute, WorkflowNode> Links { get; } = [];

    public WorkflowRoute? ContinuationRoute => Kind switch
    {
        WorkflowNodeKind.Start or WorkflowNodeKind.Copy => WorkflowRoute.Next,
        WorkflowNodeKind.SingleApproval or WorkflowNodeKind.Countersign => WorkflowRoute.Approve,
        _ => null
    };

    public WorkflowRoute[] EditableRoutes => Kind switch
    {
        WorkflowNodeKind.Start or WorkflowNodeKind.Copy => [WorkflowRoute.Next],
        WorkflowNodeKind.SingleApproval or WorkflowNodeKind.Countersign => [WorkflowRoute.Approve, WorkflowRoute.Reject],
        WorkflowNodeKind.Condition => [WorkflowRoute.Match, WorkflowRoute.Otherwise],
        _ => []
    };

}

internal sealed class WorkflowGraph
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "申请审批流程";
    public List<WorkflowNode> Nodes { get; } = [];
    public WorkflowNode? StartNode => Nodes.FirstOrDefault(node => node.Kind == WorkflowNodeKind.Start);
    public static WorkflowGraph New()
    {
        var workflow = new WorkflowGraph();
        var start = new WorkflowNode(WorkflowNodeKind.Start, "提交申请", new Point(309, 36));
        var finish = new WorkflowNode(WorkflowNodeKind.End, "流程结束", new Point(309, 580));
        workflow.Nodes.AddRange([start, finish]);
        return workflow;
    }

    public IReadOnlyList<string> Validate(WorkflowAssigneeSources sources, IReadOnlyDictionary<string, WorkflowConditionOption> conditions)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("请填写流程名称。");
        if (StartNode is null) errors.Add("请添加开始节点。");

        foreach (var node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name)) errors.Add("有节点尚未命名。");
            foreach (var route in node.EditableRoutes)
            {
                if (!node.Links.TryGetValue(route, out var target))
                    errors.Add($"「{node.Name}」缺少{PortName(route)}去向。");
                else if (route == WorkflowRoute.Reject && target.Kind != WorkflowNodeKind.End)
                    errors.Add($"「{node.Name}」的拒绝出口必须指向结束节点。");
            }

            if (node.Kind == WorkflowNodeKind.Condition)
            {
                var rule = node.Rule;
                if (rule.FieldKey is not { } key)
                    errors.Add($"请为「{node.Name}」选择判断字段。");
                else if (!conditions.TryGetValue(key, out var field))
                    errors.Add($"「{node.Name}」的判断字段「{key}」不可用。");
                else if (field.ValueKind != rule.ValueKind && field.ValueKind != WorkflowConditionValueKind.Auto)
                    errors.Add($"「{node.Name}」的判断字段类型已变更，请重新选择字段。");
                else if (!rule.IsConfigured)
                    errors.Add($"请检查「{node.Name}」的比较方式与比较值。");
            }

            if (node.Kind == WorkflowNodeKind.SingleApproval && sources.Find(node.Assignee) is null)
                errors.Add($"请为「{node.Name}」选择一个可用的审批对象。");
            if (node.Kind == WorkflowNodeKind.Countersign)
            {
                if (node.Signers.Length < 2)
                    errors.Add($"「{node.Name}」需要至少两位会签人员。");
                if (node.Signers.Any(signer => sources.Find(signer) is null))
                    errors.Add($"「{node.Name}」包含不可用的会签人员。");
            }
            if (node.IsApproval && node.HasDeadline && (node.Hours is < 1 or > 720 ||
                node.TimeoutAction == WorkflowTimeoutAction.Escalate && sources.Find(node.Escalation) is null))
                errors.Add($"请补齐「{node.Name}」的超时规则。");
            if (node.Kind == WorkflowNodeKind.Copy)
            {
                if (node.LegacyRecipients is not null)
                    errors.Add($"请核对「{node.Name}」的旧版抄送名单，并从目录选择抄送对象。");
                if (node.CopyRecipientKind is null)
                    errors.Add($"「{node.Name}」的旧名单包含两种类型，请选择抄送人员或角色。");
                else if (node.Recipients.Length == 0 && node.LegacyRecipients is null)
                    errors.Add($"请为「{node.Name}」选择抄送对象。");
                if (node.Recipients.Any(recipient => sources.Find(recipient) is null))
                    errors.Add($"「{node.Name}」包含不可用的抄送对象。");
            }
        }

        if (StartNode is { } start)
        {
            HashSet<WorkflowNode> reached = [];
            HashSet<WorkflowNode> visiting = [];
            Visit(start);
            foreach (var node in Nodes.Except(reached))
                errors.Add($"「{node.Name}」尚未接入从开始节点发起的流程。");

            void Visit(WorkflowNode node)
            {
                if (visiting.Contains(node))
                {
                    errors.Add($"「{node.Name}」形成循环。");
                    return;
                }
                if (!reached.Add(node)) return;
                visiting.Add(node);
                foreach (var next in node.Links.Values) Visit(next);
                visiting.Remove(node);
            }
        }
        return errors;
    }

    public static string PortName(WorkflowRoute route) => route switch
    {
        WorkflowRoute.Approve => "同意",
        WorkflowRoute.Reject => "拒绝",
        WorkflowRoute.Match => "满足条件",
        WorkflowRoute.Otherwise => "不满足条件",
        _ => "下一步"
    };
}
