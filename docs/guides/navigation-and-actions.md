# 导航、浮层和操作栏

## AppLayout 与 Sidebar

用 `AlderAppLayout` 放置 `Sidebar`、`Navbar`、`Toolbar`、`Content`、`Aside` 和 `Footer`。侧栏通过 `AlderSidebarGroup`、`AlderSidebarMenu` 组织项目，可以用 `IsSearchEnabled` 启用统一搜索。把 `AlderSidebar.SelectedItem` 绑定到导航模型，收到选择后根据项目的路由打开内容。

保存路由键和独立的侧栏偏好，例如用户在桌面端选择的打开状态。恢复时先加载可用路由，再查找原路由对应的项。失效路由回到应用设定的默认页面。移动端侧栏的临时展开状态可随紧凑布局重新决定。[AppLayout 示例](../../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) · [Sidebar 示例](../../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) · [导航绑定 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/NavigationBindingViewModel.cs)

| 控件 | 数据与恢复 |
| --- | --- |
| Tabs、Expandable Tabs、Morphing Tabs | 从模型或模板建立页签和内容，绑定当前选择。保存业务页签键，加载完页签后解析成控件需要的项或索引。[Tabs](../../samples/Alder.Avalonia.Gallery/Views/TabsExample.axaml) · [Expandable](../../samples/Alder.Avalonia.Gallery/Views/ExpandableTabsExample.axaml) · [Morphing](../../samples/Alder.Avalonia.Gallery/Views/MorphingTabsExample.axaml) |
| Segment | 给 `AlderSegmentItem.Key` 指定稳定值，双向绑定 `SelectedKey`。保存键可在选项换序后恢复同一选择。[示例](../../samples/Alder.Avalonia.Gallery/Views/SegmentExample.axaml) |
| Stepper、Pagination | 从流程模型计算当前步骤及完成状态。分页用 `CurrentPage` 和 `PageCount`，按新结果页数恢复或重置页码。[Stepper](../../samples/Alder.Avalonia.Gallery/Views/StepperExample.axaml) · [Pagination](../../samples/Alder.Avalonia.Gallery/Views/PaginationExample.axaml) |
| Accordion、Disclosure | 组合标题、展开项和内容，按需将展开状态对应到稳定的项目键。[Accordion](../../samples/Alder.Avalonia.Gallery/Views/AccordionExample.axaml) · [Disclosure Group](../../samples/Alder.Avalonia.Gallery/Views/DisclosureGroupExample.axaml) |
| Breadcrumbs、Navbar、Link | 从应用的路由与当前页生成导航内容，链接或命令负责跳转。[Breadcrumbs](../../samples/Alder.Avalonia.Gallery/Views/BreadcrumbsExample.axaml) · [Navbar](../../samples/Alder.Avalonia.Gallery/Views/NavbarExample.axaml) · [Link](../../samples/Alder.Avalonia.Gallery/Views/LinkExample.axaml) |

## Transitioning Content

将当前页面、步骤或详情对象绑定到 `AlderTransitioningContentControl.Content`。用 `ContentTemplate` 显示同一类型的页面，或在 `DataTemplates` 中为不同类型声明模板。第一次加载直接显示内容，之后替换 `Content` 才会播放过渡。

```xml
<alder:AlderTransitioningContentControl
    Transition="Slide" MinHeight="180" Padding="18"
    Content="{CompiledBinding CurrentPage}"
    ContentTemplate="{StaticResource PageTemplate}"
    IsTransitionReversed="{CompiledBinding IsNavigatingBack}" />
```

先更新 `IsNavigatingBack`，再替换 `CurrentPage`。默认方向为向前，横向推移从右侧入场，垂直浮入从下方入场，遮罩从左侧揭示。返回时三个方向反转。预览区域可用 `Height` 固定尺寸，或用 `MinHeight` 给短内容预留空间。`Background`、`BorderBrush`、`BorderThickness` 和 `CornerRadius` 控制固定外框，`Padding` 控制内容内距。

| `Transition` | 默认时长 | 行为 |
| --- | ---: | --- |
| `CrossFade` | 180 ms | 两层原地淡入淡出，默认模式 |
| `Slide` | 260 ms | 两页等距水平推移 |
| `Rise` | 220 ms | 入场位移 24 DIP，并伴随淡入淡出 |
| `Scale` | 240 ms | 旧内容略微缩小，新内容从 94% 缩放到正常尺寸 |
| `Blur` | 260 ms | 旧内容失焦，新内容渐清 |
| `Reveal` | 300 ms | 同一条遮罩边界水平扫过，扫过的区域显示新页，另一侧保留旧页 |
| `None` | 即时 | 直接显示新内容 |

`Duration` 接受可绑定的 `TimeSpan?`。例如 `Duration="0:0:0.4"` 将过渡设为 400 ms，设为零可即时切换。窗口或任意祖先上的 `AlderMotion.Reduced="True"` 会使用最多 120 ms 的原地淡入淡出。继承的 `PageTransition` 可接入自定义 Avalonia 过渡。若需要在退出完成后释放页面资源，可订阅 `TransitionCompleted` 并检查 `HasRunToCompletion` 和 `From`。

Gallery 为六种动效各提供独立的 Preview、AXAML 和 C#。在每个示例中可双向切换概览、活动和版本，也可开启慢放和减少动效。[Gallery 示例](../../samples/Alder.Avalonia.Gallery/Views/TransitioningContentExample.axaml) · [绑定 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/TransitioningContentViewModel.cs) · [SVG 原型](../prototypes/alder-transitioning-content-control.svg)

## Resizable

在 `AlderResizable` 中交替放置 `AlderResizablePanel` 和 `AlderResizableHandle`。下面是一个左右布局。让 ViewModel 的 `SidebarSize` 使用 `GridLength?`，`SidebarCollapsed` 使用 `bool`。

```xml
<alder:AlderResizable>
  <alder:AlderResizablePanel DefaultSize="240" MinSize="160" IsCollapsible="True"
                              Size="{CompiledBinding SidebarSize, Mode=TwoWay}"
                              IsCollapsed="{CompiledBinding SidebarCollapsed, Mode=TwoWay}">
    <TextBlock Text="侧栏" />
  </alder:AlderResizablePanel>
  <alder:AlderResizableHandle />
  <alder:AlderResizablePanel DefaultSize="*">
    <TextBlock Text="内容" />
  </alder:AlderResizablePanel>
</alder:AlderResizable>
```

保存尺寸时同时记录数值与单位，如 `240` DIP 或 `1` 星号。恢复时重建 `GridLength`，用保存的开关状态初始化 `IsCollapsed`。首次打开可留空 `Size` 以使用 `DefaultSize`。需要记住折叠之前的尺寸时，业务层可单独记录上一次展开尺寸。[布局示例](../../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) · [拖动绑定 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ResizableViewModel.cs)

## Command Palette

给 `AlderCommand.Groups` 提供 `AlderCommandGroup`。每一项用 `Label`、`Keywords` 和 `Command` 描述执行内容，或者用嵌套的 `Groups` 建立下一级菜单。可以用 `ShowAsync(owner)` 打开浮层。

```csharp
using System.Threading.Tasks;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Controls;

static Task<DialogResult> ShowCommandsAsync(Control owner, ICommand openSettings)
{
    var palette = new AlderCommand
    {
        Groups = new[]
        {
            new AlderCommandGroup
            {
                Heading = "应用",
                Items = new[]
                {
                    new AlderCommandItem
                    {
                        Label = "打开设置",
                        Keywords = "preferences",
                        Command = openSettings
                    }
                }
            }
        }
    };
    return palette.ShowAsync(owner);
}
```

收藏命令或记录历史时保存应用命令 ID，通过当前命令注册重新绑定操作。查询文本、键盘活跃项等状态可随一次打开结束而重置。[Command 示例](../../samples/Alder.Avalonia.Gallery/Views/CommandExample.axaml) · [分组、子菜单和预览 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/CommandViewModel.cs)

## Dialog、Drawer、Sheet 与菜单

给覆盖内容提供 `DialogHost`，再调用 `Dialog.ShowModalAsync`、`Drawer.ShowAsync`、`Sheet.ShowAsync` 或 `MessageBox.ShowAsync` 并等待结果。独立桌面窗口可用 `Dialog.ShowWindowAsync`。为对话框表单保留一个临时草稿，在收到确认结果后提交。需要恢复未提交编辑时保存草稿及业务记录 ID，载入时重新创建对话框内容。

```csharp
using System.Threading.Tasks;
using Alder.Avalonia.Controls;
using Avalonia.Controls;

static async Task<bool> ConfirmAsync(Control owner)
{
    var result = await MessageBox.ShowAsync(
        "保存当前修改？",
        new MessageBoxOptions { Buttons = DialogButtons.YesNo },
        owner: owner);
    return result == DialogResult.Yes;
}
```

| 场景 | 接入方式 |
| --- | --- |
| Dialog、Drawer、Sheet | 从视图或 ViewModel 创建内容，按场景选择模态确认、侧边编辑或贴边呈现。[Dialog](../../samples/Alder.Avalonia.Gallery/Views/DialogExample.axaml) · [Drawer](../../samples/Alder.Avalonia.Gallery/Views/DrawerExample.axaml) · [Sheet](../../samples/Alder.Avalonia.Gallery/Views/SheetExample.axaml) |
| Context Menu、Popover、Bloom Menu | 由触发器提供业务上下文，给各菜单条目绑定命令及参数。开关状态用于控制这次交互。[Context Menu](../../samples/Alder.Avalonia.Gallery/Views/ContextMenuExample.axaml) · [Popover](../../samples/Alder.Avalonia.Gallery/Views/PopoverExample.axaml) · [Bloom Menu](../../samples/Alder.Avalonia.Gallery/Views/BloomMenuExample.axaml) |
| Emoji Picker | 绑定 `SelectedEmoji`，用 `RecentEmojis` 提供最近选择的可写列表。保存 Emoji 字符串，重新打开时填充该列表。[示例](../../samples/Alder.Avalonia.Gallery/Views/EmojiPickerExample.axaml) |
| Toast、Tooltip、Hover Card | Toast 由操作结果触发，Tooltip 与 Hover Card 从当前元素及其数据生成提示内容。[Toast](../../samples/Alder.Avalonia.Gallery/Views/ToastExample.axaml) · [Tooltip](../../samples/Alder.Avalonia.Gallery/Views/ToolTipExample.axaml) · [Hover Card](../../samples/Alder.Avalonia.Gallery/Views/HoverCardExample.axaml) |

## Action Bar、Expandable Action Bar 与 Overflow Actions

- `AlderActionBar` 适用于选中行后的批量操作。由业务选中项决定 `IsOpen` 和操作可用性，`Prefix` 可以显示选中数量。恢复后根据加载的选择键重新计算操作状态。[示例](../../samples/Alder.Avalonia.Gallery/Views/ActionBarExample.axaml) · [操作 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ActionBarViewModel.cs)
- `AlderExpandableActionBar` 适用于常驻图标工具栏。用 `AlderExpandableActionBarItem.Command` 执行动作，`IconMotion` 设置图标效果。需要记住选中工具时，保存应用的工具 ID，再映射到 `SelectedIndex`。展开由悬停、聚焦或应用设置驱动。[示例](../../samples/Alder.Avalonia.Gallery/Views/ExpandableActionBarExample.axaml) · [ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ExpandableActionBarViewModel.cs)
- `AlderOverflowActions` 把主要操作放入 `PrimaryActions`，将次要操作放入 `OverflowActions`。每一项是可绑定命令的 `AlderOverflowAction`，`CollapseOnAction` 控制执行后是否收起。应用可按权限和数据上下文重建操作列表。[示例](../../samples/Alder.Avalonia.Gallery/Views/OverflowActionsExample.axaml) · [ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/OverflowActionsViewModel.cs)
