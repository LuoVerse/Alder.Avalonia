# 表单、选择与文件输入

可编辑字段双向绑定到 ViewModel 的可通知属性。保存表单时从 ViewModel 读取业务值。重新打开时先加载选项与约束，再填入保存的值。涉及本地化时保存日期、数字、颜色或选项 ID 等结构化值，展示格式由控件和当前语言决定。

| 控件 | 绑定与保存 |
| --- | --- |
| Input、Input Group、Text Area、Search Field | 用 `Text` 绑定输入。Input Group 可以组合前后缀与操作。搜索字段的提交和结果加载由应用接管，按需保存查询文本。[Input](../../samples/Alder.Avalonia.Gallery/Views/InputExample.axaml) · [Text Area](../../samples/Alder.Avalonia.Gallery/Views/TextAreaExample.axaml) · [Search Field](../../samples/Alder.Avalonia.Gallery/Views/SearchFieldExample.axaml) |
| Number Field、Number Stepper | 用 `Value` 绑定数值，按业务设置范围和步长。保存数字，可按需要用 `FormatString` 呈现带单位的文本。[Number Field](../../samples/Alder.Avalonia.Gallery/Views/NumberFieldExample.axaml) · [Number Stepper](../../samples/Alder.Avalonia.Gallery/Views/NumberStepperExample.axaml) |
| CheckBox、Switch、Cell Switch、Toggle Button | 用 `IsChecked` 绑定布尔状态。业务有第三种状态时，为支持三态的控件配置相应能力，再保存可空值。[CheckBox](../../samples/Alder.Avalonia.Gallery/Views/CheckBoxExample.axaml) · [Switch](../../samples/Alder.Avalonia.Gallery/Views/NotificationExample.axaml) · [Cell Switch](../../samples/Alder.Avalonia.Gallery/Views/CellSwitchExample.axaml) |
| Choice、Radio Group、Checkbox Button Group、Radio Button Group | 用项键标记业务选项，以相应组的选择属性作为 ViewModel 的状态。比如 `AlderRadioGroup.SelectedKeys` 接收选择键。[Choice](../../samples/Alder.Avalonia.Gallery/Views/ChoiceExample.axaml) · [Radio Group](../../samples/Alder.Avalonia.Gallery/Views/RadioGroupExample.axaml) · [Checkbox Button Group](../../samples/Alder.Avalonia.Gallery/Views/CheckboxButtonGroupExample.axaml) · [Radio Button Group](../../samples/Alder.Avalonia.Gallery/Views/RadioButtonGroupExample.axaml) |
| Select、Cell Select、Inline Select、Native Select | 建立选项列表，用 `SelectedItem` 或 `SelectedIndex` 绑定选择。选项可能重排时保存选项 ID，读取后映射到新的选项对象或索引。[Select 用法](../COMPONENTS.md#在表单中选择组件) · [Native Select](../../samples/Alder.Avalonia.Gallery/Views/NativeSelectExample.axaml) |
| ComboBox、Autocomplete | 分别维护候选项、已选项和编辑中的文本。持久化前由业务决定接收已选项还是允许输入的自定义值。[ComboBox](../../samples/Alder.Avalonia.Gallery/Views/ComboBoxExample.axaml) · [Autocomplete](../../samples/Alder.Avalonia.Gallery/Views/AutocompleteExample.axaml) |
| Slider、Cell Slider、Rating | 双向绑定 `Value`，保留业务单位并以同一范围恢复。Slider 的基本示例通过 `AlderSliderTheme` 使用 Avalonia Slider。[Slider](../../samples/Alder.Avalonia.Gallery/Views/SliderExample.axaml) · [Cell Slider](../../samples/Alder.Avalonia.Gallery/Views/CellSliderExample.axaml) · [Rating](../../samples/Alder.Avalonia.Gallery/Views/RatingExample.axaml) |
| Input OTP | 用 `Text` 取得当前验证码，按应用的提交和超时逻辑清除。[示例](../../samples/Alder.Avalonia.Gallery/Views/InputOtpExample.axaml) |

## 日期、时间和范围

`AlderDateField` 与 `AlderDatePicker` 的 `Value` 使用 `DateOnly?`，`AlderTimeField` 与 `AlderTimePicker` 使用 `TimeOnly?`，`AlderDateRangePicker` 使用 `AlderDateRange?`。把日期范围的开始和结束分别保存。使用结构化字段或固定格式记录，避免按打开时的语言解析上次的显示文本。

```xml
<alder:AlderDatePicker Value="{CompiledBinding AppointmentDate, Mode=TwoWay}"
                       DisplayFormat="dd MMM yyyy" ParseFormat="yyyyMMdd"
                       IsRequired="True" />
<alder:AlderTimePicker Value="{CompiledBinding AppointmentTime, Mode=TwoWay}"
                       DisplayFormat="h:mm tt" ParseFormat="HHmmss" />
```

`DisplayFormat` 决定分段字段的显示顺序、分隔符和数字位数。日期段支持年、月、日、月份名称和星期，时间段支持小时、分钟、秒及上午／下午。时间字段设置 `DisplayFormat` 后，由格式中的 `h`、`H` 和 `s` 决定时制和秒。省略格式时沿用 `Culture`、`Clock` 和 `Granularity`。分段输入沿用键盘编辑，`ParseFormat` 用于粘贴和整段文本的精确解析。日期范围的两个端点共用这两个格式。

独立的 `AlderCalendar` 和 `AlderRangeCalendar` 可以用 `DisplayFormat` 控制日期标题，`AlderAgenda` 用它控制时间轴和事件时间标签。

同样可以绑定 `AlderCalendar` 与 `AlderRangeCalendar` 的 `Value`，视需求把日历放在页面内。用 `Minimum`、`Maximum` 和验证属性表达输入约束。修改约束后，在 ViewModel 中检查历史数据是否仍有效。[日期与时间示例索引](../COMPONENTS.md#输入与编辑) · [日期范围 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DateRangePickerViewModel.cs)

## 调色和提交

用一个 `AlderColorValue` 作为颜色状态，可同时绑定 `AlderColorField`、`AlderColorArea` 和 `AlderColorSlider` 的 `Value`。色板通过 `ItemsSource` 提供预设，并绑定 `SelectedItem`。选择器需要确认后发布时，设 `CommitOnApply="True"`，把实时预览绑定到 `DraftValue`，把最终颜色绑定到 `Value`。

若业务只需恢复颜色外观，用 `Value.ToColor()` 读取 `R`、`G`、`B`、`A` 字节并保存，加载后通过 `AlderColorValue.FromColor(Color.FromArgb(a, r, g, b))` 恢复。若还要恢复用户选择的颜色空间或预设，在记录中保留对应信息。[颜色控件索引](../COMPONENTS.md#颜色) · [颜色 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ColorsViewModel.cs)

## Drop Zone

`AlderDropZone` 处理浏览文件和拖放。配置 `AllowMultiple`、`FileTypeFilter`、`MaxFiles` 和 `MaxFileSize`，在 `FilesChanged` 中接收新增与拒绝的文件。每个接受项的 `File` 是可供宿主读取的文件句柄，上传或导入过程可更新条目的 `Status` 和 `Progress`。通过 `RetryRequested` 将重试交给同一处理逻辑。

宿主在读取、上传或复制成功后，保存业务文件 ID、文件名和所需元数据。重新打开时，从这些记录重建已上传文件列表。应用需要再次读取本地文件时，使用平台允许重新访问的引用，或由用户重新选择。[Drop Zone 示例](../../samples/Alder.Avalonia.Gallery/Views/DropZoneExample.axaml) · [文件反馈与重试 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DropZoneViewModel.cs)
