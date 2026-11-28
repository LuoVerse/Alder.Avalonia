# Component catalog

**English** · [简体中文](COMPONENTS.zh-CN.md)

All components are in `Alder.Avalonia.Controls`. Load `AlderTheme` in your application to compose components in AXAML or C#. For public properties, events, and enums, see the source and the XML API documentation produced by the build. [Theme setup](../README.md#use-in-an-avalonia-app)

## Integration guides

| Task | Guide |
| --- | --- |
| Build, edit, save, and reopen rich text | [Rich Text Editor](guides/rich-text-editor.md) |
| Bind fields, dates, selections, colors, and files | [Forms, selection, and file input](guides/forms.md) |
| Handle table selection, Kanban drag and drop, schedule editing, and map viewports | [Tables, boards, schedules, and maps](guides/data-controls.md) |
| Stream Markdown, restore conversations, and integrate tools and attachments | [AI conversations and Markdown](guides/chat-and-markdown.md) |
| Organize series, configure tooltips, and restore charts | [Charts](guides/charts.md) |
| Restore pages and panel sizes, and integrate commands and action bars | [Navigation, overlays, and action bars](guides/navigation-and-actions.md) |

### Icon properties

An Alder component's `Icon` accepts a Geometry resource, a path string, or an Avalonia icon control. Menu and tab items use Avalonia's `Icon` with the Alder theme's icon slot. Command items, file tree nodes, emoji categories, Markdown block actions, and popup options also accept these values.

Reference `Material.Icons.Avalonia` and `FluentIcons.Avalonia` in the application project. To use Material icons, add `<mi:MaterialIconStyles />` to `Application.Styles`.

```xml
xmlns:mi="using:Material.Icons.Avalonia"
xmlns:fi="using:FluentIcons.Avalonia.Markup"

<alder:AlderButton Content="Geometry" Icon="{StaticResource AlderIconInfo}" />
<alder:AlderButton Content="Material" Icon="{mi:MaterialIconExt Kind=Info}" />
<alder:AlderButton Content="Fluent" Icon="{fi:SymbolIcon Symbol=Info}" />
```

The theme sets icon slot sizes. Button and Item Card use 14, 16, and 20 DIP for Small, Medium, and Large. Rating uses 16, 20, and 28 DIP and preserves fractional fills for custom icons. Badge and Chip choose an `IconSize` based on the control size by default and accept an explicit size. Icons scale proportionally, and custom `IconTemplate` instances still receive the original data.

Built-in window title bar icons use the theme's Geometry resources.

Icon controls generally need a distinct instance per component. Rating, Bloom Menu transitions, and Markdown block actions reuse icon rendering. Rebuild consumers after upgrading. C# code that reads a Geometry from `Icon` can pattern-match with `is Geometry geometry`.

## Choosing form controls

| Scenario | Control | Main usage |
| --- | --- | --- |
| Standard selection | `AlderSelect` | Supply a collection through `ItemsSource` and bind `SelectedItem` or `SelectedIndex`. |
| In-place selection in a sentence or toolbar | `AlderInlineSelect` | Use the compact trigger and configure the popup with `DropDownWidth`, `DropDownPlacement`, and `MaxDropDownHeight`. |
| Selection in a settings list | `AlderCellSelect` | Describe the setting with `Label`, bind the selection, and set the surface through `Variant`. |
| Editable or searchable choices | `AlderComboBox`, `AlderAutocomplete` | Configure bindings for text, selected values, suggestions, and custom values. |
| Color selection and settings rows | `AlderColorPicker`, `AlderCellColorPicker` | Bind `Value` and use `CommitOnApply` to control when it updates. The cell also accepts `Presets`. |
| Always-visible, mutually exclusive options | `AlderSegment` | Set a stable `AlderSegmentItem.Key` and bind `SelectedKey`. |
| Options that each perform an action | `AlderDropdownFlyout`, `AlderMenuFlyout` | Bind `AlderMenuItem` to a command in action menus and context menus. |

Both snippets present the same options. With MVVM, replace the inline collection and use a two-way binding for selection. [Complete Select example](../samples/Alder.Avalonia.Gallery/Views/SelectExample.axaml)

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

## Navigation and window layout

- Use `AlderSidebar` to display a sidebar. Supply groups and items through `AlderSidebarGroup` and `AlderSidebarMenu`. The sidebar's `SelectedItem` holds a single selection shared across groups. Group headings, navigable children, and command rows have their own properties and templates.
- Use `Header` and `HeaderIcon` for the name and brand mark. The icon stays visible in the collapsed icon rail. Enable search with `IsSearchEnabled` and bind `SelectedItem` so search and normal navigation share the same selection.
- Use `AlderAppLayout` to arrange `Sidebar`, `Navbar`, `Toolbar`, `Content`, `Aside`, and `Footer`. Set `SidebarCollapse="Icon"` to use an icon rail. Provide `MobileSidebar` for compact layouts.
- `AlderUserMenu` supports custom menu content, built-in theme switching, and sign-out. `ThemeModeChanged` reports theme changes, and `LogOutCommand` and `LogOutRequested` handle sign-out. The trailing chevron rotates with a 150 ms composition animation. It changes direction immediately with `AlderMotion.Reduced`.
- Use `AlderWindow.ShowTitleBar` to control the default title bar. For a custom drag region, use Avalonia's `WindowDecorationProperties.ElementRole="TitleBar"`.
- `AlderSalashWindow` uses `Background` and right-aligned `Content` to show a background and sign-in card. Set the window's `DataContext` to a ViewModel that implements `IDialogContext`, and bind the card's buttons to application commands. On success, have the ViewModel invoke `RequestClose?.Invoke(this, DialogResult.OK)`. The base class subscribes to the event. Override `CreateNextWindow()` in the derived window's code-behind and read `CloseResult` to choose the next window. Closing from the title bar produces `None`. Returning `null` closes the current window directly. For a successful handoff, the base class shows the next window according to `ShowActivated`, updates the desktop lifetime's `MainWindow` when needed, then closes the old window and clears its MessageBox and Toast hosts. [Gallery window](../samples/Alder.Avalonia.Gallery/Views/SalashPreviewWindow.axaml.cs) · [Gallery ViewModel](../samples/Alder.Avalonia.Gallery/ViewModels/SalashWindowViewModel.cs)

[Inline and MVVM sidebar examples](../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) · [Inline and MVVM AppLayout examples](../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) · [Sidebar binding](../samples/Alder.Avalonia.Gallery/Views/NavigationPreviewSidebar.axaml)

## Data, motion, and AI

| Scenario | Common bindings and extension points |
| --- | --- |
| Tables and data grids | Use `ItemsSource` and column definitions to organize rows in `AlderTable` and `AlderDataGrid`. Use `RowKeySelector` to keep selection attached to the same row after sorting. Provide a bounded viewport for virtualization and check the realized rows while scrolling. |
| File trees and Kanban boards | Use observable collections to track positions and nodes. Drag and drop submits a move request for the application to apply and persist. Use `AlderFileTreeNode.Children` for hierarchy and `IsExpanded` to observe expansion. |
| Resizable layouts | Alternate `AlderResizablePanel` with `AlderResizableHandle` inside `AlderResizable`. Use the panel's `DefaultSize` for its initial size, and bind `Size` to the ViewModel for persistence. |
| Press feedback and animated values | Reuse `AlderPressableFeedback` for press effects and `AlderNumberValue` for animated values. Set `AlderMotion.Reduced` on the view or window to reduce optional animations. |
| Prompt input and model selection | Bind application state to `AlderPromptInput.Text`, `SendCommand`, `StopCommand`, and `IsRunning`. Put `AlderModelSelector` in the tools area and bind `SelectedModel` and `SelectedEffort` two-way. |
| Chat, Markdown, and tool feedback | Use live message, attachment, and tool state. The host drives sending, approval, cancellation, and error recovery. Text and syntax highlighting use the library's native text pipeline. |

[Table](../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) · [Resizable](../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) · [Prompt Input](../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) · [Model Selector](../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml)

## Display and feedback

| Components | Common patterns |
| --- | --- |
| Button, ButtonGroup, Toolbar | Use `Command` and `CommandParameter` for application actions. Organize buttons in groups or toolbars and derive visibility and availability from selection, permissions, or request state. |
| Alert, Empty State, Skeleton, Spinner, Loader, Text Shimmer | Show feedback for requests, errors, empty results, and loading. On reopen, choose the feedback from current data and request state. |
| Meter, Progress, Number Value, Trend Chip, Emoji Reaction Button | Bind progress, values, trends, or reaction counts to application data. Persist the raw metrics or user reactions and render them with the current animation and styling. |
| Card, Surface, Separator, Item Card, Holo Card | Hold content, divide regions, and display data items. Persist the underlying models or application layout preferences. |
| Avatar, AvatarGroup, Typography, Kbd, Badge, Chip | Build displays from data, avatar URLs, roles, text, and keyboard hints. For removable chips, bind the application's removal action. |
| ScrollShadow, Pressable Feedback | Indicate scroll edges and pressed states. To restore scrolling, record the position or content anchor in the host. |

## Gallery example index

These tables follow the Gallery sidebar. Each link opens the example source for the page with previews and code tabs. Shared entries list the views shown on the same page. When adding a component, choose a sidebar group based on its primary use and add its index entry to the same group.

The Gallery home page brings together basic and advanced controls. The sidebar configuration and view entry point are also available in source. [GalleryHomeView](../samples/Alder.Avalonia.Gallery/Views/GalleryHomeView.cs) · [GalleryWindowViewModel](../samples/Alder.Avalonia.Gallery/ViewModels/GalleryWindowViewModel.cs) · [GalleryWindow](../samples/Alder.Avalonia.Gallery/Views/GalleryWindow.axaml.cs)

### Actions & Menus

| Gallery page | Example source |
| --- | --- |
| Button | [Button](../samples/Alder.Avalonia.Gallery/Views/ButtonExample.axaml) · [ButtonGroup](../samples/Alder.Avalonia.Gallery/Views/ButtonGroupExample.axaml) |
| ToggleButton | [ToggleButton](../samples/Alder.Avalonia.Gallery/Views/ToggleButtonExample.axaml) |
| Toolbar | [Toolbar](../samples/Alder.Avalonia.Gallery/Views/ToolbarExample.axaml) |
| Action Bar | [ActionBar](../samples/Alder.Avalonia.Gallery/Views/ActionBarExample.axaml) |
| Expandable Action Bar | [ExpandableActionBar](../samples/Alder.Avalonia.Gallery/Views/ExpandableActionBarExample.axaml) |
| Overflow Actions | [OverflowActions](../samples/Alder.Avalonia.Gallery/Views/OverflowActionsExample.axaml) |
| Command palette | [Command](../samples/Alder.Avalonia.Gallery/Views/CommandExample.axaml) |
| Menu | [Menu](../samples/Alder.Avalonia.Gallery/Views/MenuExample.axaml) |
| Context Menu | [ContextMenu](../samples/Alder.Avalonia.Gallery/Views/ContextMenuExample.axaml) |
| Bloom Menu | [BloomMenu](../samples/Alder.Avalonia.Gallery/Views/BloomMenuExample.axaml) |
| Emoji Reaction Button | [EmojiReactionButton](../samples/Alder.Avalonia.Gallery/Views/EmojiReactionButtonExample.axaml) |

`AlderMenu` provides a desktop menu bar. Add `AlderMenuItem` entries and submenus, and bind `Command` and `CommandParameter` for actions. An underscore in `Header` marks an access key. `ToggleType` supports checkable and radio items. Dropdowns and submenus enter with a composition animation near their trigger. Switching menus or pressing Esc closes the current menu immediately. Context submenus use the same entrance.

`AlderBloomMenu` expands the trigger button from its center into a three-column menu. Add `AlderBloomMenuItem` objects through `Items` or bind data through `ItemsSource` and `ItemTemplate`. Each action accepts `Command`, `CommandParameter`, and `Icon`.

`IsOpen` binds two-way by default. Bind `Header` to the label shared by the trigger and expanded heading, and `Icon` to the trigger's `Geometry`. The default theme shows a plus sign. Set `Icon="{x:Null}"` to hide it. Use `CloseLabel` to name the close button for assistive technology. The animation follows beUI and the surface uses Alder overlay resources. [beUI web preview](https://beui.dev/components/blocks/bloom-menu) · [Component documentation](https://beui.dev/components/blocks/bloom-menu.md)

`AlderActionBar.SurfaceBackdropBlurRadius` defaults to `0`. When enabled, the blur follows the bar as it opens and closes. Use a translucent `Background` to customize the surface color. The Gallery table example switches between regular and blurred surfaces while preserving the current selection.

`AlderExpandableActionBar` is a persistent icon toolbar. Hover, keyboard focus, or the first touch reveals labels.
Bind `IsExpanded` and `SelectedIndex`, and configure behavior with `ExpandOnHover`, `ExpandOnFocus`, and `Size`.
When the toolbar overlays images or scrolling content, set `SurfaceBackdropBlurRadius` to blur the area behind it. The default is `0`. A translucent `Background` supplies a custom tint.
`AlderExpandableActionBarItem` uses a standard button `Command` and supports `Icon`, `Badge`, `Shortcut` hints, and `IconMotion`.
The motion follows beUI and the surface uses Alder toolbar theme resources. [beUI web preview](https://beui.dev/components/blocks/expandable-action-bar) · [Component documentation](https://beui.dev/components/blocks/expandable-action-bar.md)

`AlderOverflowActions` displays `PrimaryActions` and `OverflowActions` in one bar.
Both groups accept `AlderOverflowAction` items with a standard `Command`. Bind `IsExpanded` two-way and configure `CollapseOnAction` and `Size` as needed.
In a narrow layout, the actions can scroll and the expand or collapse button stays at the end. [beUI web preview](https://beui.dev/components/blocks/overflow-actions) · [Component documentation](https://beui.dev/components/blocks/overflow-actions.md)

Set `AlderOverflowActions.SurfaceBackdropBlurRadius` to blur content behind the floating bar. The default is `0`. The blurred region follows the bar as it expands or collapses. A translucent `Background` supplies a custom tint.

### Input & Editing

| Gallery page | Example source |
| --- | --- |
| Input | [Input](../samples/Alder.Avalonia.Gallery/Views/InputExample.axaml) · [InputGroup](../samples/Alder.Avalonia.Gallery/Views/InputGroupExample.axaml) · [InputOtp](../samples/Alder.Avalonia.Gallery/Views/InputOtpExample.axaml) · [NumberField](../samples/Alder.Avalonia.Gallery/Views/NumberFieldExample.axaml) · [SearchField](../samples/Alder.Avalonia.Gallery/Views/SearchFieldExample.axaml) · [TextArea](../samples/Alder.Avalonia.Gallery/Views/TextAreaExample.axaml) |
| Text Field | [TextField](../samples/Alder.Avalonia.Gallery/Views/TextFieldExample.axaml) |
| Fieldset | [Fieldset](../samples/Alder.Avalonia.Gallery/Views/FieldsetExample.axaml) |
| Form | [Form](../samples/Alder.Avalonia.Gallery/Views/FormExample.axaml) |
| Description / Field Error / Error Message | [Field messages](../samples/Alder.Avalonia.Gallery/Views/FieldMessagesExample.axaml) |
| Rich Text Editor | [Build, save, and load](guides/rich-text-editor.md) · [Gallery](../samples/Alder.Avalonia.Gallery/Views/RichTextEditorExample.axaml) |
| Number Stepper | [NumberStepper](../samples/Alder.Avalonia.Gallery/Views/NumberStepperExample.axaml) |
| Slider | [Slider](../samples/Alder.Avalonia.Gallery/Views/SliderExample.axaml) |
| Cell Slider | [CellSlider](../samples/Alder.Avalonia.Gallery/Views/CellSliderExample.axaml) |
| Drop Zone | [DropZone](../samples/Alder.Avalonia.Gallery/Views/DropZoneExample.axaml) |
| Calendars & fields | [Calendar](../samples/Alder.Avalonia.Gallery/Views/CalendarExample.axaml) · [DateField](../samples/Alder.Avalonia.Gallery/Views/DateFieldExample.axaml) · [DatePicker](../samples/Alder.Avalonia.Gallery/Views/DatePickerExample.axaml) · [DateRangePicker](../samples/Alder.Avalonia.Gallery/Views/DateRangePickerExample.axaml) · [RangeCalendar](../samples/Alder.Avalonia.Gallery/Views/RangeCalendarExample.axaml) · [TimeField](../samples/Alder.Avalonia.Gallery/Views/TimeFieldExample.axaml) |
| Agenda | [Agenda](../samples/Alder.Avalonia.Gallery/Views/AgendaExample.axaml) |
| Time Picker | [TimePicker](../samples/Alder.Avalonia.Gallery/Views/TimePickerExample.axaml) |

`AlderTimePicker` and `AlderTimeField` share a nullable `TimeOnly` `Value` and segmented editing. Click the clock or press Alt+↓ to open the picker. Confirm to apply the time, or cancel or press Esc to discard it. `Clock` selects the hour cycle, `Granularity` controls whether seconds appear, and `Minimum` and `Maximum` bound the accepted time.

Set `DisplayFormat` and `ParseFormat` on date and time fields directly in AXAML. The date, date range, and time picker pages in the Gallery compare formats. Calendar headings and agenda time labels also support `DisplayFormat`. [Format and binding examples](guides/forms.md#date-time-and-ranges)

Set `ShowClearButton="True"` on `AlderInput` or `AlderInputGroup` to show a trailing clear button when the field is editable and contains text. Clicking it updates the two-way binding and returns focus to the input. Tab and Shift+Tab skip trailing clear buttons and input group actions.

`AlderTextField` combines a `Label`, a native editor, a `Description`, and field errors. `Text` binds two-way by default and supports `INotifyDataErrorInfo` on the ViewModel. Use `IsInvalid` and `ErrorMessage` for manual validation, or `IsRequired` and `RequiredErrorMessage` for a required field. Set `AcceptsReturn` for multiline editing. In a custom theme, keep the editor name `PART_Input` and bind `Text` two-way so the form can focus it after a failed submit.

`AlderFieldset` groups fields under a `Legend` and `Description` and displays controls in `Actions`. Setting `IsEnabled="False"` disables its fields and actions.

Bind the submit button to `AlderForm.Submit`. A valid form runs the application's `SubmitCommand`. On failure, the form focuses the first invalid field. For asynchronous submission, bind `IsSubmitting` to the application's busy state to prevent duplicate submissions. After resetting the data, call `ResetValidation()` to clear required-field errors. The native editor handles Enter inside an input.

Use `AlderDescription` for helper text and associate a standalone input via `AutomationProperties.HelpText`. `AlderFieldError.Target` reads binding errors from the target control. Set `Text` to supply a custom message. `AlderErrorMessage.Text` displays application errors and can be used outside forms.

`AlderDropZone` lets you bind file selection, retry, and removal to `ICommand` properties on the ViewModel from AXAML.

```xml
<alder:AlderDropZone AllowMultiple="True"
                     FilesChangedCommand="{CompiledBinding UploadCommand}"
                     RetryRequestedCommand="{CompiledBinding RetryCommand}"
                     FileRemovedCommand="{CompiledBinding CancelCommand}" />
```

`FilesChangedCommand` receives `DropZoneFilesChangedEventArgs`. Read upload candidates from `Added` and validation results from `Rejected`. The retry and remove commands receive the corresponding `AlderDropZoneFile`. Replacing a previous file in single-file mode also runs the remove command.
In the ViewModel, open the stream with `file.File.OpenReadAsync()`, call the application API asynchronously, and update `Status` and `Progress`.
The control checks each command's `CanExecute`. To accept additional batches during an upload, queue new files in the command and upload them asynchronously from the ViewModel.

### Options & Toggles

| Gallery page | Example source |
| --- | --- |
| Checkbox | [CheckBox](../samples/Alder.Avalonia.Gallery/Views/CheckBoxExample.axaml) |
| Checkbox Button Group | [CheckboxButtonGroup](../samples/Alder.Avalonia.Gallery/Views/CheckboxButtonGroupExample.axaml) |
| Radio Button Group | [RadioButtonGroup](../samples/Alder.Avalonia.Gallery/Views/RadioButtonGroupExample.axaml) |
| Selection | [Choice](../samples/Alder.Avalonia.Gallery/Views/ChoiceExample.axaml) · [RadioGroup](../samples/Alder.Avalonia.Gallery/Views/RadioGroupExample.axaml) |
| Switch | [Notification](../samples/Alder.Avalonia.Gallery/Views/NotificationExample.axaml) |
| Cell Switch | [CellSwitch](../samples/Alder.Avalonia.Gallery/Views/CellSwitchExample.axaml) |
| Select | [Select](../samples/Alder.Avalonia.Gallery/Views/SelectExample.axaml) |
| Autocomplete | [Autocomplete](../samples/Alder.Avalonia.Gallery/Views/AutocompleteExample.axaml) |
| ComboBox | [ComboBox](../samples/Alder.Avalonia.Gallery/Views/ComboBoxExample.axaml) |
| Native Select | [NativeSelect](../samples/Alder.Avalonia.Gallery/Views/NativeSelectExample.axaml) |
| Inline Select | [InlineSelect](../samples/Alder.Avalonia.Gallery/Views/InlineSelectExample.axaml) |
| Cell Select | [CellSelect](../samples/Alder.Avalonia.Gallery/Views/CellSelectExample.axaml) |
| Emoji Picker | [EmojiPicker](../samples/Alder.Avalonia.Gallery/Views/EmojiPickerExample.axaml) |
| Rating | [Rating](../samples/Alder.Avalonia.Gallery/Views/RatingExample.axaml) |

### Navigation & Flow

| Gallery page | Example source |
| --- | --- |
| Sidebar | [Sidebar](../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) |
| Navbar | [Navbar](../samples/Alder.Avalonia.Gallery/Views/NavbarExample.axaml) |
| Tabs | [ExpandableTabs](../samples/Alder.Avalonia.Gallery/Views/ExpandableTabsExample.axaml) · [MorphingTabs](../samples/Alder.Avalonia.Gallery/Views/MorphingTabsExample.axaml) · [Tabs](../samples/Alder.Avalonia.Gallery/Views/TabsExample.axaml) |
| Segment | [Segment](../samples/Alder.Avalonia.Gallery/Views/SegmentExample.axaml) |
| Breadcrumbs | [Breadcrumbs](../samples/Alder.Avalonia.Gallery/Views/BreadcrumbsExample.axaml) |
| Link | [Link](../samples/Alder.Avalonia.Gallery/Views/LinkExample.axaml) |
| Pagination | [Pagination](../samples/Alder.Avalonia.Gallery/Views/PaginationExample.axaml) |
| Stepper | [Stepper](../samples/Alder.Avalonia.Gallery/Views/StepperExample.axaml) |
| Floating TOC | [FloatingToc](../samples/Alder.Avalonia.Gallery/Views/FloatingTocExample.axaml) |

`AlderNavbar.HeaderBackdropBlurRadius` defaults to `0`. When enabled, content passing behind the header is blurred under a translucent version of its `Background`. The compact menu continues to use the original `Background`.

`AlderFloatingToc.SurfaceBackdropBlurRadius` defaults to `0` and applies only to the expanded table of contents. The Gallery's illustrated article can scroll while the panel is open, so you can switch blur on and off over the same content.

### Layout & Windows

| Gallery page | Example source |
| --- | --- |
| AppLayout | [AppLayout](../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) |
| SalashWindow | [SalashWindow](../samples/Alder.Avalonia.Gallery/Views/SalashWindowExample.axaml) |
| Accordion | [Accordion](../samples/Alder.Avalonia.Gallery/Views/AccordionExample.axaml) |
| Disclosure | [Disclosure](../samples/Alder.Avalonia.Gallery/Views/DisclosureExample.axaml) · [DisclosureGroup](../samples/Alder.Avalonia.Gallery/Views/DisclosureGroupExample.axaml) |
| Card | [Card](../samples/Alder.Avalonia.Gallery/Views/CardExample.axaml) |
| Holo Card | [HoloCard](../samples/Alder.Avalonia.Gallery/Views/HoloCardExample.axaml) |
| Item Card | [ItemCard](../samples/Alder.Avalonia.Gallery/Views/ItemCardExample.axaml) |
| Surface | [Surface](../samples/Alder.Avalonia.Gallery/Views/SurfaceExample.axaml) |
| Separator | [Separator](../samples/Alder.Avalonia.Gallery/Views/SeparatorExample.axaml) |
| Resizable | [Resizable](../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) |
| ScrollShadow | [ScrollShadow](../samples/Alder.Avalonia.Gallery/Views/ScrollShadowExample.axaml) |
| Transitioning Content | [Integration guide](guides/navigation-and-actions.md#transitioning-content) · [Six transition examples](../samples/Alder.Avalonia.Gallery/Views/TransitioningContentExample.axaml) |

`AlderCard.SurfaceBackdropBlurRadius` defaults to `0`. When enabled, the blur follows content behind the card and stays within its rounded corners. `Variant` determines the theme surface color. Use a translucent `Background` for a custom tint. The Gallery card example switches between regular and blurred surfaces over the same scrolling photos.

### Overlays & Dialogs

| Gallery page | Example source |
| --- | --- |
| Dialog | [Dialog](../samples/Alder.Avalonia.Gallery/Views/DialogExample.axaml) |
| MessageBox | [MessageBox](../samples/Alder.Avalonia.Gallery/Views/MessageBoxExample.axaml) |
| Drawer | [Drawer](../samples/Alder.Avalonia.Gallery/Views/DrawerExample.axaml) |
| Sheet | [Sheet](../samples/Alder.Avalonia.Gallery/Views/SheetExample.axaml) |
| Popover | [Popover](../samples/Alder.Avalonia.Gallery/Views/PopoverExample.axaml) |
| Hover Card | [HoverCard](../samples/Alder.Avalonia.Gallery/Views/HoverCardExample.axaml) |
| Tooltip | [ToolTip](../samples/Alder.Avalonia.Gallery/Views/ToolTipExample.axaml) |

Set `AlderPopover.SurfaceBackdropBlurRadius` to blur the backdrop of an arrowless surface. The default is `0`. Blur uses the current theme's translucent surface color. For a custom `SurfaceBackground`, use a translucent brush to keep the blur visible. The Background Blur example compares regular and blurred surfaces over the same scrolling photos.

`AlderHoverCard.SurfaceBackdropBlurRadius` likewise applies to arrowless previews and defaults to `0`. A translucent `Background` can override the theme surface color.

### Status & Feedback

| Gallery page | Example source |
| --- | --- |
| Alert | [Alert](../samples/Alder.Avalonia.Gallery/Views/AlertExample.axaml) |
| Toast | [Toast](../samples/Alder.Avalonia.Gallery/Views/ToastExample.axaml) |
| Empty State | [EmptyState](../samples/Alder.Avalonia.Gallery/Views/EmptyStateExample.axaml) |
| Progress | [ProgressBar](../samples/Alder.Avalonia.Gallery/Views/ProgressBarExample.axaml) · [ProgressCircle](../samples/Alder.Avalonia.Gallery/Views/ProgressCircleExample.axaml) |
| Spinner | [Spinner](../samples/Alder.Avalonia.Gallery/Views/SpinnerExample.axaml) |
| Skeleton | [Skeleton](../samples/Alder.Avalonia.Gallery/Views/SkeletonExample.axaml) |
| Loader | [Loader](../samples/Alder.Avalonia.Gallery/Views/LoaderExample.axaml) |
| Text Shimmer | [TextShimmer](../samples/Alder.Avalonia.Gallery/Views/TextShimmerExample.axaml) |
| Pressable Feedback | [PressableFeedback](../samples/Alder.Avalonia.Gallery/Views/PressableFeedbackExample.axaml) |

### Content & Labels

| Gallery page | Example source |
| --- | --- |
| Typography | [Typography](../samples/Alder.Avalonia.Gallery/Views/TypographyExample.axaml) |
| Kbd | [Kbd](../samples/Alder.Avalonia.Gallery/Views/KbdExample.axaml) |
| Markdown | [Markdown](../samples/Alder.Avalonia.Gallery/Views/MarkdownExample.axaml) |
| Code Block | [CodeBlock](../samples/Alder.Avalonia.Gallery/Views/CodeBlockExample.axaml) |
| Image View | [ImageView](../samples/Alder.Avalonia.Gallery/Views/ImageViewExample.axaml) |
| Carousel | [Carousel](../samples/Alder.Avalonia.Gallery/Views/CarouselExample.axaml) |
| Avatar | [Avatar](../samples/Alder.Avalonia.Gallery/Views/AvatarExample.axaml) · [AvatarGroup](../samples/Alder.Avalonia.Gallery/Views/AvatarGroupExample.axaml) |
| Badge | [Badge](../samples/Alder.Avalonia.Gallery/Views/BadgeExample.axaml) |
| Chip | [Chip](../samples/Alder.Avalonia.Gallery/Views/ChipExample.axaml) |
| Tag / Tag Group | [Tag](../samples/Alder.Avalonia.Gallery/Views/TagExample.axaml) |

`AlderTag` supports standalone display, a leading icon, three sizes, and two surface variants. `AlderTagGroup` wraps automatically and accepts inline tags or an `ItemsSource` with an `ItemTemplate`. It supports display-only, single-selection, and multiple-selection modes. Bind `SelectedItem` for single selection or `SelectedItems` for multiple selection.

With `IsRemovable` set, clicking the remove button or pressing Delete or Backspace raises `RemoveRequested`. The group's `RemoveCommand` can pass the original data item to the ViewModel. Update the source collection in the application. When a focused tag is removed, focus moves to the next available tag, then tries the previous tag. Once all tags are removed, Tab moves out of the group.

Set `AlderImageView.Source` to a single `IImage`, or supply `AlderImageViewItem` objects through `Items` or `ItemsSource`. Bind `SelectedIndex` or `SelectedItem` two-way.
Each item can provide a `Title`, `FileName`, accessible `Description`, and separate `Thumbnail`.
Use `ViewportHeight` for the embedded canvas height and `ShowDetails`, `ShowThumbnails`, and `AllowImmersivePreview` to configure the UI. Observe state through `Zoom` and `IsImmersive`, and control the view with `Fit()`, `ShowActualSize()`, `TryOpenImmersive()`, and `CloseImmersive()`.
Set `ToolbarBackdropBlurRadius` above `0` to float the toolbar at the bottom of the canvas and blur the image behind it. At the default of `0`, the toolbar sits below the image.
The mouse wheel zooms around the pointer. Double-click to switch between fit and actual size, and drag to pan when zoomed in. Keyboard shortcuts include `+` / `−`, `0`, `1`, `←` / `→`, `F`, and `Esc`. Actual size maps bitmap pixels to physical screen pixels. Thumbnails use Alder Carousel, and the immersive transition follows the beUI Image Viewer. [beUI Image Viewer](https://beui.dev/components/motion/image-viewer)
Set `ImageTransition` to choose the effect, for example `ImageTransition="Slide"`. Options are `CrossFade` (the default), `Slide` (horizontal movement in the browsing direction), `Zoom` (centered scaling), `Lift` (upward entrance), and `None` (instant switching).
Transitions use 200 ms composition keyframes. With `AlderMotion.Reduced` enabled, the target image appears immediately.

### Data Views

| Gallery page | Example source |
| --- | --- |
| Table | [Table](../samples/Alder.Avalonia.Gallery/Views/TableExample.axaml) |
| Data Grid | [DataGrid](../samples/Alder.Avalonia.Gallery/Views/DataGridExample.axaml) |
| List View | [ListView](../samples/Alder.Avalonia.Gallery/Views/ListViewExample.axaml) |
| File Tree | [FileTree](../samples/Alder.Avalonia.Gallery/Views/FileTreeExample.axaml) |
| Tree View | [TreeView](../samples/Alder.Avalonia.Gallery/Views/TreeViewExample.axaml) |
| Kanban | [Kanban](../samples/Alder.Avalonia.Gallery/Views/KanbanExample.axaml) |
| Timeline | [Timeline](../samples/Alder.Avalonia.Gallery/Views/TimelineExample.axaml) |
| Map | [Map](../samples/Alder.Avalonia.Gallery/Views/MapExample.axaml) |
| Widget | [Widget](../samples/Alder.Avalonia.Gallery/Views/WidgetExample.axaml) |
| Workflow Editor | [Workflow Editor](../samples/Alder.Avalonia.Gallery/Views/WorkflowEditorExample.axaml) · [Binding and JSON format](../samples/Alder.Avalonia.WorkflowPrototype/README.md) |

`AlderTreeView` supports inline `AlderTreeViewItem` controls or hierarchical data bound through `ItemsSource` and `TreeDataTemplate`. Read selections from `SelectedItem` or `SelectedItems` and configure single, multiple, or range selection through `SelectionMode`. When expanding or collapsing, subtree height and rows below it move together while the chevron rotates. Set `AlderMotion.Reduced="True"` on the host for an immediate change.

`AlderMap.ZoomControlsBackdropBlurRadius` applies only to the bottom-right zoom controls and defaults to `0`. The Gallery route planner lets you toggle it while preserving the map center and zoom level.

### Charts & Metrics

| Gallery page | Example source |
| --- | --- |
| Area Chart | [AreaChart](../samples/Alder.Avalonia.Gallery/Views/AreaChartExample.axaml) |
| Bar Chart | [BarChart](../samples/Alder.Avalonia.Gallery/Views/BarChartExample.axaml) |
| Line Chart | [LineChart](../samples/Alder.Avalonia.Gallery/Views/LineChartExample.axaml) |
| Pie Chart | [PieChart](../samples/Alder.Avalonia.Gallery/Views/PieChartExample.axaml) |
| Radar Chart | [RadarChart](../samples/Alder.Avalonia.Gallery/Views/RadarChartExample.axaml) |
| Radial Chart | [RadialChart](../samples/Alder.Avalonia.Gallery/Views/RadialChartExample.axaml) |
| Composed Chart | [ComposedChart](../samples/Alder.Avalonia.Gallery/Views/ComposedChartExample.axaml) |
| Chart Tooltip | [ChartTooltip](../samples/Alder.Avalonia.Gallery/Views/ChartTooltipExample.axaml) |
| KPI | [Kpi](../samples/Alder.Avalonia.Gallery/Views/KpiExample.axaml) |
| Meter | [Meter](../samples/Alder.Avalonia.Gallery/Views/MeterExample.axaml) |
| Trend Chip | [TrendChip](../samples/Alder.Avalonia.Gallery/Views/TrendChipExample.axaml) |
| Number Value | [NumberValue](../samples/Alder.Avalonia.Gallery/Views/NumberValueExample.axaml) |

### Colors

| Gallery page | Example source |
| --- | --- |
| Cell Color Picker | [CellColorPicker](../samples/Alder.Avalonia.Gallery/Views/CellColorPickerExample.axaml) |
| Color area | [ColorArea](../samples/Alder.Avalonia.Gallery/Views/ColorAreaExample.axaml) |
| Color field | [ColorField](../samples/Alder.Avalonia.Gallery/Views/ColorFieldExample.axaml) |
| Color picker | [ColorPicker](../samples/Alder.Avalonia.Gallery/Views/ColorPickerExample.axaml) |
| Color slider | [ColorSlider](../samples/Alder.Avalonia.Gallery/Views/ColorSliderExample.axaml) |
| Color swatch | [ColorSwatch](../samples/Alder.Avalonia.Gallery/Views/ColorSwatchExample.axaml) |
| Color swatch picker | [ColorSwatchPicker](../samples/Alder.Avalonia.Gallery/Views/ColorSwatchPickerExample.axaml) |

### AI Chat

| Gallery page | Example source |
| --- | --- |
| Chat Conversation | [ChatConversation](../samples/Alder.Avalonia.Gallery/Views/ChatConversationExample.axaml) |
| Chat List View | [ChatListView](../samples/Alder.Avalonia.Gallery/Views/ChatListViewExample.axaml) |
| Chat Message | [ChatMessage](../samples/Alder.Avalonia.Gallery/Views/ChatMessageExample.axaml) |
| Chat Message Actions | [ChatMessageActions](../samples/Alder.Avalonia.Gallery/Views/ChatMessageActionsExample.axaml) |
| Chat Attachment | [ChatAttachment](../samples/Alder.Avalonia.Gallery/Views/ChatAttachmentExample.axaml) |
| Chat Source | [ChatSource](../samples/Alder.Avalonia.Gallery/Views/ChatSourceExample.axaml) |
| Chat Tool | [ChatTool](../samples/Alder.Avalonia.Gallery/Views/ChatToolExample.axaml) |
| Chat Loader | [ChatLoader](../samples/Alder.Avalonia.Gallery/Views/ChatLoaderExample.axaml) |
| Prompt Input | [PromptInput](../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) |
| Prompt Suggestion | [PromptSuggestion](../samples/Alder.Avalonia.Gallery/Views/PromptSuggestionExample.axaml) |
| Model Selector | [ModelSelector](../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml) |
| Reasoning | [Reasoning](../samples/Alder.Avalonia.Gallery/Views/ReasoningExample.axaml) |
| Chain Of Thought | [ChainOfThought](../samples/Alder.Avalonia.Gallery/Views/ChainOfThoughtExample.axaml) |
| Approval Card | [ApprovalCard](../samples/Alder.Avalonia.Gallery/Views/ApprovalCardExample.axaml) |
