using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A segmented time field with a flyout for choosing and confirming a time.</summary>
public sealed class AlderTimePicker : AlderTimeField
{
    /// <summary>Identifies the <see cref="Indicator"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorProperty = AlderDatePicker.IndicatorProperty.AddOwner<AlderTimePicker>();

    private Button? trigger;
    private AlderPopover? popover;

    /// <summary>Gets or sets the icon displayed by the picker button.</summary>
    public Geometry? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }

    /// <summary>Gets whether the time selection flyout is open.</summary>
    public bool IsPickerOpen => popover?.IsOpen == true;

    /// <summary>Opens the time selection flyout when the field is editable and attached.</summary>
    public void OpenPicker()
    {
        if (IsPickerOpen || IsReadOnly || !IsEffectivelyEnabled || !this.IsAttachedToVisualTree()) return;
        ApplyTemplate();
        if (trigger is not { } button) return;

        var now = DateTime.Now;
        var seed = Value ?? new TimeOnly(now.Hour, now.Minute, ShowsSeconds ? now.Second : 0);
        if (!IsAllowed(DateTime.Today.Add(seed.ToTimeSpan()))) seed = Minimum ?? Maximum ?? seed;

        var flyout = new AlderPopover
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FocusReturnTarget = button,
            OverlayDismissEventPassThrough = true,
            OverlayInputPassThroughElement = button,
            ContentPadding = new Thickness(12),
            Content = BuildOptions(seed)
        };
        popover = flyout;
        flyout.ShowAt(this);
    }

    /// <summary>Dismisses the flyout without committing its pending selection.</summary>
    public void ClosePicker()
    {
        if (!IsPickerOpen) return;
        if (trigger is { IsEffectivelyEnabled: true } button)
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as InputElement;
            button.Focus(focused?.Classes.Contains(":focus-visible") == true ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
        popover?.Hide();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        popover?.Hide();
        if (trigger is not null) trigger.Click -= TogglePicker;
        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_TimeTrigger");
        if (trigger is not null) trigger.Click += TogglePicker;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        popover?.Hide();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && !IsReadOnly)
        {
            trigger?.Focus(NavigationMethod.Directional);
            OpenPicker();
            e.Handled = IsPickerOpen;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClockProperty || change.Property == CultureProperty || change.Property == GranularityProperty ||
            change.Property == DisplayFormatProperty ||
            change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == ValueProperty ||
            change.Property == IsReadOnlyProperty && IsReadOnly || change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            popover?.Hide();
    }

    private void TogglePicker(object? sender, RoutedEventArgs e)
    {
        if (IsPickerOpen) ClosePicker();
        else OpenPicker();
    }

    private Control BuildOptions(TimeOnly seed)
    {
        var culture = InputCulture;
        bool usePeriod = UsesTwelveHourClock;
        var columns = new Grid { ColumnSpacing = 6 };
        var hours = AddColumn("HourOptions", "AlderText.Hour", Numbers(usePeriod ? 1 : 0, usePeriod ? 12 : 24),
            usePeriod ? (seed.Hour + 11) % 12 : seed.Hour);
        var minutes = AddColumn("MinuteOptions", "AlderText.Minute", Numbers(0, 60), seed.Minute);
        var seconds = ShowsSeconds
            ? AddColumn("SecondOptions", "AlderText.SecondUnit", Numbers(0, 60), seed.Second) : null;
        var periods = usePeriod ? AddColumn("PeriodOptions", "AlderText.Period",
            [culture.DateTimeFormat.AMDesignator, culture.DateTimeFormat.PMDesignator], seed.Hour / 12) : null;

        var error = new AlderErrorMessage();
        error[!TextBlock.TextProperty] = new DynamicResourceExtension("AlderText.TimePickerOutOfRange");
        var apply = new AlderButton { Name = "ApplyTime", Size = AlderSize.Small };
        apply[!ContentControl.ContentProperty] = new DynamicResourceExtension("AlderText.Dialog.OK");
        var cancel = new AlderButton { Name = "CancelTime", Variant = ButtonVariant.Secondary, Size = AlderSize.Small };
        cancel[!ContentControl.ContentProperty] = new DynamicResourceExtension("AlderText.Dialog.Cancel");
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, ItemSpacing = 8, LineSpacing = 6 };
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        var content = new StackPanel { Spacing = 8, Children = { columns, error, actions } };
        KeyboardNavigation.SetTabNavigation(content, KeyboardNavigationMode.Cycle);
        cancel.Click += (_, _) => ClosePicker();
        apply.Click += (_, _) =>
        {
            var draft = ReadDraft();
            if (!CanApply(draft)) return;
            ClosePicker();
            SetCurrentValue(ValueProperty, draft);
        };
        foreach (var list in new[] { hours, minutes, seconds, periods })
            if (list is not null) list.SelectionChanged += (_, _) => Refresh();
        Refresh();
        return content;

        string[] Numbers(int start, int count) => Enumerable.Range(start, count).Select(value => value.ToString("00", culture)).ToArray();

        TimeOnly? ReadDraft()
        {
            if (hours.SelectedIndex < 0 || minutes.SelectedIndex < 0 || seconds?.SelectedIndex < 0 || periods?.SelectedIndex < 0)
                return null;
            int hour = periods is null ? hours.SelectedIndex : (hours.SelectedIndex + 1) % 12 + periods.SelectedIndex * 12;
            return new TimeOnly(hour, minutes.SelectedIndex, seconds?.SelectedIndex ?? seed.Second);
        }

        bool CanApply(TimeOnly? draft) => draft is { } time && IsAllowed(DateTime.Today.Add(time.ToTimeSpan()));

        void Refresh()
        {
            bool valid = CanApply(ReadDraft());
            apply.IsEnabled = valid;
            error.IsVisible = !valid;
        }

        AlderListBox AddColumn(string name, string labelKey, string[] items, int selectedIndex)
        {
            var caption = new AlderDescription { HorizontalAlignment = HorizontalAlignment.Center };
            caption[!TextBlock.TextProperty] = new DynamicResourceExtension(labelKey);
            var list = new AlderListBox { Name = name, ItemsSource = items, SelectedIndex = selectedIndex, Height = 144, MinWidth = 55, Focusable = true };
            list[!ThemeProperty] = new DynamicResourceExtension("AlderTimePickerOptionsTheme");
            list[!ItemsControl.ItemContainerThemeProperty] = new DynamicResourceExtension("AlderTimePickerOptionTheme");
            AutomationProperties.SetLabeledBy(list, caption);
            var column = new StackPanel { Spacing = 4, Children = { caption, list } };
            Grid.SetColumn(column, columns.ColumnDefinitions.Count);
            columns.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            columns.Children.Add(column);
            return list;
        }
    }
}
