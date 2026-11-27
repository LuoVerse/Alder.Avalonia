using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Alder.Avalonia.Gallery.Views;

internal sealed class WorkflowTrialView : UserControl
{
    private readonly AlderWorkflowDefinition definition;
    private readonly Dictionary<Guid, AlderWorkflowNode> nodes;
    private readonly Dictionary<(Guid, WorkflowRoute), Guid> edges;
    private readonly Dictionary<string, string> input = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fieldLabels;
    private readonly List<AlderWorkflowAssignee> waiting = [];
    private readonly StackPanel result = new() { Spacing = 8 };
    private readonly StackPanel trace = new() { Spacing = 6 };
    private AlderWorkflowNode? pending;
    private int required;
    private string endMessage = "WorkflowEditorTrialCompleted";

    public WorkflowTrialView(AlderWorkflowDefinition definition, IEnumerable<WorkflowConditionOption> fields)
    {
        this.definition = definition;
        nodes = definition.Nodes.ToDictionary(node => node.Id);
        edges = definition.Edges.ToDictionary(edge => (edge.From, edge.Route), edge => edge.To);
        fieldLabels = fields.ToDictionary(field => field.Id, field => field.DisplayName, StringComparer.Ordinal);
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(5) };
        foreach (var key in definition.Nodes.Select(node => node.Condition?.FieldKey).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            string label = fieldLabels.GetValueOrDefault(key, key);
            input[key] = key == "Amount" ? "15000" : "";
            var field = new AlderInput { Text = input[key], FullWidth = true };
            field.PropertyChanged += (_, change) =>
            {
                if (change.Property == TextBox.TextProperty) input[key] = field.Text ?? "";
            };
            AutomationProperties.SetName(field, label);
            body.Children.Add(new AlderLabel { Content = label, Target = field });
            body.Children.Add(field);
        }
        body.Children.Add(Action("WorkflowEditorTrialStart", ButtonVariant.Primary, Start));
        body.Children.Add(result);
        body.Children.Add(trace);
        Content = new AlderScrollViewer { Content = body, MaxHeight = 510, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void Start()
    {
        endMessage = "WorkflowEditorTrialCompleted";
        pending = null;
        waiting.Clear();
        ClearResult();
        trace.Children.Clear();
        MoveTo(definition.Nodes.Single(node => node.Kind == WorkflowNodeKind.Start));
    }

    private void MoveTo(AlderWorkflowNode node)
    {
        while (true)
        {
            WorkflowRoute route;
            switch (node.Kind)
            {
                case WorkflowNodeKind.Condition:
                    var rule = node.Condition!;
                    var key = rule.FieldKey!;
                    if (!rule.TryMatch(input.GetValueOrDefault(key), out bool matched))
                    {
                        result.Children.Add(Label(Format("WorkflowEditorTrialMissing", fieldLabels.GetValueOrDefault(key, key)), "AlderDangerInk"));
                        return;
                    }
                    route = matched ? WorkflowRoute.Match : WorkflowRoute.Otherwise;
                    Record(node, Text(matched ? "WorkflowEditorTrialMatched" : "WorkflowEditorTrialOtherwise"));
                    break;
                case WorkflowNodeKind.SingleApproval:
                case WorkflowNodeKind.Countersign:
                    pending = node;
                    waiting.Clear();
                    if (node.Kind == WorkflowNodeKind.SingleApproval) waiting.Add(node.Assignee!);
                    else waiting.AddRange(node.Signers);
                    required = waiting.Count;
                    PresentApproval();
                    return;
                case WorkflowNodeKind.End:
                    string message = Text(endMessage);
                    result.Children.Add(Label(message, endMessage == "WorkflowEditorTrialCompleted" ? "AlderInk" : "AlderDangerInk"));
                    Record(node, message);
                    return;
                default:
                    Record(node, node.Kind == WorkflowNodeKind.Copy
                        ? Format("WorkflowEditorTrialCopied", string.Join("、", node.CopyRecipients.Select(AssigneeLabel))) : null);
                    route = WorkflowRoute.Next;
                    break;
            }
            node = nodes[edges[(node.Id, route)]];
        }
    }

    private void PresentApproval()
    {
        var node = pending!;
        ClearResult();
        result.Children.Add(Label(node.Name, "AlderInk"));
        result.Children.Add(Label(Format("WorkflowEditorTrialProgress", required - waiting.Count, required)));
        var actor = new AlderSelect
        {
            Label = Text("WorkflowEditorTrialAssignee"), FullWidth = true, ItemsSource = waiting.ToArray(),
            TextSelector = static item => item is AlderWorkflowAssignee person ? AssigneeLabel(person) : null, SelectedIndex = 0
        };
        result.Children.Add(actor);
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        actions.Children.Add(Action("WorkflowEditorTrialApprove", ButtonVariant.Primary, () =>
        {
            var selected = (AlderWorkflowAssignee)actor.SelectedItem!;
            Record(node, Format("WorkflowEditorTrialApprovedBy", AssigneeLabel(selected)));
            waiting.Remove(selected);
            if (waiting.Count == 0) FinishApproval(WorkflowRoute.Approve);
            else PresentApproval();
        }));
        actions.Children.Add(Action("WorkflowEditorTrialReject", ButtonVariant.DangerSoft, () =>
        {
            Record(node, Format("WorkflowEditorTrialRejectedBy", AssigneeLabel((AlderWorkflowAssignee)actor.SelectedItem!)));
            FinishApproval(WorkflowRoute.Reject, "WorkflowEditorTrialRejected");
        }));
        if (node.Deadline is { Enabled: true } deadline)
            actions.Children.Add(Action("WorkflowEditorTrialTimeout", ButtonVariant.Secondary, () =>
            {
                if (deadline.Action == WorkflowTimeoutAction.Return)
                {
                    FinishApproval(WorkflowRoute.Reject, "WorkflowEditorTrialTimedOut");
                }
                else if (deadline.Action == WorkflowTimeoutAction.Escalate)
                {
                    waiting[0] = deadline.Escalation!;
                    Record(node, Format("WorkflowEditorTrialEscalated", AssigneeLabel(waiting[0])));
                    PresentApproval();
                }
                else
                    Record(node, Format("WorkflowEditorTrialReminded", string.Join("、", waiting.Select(AssigneeLabel))));
            }));
        result.Children.Add(actions);
    }

    private void FinishApproval(WorkflowRoute route, string? outcome = null)
    {
        var from = pending!.Id;
        pending = null;
        waiting.Clear();
        ClearResult();
        if (outcome is not null) endMessage = outcome;
        MoveTo(nodes[edges[(from, route)]]);
    }

    private void ClearResult()
    {
        if (result.Children.OfType<AlderSelect>().FirstOrDefault() is { IsDropDownOpen: true } picker)
            picker.IsDropDownOpen = false;
        result.Children.Clear();
    }

    private void Record(AlderWorkflowNode node, string? message) =>
        trace.Children.Add(Label(message is null ? node.Name : $"{node.Name} · {message}"));

    private static string AssigneeLabel(AlderWorkflowAssignee person) => person.Label ?? person.Id ?? "";
    private static string Text(string key) => GalleryLocalization.Current.Text(key);
    private static string Format(string key, params object?[] args) => GalleryLocalization.Current.Render(new GalleryMessage(key, args));
    private static AlderTextBlock Label(string text, string brush = "AlderMuted")
    {
        var label = new AlderTextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brush);
        return label;
    }

    private static AlderButton Action(string key, ButtonVariant variant, Action action)
    {
        var button = new AlderButton { Content = Text(key), Variant = variant, Size = AlderSize.Small, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => action();
        return button;
    }
}
