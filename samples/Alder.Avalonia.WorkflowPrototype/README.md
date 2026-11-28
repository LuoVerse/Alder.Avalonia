# AlderWorkflowEditor

**简体中文** · [English](README.en.md)

在 Gallery 的「数据视图 → 工作流编辑器」中可以边编辑边测试保存命令、试跑流程和 JSON 导入导出。也可以从仓库根目录启动独立示例。

```sh
dotnet run --project samples/Alder.Avalonia.WorkflowPrototype
```

- 左侧节点库分别提供「单人审批」和「会签审批」。拖动画布中的节点调整位置，右侧可配置审批对象、时限与连线去向。
- 单人审批和超时转交用 `AlderRadioButtonGroup` 选择「人员」或「角色」，再从对应目录下拉单选。会签使用 `AlderSelect` 多选人员，至少选择两人，全部同意才继续。任一人拒绝都沿拒绝连线直接进入普通结束节点。
- 抄送节点用同一单选组确定「人员」或「角色」，再从当前类型的目录多选。切换类型时清除原类型的选择。
- 单人审批超时可提醒、转交指定人员或角色、结束流程。会签超时可提醒所有待签人员或结束流程，已通过的签名继续计入当前会签。
- 条件节点从宿主提供的目录选择字段，选择比较方式，输入比较值，再分别选择满足与不满足条件的流转节点。示例目录包括申请金额、请假时长、提交日期时间和申请类型。
- 「试跑流程」显示当前条件节点使用的字段。流程遇到缺少数据的条件会暂停并提示。默认按 ¥10,000 分流，等于阈值走部门主管审批，高于阈值走财务会签。
- 「导出 JSON」保存完整流程定义，含节点坐标、连线、条件、审批和抄送配置，未完成的草稿也可以保存。「导入 JSON」恢复为可编辑流程。
- 试跑的申请数据和审批记录保存在本次试跑的内存状态中。

## 接入系统

宿主应用加载 `AlderTheme` 后可以直接绑定业务集合。每个目录使用各自的键，键在本目录内须唯一。人员和角色的 `DisplayMemberBinding` 在每条业务记录上求值，下拉框选中项是原始业务对象。

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

`Workflow` 绑定到宿主视图模型的 `AlderWorkflowDefinition?` 属性，替换属性值即可加载流程。编辑操作会更新该属性。组件在提供 `SaveCommand` 时显示保存按钮，执行命令时传入包含当前节点、连线和坐标的 `AlderWorkflowDefinition` 快照。宿主负责持久化和文件选择。

直接运行示例应用会加载演示流程。集合替换、`ObservableCollection<T>` 增删和绑定标签的属性通知会刷新显示名称。绑定没有可用名称时，依次采用 `UserDisplayMemberPath` 或 `RoleDisplayMemberPath`、目录键。裁剪发布的应用可直接绑定 `WorkflowAssigneeOption` 和 `WorkflowConditionOption` 集合。

`ConditionalValueKindMemberPath` 可映射 `WorkflowConditionValueKind` 枚举或同名字符串。

| 类型 | 比较值示例 | 比较方式 |
| --- | --- | --- |
| `Number` | `10000`、`2.5` | 大于、大于等于、小于、小于等于、等于、不等于 |
| `Duration` | `16 小时`、`2 工作日` | 同上。以 8 小时换算 1 个工作日 |
| `DateTime` | `2026-11-26 17:30` | 同上。使用本地日期和时刻 |
| `Text` | `请假申请`、`001` | 等于、不等于 |
| `Auto` | 按比较值识别数字、日期时间、时长或文本 | 省略字段类型时使用 |

```csharp
// 宿主 ViewModel 的 Workflow 属性，加载后通过绑定传入组件。
Workflow = AlderWorkflowDefinition.FromJson(savedJson);

// SaveCommand 收到的参数，序列化后交给业务存储。
string workflowJson = definition.ToJson();
```

当前导出 `schemaVersion: 4`。审批的 `Reject` 连线直接指向普通结束节点。抄送类型保存在 `copyRecipientKind`，空名单的草稿也能恢复类型。单人审批的 `assignee`、超时转交的 `deadline.escalation`、会签的 `signers` 和抄送的 `copyRecipients` 使用相同的对象格式。

```json
{ "kind": "Role", "id": "role.purchasing", "label": "采购专员" }
```

`kind` 和 `id` 用于恢复选择。导出时会写入当前目录的显示名称。目录缺项时会保留文件中的名称，并在选择框和 `ValidateWorkflow()` 的结果中提示。节点去向保存在 `edges` 中。条件保存 `fieldKey`、`valueKind`、`operator` 和 `value`。

可导入版本 1 和 2 的文件。旧版的抄送文本保存在 `legacyRecipients` 中，可在右侧核对后重新从目录选择，再清除旧版名单。此前版本 3 的抄送节点按名单恢复类型。名单包含两种类型时会显示原名单，选择类型后保留对应名单。旧条件字段键为 `Amount`、`LeaveDuration`、`SubmittedAt` 和 `RequestType`。

导入旧版流程时，编辑器会把拒绝连线接到对应的普通结束节点。只有旧退回节点时，该节点会转换为普通结束节点。旧流程有多个结束节点且无法唯一判断目标时，可在审批节点的「拒绝」下拉框中选择。
