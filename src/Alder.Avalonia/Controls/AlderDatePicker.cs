using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Combines a segmented date field with a calendar flyout for choosing the same value.</summary>
public sealed class AlderDatePicker : AlderDateField
{
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorProperty =
        AvaloniaProperty.Register<AlderDatePicker, Geometry?>(nameof(Indicator));
    /// <summary>Identifies the <see cref="VisibleMonths"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> VisibleMonthsProperty = AlderCalendarBase.VisibleMonthsProperty.AddOwner<AlderDatePicker>(
        new StyledPropertyMetadata<int>(coerce: AlderCalendarBase.CoerceVisibleMonths));
    /// <summary>Identifies the <see cref="ShowYearPicker"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowYearPickerProperty = AlderCalendarBase.ShowYearPickerProperty.AddOwner<AlderDatePicker>();
    /// <summary>Identifies the <see cref="DateIndicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, IBrush?>?> DateIndicatorProperty =
        AlderCalendarBase.DateIndicatorProperty.AddOwner<AlderDatePicker>();

    /// <summary>The geometry for opening the date picker calendar.</summary>
    public Geometry? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>The number of months shown in the popup calendar, from one to three.</summary>
    public int VisibleMonths { get => GetValue(VisibleMonthsProperty); set => SetValue(VisibleMonthsProperty, value); }
    /// <summary>Whether to show the year picker.</summary>
    public bool ShowYearPicker { get => GetValue(ShowYearPickerProperty); set => SetValue(ShowYearPickerProperty, value); }
    /// <summary>A callback that supplies an optional brush for each cell in the popup calendar.</summary>
    public Func<DateOnly, IBrush?>? DateIndicator { get => GetValue(DateIndicatorProperty); set => SetValue(DateIndicatorProperty, value); }

    private Button? trigger;
    private AlderCalendar? calendar;
    private AlderDatePopover? popover;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        popover?.Hide();
        if (trigger is not null) trigger.Click -= ToggleCalendar;
        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_CalendarTrigger");
        if (trigger is not null) trigger.Click += ToggleCalendar;
    }

    /// <summary>Opens the calendar flyout when the field is editable and attached.</summary>
    public void OpenCalendar() => OpenCalendar(NavigationMethod.Unspecified);

    private void OpenCalendar(NavigationMethod method)
    {
        if (IsReadOnly || !IsEffectivelyEnabled || trigger is null || !this.IsAttachedToVisualTree()) return;
        if (calendar is null)
        {
            calendar = new AlderCalendar();
            calendar.DateSelected += (_, _) =>
            {
                SetCurrentValue(ValueProperty, calendar.Value);
                SyncSegments();
                popover?.Dismiss();
            };
            popover = new AlderDatePopover(calendar);
        }
        UpdateCalendar();
        var date = Value ?? PlaceholderDate ?? DateOnly.FromDateTime(DateTime.Today);
        calendar.DisplayDate = date;
        calendar.FocusedDate = date;
        popover!.Open(this, trigger, method);
    }

    /// <summary>Dismisses the calendar flyout if it is open.</summary>
    public void CloseCalendar() => popover?.Dismiss();

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && !IsReadOnly && IsEffectivelyEnabled)
        {
            OpenCalendar(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            if (IsReadOnly || !IsEffectivelyEnabled) popover?.Hide();
        }
        else if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == IsDateUnavailableProperty || change.Property == DateIndicatorProperty ||
                 change.Property == VisibleMonthsProperty || change.Property == CultureProperty ||
                 change.Property == ShowYearPickerProperty || change.Property == FlowDirectionProperty)
            UpdateCalendar();
    }

    private void ToggleCalendar(object? sender, RoutedEventArgs e)
    {
        if (popover?.IsOpen == true) CloseCalendar();
        else OpenCalendar(AlderDateTriggerButton.GetActivationMethod(trigger));
    }

    private void UpdateCalendar()
    {
        if (calendar is null) return;
        calendar.Value = Value;
        calendar.Culture = InputCulture;
        calendar.Minimum = Minimum;
        calendar.Maximum = Maximum;
        calendar.VisibleMonths = VisibleMonths;
        calendar.ShowYearPicker = ShowYearPicker;
        calendar.IsDateUnavailable = IsDateUnavailable;
        calendar.DateIndicator = DateIndicator;
        calendar.FlowDirection = FlowDirection;
    }
}
