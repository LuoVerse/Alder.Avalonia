# Navigation, overlays, and action bars

**English** · [简体中文](navigation-and-actions.zh-CN.md)

## AppLayout and Sidebar

Use `AlderAppLayout` to host `Sidebar`, `Navbar`, `Toolbar`, `Content`, `Aside`, and `Footer`. Organize sidebar items with `AlderSidebarGroup` and `AlderSidebarMenu`. Set `IsSearchEnabled` to enable search across the sidebar. Bind `AlderSidebar.SelectedItem` to the navigation model and use the selected item's route to open content.

Save the route key and separate sidebar preferences, such as whether the desktop sidebar was left open. Load the available routes before restoring the saved item. For an invalid route, open the application's default page. Let the compact layout determine whether the sidebar is initially expanded on mobile. [AppLayout example](../../samples/Alder.Avalonia.Gallery/Views/AppLayoutExample.axaml) · [Sidebar example](../../samples/Alder.Avalonia.Gallery/Views/SidebarExample.axaml) · [Navigation binding ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/NavigationBindingViewModel.cs)

| Control | Data and restoration |
| --- | --- |
| Tabs, Expandable Tabs, Morphing Tabs | Build tabs and content from models or templates and bind the current selection. Save a tab key and resolve it to the item or index after loading the tabs. [Tabs](../../samples/Alder.Avalonia.Gallery/Views/TabsExample.axaml) · [Expandable](../../samples/Alder.Avalonia.Gallery/Views/ExpandableTabsExample.axaml) · [Morphing](../../samples/Alder.Avalonia.Gallery/Views/MorphingTabsExample.axaml) |
| Segment | Assign a stable `AlderSegmentItem.Key` and bind `SelectedKey` two-way. A saved key restores the same selection if the options are reordered. [Example](../../samples/Alder.Avalonia.Gallery/Views/SegmentExample.axaml) |
| Stepper, Pagination | Derive the current step and completion states from the flow model. For pagination, use `CurrentPage` and `PageCount` and restore or reset the page number based on the new result count. [Stepper](../../samples/Alder.Avalonia.Gallery/Views/StepperExample.axaml) · [Pagination](../../samples/Alder.Avalonia.Gallery/Views/PaginationExample.axaml) |
| Accordion, Disclosure | Compose headings, expandable items, and content. Associate expansion state with stable item keys where needed. [Accordion](../../samples/Alder.Avalonia.Gallery/Views/AccordionExample.axaml) · [Disclosure Group](../../samples/Alder.Avalonia.Gallery/Views/DisclosureGroupExample.axaml) |
| Breadcrumbs, Navbar, Link | Generate navigation from the application's routes and current page. Use links or commands to navigate. [Breadcrumbs](../../samples/Alder.Avalonia.Gallery/Views/BreadcrumbsExample.axaml) · [Navbar](../../samples/Alder.Avalonia.Gallery/Views/NavbarExample.axaml) · [Link](../../samples/Alder.Avalonia.Gallery/Views/LinkExample.axaml) |

## Transitioning Content

Bind the current page, step, or detail object to `AlderTransitioningContentControl.Content`. Use `ContentTemplate` for pages of the same type or declare templates for different types in `DataTemplates`. The first content appears immediately. Replacing `Content` plays the transition.

```xml
<alder:AlderTransitioningContentControl
    Transition="Slide" MinHeight="180" Padding="18"
    Content="{CompiledBinding CurrentPage}"
    ContentTemplate="{StaticResource PageTemplate}"
    IsTransitionReversed="{CompiledBinding IsNavigatingBack}" />
```

Update `IsNavigatingBack` before replacing `CurrentPage`. By default, navigation moves forward. The horizontal slide enters from the right, the rise enters from below, and the reveal starts on the left. These directions reverse when going back. Use `Height` to size the preview area or `MinHeight` to reserve space for shorter content. Set the outer frame with `Background`, `BorderBrush`, `BorderThickness`, and `CornerRadius`, and set content spacing with `Padding`.

| `Transition` | Default duration | Behavior |
| --- | ---: | --- |
| `CrossFade` | 180 ms | Fades both layers in place. This is the default. |
| `Slide` | 260 ms | Slides both pages horizontally by the same distance. |
| `Rise` | 220 ms | Moves the incoming content by 24 DIP with a crossfade. |
| `Scale` | 240 ms | Shrinks the old content slightly and scales the new content from 94% to full size. |
| `Blur` | 260 ms | Blurs the old content and brings the new content into focus. |
| `Reveal` | 300 ms | Moves a horizontal mask boundary across the viewport, revealing the new page while the old page remains on the other side. |
| `None` | Instant | Shows new content immediately. |

`Duration` accepts a bindable `TimeSpan?`. For example, `Duration="0:0:0.4"` sets it to 400 ms, and zero switches content immediately. Setting `AlderMotion.Reduced="True"` on the window or any ancestor gives an in-place crossfade of at most 120 ms. Use inherited `PageTransition` to supply a custom Avalonia transition. To release resources when a page finishes exiting, subscribe to `TransitionCompleted` and check `HasRunToCompletion` and `From`.

The Gallery has separate Preview, AXAML, and C# tabs for all six animated transitions. Each example lets you navigate both ways among Overview, Activity, and Releases, try slow motion, and enable reduced motion. [Gallery example](../../samples/Alder.Avalonia.Gallery/Views/TransitioningContentExample.axaml) · [Binding ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/TransitioningContentViewModel.cs)

## Resizable

Alternate `AlderResizablePanel` and `AlderResizableHandle` inside `AlderResizable`. The example below lays out two panels horizontally. Use `GridLength?` for the ViewModel's `SidebarSize` and `bool` for `SidebarCollapsed`.

```xml
<alder:AlderResizable>
  <alder:AlderResizablePanel DefaultSize="240" MinSize="160" IsCollapsible="True"
                              Size="{CompiledBinding SidebarSize, Mode=TwoWay}"
                              IsCollapsed="{CompiledBinding SidebarCollapsed, Mode=TwoWay}">
    <TextBlock Text="Sidebar" />
  </alder:AlderResizablePanel>
  <alder:AlderResizableHandle />
  <alder:AlderResizablePanel DefaultSize="*">
    <TextBlock Text="Content" />
  </alder:AlderResizablePanel>
</alder:AlderResizable>
```

Persist the size and unit together, such as 240 DIP or 1 star. Restore a `GridLength` and initialize `IsCollapsed` from the saved state. On first open, leave `Size` unset to use `DefaultSize`. To remember the size before collapse, store the last expanded size separately in the application. [Layout example](../../samples/Alder.Avalonia.Gallery/Views/ResizableExample.axaml) · [Resize binding ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ResizableViewModel.cs)

## Command Palette

Populate `AlderCommand.Groups` with `AlderCommandGroup` objects. Define each action with `Label`, `Keywords`, and `Command`, or use nested `Groups` for a submenu. Open the palette with `ShowAsync(owner)`.

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
                Heading = "Application",
                Items = new[]
                {
                    new AlderCommandItem
                    {
                        Label = "Open settings",
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

Store application command IDs for favorites and history, and resolve them through the current command registry. Reset transient state such as the search query and the active keyboard item when the palette closes. [Command example](../../samples/Alder.Avalonia.Gallery/Views/CommandExample.axaml) · [Groups, submenus, and preview ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/CommandViewModel.cs)

## Dialog, Drawer, Sheet, and menus

Provide a `DialogHost` for overlays, then await `Dialog.ShowModalAsync`, `Drawer.ShowAsync`, `Sheet.ShowAsync`, or `MessageBox.ShowAsync`. For a standalone desktop window, use `Dialog.ShowWindowAsync`. Keep a temporary draft for dialog forms and commit it after confirmation. To restore unsubmitted edits, persist the draft and record ID and recreate the dialog content on load.

```csharp
using System.Threading.Tasks;
using Alder.Avalonia.Controls;
using Avalonia.Controls;

static async Task<bool> ConfirmAsync(Control owner)
{
    var result = await MessageBox.ShowAsync(
        "Save your changes?",
        new MessageBoxOptions { Buttons = DialogButtons.YesNo },
        owner: owner);
    return result == DialogResult.Yes;
}
```

| Scenario | Integration |
| --- | --- |
| Dialog, Drawer, Sheet | Create content from a view or ViewModel. Choose a modal confirmation, side panel, or edge-aligned surface for the task. [Dialog](../../samples/Alder.Avalonia.Gallery/Views/DialogExample.axaml) · [Drawer](../../samples/Alder.Avalonia.Gallery/Views/DrawerExample.axaml) · [Sheet](../../samples/Alder.Avalonia.Gallery/Views/SheetExample.axaml) |
| Context Menu, Popover, Bloom Menu | Use the trigger to provide the application context and bind each item's command and parameters. Use open state to control the current interaction. [Context Menu](../../samples/Alder.Avalonia.Gallery/Views/ContextMenuExample.axaml) · [Popover](../../samples/Alder.Avalonia.Gallery/Views/PopoverExample.axaml) · [Bloom Menu](../../samples/Alder.Avalonia.Gallery/Views/BloomMenuExample.axaml) |
| Emoji Picker | Bind `SelectedEmoji` and supply a writable list of recent choices through `RecentEmojis`. Persist emoji strings and populate the list on reopen. [Example](../../samples/Alder.Avalonia.Gallery/Views/EmojiPickerExample.axaml) |
| Toast, Tooltip, Hover Card | Show a toast based on an action's result. Use the current element and its data to populate tooltips and hover cards. [Toast](../../samples/Alder.Avalonia.Gallery/Views/ToastExample.axaml) · [Tooltip](../../samples/Alder.Avalonia.Gallery/Views/ToolTipExample.axaml) · [Hover Card](../../samples/Alder.Avalonia.Gallery/Views/HoverCardExample.axaml) |

## Action Bar, Expandable Action Bar, and Overflow Actions

- Use `AlderActionBar` for bulk actions on selected rows. Derive `IsOpen` and action availability from the application selection, and use `Prefix` to show the selected count. Recompute action state from the restored selection keys. [Example](../../samples/Alder.Avalonia.Gallery/Views/ActionBarExample.axaml) · [Action ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ActionBarViewModel.cs)
- Use `AlderExpandableActionBar` for a persistent icon toolbar. Use `AlderExpandableActionBarItem.Command` to execute actions and `IconMotion` to set icon animations. To remember the selected tool, save its application ID and map it back to `SelectedIndex`. Hover, focus, or application settings drive expansion. [Example](../../samples/Alder.Avalonia.Gallery/Views/ExpandableActionBarExample.axaml) · [ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/ExpandableActionBarViewModel.cs)
- In `AlderOverflowActions`, assign the main actions to `PrimaryActions` and secondary actions to `OverflowActions`. Each item is an `AlderOverflowAction` with a bindable command. Set `CollapseOnAction` to control whether the overflow collapses after an action. The application can rebuild the action lists based on permissions and current data. [Example](../../samples/Alder.Avalonia.Gallery/Views/OverflowActionsExample.axaml) · [ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/OverflowActionsViewModel.cs)
