# Forms, selection, and file input

[简体中文](forms.md) · **English**

Bind editable fields two-way to ViewModel properties that raise change notifications. Read the application values from the ViewModel when saving a form. When reopening one, load the options and constraints before assigning the saved values. For localized forms, persist structured values such as dates, numbers, colors, and option IDs. The control and current language determine the display format.

| Control | Binding and persistence |
| --- | --- |
| Input, Input Group, Text Area, Search Field | Bind input with `Text`. Input Group can combine prefixes, suffixes, and actions. The application handles search submission and result loading. Save the query text if needed. [Input](../../samples/Alder.Avalonia.Gallery/Views/InputExample.axaml) · [Text Area](../../samples/Alder.Avalonia.Gallery/Views/TextAreaExample.axaml) · [Search Field](../../samples/Alder.Avalonia.Gallery/Views/SearchFieldExample.axaml) |
| Number Field, Number Stepper | Bind numbers through `Value`. Set the range and step based on the field requirements. Persist the number and use `FormatString` to display units if needed. [Number Field](../../samples/Alder.Avalonia.Gallery/Views/NumberFieldExample.axaml) · [Number Stepper](../../samples/Alder.Avalonia.Gallery/Views/NumberStepperExample.axaml) |
| CheckBox, Switch, Cell Switch, Toggle Button | Bind Boolean state through `IsChecked`. If a third state is meaningful, enable it on a control that supports it and persist a nullable value. [CheckBox](../../samples/Alder.Avalonia.Gallery/Views/CheckBoxExample.axaml) · [Switch](../../samples/Alder.Avalonia.Gallery/Views/NotificationExample.axaml) · [Cell Switch](../../samples/Alder.Avalonia.Gallery/Views/CellSwitchExample.axaml) |
| Choice, Radio Group, Checkbox Button Group, Radio Button Group | Assign each option a key and bind the group's selection property to the ViewModel. For example, `AlderRadioGroup.SelectedKeys` accepts the selected keys. [Choice](../../samples/Alder.Avalonia.Gallery/Views/ChoiceExample.axaml) · [Radio Group](../../samples/Alder.Avalonia.Gallery/Views/RadioGroupExample.axaml) · [Checkbox Button Group](../../samples/Alder.Avalonia.Gallery/Views/CheckboxButtonGroupExample.axaml) · [Radio Button Group](../../samples/Alder.Avalonia.Gallery/Views/RadioButtonGroupExample.axaml) |
| Select, Cell Select, Inline Select, Native Select | Provide a list of options and bind `SelectedItem` or `SelectedIndex`. If options can move, persist an option ID and resolve it to the corresponding item or index on load. [Choosing a select control](../COMPONENTS.en.md#choosing-form-controls) · [Native Select](../../samples/Alder.Avalonia.Gallery/Views/NativeSelectExample.axaml) |
| ComboBox, Autocomplete | Track suggestions, the selected item, and the text being edited separately. Decide whether to persist a selected item or allow a custom value. [ComboBox](../../samples/Alder.Avalonia.Gallery/Views/ComboBoxExample.axaml) · [Autocomplete](../../samples/Alder.Avalonia.Gallery/Views/AutocompleteExample.axaml) |
| Slider, Cell Slider, Rating | Bind `Value` two-way, keep the application units, and restore the value within the same range. The basic Slider example uses Avalonia Slider with `AlderSliderTheme`. [Slider](../../samples/Alder.Avalonia.Gallery/Views/SliderExample.axaml) · [Cell Slider](../../samples/Alder.Avalonia.Gallery/Views/CellSliderExample.axaml) · [Rating](../../samples/Alder.Avalonia.Gallery/Views/RatingExample.axaml) |
| Input OTP | Read the current code from `Text` and clear it according to the application's submission and timeout behavior. [Example](../../samples/Alder.Avalonia.Gallery/Views/InputOtpExample.axaml) |

## Date, time, and ranges

`AlderDateField` and `AlderDatePicker` expose a `DateOnly?` value. `AlderTimeField` and `AlderTimePicker` use `TimeOnly?`, and `AlderDateRangePicker` uses `AlderDateRange?`. Save the two endpoints of a date range separately. Persist them as structured fields or in a fixed format so reopening the form works in any display language.

```xml
<alder:AlderDatePicker Value="{CompiledBinding AppointmentDate, Mode=TwoWay}"
                       DisplayFormat="dd MMM yyyy" ParseFormat="yyyyMMdd"
                       IsRequired="True" />
<alder:AlderTimePicker Value="{CompiledBinding AppointmentTime, Mode=TwoWay}"
                       DisplayFormat="h:mm tt" ParseFormat="HHmmss" />
```

`DisplayFormat` controls segment order, separators, and digit counts. Date segments cover years, months, days, month names, and weekdays. Time segments cover hours, minutes, seconds, and AM/PM. When you set `DisplayFormat` on a time field, `h`, `H`, and `s` determine the hour cycle and whether seconds are shown. When you omit the format, `Culture`, `Clock`, and `Granularity` apply. Keyboard editing still works segment by segment, while `ParseFormat` defines exact parsing for pastes and complete text. Both endpoints of a date range share the same formats.

On a standalone `AlderCalendar` or `AlderRangeCalendar`, `DisplayFormat` formats date headings. On an `AlderAgenda`, it formats the time axis and event time labels.

To place a calendar directly on a page, bind `Value` on `AlderCalendar` or `AlderRangeCalendar`. Use `Minimum`, `Maximum`, and validation properties to express the input constraints. When requirements change, check whether saved values are still valid in the ViewModel. [Date and time examples](../COMPONENTS.en.md#input--editing) · [Date range ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DateRangePickerViewModel.cs)

## Selecting and applying colors

Use a single `AlderColorValue` to represent the color and bind it to `Value` on `AlderColorField`, `AlderColorArea`, and `AlderColorSlider`. Pass preset swatches to `ItemsSource` and bind `SelectedItem`. To apply a picker change only after confirmation, set `CommitOnApply="True"`. Bind the live preview to `DraftValue` and the applied color to `Value`.

To persist the color's appearance, read the `R`, `G`, `B`, and `A` bytes with `Value.ToColor()` and restore them with `AlderColorValue.FromColor(Color.FromArgb(a, r, g, b))`. Save the color space or preset as well if those choices matter to your application. [Color controls](../COMPONENTS.en.md#colors) · [Color ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ColorsViewModel.cs)

## Drop Zone

`AlderDropZone` handles file browsing and drag and drop. Configure `AllowMultiple`, `FileTypeFilter`, `MaxFiles`, and `MaxFileSize`, then handle accepted and rejected files in `FilesChanged`. Each accepted entry's `File` is a handle the host can read. Update the entry's `Status` and `Progress` during upload or import. Use `RetryRequested` to route retries to the same processing logic.

Once reading, uploading, or copying succeeds, save the application file ID, file name, and any required metadata. Rebuild the uploaded file list from those records when reopening the form. To read the local file again, use a persistent reference the platform supports or ask the user to pick it again. [Drop Zone example](../../samples/Alder.Avalonia.Gallery/Views/DropZoneExample.axaml) · [File feedback and retry ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/DropZoneViewModel.cs)
