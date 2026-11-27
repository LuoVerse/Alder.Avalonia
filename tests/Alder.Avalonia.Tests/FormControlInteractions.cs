using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;

using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace Alder.Avalonia.Tests;

public sealed class FormControlInteractions
{
    [AvaloniaFact]
    public void InputClearButtonUpdatesTheBindingAndMatchesOtherSuffixActions()
    {
        var model = new InputTextModel();
        var suffix = new AlderButton { Size = AlderSize.Small, IsIconOnly = true, Variant = ButtonVariant.Ghost };
        var input = new AlderInputGroup { ShowClearButton = true, Suffix = suffix, Width = 260 };
        input.Bind(AlderInput.TextProperty, CompiledBinding.Create((InputTextModel vm) => vm.Text,
            mode: BindingMode.TwoWay, source: model));
        var window = Show(input);
        try
        {
            var clear = Part<AlderButton>(input, "PART_ClearButton");
            Assert.True(clear.IsEffectivelyVisible);
            Assert.Equal(suffix.Bounds.Size, clear.Bounds.Size);
            Assert.Equal(suffix.Size, clear.Size);
            Assert.Equal(suffix.Variant, clear.Variant);

            var point = clear.TranslatePoint(new Rect(clear.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump(window);
            Assert.Equal("", model.Text);
            Assert.False(clear.IsEffectivelyVisible);
            Assert.True(input.IsFocused);
            Assert.True(suffix.IsEffectivelyVisible);

            input.SetCurrentValue(AlderInput.TextProperty, "New text");
            Assert.Equal("New text", model.Text);
            Assert.True(clear.IsEffectivelyVisible);
            input.IsReadOnly = true;
            Assert.False(clear.IsEffectivelyVisible);
            Assert.Equal("New text", input.Text);
        }
        finally { window.Close(); }
    }

    private sealed class InputTextModel
    {
        public string? Text { get; set; } = "Alder";
    }

    [AvaloniaFact]
    public void TabbingToFieldActionsMovesTheFocusIndicatorOffTheEditor()
    {
        var reveal = new AlderButton { Content = "Reveal" };
        var nested = new AlderButton { Content = "Custom" };
        var input = new AlderInputGroup { Text = "Secret", Type = InputType.Password, ShowClearButton = true,
            Suffix = new StackPanel { Orientation = Orientation.Horizontal, Children = { reveal, nested } } };
        var search = new AlderSearchField { Text = "Search" };
        var numberAction = new AlderButton { Content = "Units" };
        var number = new AlderNumberField { Value = 3, ShowButtonSpinner = false, InnerRightContent = numberAction };
        var colorAction = new AlderButton { Content = "Copy" };
        var color = new AlderColorField { Suffix = colorAction };
        var area = new AlderTextArea { Text = "Notes", ShowClearButton = true, Rows = 2 };
        var date = new AlderDatePicker();
        var range = new AlderDateRangePicker();
        var timeAction = new AlderButton { Content = "Now" };
        var time = new AlderTimeField { Suffix = timeAction };
        var promptAction = new AlderButton { Content = "Tool" };
        var prompt = new AlderPromptInput { Tools = promptAction };
        var outside = new AlderButton { Content = "Outside" };
        var darkTime = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Child = time };
        var window = Show(new StackPanel { Children = { input, search, number, color, area, date, range, darkTime, prompt, outside } });
        try
        {
            var clear = Part<AlderButton>(input, "PART_ClearButton");
            MoveToAction(input, input, clear);
            Press(window, PhysicalKey.Tab);
            CheckAction(reveal);
            Press(window, PhysicalKey.Tab);
            CheckAction(nested);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Pump(window);
            CheckAction(reveal);

            var clearSearch = search.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible);
            MoveToAction(search, search, clearSearch);
            Press(window, PhysicalKey.Space);
            Assert.Equal("", search.Text);
            Assert.True(search.IsFocused);
            MoveToAction(number, Part<TextBox>(number, "PART_TextBox"), numberAction);
            MoveToAction(color, color, colorAction);
            MoveToAction(area, area, Part<AlderButton>(area, "PART_ClearButton"));
            MoveToAction(date, LastSegment(date), Part<Button>(date, "PART_CalendarTrigger"));
            MoveToAction(range, LastSegment(Part<AlderDateField>(range, "PART_End")), Part<Button>(range, "PART_CalendarTrigger"));
            MoveToAction(time, LastSegment(time), timeAction);

            Assert.True(Part<TextBox>(prompt, "PART_Editor").Focus());
            Press(window, PhysicalKey.Tab);
            CheckAction(promptAction);
            Assert.True(outside.Focus(NavigationMethod.Tab));
            CheckAction(outside);
        }
        finally { window.Close(); }

        void MoveToAction(Control field, Control editor, Button action)
        {
            Assert.True(editor.Focus());
            Pump(window);
            var ring = field.GetVisualDescendants().OfType<InputFocusRing>().Last(candidate => candidate.Name == "FieldRing");
            Assert.True(ring.BoxShadow[0].Color.A > 0);
            Press(window, PhysicalKey.Tab);
            CheckAction(action);
            Assert.Equal(0, ring.BoxShadow[0].Color.A);
        }

        void CheckAction(Button action)
        {
            Assert.True(action.IsFocused);
            if (action is AlderButton)
                Assert.True(Part<ButtonTint>(action, "Tint").Opacity > 0);
            else if (action is AlderDateTriggerButton)
                Assert.True(Assert.IsAssignableFrom<ISolidColorBrush>(Part<Border>(action, "Face").Background).Color.A > 0);
            Assert.DoesNotContain(action.GetVisualDescendants().OfType<Border>(), border => border.Name == "FocusRing");
        }

        static Button LastSegment(Control field) =>
            Part<StackPanel>(field, "PART_Segments").Children.OfType<Button>().Last();
    }

    [AvaloniaFact]
    public void NativeSelectPopupUsesTheFieldThemeAndSkipsDisabledOptions()
    {
        var select = new AlderNativeSelect
        {
            Width = 180,
            Items =
            {
                new AlderPickerOption { Text = "Ready", Group = "Status" },
                new AlderPickerOption { Text = "Locked", IsEnabled = false, Group = "Status" },
                new AlderPickerOption { Text = "Done", Group = "Status" }
            },
            SelectedIndex = 0
        };
        var scope = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Child = select };
        var window = Show(scope);
        try
        {
            Part<Button>(select, "PART_Trigger").Focus();
            select.IsDropDownOpen = true;
            Pump(window);
            var surface = Part<Control>(window, "AlderPopoverSurface");
            var frame = Assert.Single(surface.GetVisualChildren().OfType<Border>());
            Assert.Equal(Color.Parse("#18181B"), Assert.IsAssignableFrom<ISolidColorBrush>(frame.Background).Color);
            Assert.True(Part<PathIcon>(select, "SelectArrow").RenderTransform!.Value.IsIdentity);

            scope.RequestedThemeVariant = ThemeVariant.Light;
            Pump(window);
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(frame.Background).Color);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(2, select.SelectedIndex);
            Assert.False(select.IsDropDownOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineSelectOpensAboveAndSupportsMultipleSelections()
    {
        var select = new AlderInlineSelect
        {
            Items = { "Design", "Research", "Writing" }, SelectedIndex = 0,
            DropDownPlacement = PlacementMode.TopEdgeAlignedRight, SelectionMode = SelectionMode.Multiple,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(select);
        try
        {
            Part<Button>(select, "PART_Trigger").Focus();
            select.IsDropDownOpen = true;
            Pump(window);
            var frame = Part<Control>(window, "AlderPopoverSurface").GetVisualChildren().OfType<Border>().Single();
            var bottom = frame.TranslatePoint(new Point(0, frame.Bounds.Height), window)!.Value.Y;
            Assert.True(bottom < select.TranslatePoint(default, window)!.Value.Y);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            Assert.Contains("Design", select.SelectedItems!.Cast<string>());
            Assert.Contains("Research", select.SelectedItems!.Cast<string>());
            Assert.True(select.IsDropDownOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineSelectKeepsAllFourOptionsInsideThePopupAndSelectsTheLastWithTheKeyboard()
    {
        var select = new AlderInlineSelect
        {
            Items = { "English", "Spanish", "French", "Japanese" }, SelectedIndex = 0, DropDownWidth = 140,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 40, 0, 0)
        };
        var window = Show(select);
        try
        {
            select.IsDropDownOpen = true;
            Pump(window);
            var surface = Part<Control>(window, "AlderPopoverSurface");
            var list = surface.GetVisualDescendants().OfType<ListBox>().Single();
            var scroll = Part<ScrollViewer>(list, "PART_PickerScroll");
            Assert.Equal(0, scroll.ScrollBarMaximum.Y);
            var last = Assert.IsAssignableFrom<Control>(list.ContainerFromIndex(3));
            var bottom = last.TranslatePoint(new Point(0, last.Bounds.Height), surface)!.Value.Y;
            Assert.True(surface.Bounds.Height - bottom >= 8);

            Press(window, PhysicalKey.End);
            Press(window, PhysicalKey.Enter);
            Assert.Equal("Japanese", select.SelectedItem);
            Assert.False(select.IsDropDownOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineSelectCanReopenDuringItsExit()
    {
        var select = new AlderInlineSelect
        {
            Items = { "English", "Spanish" }, SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(select, reducedMotion: false);
        try
        {
            var trigger = Part<Button>(select, "PART_Trigger");
            var center = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, window)!.Value;
            window.MouseMove(center);
            window.MouseDown(center, MouseButton.Left);
            Pump(window);
            Assert.True(select.IsDropDownOpen);
            window.MouseUp(center, MouseButton.Left);
            var frame = Part<Control>(window, "AlderPopoverSurface");
            Assert.True(frame.Bounds.Width >= 140);

            select.IsDropDownOpen = false;
            Assert.False(frame.IsHitTestVisible);
            select.IsDropDownOpen = true;
            Pump(window);
            Assert.True(select.IsDropDownOpen);
            Assert.True(Part<Control>(window, "AlderPopoverSurface").IsHitTestVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(480, 220, 160)]
    [InlineData(140, 100, 100)]
    public void InlineSelectUsesItsRequestedPopupWidthWithinTheViewport(int windowWidth, int triggerWidth, int popupWidth)
    {
        var select = new AlderInlineSelect
        {
            Items = { "Email", "WhatsApp", "Push Notification" },
            SelectedIndex = 0, Width = triggerWidth, DropDownWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var window = Show(select, width: windowWidth);
        try
        {
            select.IsDropDownOpen = true;
            Pump(window);
            var surface = Part<Control>(window, "AlderPopoverSurface");
            Assert.Equal(popupWidth, surface.Bounds.Width);
            Assert.Equal(popupWidth, Part<ScrollViewer>(surface, "PART_PickerScroll").Bounds.Width);
            var origin = surface.TranslatePoint(default, window)!.Value;
            Assert.InRange(origin.X, 0, windowWidth - surface.Bounds.Width);
            if (popupWidth < triggerWidth)
            {
                var trigger = Part<Button>(select, "PART_Trigger");
                var triggerRight = trigger.TranslatePoint(new Point(trigger.Bounds.Width, 0), window)!.Value.X;
                Assert.Equal(triggerRight, origin.X + surface.Bounds.Width);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task SelectTriggersSettleToTheirHoverBackground()
    {
        foreach (var select in new AlderSelect[] { new AlderInlineSelect(), new AlderNativeSelect() })
        {
            select.Items.Add("English");
            select.SelectedIndex = 0;
            select.VerticalAlignment = VerticalAlignment.Center;
            var window = Show(select, reducedMotion: false);
            try
            {
                var trigger = Part<Button>(select, "PART_Trigger");
                var field = Part<Border>(select, "FieldFill");
                var center = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, window)!.Value;
                window.MouseMove(center);
                Pump(window);
                await Task.Delay(220);
                Pump(window);
                Assert.Equal(Color.Parse("#EBF8F8F8"), Assert.IsAssignableFrom<ISolidColorBrush>(field.Background).Color);
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaFact]
    public async Task DropZoneValidatesARealDropAndAllowsRetryAndRemoval()
    {
        var commands = new DropZoneCommandModel();
        var zone = new AlderDropZone
        {
            FileTypeFilter = [new("Images") { Patterns = ["*.png"] }],
            MaxFileSize = 8, AllowMultiple = true, MaxFiles = 1
        };
        zone.Bind(AlderDropZone.FilesChangedCommandProperty, CompiledBinding.Create((DropZoneCommandModel model) => model.Upload, source: commands));
        zone.Bind(AlderDropZone.RetryRequestedCommandProperty, CompiledBinding.Create((DropZoneCommandModel model) => model.Retry, source: commands));
        zone.Bind(AlderDropZone.FileRemovedCommandProperty, CompiledBinding.Create((DropZoneCommandModel model) => model.Cancel, source: commands));
        var window = Show(zone);
        var directory = Directory.CreateTempSubdirectory("alder-drop-");
        try
        {
            async Task<IStorageFile> FileHandle(string name, int size)
            {
                var path = Path.Combine(directory.FullName, name);
                File.WriteAllBytes(path, new byte[size]);
                return Assert.IsAssignableFrom<IStorageFile>(await window.StorageProvider.TryGetFileFromPathAsync(path));
            }

            using var accepted = await FileHandle("image.PNG", 8);
            using var tooLarge = await FileHandle("large.png", 9);
            using var wrongType = await FileHandle("notes.txt", 8);
            using var overflow = await FileHandle("overflow.png", 2);
            var changed = new TaskCompletionSource<DropZoneFilesChangedEventArgs>();
            zone.FilesChanged += (_, args) => changed.TrySetResult(args);
            var area = Part<Control>(zone, "PART_DropArea");
            using var data = new DataTransfer();
            foreach (var file in new[] { wrongType, tooLarge, accepted, overflow })
                data.Add(DataTransferItem.CreateFile(file));
            var over = new DragEventArgs(DragDrop.DragOverEvent, data, area, new Point(12, 12), KeyModifiers.None)
                { DragEffects = DragDropEffects.Copy | DragDropEffects.Move };
            area.RaiseEvent(over);
            Assert.Equal(DragDropEffects.Copy, over.DragEffects);
            Assert.Contains(":drop-target", zone.Classes);
            area.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, area, new Point(12, 12), KeyModifiers.None)
                { DragEffects = DragDropEffects.Copy });
            var result = await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Pump(window);

            Assert.Same(result, Assert.Single(commands.Upload.Calls));
            Assert.Equal(new[] { DropZoneFileError.Type, DropZoneFileError.Size, DropZoneFileError.Count }, result.Rejected.Select(item => item.Error));
            var fileRecord = Assert.Single(zone.Files);
            Assert.Same(accepted, fileRecord.File);
            Assert.Equal(8ul, fileRecord.Size);
            Assert.DoesNotContain(":drop-target", zone.Classes);
            Assert.True(Part<ItemsControl>(zone, "FileList").IsVisible);
            Assert.True(Part<TextBlock>(zone, "DropError").IsVisible);
            Assert.Contains(zone.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == accepted.Name);

            zone.ShowDropArea = false;
            Pump(window);
            Assert.False(area.IsEffectivelyVisible);
            Assert.True(Part<ItemsControl>(zone, "FileList").IsEffectivelyVisible);

            AlderDropZoneFile? retried = null;
            zone.RetryRequested += (_, item) => retried = item;
            fileRecord.Status = DropZoneFileStatus.Failed;
            Pump(window);
            var retry = zone.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Command == zone.RetryFileCommand);
            commands.Retry.Enabled = false;
            Click(retry);
            Assert.Empty(commands.Retry.Calls);
            commands.Retry.Enabled = true;
            Click(retry);
            Assert.Same(fileRecord, retried);
            Assert.Same(fileRecord, Assert.Single(commands.Retry.Calls));
            Click(zone.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Command == zone.RemoveFileCommand));
            Assert.Empty(zone.Files);
            Assert.Same(fileRecord, Assert.Single(commands.Cancel.Calls));

            await zone.AddFilesAsync([overflow]);
            var replaced = Assert.Single(zone.Files);
            Assert.Same(overflow, replaced.File);
            zone.AllowMultiple = false;
            await zone.AddFilesAsync([accepted]);
            Assert.Same(accepted, Assert.Single(zone.Files).File);
            Assert.Same(replaced, commands.Cancel.Calls.Last());
            Assert.Same(Assert.Single(zone.Files), Assert.Single(commands.Upload.Calls.Last().Added));
            Assert.Equal(DropZoneFileError.None, zone.LastError);

            void Click(Control control)
            {
                var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Pump(window);
            }
        }
        finally
        {
            window.Close();
            directory.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void RadioButtonGroupKeepsAHighestPrioritySelectionAcrossKeyboardAndReflow()
    {
        var first = Radio("first");
        var unavailable = Radio("unavailable");
        unavailable.IsEnabled = false;
        var last = Radio("last");
        var group = new AlderRadioButtonGroup
        {
            Layout = RadioButtonGroupLayout.Grid, SelectedKey = "first",
            Children = { first, unavailable, last }
        };
        var window = Show(group, 600);
        try
        {
            Assert.True(first.IsChecked);
            Assert.Equal(first.Bounds.Top, last.Bounds.Top);
            Assert.True(first.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.ArrowRight);
            Assert.True(last.IsChecked);
            Assert.False(first.IsChecked);
            Assert.Equal("last", group.SelectedKey);

            window.Width = 300;
            Pump(window);
            Assert.True(last.Bounds.Top > unavailable.Bounds.Bottom);
            group.IsReadOnly = true;
            Press(window, PhysicalKey.ArrowLeft);
            Assert.Equal("last", group.SelectedKey);
            group.Children.Remove(last);
            Assert.Null(group.SelectedKey);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RadioCardIndicatorDoesNotNarrowItsDescription()
    {
        var card = new AlderRadioButtonGroupItem
        {
            Width = 154, Content = "Standard", Description = "4-10 business days",
            Indicator = new Border { Width = 20, Height = 20 }
        };
        var window = Show(card);
        try
        {
            var description = Part<TextBlock>(card, "CardDescription");
            var indicator = Part<Panel>(card, "IndicatorSlot");
            Assert.Equal(112, description.Bounds.Width);
            Assert.Equal(new Point(117, 13), indicator.TranslatePoint(default, card));

            card.IndicatorVerticalAlignment = VerticalAlignment.Center;
            Pump(window);
            var center = indicator.TranslatePoint(new Rect(indicator.Bounds.Size).Center, card)!.Value;
            Assert.InRange(Math.Abs(center.Y - card.Bounds.Height / 2), 0, .5);
            Assert.Equal(112, description.Bounds.Width);

            card.ShowIndicator = false;
            Pump(window);
            Assert.False(indicator.IsVisible);
            Assert.Equal(112, description.Bounds.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RadioCardIndicatorsTransitionInBothDirectionsAndHonorReducedMotion()
    {
        var first = Radio("first");
        var last = Radio("last");
        var group = new AlderRadioButtonGroup { SelectedKey = "first", Children = { first, last } };
        var window = Show(group, reducedMotion: false);
        try
        {
            var oldDot = Part<Ellipse>(first, "RadioDot");
            var newDot = Part<Ellipse>(last, "RadioDot");
            var oldRing = Part<Border>(first, "SelectionRing");
            var newRing = Part<Border>(last, "SelectionRing");
            await Task.Delay(220);
            Pump(window);
            Assert.Equal(1, oldDot.Opacity);
            Assert.Equal(0, newDot.Opacity);

            group.SelectedKey = "last";
            Pump(window);
            Assert.False(oldRing.IsVisible);
            Assert.True(newRing.IsVisible);
            var oldFrames = new List<double>();
            var newFrames = new List<double>();
            for (var i = 0; i < 20 && (oldDot.Opacity > 0 || newDot.Opacity < 1); i++)
            {
                await Task.Delay(16);
                Pump(window);
                oldFrames.Add(oldDot.Opacity);
                newFrames.Add(newDot.Opacity);
            }
            Assert.Contains(oldFrames, opacity => opacity is > 0 and < 1);
            Assert.Contains(newFrames, opacity => opacity is > 0 and < 1);
            Assert.Equal(0, oldDot.Opacity);
            Assert.Equal(1, newDot.Opacity);

            AlderMotion.SetReduced(window, true);
            group.SelectedKey = "first";
            Pump(window);
            Assert.Equal(1, oldDot.Opacity);
            Assert.Equal(0, newDot.Opacity);
        }
        finally { window.Close(); }
    }

    private sealed class DropZoneCommandModel
    {
        public RecordingCommand<DropZoneFilesChangedEventArgs> Upload { get; } = new();
        public RecordingCommand<AlderDropZoneFile> Retry { get; } = new();
        public RecordingCommand<AlderDropZoneFile> Cancel { get; } = new();
    }

    private sealed class RecordingCommand<T> : ICommand
    {
        public bool Enabled { get; set; } = true;
        public List<T> Calls { get; } = [];
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Enabled && parameter is T;
        public void Execute(object? parameter) => Calls.Add(Assert.IsAssignableFrom<T>(parameter));
    }

    private static AlderRadioButtonGroupItem Radio(string key)
    {
        var radio = new AlderRadioButtonGroupItem { Content = key };
        AlderChoiceGroup.SetKey(radio, key);
        return radio;
    }

    private static T Part<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static Window Show(Control content, int width = 480, bool reducedMotion = true)
    {
        var window = new AlderTestWindow { Width = width, Height = 440, RequestedThemeVariant = ThemeVariant.Light, Content = content };
        AlderMotion.SetReduced(window, reducedMotion);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

}
