# Tables, boards, schedules, and maps

**English** · [简体中文](data-controls.zh-CN.md)

These controls display data from application models. Save data and ordering in application records, and save the user's last view state in preferences. When reopening a page, rebuild the data collections and templates before restoring selection, expansion, and position using stable IDs.

## Table and Data Grid

Use `AlderTable` to show rows, columns, and selection. For pinned columns, drag reordering, explicit virtualization settings, or server-side sorting, use `AlderDataGrid` with the same column definitions. Put the following AXAML in a view that declares the `alder` and `local` namespaces and the ViewModel's `x:DataType`.

```xml
<alder:AlderDataGrid Height="320" RowHeight="40"
                     ItemsSource="{CompiledBinding Rows}"
                     RowKeySelector="{CompiledBinding RowKey}"
                     SelectionMode="Multiple" ShowSelectionCheckboxes="True"
                     SelectedKeys="{CompiledBinding SelectedKeys, Mode=TwoWay}"
                     SortDescriptor="{CompiledBinding Sort, Mode=TwoWay}">
  <alder:AlderDataGrid.Columns>
    <alder:AlderTableColumn Key="title" Header="Task" Width="*"
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

For example, define `TaskRow` as `record TaskRow(string Id, string Title)` and `Rows` as an `ObservableCollection<TaskRow>`. Use a `Func<object, object>` for `RowKey` (`row => ((TaskRow)row).Id`) and a `Comparison<object>` for `ByTitle` (compare the titles of two `TaskRow` objects). Use `IReadOnlyCollection<object>?` for `SelectedKeys` and `AlderTableSort?` for `Sort`. Raise property change notifications when assigning new values from application code.

| Operation | Integration |
| --- | --- |
| Tree rows | Provide `ChildrenSelector`, set `TreeColumnKey`, and bind `ExpandedKeys` two-way. |
| Server-side sorting | Set `SortMode="External"` and requery using the `AlderTableSort` passed to `SortChangedCommand`. Use the saved sort for the first request. |
| Incremental loading | Manage subsequent pages with `HasMoreItems`, `IsLoading`, and `LoadMoreCommand`. Prevent duplicate requests in the application command and discard stale results when query criteria change. |
| Dragging rows | Enable `CanReorderRows` and bind `ReorderCommand`. Update the source using the row key, target key, position, and parent key from `DataGridReorderRequest`. Dragging is disabled by default while sorting is active. |
| Restoring view state | Save the application IDs for selected and expanded items, along with the sort's `ColumnKey` and `Direction`. Restore the original ID types, such as integers for integer keys. Drop missing columns and deleted items. |

Give the grid a bounded height, for example by placing it in a star-sized `Grid` row, so it can scroll internally. `VisibleRowCount` counts all rows in the flattened view after expansion. To verify virtualization, inspect the row containers actually created within a bounded viewport. [Table example](../../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) · [Data Grid example](../../samples/Alder.Avalonia.Gallery/Views/DataGridExample.axaml) · [Data Grid ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DataGridViewModel.cs)

## Kanban

`AlderKanban.ItemsSource` supplies the columns. Use `AlderKanbanColumn` in the column template to declare `Key`, `Header`, and the card `ItemsSource`. The drag command receives a `KanbanMoveRequest`. `SourceColumn` and `TargetColumn` contain the column keys, `Item` is the card object, and `TargetIndex` is the insertion index after the move. Use `ObservableCollection.Move` within a column. Between columns, remove from the source collection and insert into the target. Look up the card in the current collection before committing to account for updates during the drag.

The following record is an example of what to persist. Use the drag command to update the card's column ID and order. When opening the board, group by column ID and build the bound collections in each column's saved order. When moving across columns, save both the source and target order together.

```json
{
  "columns": [
    { "id": "todo", "title": "To do", "taskIds": ["t-12", "t-18"] },
    { "id": "doing", "title": "In progress", "taskIds": ["t-09"] }
  ]
}
```

[Column and card templates](../../samples/Alder.Avalonia.Gallery/Views/KanbanExample.axaml) · [Moving cards within and between columns](../../samples/Alder.Avalonia.Gallery/ViewModels/KanbanViewModel.cs)

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

Use `Key` to identify nodes and `Children` to represent the hierarchy. To support dragging, enable drag and drop and bind `MoveCommand` as shown in the Gallery. The command receives an `AlderFileTreeMove`. Use its `Items`, `Target`, and `Position` to determine the move. The application checks permissions, prevents a directory from moving into its own subtree, and updates the node collection after the file move succeeds.

Persist your own node records with fields such as `id`, `parentId`, order, and name. For nodes generated from the file system, restore state by stable ID after refreshing. Save expanded node keys and set each node's `IsExpanded` when rebuilding. For multi-selection, save selected keys and resolve them to the newly loaded node objects. [File Tree example](../../samples/Alder.Avalonia.Gallery/Views/FileTreeExample.axaml) · [Drag-and-drop and multi-selection ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/FileTreeViewModel.cs)

## Agenda

`Events` accepts an observable collection of `AlderAgendaEvent` objects. Route creation, moves, duration changes, and deletion to `EditCommand`, which receives an `AlderAgendaEdit`. Assign a stable ID when creating an event, replace the record by ID on edit, and remove it on deletion. The command handles application validation and persistence. The code below shows the ViewModel branch for moving or resizing an event.

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

Persist the application event ID, title, time, and all-day flag. The control's `Start` and `End` use wall-clock time in the schedule's time zone. To persist actual instants, retain the time zone and perform the conversion in the application. All-day end dates are exclusive. For example, an all-day event on November 26 ends on November 27. To restore the view, set `DisplayDate`, `View`, and `SelectedEventId`. [Agenda example](../../samples/Alder.Avalonia.Gallery/Views/AgendaExample.axaml) · [Full editing ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/AgendaViewModel.cs)

## Map

`AlderMap` draws geographic data supplied by the host. Use `Basemap` for basemap polygons, `Paths` for lines or regions, `RoadLayers` for road networks by zoom level, and `Markers` for places and their popup content.

```csharp
using Alder.Avalonia.Controls;

var station = new MapCoordinate(31.2304, 121.4737);
var map = new AlderMap
{
    Center = station,
    Zoom = 12,
    Markers = new[] { new AlderMapMarker(station, "Station A") }
};
```

The example supplies one marker. Load the corresponding geographic layers from the application to show roads and boundaries. Use `FlyTo(center, zoom)` to navigate from a result list. Save `Center`, `Zoom`, and `Bearing` as user preferences. Persist application IDs for places, resolve an ID to the current object in `Markers` on load, and set `SelectedMarker`. [Map layers and navigation example](../../samples/Alder.Avalonia.Gallery/Views/MapExample.axaml) · [Map ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/MapViewModel.cs)

## Other data displays

| Control | Data and state |
| --- | --- |
| List View | Bind `ItemsSource` and an item template. Enable single or multiple selection as needed. Persist application keys and map them to the rebuilt row objects when restoring selection. [Example](../../samples/Alder.Avalonia.Gallery/Views/ListViewExample.axaml) |
| Carousel | Bind `ItemsSource` and `ItemTemplate`. Use `SelectedIndex` or `GoTo` to restore position. If the content order can change, save the item ID and recompute its index. [Example](../../samples/Alder.Avalonia.Gallery/Views/CarouselExample.axaml) |
| Floating TOC | Supply heading entries and content anchors. Connect scrolling with `ScrollHost`. To remember the reading position, save the application section ID. [Example](../../samples/Alder.Avalonia.Gallery/Views/FloatingTocExample.axaml) |
| Timeline, KPI, Widget | Rebuild the display from events, metrics, or widget configuration. Persist raw values and application data such as their timestamps and units. [Timeline](../../samples/Alder.Avalonia.Gallery/Views/TimelineExample.axaml) · [KPI](../../samples/Alder.Avalonia.Gallery/Views/KpiExample.axaml) · [Widget](../../samples/Alder.Avalonia.Gallery/Views/WidgetExample.axaml) |
