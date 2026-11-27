# 图表

图表从数据序列和类别生成视图。先建立业务数据到图表模型的映射，再把数据绑定到控件。下面的折线含两个系列，每个值与同索引的月份对应。`null` 表示该位置缺少值。

```csharp
using Alder.Avalonia.Controls;

var chart = new AlderLineChart
{
    Height = 180,
    Categories = new[] { "一月", "二月", "三月" },
    Series = new[]
    {
        new AlderChartSeries("访问", new double?[] { 120, null, 182 }),
        new AlderChartSeries("注册", new double?[] { 12, 19, 24 })
    },
    ShowTooltip = true
};
```

在 MVVM 中将 `Categories`、`Series` 等绑定到 ViewModel。重新查询或切换时间范围时发布新的数组或列表实例，并发出属性变更通知。可用 `BrushResourceKey` 绑定主题色键，或通过系列的 `Brush` 提供自定义画刷。

| 图表 | 数据模型及选项 |
| --- | --- |
| Line、Area、Bar | `Categories` 和 `Series`。使用 `AlderChartSeries` 提供名称与可空数值。按控件支持的选项设置标记、填充、方向和堆叠。[Line](../../samples/Alder.Avalonia.Gallery/Views/LineChartExample.axaml) · [Area](../../samples/Alder.Avalonia.Gallery/Views/AreaChartExample.axaml) · [Bar](../../samples/Alder.Avalonia.Gallery/Views/BarChartExample.axaml) |
| Composed | 同一组类别下，通过 `AlderChartSeries.Kind` 混合柱、线和面积系列。需要第二个坐标轴时，给相应系列设置 `UseSecondaryAxis`。[示例](../../samples/Alder.Avalonia.Gallery/Views/ComposedChartExample.axaml) |
| Pie | `Slices` 接收 `AlderChartSlice` 集合。用 `InnerRadius` 绘制环形图，传入 `InnerSlices` 时可组合内外两圈。[示例](../../samples/Alder.Avalonia.Gallery/Views/PieChartExample.axaml) |
| Radar | `Categories` 定义维度，`Series` 定义各维度的数值。可为多组可比较数据设置同一 `Maximum`。[示例](../../samples/Alder.Avalonia.Gallery/Views/RadarChartExample.axaml) |
| Radial | `Rings` 接收 `AlderRadialSeries` 集合。每个系列可设置自己的当前值和上限。[示例](../../samples/Alder.Avalonia.Gallery/Views/RadialChartExample.axaml) |
| Tooltip | 用 `TooltipTemplate` 渲染 `AlderChartTooltipData`。模板读取 `Label`、`Entries` 和 `Total`。[示例](../../samples/Alder.Avalonia.Gallery/Views/ChartTooltipExample.axaml) |

## 保存图表配置与恢复

保存时间范围、指标 ID、聚合粒度和用户选择的图表类型。需要离线回放时也保存类别和原始数值，并保留 `null` 值。重新打开页面后用配置发起数据请求，或用已保存的快照重建 `AlderChartSeries`、`AlderChartSlice` 或 `AlderRadialSeries`。主题画刷、数据模板和悬停提示从视图和当前数据重新计算。[Gallery 数据映射](../../samples/Alder.Avalonia.Gallery/ViewModels/ChartsViewModel.cs)
