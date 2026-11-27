using Alder.Avalonia.Controls;

namespace Alder.Avalonia.Gallery.ViewModels;

internal static class WorkflowExampleData
{
    public static WorkflowAssigneeOption[] Users { get; } =
        [new("person.lin", "林晨"), new("person.shen", "沈宁"), new("person.zhou", "周然")];
    public static WorkflowAssigneeOption[] Roles { get; } =
        [new("role.manager", "部门主管"), new("role.finance-director", "财务总监"), new("role.purchasing", "采购专员")];
    public static WorkflowConditionOption[] Conditions { get; } =
    [
        new("Amount", "申请金额（元）", WorkflowConditionValueKind.Number),
        new("LeaveDuration", "请假时长", WorkflowConditionValueKind.Duration),
        new("SubmittedAt", "提交时间", WorkflowConditionValueKind.DateTime),
        new("RequestType", "申请类型", WorkflowConditionValueKind.Text)
    ];

    public static AlderWorkflowDefinition Create()
    {
        var start = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.Start, "提交申请", 309, 36);
        var condition = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.Condition, "按申请条件分流", 309, 152,
            Condition: new(WorkflowConditionOperator.Greater, "Amount", WorkflowConditionValueKind.Number, "10000"));
        var manager = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.SingleApproval, "部门主管审批", 99, 308,
            Assignee: new(WorkflowAssigneeKind.Role, "role.manager", "部门主管"), Deadline: new(true, 24, WorkflowTimeoutAction.Return));
        var finance = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.Countersign, "财务会签", 519, 308,
            Deadline: new(true, 48, WorkflowTimeoutAction.Remind),
            Signers: [new(WorkflowAssigneeKind.Person, "person.lin", "林晨"), new(WorkflowAssigneeKind.Person, "person.shen", "沈宁")]);
        var copy = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.Copy, "抄送采购", 309, 458,
            CopyRecipients: [new(WorkflowAssigneeKind.Role, "role.purchasing", "采购专员")], CopyRecipientKind: WorkflowAssigneeKind.Role);
        var end = new AlderWorkflowNode(Guid.NewGuid(), WorkflowNodeKind.End, "流程结束", 309, 580);
        return new(Guid.NewGuid(), "申请审批流程", [start, condition, manager, finance, copy, end],
        [
            new(start.Id, WorkflowRoute.Next, condition.Id),
            new(condition.Id, WorkflowRoute.Otherwise, manager.Id), new(condition.Id, WorkflowRoute.Match, finance.Id),
            new(manager.Id, WorkflowRoute.Approve, copy.Id), new(manager.Id, WorkflowRoute.Reject, end.Id),
            new(finance.Id, WorkflowRoute.Approve, copy.Id), new(finance.Id, WorkflowRoute.Reject, end.Id),
            new(copy.Id, WorkflowRoute.Next, end.Id)
        ]);
    }
}
