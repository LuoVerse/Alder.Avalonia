# AlderWorkflowEditor

**English** · [简体中文](README.zh-CN.md)

Open Data Views → Workflow Editor in the Gallery to edit a workflow, test the save command, run a simulation, and import or export JSON. You can also start the standalone sample from the repository root.

```sh
dotnet run --project samples/Alder.Avalonia.WorkflowPrototype
```

- The node library on the left offers single approvals and countersignatures. Drag nodes to arrange them, then use the right panel to set assignees, deadlines, and outgoing connections.
- For single approval and timeout escalation, choose a user or a role with `AlderRadioButtonGroup`, then select one entry from that directory. A countersignature uses `AlderSelect` to select at least two users. Everyone must approve to continue. A rejection follows the rejection edge directly to a regular end node.
- A copy notification uses the same radio group to choose users or roles, then supports multiple selections from that directory. Changing the type clears the previous selection.
- A timed-out single approval can send a reminder, escalate to a user or role, or end the workflow. A timed-out countersignature can remind all pending signers or end the workflow. Existing approvals still count for the current countersignature.
- A condition node uses a field from the host catalog, a comparison operator, a value, and separate destinations for the two outcomes. The sample catalog includes request amount, leave duration, submission date and time, and request type.
- The simulation shows the fields used by the current conditions. If a condition needs missing data, the simulation pauses and reports it. The default threshold is ¥10,000. An amount equal to the threshold goes to department approval, and a larger amount goes to a finance countersignature.
- Export JSON saves the workflow definition with node positions, edges, conditions, and approval and notification settings. Incomplete drafts can be exported. Import JSON restores an editable workflow.
- Request data and approval records live in memory for the current simulation.

## Integrate with an application

After loading `AlderTheme`, bind the host's business collections. Each directory has its own key, and keys must be unique within the directory. User and role `DisplayMemberBinding` expressions evaluate against each record. Dropdown selections contain the original business objects.

```xml
<alder:AlderWorkflowEditor
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:alder="using:Alder.Avalonia.Controls"
    xmlns:vm="using:YourApp.ViewModels"
    x:DataType="vm:WorkflowEditorViewModel"
    Workflow="{CompiledBinding Workflow, Mode=TwoWay}"
    SaveCommand="{CompiledBinding SaveCommand}"
    UserItems="{CompiledBinding Users}"
    UserKey="UserId"
    UserDisplayMemberBinding="{CompiledBinding Label, x:DataType=vm:WorkflowUser}"
    RoleItems="{CompiledBinding Roles}"
    RoleKey="RoleId"
    RoleDisplayMemberBinding="{CompiledBinding Label, x:DataType=vm:WorkflowRole}"
    ConditionalTypes="{CompiledBinding Fields}"
    ConditionalKey="FieldCode"
    ConditionalDisplayMemberPath="FieldName"
    ConditionalValueKindMemberPath="ValueKind" />
```

Bind `Workflow` to an `AlderWorkflowDefinition?` property in the host view model. Replacing the value loads another workflow, and edits update the property. The editor shows a save button when `SaveCommand` is set. Its command argument is an `AlderWorkflowDefinition` snapshot of the nodes, edges, and positions. The host handles persistence and file selection.

The standalone sample loads a demo workflow. Replacing a collection, adding or removing items in an `ObservableCollection<T>`, or notifying changes to a bound label refreshes the displayed names. If a binding yields no label, the editor tries `UserDisplayMemberPath` or `RoleDisplayMemberPath`, then the directory key. Trimmed apps can bind directly to `WorkflowAssigneeOption` and `WorkflowConditionOption` collections.

`ConditionalValueKindMemberPath` accepts a `WorkflowConditionValueKind` value or the corresponding string.

| Kind | Example comparison value | Comparison |
| --- | --- | --- |
| `Number` | `10000`, `2.5` | Greater than, at least, less than, at most, equal, or not equal |
| `Duration` | `16h`, `2d` (16 hours, 2 workdays) | Same operators. One workday is eight hours. |
| `DateTime` | `2026-11-26 17:30` | Same operators. Uses local date and time. |
| `Text` | `Leave request`, `001` | Equal or not equal |
| `Auto` | Infers number, date and time, duration, or text from the comparison value | Used when the field kind is omitted |

```csharp
// Load the bound Workflow property on the host view model.
Workflow = AlderWorkflowDefinition.FromJson(savedJson);

// Serialize the SaveCommand argument for application storage.
string workflowJson = definition.ToJson();
```

The current export uses `schemaVersion: 4`. An approval's `Reject` edge points directly to a regular end node. The `copyRecipientKind` field preserves the notification type even in drafts with an empty recipient list. Single-approval `assignee`, timeout `deadline.escalation`, countersignature `signers`, and notification `copyRecipients` use the same object format.

```json
{ "kind": "Role", "id": "role.purchasing", "label": "Purchasing specialist" }
```

The editor restores selections from `kind` and `id`. Export writes the current directory label. If the directory lacks an entry, the editor retains the label from the file and reports the missing entry in the picker and in `ValidateWorkflow()`. Node destinations are stored in `edges`. Conditions use `fieldKey`, `valueKind`, `operator`, and `value`.

Versions 1 and 2 can be imported. Legacy notification text is stored in `legacyRecipients`. Review it in the right panel, select entries from the directory, and clear the old list. Version 3 restores each notification type from its recipient list. If the list contains both types, the editor shows the original list and keeps the matching entries after a type is chosen. Legacy condition field keys are `Amount`, `LeaveDuration`, `SubmittedAt`, and `RequestType`.

When importing an older workflow, the editor connects rejection edges to the corresponding regular end node. If the workflow only has a legacy return node, it converts that node to a regular end. When several end nodes exist and the destination is ambiguous, choose it in the approval node's rejection dropdown.
