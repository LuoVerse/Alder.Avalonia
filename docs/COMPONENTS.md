# 组件目录

所有组件位于 `Alder.Avalonia.Controls`。在应用中加载 [AlderTheme](../README.md#在-avalonia-应用中使用) 后，即可用 AXAML 或 C# 组合组件。控件公开属性、事件和枚举的详细定义以代码及构建生成的 XML API 文档为准。

## 接入指南

| 任务 | 指南 |
| --- | --- |
| 构建、编辑、保存和重开富文本 | [Rich Text Editor](guides/rich-text-editor.md) |
| 绑定字段、日期、选择、颜色和文件 | [表单、选择与文件输入](guides/forms.md) |
| 处理表格选择、看板拖拽、日程编辑和地图视口 | [表格、看板、日程与地图](guides/data-controls.md) |
| 显示流式 Markdown、恢复会话、接入工具和附件 | [AI 会话与 Markdown](guides/chat-and-markdown.md) |
| 组织序列、配置提示和恢复图表 | [图表](guides/charts.md) |
| 恢复页面、布局尺寸，接入命令和操作栏 | [导航、浮层和操作栏](guides/navigation-and-actions.md) |

### 图标属性

Alder 组件的 `Icon` 接受 Geometry 资源、路径字符串或 Avalonia 图标控件。菜单与标签项沿用 Avalonia 的 `Icon`，并使用 Alder 主题中的图标槽。命令项、文件树节点、Emoji 分类、Markdown 块操作和弹层选项的 `Icon` 也接受这些值。

在应用项目中引用 `Material.Icons.Avalonia` 和 `FluentIcons.Avalonia`。使用 Material 时，在 `Application.Styles` 中加入 `<mi:MaterialIconStyles />`。

```xml
xmlns:mi="using:Material.Icons.Avalonia"
xmlns:fi="using:FluentIcons.Avalonia.Markup"

<alder:AlderButton Content="Geometry" Icon="{StaticResource AlderIconInfo}" />
<alder:AlderButton Content="Material" Icon="{mi:MaterialIconExt Kind=Info}" />
<alder:AlderButton Content="Fluent" Icon="{fi:SymbolIcon Symbol=Info}" />
```

主题决定图标槽位。按钮和 Item Card 的 Small、Medium、Large 分别使用 14、16、20 DIP。Rating 使用 16、20、28 DIP，并保留自定义图标的分数填充。Badge 和 Chip 的 `IconSize` 默认随尺寸档位变化，显式设置仍然生效。图标等比缩放，自定义的 `IconTemplate` 继续接收原始数据。

窗口的内置标题栏图标继续使用主题提供的 Geometry。

控件类型的图标通常每个独立组件使用一个实例。Rating、Bloom Menu 过渡和 Markdown 块操作会复用图标绘制。更新库后请重新编译调用方，读取 `Icon` 为 Geometry 的 C# 代码可用 `is Geometry geometry` 匹配。

## 在表单中选择组件

| 场景 | 控件 | 主要用法 |
| --- | --- | --- |
| 普通选项 | `AlderSelect` | 通过 `ItemsSource` 提供集合，以 `SelectedItem` 或 `SelectedIndex` 绑定选择 |
| 句子或工具栏中就地切换 | `AlderInlineSelect` | 使用小型触发器，可用 `DropDownWidth`、`DropDownPlacement` 和 `MaxDropDownHeight` 调整弹层 |
| 设置列表中的选择 | `AlderCellSelect` | 使用 `Label` 给出设置含义，绑定选择值，使用 `Variant` 调整表面 |
| 可输入或可搜索 | `AlderComboBox`、`AlderAutocomplete` | 分别检查文本、选中值、建议列表和自定义值的绑定 |
| 颜色选择和设置单元格 | `AlderColorPicker`、`AlderCellColorPicker` | 绑定 `Value`，用 `CommitOnApply` 控制发布时机。Cell 还可传入 `Presets` |
| 一组始终可见的互斥选项 | `AlderSegment` | 用 `AlderSegmentItem.Key` 作为稳定标识，以 `SelectedKey` 绑定 |
| 选项各自带操作 | `AlderDropdownFlyout`、`AlderMenuFlyout` | 将 `AlderMenuItem` 绑定到命令，用于动作菜单与上下文菜单 |

以下两段代码呈现同一组选择项。用于 MVVM 时，替换内联集合并对选择属性使用双向绑定。[完整 Select 示例](../samples/Alder.Avalonia.Gallery/Views/SelectExample.axaml)

```xml
<alder:AlderSelect xmlns="https://github.com/avaloniaui"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   xmlns:alder="using:Alder.Avalonia.Controls"
                   Label="Delivery" SelectedIndex="0">
  <x:String>Standard</x:String>
  <x:String>Express</x:String>
</alder:AlderSelect>
```

```csharp
using Alder.Avalonia.Controls;

var select = new AlderSelect
{
    Label = "Delivery",
    ItemsSource = new[] { "Standard", "Express" },
    SelectedIndex = 0
};
```

## 导航与窗口布局

- 用 `AlderSidebar` 绘制侧栏，`AlderSidebarGroup` 和 `AlderSidebarMenu` 提供分组及项集合。侧栏的 `SelectedItem` 是组间共享的单选状态。分组标题、可导航的子项和执行命令的行使用各自的属性与模板。
- 用 `Header` 和 `HeaderIcon` 同时呈现名称与品牌。收缩到图标栏时保留图标。通过 `IsSearchEnabled` 启用搜索，使用 `SelectedItem` 把搜索与常规导航统一到同一套选择绑定。
- 用 `AlderAppLayout` 组合 `Sidebar`、`Navbar`、`Toolbar`、`Content`、`Aside` 和 `Footer`。`SidebarCollapse="Icon"` 使用图标栏。紧凑状态中的侧栏可使用 `MobileSidebar`。
- `AlderUserMenu` 支持自定义菜单内容、内置主题切换和退出动作。`ThemeModeChanged` 提供主题变更事件，`LogOutCommand` 与 `LogOutRequested` 提供退出入口。尾部箭头随开合执行 150 ms 的合成旋转，启用 `AlderMotion.Reduced` 时立即切换方向。
- 自定义窗口标题区时，`AlderWindow.ShowTitleBar` 控制默认标题栏，拖动区域使用 Avalonia 的 `WindowDecorationProperties.ElementRole="TitleBar"`。
- `AlderSalashWindow` 通过 `Background` 和右对齐的 `Content` 放置背景与登录卡片。把实现 `IDialogContext` 的 ViewModel 设为窗口的 `DataContext`，将卡片按钮绑定到自己的异步命令。业务处理成功后，ViewModel 触发 `RequestClose?.Invoke(this, DialogResult.OK)`。基类自动订阅事件，派生窗口在 CodeBehind 重写 `CreateNextWindow()` 并读取 `CloseResult` 决定返回哪个窗口。标题栏关闭时结果为 `None`，返回 `null` 时直接关闭。成功交接时，基类先显示新窗口，有需要时更新桌面生命周期的 `MainWindow`，再关闭自身并清理旧窗口的 MessageBox 和 Toast Host。[Gallery 窗口](../samples/Alder.Avalonia.Gallery/Views/SalashPreviewWindow.axaml.cs) · [Gallery ViewModel](../samples/Alder.Avalonia.Gallery/ViewModels/SalashWindowViewModel.cs)

[Sidebar 内联与 MVVM 示例](../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) · [AppLayout 内联与 MVVM 示例](../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) · [侧栏绑定实现](../samples/Alder.Avalonia.Gallery/Views/NavigationPreviewSidebar.axaml)

## 数据、动画与 AI

| 场景 | 典型绑定与扩展 |
| --- | --- |
| 表格与数据网格 | `AlderTable` 和 `AlderDataGrid` 用 `ItemsSource` 与列定义组织行，`RowKeySelector` 让选择在排序后仍对应同一行。为虚拟化提供有限视口，以实际滚动和已实现行数验证 |
| 文件树、看板 | 用可观察的数据集合维护位置和节点。拖拽提交移动请求，业务侧执行与持久化数据修改。文件树的 `AlderFileTreeNode.Children` 与 `IsExpanded` 可用于组合和观察展开 |
| 可调布局 | 在 `AlderResizable` 里交替放置 `AlderResizablePanel` 与 `AlderResizableHandle`。面板的 `DefaultSize` 是起点，`Size` 可交给视图模型保存 |
| 按压和动态数值 | 按压效果复用 `AlderPressableFeedback`。动态数值使用 `AlderNumberValue`。在对应视图或窗口设置 `AlderMotion.Reduced` 以减少可选动画 |
| 提示输入与模型 | `AlderPromptInput` 使用 `Text`、`SendCommand`、`StopCommand` 与 `IsRunning` 绑定使用方状态。把 `AlderModelSelector` 放进工具区，双向绑定 `SelectedModel` 和 `SelectedEffort` |
| 聊天、Markdown 与工具反馈 | 使用真实的消息、附件和工具状态。发送、批准、取消与错误恢复由宿主驱动。文本与代码着色使用本库的原生文本管线 |

[Table](../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) · [Resizable](../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) · [Prompt Input](../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) · [Model Selector](../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml)

## 基础展示与反馈

| 组件 | 常用模式 |
| --- | --- |
| Button、ButtonGroup、Toolbar | 使用 `Command` 和 `CommandParameter` 执行业务动作。用组或工具栏组织多个按钮，显示和可用状态从当前选区、权限或请求状态计算 |
| Alert、Empty State、Skeleton、Spinner、Loader、Text Shimmer | 从请求、错误、空数据和加载状态生成页面反馈。重新打开时根据当前数据与请求决定显示内容 |
| Meter、Progress、Number Value、Trend Chip、Emoji Reaction Button | 从业务数据绑定进度、数值、趋势或反应计数。保存原始指标或用户的反应，动画和样式随本次显示计算 |
| Card、Surface、Separator、Item Card、Holo Card | 用于容纳内容、划分区域和展示数据项。保存卡片展示的模型或应用的布局偏好 |
| Avatar、AvatarGroup、Typography、Kbd、Badge、Chip | 根据模型、头像地址、角色、文字或按键提示构建展示。用户可删除的 Chip 可绑定业务上的移除动作 |
| ScrollShadow、Pressable Feedback | 反馈滚动边缘与按压状态。需要恢复滚动位置时，在宿主记录位置或内容锚点 |

## Gallery 示例索引

下表按 Gallery 侧栏组织。每个链接指向承载完整预览与代码标签的页面。共享入口会列出同页的多个视图。

首页使用 [GalleryHomeView](../samples/Alder.Avalonia.Gallery/Views/GalleryHomeView.cs) 汇集基础与高级控件。侧栏配置见 [GalleryWindowViewModel](../samples/Alder.Avalonia.Gallery/ViewModels/GalleryWindowViewModel.cs)，视图入口见 [GalleryWindow](../samples/Alder.Avalonia.Gallery/Views/GalleryWindow.axaml.cs)。


### 按钮

| Gallery 页面 | 示例源码 |
| --- | --- |
| Button | [Button](../samples/Alder.Avalonia.Gallery/Views/ButtonExample.axaml) · [ButtonGroup](../samples/Alder.Avalonia.Gallery/Views/ButtonGroupExample.axaml) |
| ToggleButton | [ToggleButton](../samples/Alder.Avalonia.Gallery/Views/ToggleButtonExample.axaml) |

### AI

| Gallery 页面 | 示例源码 |
| --- | --- |
| Approval Card | [ApprovalCard](../samples/Alder.Avalonia.Gallery/Views/ApprovalCardExample.axaml) |
| Chain Of Thought | [ChainOfThought](../samples/Alder.Avalonia.Gallery/Views/ChainOfThoughtExample.axaml) |
| Chat Attachment | [ChatAttachment](../samples/Alder.Avalonia.Gallery/Views/ChatAttachmentExample.axaml) |
| Chat Conversation | [ChatConversation](../samples/Alder.Avalonia.Gallery/Views/ChatConversationExample.axaml) |
| Chat List View | [ChatListView](../samples/Alder.Avalonia.Gallery/Views/ChatListViewExample.axaml) |
| Chat Loader | [ChatLoader](../samples/Alder.Avalonia.Gallery/Views/ChatLoaderExample.axaml) |
| Chat Message | [ChatMessage](../samples/Alder.Avalonia.Gallery/Views/ChatMessageExample.axaml) |
| Chat Message Actions | [ChatMessageActions](../samples/Alder.Avalonia.Gallery/Views/ChatMessageActionsExample.axaml) |
| Chat Source | [ChatSource](../samples/Alder.Avalonia.Gallery/Views/ChatSourceExample.axaml) |
| Chat Tool | [ChatTool](../samples/Alder.Avalonia.Gallery/Views/ChatToolExample.axaml) |
| Code Block | [CodeBlock](../samples/Alder.Avalonia.Gallery/Views/CodeBlockExample.axaml) |
| Loader | [Loader](../samples/Alder.Avalonia.Gallery/Views/LoaderExample.axaml) |
| Markdown | [Markdown](../samples/Alder.Avalonia.Gallery/Views/MarkdownExample.axaml) |
| Model Selector | [ModelSelector](../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml) |
| Prompt Input | [PromptInput](../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) |
| Prompt Suggestion | [PromptSuggestion](../samples/Alder.Avalonia.Gallery/Views/PromptSuggestionExample.axaml) |
| Reasoning | [Reasoning](../samples/Alder.Avalonia.Gallery/Views/ReasoningExample.axaml) |
| Text Shimmer | [TextShimmer](../samples/Alder.Avalonia.Gallery/Views/TextShimmerExample.axaml) |

### 表单

| Gallery 页面 | 示例源码 |
| --- | --- |
| Checkbox | [CheckBox](../samples/Alder.Avalonia.Gallery/Views/CheckBoxExample.axaml) |
| Rich Text Editor | [构建、保存与加载](guides/rich-text-editor.md) · [Gallery](../samples/Alder.Avalonia.Gallery/Views/RichTextEditorExample.axaml) |
| Checkbox Button Group | [CheckboxButtonGroup](../samples/Alder.Avalonia.Gallery/Views/CheckboxButtonGroupExample.axaml) |
| Radio Button Group | [RadioButtonGroup](../samples/Alder.Avalonia.Gallery/Views/RadioButtonGroupExample.axaml) |
| Drop Zone | [DropZone](../samples/Alder.Avalonia.Gallery/Views/DropZoneExample.axaml) |
| Number Stepper | [NumberStepper](../samples/Alder.Avalonia.Gallery/Views/NumberStepperExample.axaml) |
| Input | [Input](../samples/Alder.Avalonia.Gallery/Views/InputExample.axaml) · [InputGroup](../samples/Alder.Avalonia.Gallery/Views/InputGroupExample.axaml) · [InputOtp](../samples/Alder.Avalonia.Gallery/Views/InputOtpExample.axaml) · [NumberField](../samples/Alder.Avalonia.Gallery/Views/NumberFieldExample.axaml) · [SearchField](../samples/Alder.Avalonia.Gallery/Views/SearchFieldExample.axaml) · [TextArea](../samples/Alder.Avalonia.Gallery/Views/TextAreaExample.axaml) |
| Selection | [Choice](../samples/Alder.Avalonia.Gallery/Views/ChoiceExample.axaml) · [RadioGroup](../samples/Alder.Avalonia.Gallery/Views/RadioGroupExample.axaml) |

`AlderInput` 和 `AlderInputGroup` 设置 `ShowClearButton="True"` 后，会在可编辑且有文本时显示尾部清除按钮。点击会更新双向绑定并恢复输入焦点。

`AlderDropZone` 可通过 AXAML 将选取、重试和移除绑定到 ViewModel 的 `ICommand`。

```xml
<alder:AlderDropZone AllowMultiple="True"
                     FilesChangedCommand="{CompiledBinding UploadCommand}"
                     RetryRequestedCommand="{CompiledBinding RetryCommand}"
                     FileRemovedCommand="{CompiledBinding CancelCommand}" />
```

`FilesChangedCommand` 接收 `DropZoneFilesChangedEventArgs`，从 `Added` 取待上传文件，从 `Rejected` 取校验结果。后两条命令接收对应的 `AlderDropZoneFile`，单文件模式替换旧文件也会触发移除命令。
在 ViewModel 中通过 `file.File.OpenReadAsync()` 读取文件流，异步调用业务 API，然后更新 `Status` 和 `Progress`。
控件会检查命令的 `CanExecute`。若需要在上传期间接收后续批次，可在命令中将新文件入队，再由 ViewModel 异步上传。

### 日期与时间

| Gallery 页面 | 示例源码 |
| --- | --- |
| Calendars & fields | [Calendar](../samples/Alder.Avalonia.Gallery/Views/CalendarExample.axaml) · [DateField](../samples/Alder.Avalonia.Gallery/Views/DateFieldExample.axaml) · [DatePicker](../samples/Alder.Avalonia.Gallery/Views/DatePickerExample.axaml) · [DateRangePicker](../samples/Alder.Avalonia.Gallery/Views/DateRangePickerExample.axaml) · [RangeCalendar](../samples/Alder.Avalonia.Gallery/Views/RangeCalendarExample.axaml) · [TimeField](../samples/Alder.Avalonia.Gallery/Views/TimeFieldExample.axaml) |
| Agenda | [Agenda](../samples/Alder.Avalonia.Gallery/Views/AgendaExample.axaml) |

### 导航

| Gallery 页面 | 示例源码 |
| --- | --- |
| Accordion | [Accordion](../samples/Alder.Avalonia.Gallery/Views/AccordionExample.axaml) |
| AppLayout | [AppLayout](../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) |
| Breadcrumbs | [Breadcrumbs](../samples/Alder.Avalonia.Gallery/Views/BreadcrumbsExample.axaml) |
| Command palette | [Command](../samples/Alder.Avalonia.Gallery/Views/CommandExample.axaml) |
| Context Menu | [ContextMenu](../samples/Alder.Avalonia.Gallery/Views/ContextMenuExample.axaml) |
| Disclosure | [Disclosure](../samples/Alder.Avalonia.Gallery/Views/DisclosureExample.axaml) · [DisclosureGroup](../samples/Alder.Avalonia.Gallery/Views/DisclosureGroupExample.axaml) |
| Link | [Link](../samples/Alder.Avalonia.Gallery/Views/LinkExample.axaml) |
| Navbar | [Navbar](../samples/Alder.Avalonia.Gallery/Views/NavbarExample.axaml) |
| Pagination | [Pagination](../samples/Alder.Avalonia.Gallery/Views/PaginationExample.axaml) |
| Segment | [Segment](../samples/Alder.Avalonia.Gallery/Views/SegmentExample.axaml) |
| Sidebar | [Sidebar](../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) |
| Stepper | [Stepper](../samples/Alder.Avalonia.Gallery/Views/StepperExample.axaml) |
| Tabs | [ExpandableTabs](../samples/Alder.Avalonia.Gallery/Views/ExpandableTabsExample.axaml) · [MorphingTabs](../samples/Alder.Avalonia.Gallery/Views/MorphingTabsExample.axaml) · [Tabs](../samples/Alder.Avalonia.Gallery/Views/TabsExample.axaml) |

### 弹出层

| Gallery 页面 | 示例源码 |
| --- | --- |
| Emoji Picker | [EmojiPicker](../samples/Alder.Avalonia.Gallery/Views/EmojiPickerExample.axaml) |
| MessageBox | [MessageBox](../samples/Alder.Avalonia.Gallery/Views/MessageBoxExample.axaml) |
| Drawer | [Drawer](../samples/Alder.Avalonia.Gallery/Views/DrawerExample.axaml) |
| Dialog | [Dialog](../samples/Alder.Avalonia.Gallery/Views/DialogExample.axaml) |
| Popover | [Popover](../samples/Alder.Avalonia.Gallery/Views/PopoverExample.axaml) |
| Bloom Menu | [BloomMenu](../samples/Alder.Avalonia.Gallery/Views/BloomMenuExample.axaml) |
| Sheet | [Sheet](../samples/Alder.Avalonia.Gallery/Views/SheetExample.axaml) |
| Toast | [Toast](../samples/Alder.Avalonia.Gallery/Views/ToastExample.axaml) |
| Tooltip | [ToolTip](../samples/Alder.Avalonia.Gallery/Views/ToolTipExample.axaml) |

`AlderBloomMenu` 将触发按钮从中心展开为三列菜单。通过 `Items` 添加 `AlderBloomMenuItem`，或通过 `ItemsSource` 和 `ItemTemplate` 绑定数据。每个操作可绑定 `Command` 和 `CommandParameter`，并通过 `Icon` 指定图标。

`IsOpen` 默认双向绑定。将 `Header` 绑定到触发按钮与展开标题共用的文案，将 `Icon` 绑定到触发按钮的 `Geometry`。默认主题显示加号，显式设置 `Icon="{x:Null}"` 可隐藏图标。通过 `CloseLabel` 设置关闭按钮的无障碍名称。动效参考 [beUI Web 预览](https://beui.dev/components/blocks/bloom-menu) 和 [组件文档](https://beui.dev/components/blocks/bloom-menu.md)，外观采用 Alder 浮层资源。

### 控件

| Gallery 页面 | 示例源码 |
| --- | --- |
| Cell Slider | [CellSlider](../samples/Alder.Avalonia.Gallery/Views/CellSliderExample.axaml) |
| Cell Switch | [CellSwitch](../samples/Alder.Avalonia.Gallery/Views/CellSwitchExample.axaml) |
| Slider | [Slider](../samples/Alder.Avalonia.Gallery/Views/SliderExample.axaml) |
| Switch | [Notification](../samples/Alder.Avalonia.Gallery/Views/NotificationExample.axaml) |

### 反馈

| Gallery 页面 | 示例源码 |
| --- | --- |
| Alert | [Alert](../samples/Alder.Avalonia.Gallery/Views/AlertExample.axaml) |
| Emoji Reaction Button | [EmojiReactionButton](../samples/Alder.Avalonia.Gallery/Views/EmojiReactionButtonExample.axaml) |
| Number Value | [NumberValue](../samples/Alder.Avalonia.Gallery/Views/NumberValueExample.axaml) |
| Pressable Feedback | [PressableFeedback](../samples/Alder.Avalonia.Gallery/Views/PressableFeedbackExample.axaml) |
| Rating | [Rating](../samples/Alder.Avalonia.Gallery/Views/RatingExample.axaml) |
| Trend Chip | [TrendChip](../samples/Alder.Avalonia.Gallery/Views/TrendChipExample.axaml) |
| Meter | [Meter](../samples/Alder.Avalonia.Gallery/Views/MeterExample.axaml) |
| Progress | [ProgressBar](../samples/Alder.Avalonia.Gallery/Views/ProgressBarExample.axaml) · [ProgressCircle](../samples/Alder.Avalonia.Gallery/Views/ProgressCircleExample.axaml) |
| Skeleton | [Skeleton](../samples/Alder.Avalonia.Gallery/Views/SkeletonExample.axaml) |
| Spinner | [Spinner](../samples/Alder.Avalonia.Gallery/Views/SpinnerExample.axaml) |

### 布局

| Gallery 页面 | 示例源码 |
| --- | --- |
| Card | [Card](../samples/Alder.Avalonia.Gallery/Views/CardExample.axaml) |
| Resizable | [Resizable](../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) |
| Separator | [Separator](../samples/Alder.Avalonia.Gallery/Views/SeparatorExample.axaml) |
| Surface | [Surface](../samples/Alder.Avalonia.Gallery/Views/SurfaceExample.axaml) |
| Toolbar | [Toolbar](../samples/Alder.Avalonia.Gallery/Views/ToolbarExample.axaml) |
| Transitioning Content | [接入指南](guides/navigation-and-actions.md#transitioning-content) · [六种过渡示例](../samples/Alder.Avalonia.Gallery/Views/TransitioningContentExample.axaml) |

### 媒体

| Gallery 页面 | 示例源码 |
| --- | --- |
| Avatar | [Avatar](../samples/Alder.Avalonia.Gallery/Views/AvatarExample.axaml) · [AvatarGroup](../samples/Alder.Avalonia.Gallery/Views/AvatarGroupExample.axaml) |

### 选择器

| Gallery 页面 | 示例源码 |
| --- | --- |
| Autocomplete | [Autocomplete](../samples/Alder.Avalonia.Gallery/Views/AutocompleteExample.axaml) |
| Cell Select | [CellSelect](../samples/Alder.Avalonia.Gallery/Views/CellSelectExample.axaml) |
| Inline Select | [InlineSelect](../samples/Alder.Avalonia.Gallery/Views/InlineSelectExample.axaml) |
| Native Select | [NativeSelect](../samples/Alder.Avalonia.Gallery/Views/NativeSelectExample.axaml) |
| ComboBox | [ComboBox](../samples/Alder.Avalonia.Gallery/Views/ComboBoxExample.axaml) |
| Select | [Select](../samples/Alder.Avalonia.Gallery/Views/SelectExample.axaml) |

### 排版

| Gallery 页面 | 示例源码 |
| --- | --- |
| Kbd | [Kbd](../samples/Alder.Avalonia.Gallery/Views/KbdExample.axaml) |
| Typography | [Typography](../samples/Alder.Avalonia.Gallery/Views/TypographyExample.axaml) |

### 图表

| Gallery 页面 | 示例源码 |
| --- | --- |
| Area Chart | [AreaChart](../samples/Alder.Avalonia.Gallery/Views/AreaChartExample.axaml) |
| Bar Chart | [BarChart](../samples/Alder.Avalonia.Gallery/Views/BarChartExample.axaml) |
| Chart Tooltip | [ChartTooltip](../samples/Alder.Avalonia.Gallery/Views/ChartTooltipExample.axaml) |
| Composed Chart | [ComposedChart](../samples/Alder.Avalonia.Gallery/Views/ComposedChartExample.axaml) |
| Line Chart | [LineChart](../samples/Alder.Avalonia.Gallery/Views/LineChartExample.axaml) |
| Pie Chart | [PieChart](../samples/Alder.Avalonia.Gallery/Views/PieChartExample.axaml) |
| Radar Chart | [RadarChart](../samples/Alder.Avalonia.Gallery/Views/RadarChartExample.axaml) |
| Radial Chart | [RadialChart](../samples/Alder.Avalonia.Gallery/Views/RadialChartExample.axaml) |

### 数据展示

| Gallery 页面 | 示例源码 |
| --- | --- |
| Action Bar | [ActionBar](../samples/Alder.Avalonia.Gallery/Views/ActionBarExample.axaml) |
| Expandable Action Bar | [ExpandableActionBar](../samples/Alder.Avalonia.Gallery/Views/ExpandableActionBarExample.axaml) |
| Overflow Actions | [OverflowActions](../samples/Alder.Avalonia.Gallery/Views/OverflowActionsExample.axaml) |
| Badge | [Badge](../samples/Alder.Avalonia.Gallery/Views/BadgeExample.axaml) |
| Carousel | [Carousel](../samples/Alder.Avalonia.Gallery/Views/CarouselExample.axaml) |
| Image View | [ImageView](../samples/Alder.Avalonia.Gallery/Views/ImageViewExample.axaml) |
| Chip | [Chip](../samples/Alder.Avalonia.Gallery/Views/ChipExample.axaml) |
| Data Grid | [DataGrid](../samples/Alder.Avalonia.Gallery/Views/DataGridExample.axaml) |
| Empty State | [EmptyState](../samples/Alder.Avalonia.Gallery/Views/EmptyStateExample.axaml) |
| File Tree | [FileTree](../samples/Alder.Avalonia.Gallery/Views/FileTreeExample.axaml) |
| Floating TOC | [FloatingToc](../samples/Alder.Avalonia.Gallery/Views/FloatingTocExample.axaml) |
| Holo Card | [HoloCard](../samples/Alder.Avalonia.Gallery/Views/HoloCardExample.axaml) |
| Hover Card | [HoverCard](../samples/Alder.Avalonia.Gallery/Views/HoverCardExample.axaml) |
| Item Card | [ItemCard](../samples/Alder.Avalonia.Gallery/Views/ItemCardExample.axaml) |
| Kanban | [Kanban](../samples/Alder.Avalonia.Gallery/Views/KanbanExample.axaml) |
| KPI | [Kpi](../samples/Alder.Avalonia.Gallery/Views/KpiExample.axaml) |
| List View | [ListView](../samples/Alder.Avalonia.Gallery/Views/ListViewExample.axaml) |
| Map | [Map](../samples/Alder.Avalonia.Gallery/Views/MapExample.axaml) |
| Table | [Table](../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) |
| Timeline | [Timeline](../samples/Alder.Avalonia.Gallery/Views/TimelineExample.axaml) |
| Widget | [Widget](../samples/Alder.Avalonia.Gallery/Views/WidgetExample.axaml) |

`AlderExpandableActionBar` 用于常驻的图标操作栏。悬停、键盘聚焦或初次触摸会展开标签。
可绑定 `IsExpanded` 和 `SelectedIndex`，通过 `ExpandOnHover`、`ExpandOnFocus` 和 `Size` 调整行为。
`AlderExpandableActionBarItem` 使用标准按钮的 `Command`，支持 `Icon`、`Badge`、`Shortcut` 提示和 `IconMotion`。
交互动效参考 [beUI Web 预览](https://beui.dev/components/blocks/expandable-action-bar) 和 [组件说明](https://beui.dev/components/blocks/expandable-action-bar.md)，表面复用 Alder 工具栏主题资源。

`AlderOverflowActions` 把 `PrimaryActions` 和 `OverflowActions` 放在同一条操作栏里。
两组接受带标准 `Command` 的 `AlderOverflowAction`。通过 `IsExpanded` 双向绑定开关状态，可配置 `CollapseOnAction` 和 `Size`。
窄幅下操作区域可滚动，展开／收起按钮留在尾部。动效参考 [beUI Web 预览](https://beui.dev/components/blocks/overflow-actions) 和 [组件说明](https://beui.dev/components/blocks/overflow-actions.md)。

`AlderImageView` 的 `Source` 接受单个 `IImage`，也可以将 `AlderImageViewItem` 放入 `Items` 或绑定 `ItemsSource`，通过 `SelectedIndex` 或 `SelectedItem` 双向绑定选择。
每个集合项目可以提供 `Title`、`FileName`、可访问的 `Description` 和独立的 `Thumbnail`。
`ViewportHeight` 设置内嵌画布高度，`ShowDetails`、`ShowThumbnails` 和 `AllowImmersivePreview` 控制查看器界面。 `Zoom` 和 `IsImmersive` 可用于观察状态，`Fit()`、`ShowActualSize()`、`TryOpenImmersive()` 和 `CloseImmersive()` 可从代码控制。
滚轮以指针为锚点缩放，双击切换适配与原始大小，放大后可拖动。键盘支持 `+` / `−`、`0`、`1`、`←` / `→`、`F` 与 `Esc`。原始大小按屏幕物理像素显示位图。缩略图沿用 Alder Carousel，沉浸切换参考 [beUI Image Viewer](https://beui.dev/components/motion/image-viewer)。
通过 `ImageTransition` 选择切图效果，例如 `ImageTransition="Slide"`。可选 `CrossFade`（默认交叉淡化）、`Slide`（沿浏览方向水平滑动）、`Zoom`（居中缩放）、`Lift`（向上浮入）和 `None`（立即切换）。
动效使用 200 ms 的合成关键帧。启用 `AlderMotion.Reduced` 时直接显示目标图片。

### 颜色

| Gallery 页面 | 示例源码 |
| --- | --- |
| Cell Color Picker | [CellColorPicker](../samples/Alder.Avalonia.Gallery/Views/CellColorPickerExample.axaml) |
| Color area | [ColorArea](../samples/Alder.Avalonia.Gallery/Views/ColorAreaExample.axaml) |
| Color field | [ColorField](../samples/Alder.Avalonia.Gallery/Views/ColorFieldExample.axaml) |
| Color picker | [ColorPicker](../samples/Alder.Avalonia.Gallery/Views/ColorPickerExample.axaml) |
| Color slider | [ColorSlider](../samples/Alder.Avalonia.Gallery/Views/ColorSliderExample.axaml) |
| Color swatch | [ColorSwatch](../samples/Alder.Avalonia.Gallery/Views/ColorSwatchExample.axaml) |
| Color swatch picker | [ColorSwatchPicker](../samples/Alder.Avalonia.Gallery/Views/ColorSwatchPickerExample.axaml) |

### 实用工具

| Gallery 页面 | 示例源码 |
| --- | --- |
| ScrollShadow | [ScrollShadow](../samples/Alder.Avalonia.Gallery/Views/ScrollShadowExample.axaml) |
| Workflow Editor | [WorkflowEditor](../samples/Alder.Avalonia.Gallery/Views/WorkflowEditorExample.axaml) · [绑定与 JSON 格式](../samples/Alder.Avalonia.WorkflowPrototype/README.md) |
