# 表格、看板、日程与地图

这些控件从应用的数据模型生成界面。把数据和顺序保存在业务记录中，把用户上次使用的视图状态保存在偏好设置中。重开页面时先重建数据集合与模板，再用稳定的 ID 恢复选中、展开和定位。

## Table 与 Data Grid

使用 `AlderTable` 展示行、列和选择。需要固定列、拖拽重排、显式配置虚拟化或服务端排序时，使用同一套列定义的 `AlderDataGrid`。下面的 AXAML 放在已声明 `alder`、`local` 命名空间以及 ViewModel `x:DataType` 的视图中。

```xml
<alder:AlderDataGrid Height="320" RowHeight="40"
                     ItemsSource="{CompiledBinding Rows}"
                     RowKeySelector="{CompiledBinding RowKey}"
                     SelectionMode="Multiple" ShowSelectionCheckboxes="True"
                     SelectedKeys="{CompiledBinding SelectedKeys, Mode=TwoWay}"
                     SortDescriptor="{CompiledBinding Sort, Mode=TwoWay}">
  <alder:AlderDataGrid.Columns>
    <alder:AlderTableColumn Key="title" Header="任务" Width="*"
                            CanSort="True" SortComparison="{CompiledBinding ByTitle}">
      <alder:AlderTableColumn.CellTemplate>
        <DataTemplate x:DataType="local:TaskRow">
          <TextBlock Text="{CompiledBinding Title}" />
        </DataTemplate>
      </alder:AlderTableColumn.CellTemplate>
    </alder:AlderTableColumn>
  </alder:AlderDataGrid.Columns>
</alder:AlderDataGrid>
```

例如，令 `TaskRow` 为 `record TaskRow(string Id, string Title)`，`Rows` 为 `ObservableCollection<TaskRow>`，`RowKey` 为 `Func<object, object>`（`row => ((TaskRow)row).Id`），`ByTitle` 为 `Comparison<object>`（比较两个 `TaskRow` 的标题）。`SelectedKeys` 使用 `IReadOnlyCollection<object>?`，`Sort` 使用 `AlderTableSort?`。由应用代码重新赋值时，通过 ViewModel 的属性变更通知更新控件。

| 操作 | 接入方式 |
| --- | --- |
| 树形行 | 提供 `ChildrenSelector`，设置 `TreeColumnKey`，双向绑定 `ExpandedKeys` |
| 服务端排序 | 设 `SortMode="External"`，在 `SortChangedCommand` 中使用收到的 `AlderTableSort` 重新查询。初次载入时用已保存的排序发起首个请求 |
| 增量加载 | 用 `HasMoreItems`、`IsLoading` 和 `LoadMoreCommand` 管理后续页。由业务命令防止重复请求，查询条件变更时丢弃旧请求的结果 |
| 拖拽行 | 开启 `CanReorderRows` 并绑定 `ReorderCommand`，根据 `DataGridReorderRequest` 的行键、目标键、位置和父键更新数据源。默认在排序生效时关闭拖拽 |
| 从上次状态继续 | 保存选中和展开项的业务 ID，以及排序的列 `ColumnKey` 和 `Direction`。恢复时保留 ID 的原始类型，例如整数键恢复为整数。丢弃失效列和已删除的项 |

为网格提供有限高度，例如放在 `Grid` 的星号行中，让网格内部滚动。`VisibleRowCount` 统计的是当前展开后展平视图中的总行数。验证虚拟化时，应在有限视口内检查实际生成的行容器。[Table 示例](../../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) · [Data Grid 示例](../../samples/Alder.Avalonia.Gallery/Views/DataGridExample.axaml) · [Data Grid ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DataGridViewModel.cs)

## Kanban

`AlderKanban.ItemsSource` 提供列。列模板用 `AlderKanbanColumn` 声明 `Key`、`Header` 和卡片的 `ItemsSource`。拖拽命令收到 `KanbanMoveRequest`，其中 `SourceColumn` 和 `TargetColumn` 是列的 `Key`，`Item` 是卡片对象，`TargetIndex` 是应用移动后的插入位置。同列移动时可用 `ObservableCollection.Move`，跨列时从源集合移除后插入目标集合。提交前用当前集合重新定位卡片，以处理拖动期间的数据更新。

例如，在项目数据中保存如下记录。由拖拽命令更新卡片的列 ID 和顺序。打开看板时先按列 ID 分组，再按每列的顺序构建绑定集合。跨列移动时，源列和目标列的顺序应一并保存。

```json
{
  "columns": [
    { "id": "todo", "title": "待办", "taskIds": ["t-12", "t-18"] },
    { "id": "doing", "title": "进行中", "taskIds": ["t-09"] }
  ]
}
```

[列与卡片模板](../../samples/Alder.Avalonia.Gallery/Views/KanbanExample.axaml) · [同列和跨列移动代码](../../samples/Alder.Avalonia.Gallery/ViewModels/KanbanViewModel.cs)

## File Tree

```csharp
using Alder.Avalonia.Controls;

var folder = new AlderFileTreeNode("project-id", "Project")
{
    IsFolder = true,
    IsExpanded = true
};
folder.Children.Add(new AlderFileTreeNode("notes-id", "notes.md"));
var tree = new AlderFileTree { ItemsSource = new[] { folder } };
```

节点用 `Key` 标识，`Children` 维护层级。需要拖拽时，按 Gallery 示例启用拖放并绑定 `MoveCommand`。命令收到 `AlderFileTreeMove`，根据 `Items`、`Target` 和 `Position` 判断移动范围。应用负责权限判断、防止把目录移入自己的子树，以及在真实文件移动成功后更新节点集合。

保存应用自己的节点记录，例如 `id`、`parentId`、顺序和名称。节点从文件系统生成时，刷新后可按稳定 ID 恢复状态。展开状态存节点键，重新构建时设置各节点的 `IsExpanded`。多选也可保存键，加载后将键解析成新的节点对象再恢复选择。[File Tree 示例](../../samples/Alder.Avalonia.Gallery/Views/FileTreeExample.axaml) · [拖拽与多选 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/FileTreeViewModel.cs)

## Agenda

`Events` 接收 `AlderAgendaEvent` 的可观察集合。应用把新增、移动、调整结束时间和删除接到 `EditCommand`，接收 `AlderAgendaEdit`。创建时分配稳定 ID，修改时按 ID 替换记录，删除时从集合移除。业务校验和写入存储都在命令中完成。下面是 ViewModel 里的移动或调整时长分支。

```csharp
using Alder.Avalonia.Controls;
using System.Collections.ObjectModel;

static void UpdateTimes(ObservableCollection<AlderAgendaEvent> events, AlderAgendaEdit edit)
{
    for (int i = 0; i < events.Count; i++)
    {
        if (events[i].Id != edit.Event?.Id)
            continue;

        events[i] = events[i] with { Start = edit.Start, End = edit.End };
        return;
    }
}
```

保存业务事件的 ID、标题、时间及全天标记。控件的 `Start` 和 `End` 是日程时区中的墙上时间。保存实际时刻时，在应用层保留时区并完成映射。全天事件的结束日期是排他的，例如 11 月 26 日这一天的全天事件结束于 11 月 27 日。恢复视图时可以再设置 `DisplayDate`、`View` 和 `SelectedEventId`。[Agenda 示例](../../samples/Alder.Avalonia.Gallery/Views/AgendaExample.axaml) · [完整编辑 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/AgendaViewModel.cs)

## Map

`AlderMap` 使用宿主提供的地理数据。`Basemap` 可放底图多边形，`Paths` 画线路或区域，`RoadLayers` 按缩放层级绘制路网，`Markers` 提供地点及其弹窗内容。

```csharp
using Alder.Avalonia.Controls;

var station = new MapCoordinate(31.2304, 121.4737);
var map = new AlderMap
{
    Center = station,
    Zoom = 12,
    Markers = new[] { new AlderMapMarker(station, "站点 A") }
};
```

上面的代码只提供一个点位。实际显示道路和边界时，从应用加载对应地理图层。用 `FlyTo(center, zoom)` 实现从结果列表定位。用户偏好可保存 `Center`、`Zoom` 与 `Bearing`。点位详情在应用中保存自己的 ID，加载后将 ID 对应到当前 `Markers` 的对象，再设置 `SelectedMarker`。[地图图层和定位示例](../../samples/Alder.Avalonia.Gallery/Views/MapExample.axaml) · [Map ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/MapViewModel.cs)

## 其他数据展示

| 控件 | 数据与状态 |
| --- | --- |
| List View | 绑定 `ItemsSource` 与项目模板，按需要启用单选或多选。对需恢复的选择保存业务键，再映射回已重建的行对象。[示例](../../samples/Alder.Avalonia.Gallery/Views/ListViewExample.axaml) |
| Carousel | 绑定 `ItemsSource` 与 `ItemTemplate`，用 `SelectedIndex` 或 `GoTo` 恢复停靠位置。内容顺序可能改变时保存项目 ID，重新计算索引。[示例](../../samples/Alder.Avalonia.Gallery/Views/CarouselExample.axaml) |
| Floating TOC | 准备标题条目和内容锚点，配置 `ScrollHost` 连接正文滚动。记住阅读位置时保存业务章节 ID。[示例](../../samples/Alder.Avalonia.Gallery/Views/FloatingTocExample.axaml) |
| Timeline、KPI、Widget | 用事件、指标或小组件配置重建展示。持久化原始值及其时间、单位等业务信息。[Timeline](../../samples/Alder.Avalonia.Gallery/Views/TimelineExample.axaml) · [KPI](../../samples/Alder.Avalonia.Gallery/Views/KpiExample.axaml) · [Widget](../../samples/Alder.Avalonia.Gallery/Views/WidgetExample.axaml) |
