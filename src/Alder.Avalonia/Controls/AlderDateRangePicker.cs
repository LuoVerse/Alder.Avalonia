using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Edits a nullable date range through two date fields or a shared calendar flyout.</summary>
public sealed partial class AlderDateRangePicker : AlderTemporalInput
{
    /// <summary>Identifies the <see cref="Minimum"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> MinimumProperty = AlderDateField.MinimumProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> MaximumProperty = AlderDateField.MaximumProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="IsDateUnavailable"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty =
        AlderDateField.IsDateUnavailableProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="IsRangeEndUnavailable"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, DateOnly, bool>?> IsRangeEndUnavailableProperty =
        AlderRangeCalendar.IsRangeEndUnavailableProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="AllowNonContiguousRanges"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowNonContiguousRangesProperty =
        AlderRangeCalendar.AllowNonContiguousRangesProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorProperty = AlderDatePicker.IndicatorProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="VisibleMonths"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> VisibleMonthsProperty = AlderCalendarBase.VisibleMonthsProperty.AddOwner<AlderDateRangePicker>(
        new StyledPropertyMetadata<int>(coerce: AlderCalendarBase.CoerceVisibleMonths));
    /// <summary>Identifies the <see cref="ShowYearPicker"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowYearPickerProperty = AlderCalendarBase.ShowYearPickerProperty.AddOwner<AlderDateRangePicker>();
    /// <summary>Identifies the <see cref="DateIndicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, IBrush?>?> DateIndicatorProperty =
        AlderCalendarBase.DateIndicatorProperty.AddOwner<AlderDateRangePicker>();

    /// <summary>The committed inclusive date interval. Partial input is held in the editor until a valid interval is available.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay, EnableDataValidation = true)]
    public partial AlderDateRange? Value { get; set; }
    /// <summary>The earliest acceptable endpoint, inclusive.</summary>
    public DateOnly? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>The latest acceptable endpoint, inclusive.</summary>
    public DateOnly? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>A predicate that marks individual dates as unavailable.</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }
    /// <summary>A predicate that rejects an ordered pair of start and end dates.</summary>
    public Func<DateOnly, DateOnly, bool>? IsRangeEndUnavailable { get => GetValue(IsRangeEndUnavailableProperty); set => SetValue(IsRangeEndUnavailableProperty, value); }
    /// <summary>Whether an interval may cross unavailable interior dates.</summary>
    public bool AllowNonContiguousRanges { get => GetValue(AllowNonContiguousRangesProperty); set => SetValue(AllowNonContiguousRangesProperty, value); }
    /// <summary>The geometry for opening the range calendar.</summary>
    public Geometry? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>The number of months shown in the popup calendar, from one to three.</summary>
    public int VisibleMonths { get => GetValue(VisibleMonthsProperty); set => SetValue(VisibleMonthsProperty, value); }
    /// <summary>Whether to show the year picker.</summary>
    public bool ShowYearPicker { get => GetValue(ShowYearPickerProperty); set => SetValue(ShowYearPickerProperty, value); }
    /// <summary>A callback that supplies an optional brush for each cell in the popup calendar.</summary>
    public Func<DateOnly, IBrush?>? DateIndicator { get => GetValue(DateIndicatorProperty); set => SetValue(DateIndicatorProperty, value); }

    /// <inheritdoc />
    protected override Control? FocusTarget => start;
    private AlderDateField? start;
    private AlderDateField? end;
    private Button? trigger;
    private AlderRangeCalendar? calendar;
    private AlderDatePopover? popover;
    private bool updatingFields;
    private bool committingDraft;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        popover?.Hide();
        if (start is not null) start.PropertyChanged -= OnFieldChanged;
        if (end is not null) end.PropertyChanged -= OnFieldChanged;
        if (trigger is not null) trigger.Click -= ToggleCalendar;
        base.OnApplyTemplate(e);
        start = e.NameScope.Find<AlderDateField>("PART_Start");
        end = e.NameScope.Find<AlderDateField>("PART_End");
        trigger = e.NameScope.Find<Button>("PART_CalendarTrigger");
        if (start is not null) start.PropertyChanged += OnFieldChanged;
        if (end is not null) end.PropertyChanged += OnFieldChanged;
        if (trigger is not null) trigger.Click += ToggleCalendar;
        SyncFields();
    }

    /// <summary>Opens the range calendar when the field is editable and attached.</summary>
    public void OpenCalendar() => OpenCalendar(NavigationMethod.Unspecified);

    private void OpenCalendar(NavigationMethod method)
    {
        if (IsReadOnly || !IsEffectivelyEnabled || trigger is null || !this.IsAttachedToVisualTree()) return;
        if (calendar is null)
        {
            calendar = new AlderRangeCalendar();
            calendar.RangeSelected += (_, _) =>
            {
                SetCurrentValue(ValueProperty, calendar.Value);
                SyncFields();
                popover?.Dismiss();
            };
            popover = new AlderDatePopover(calendar);
            popover.Closed += (_, _) => calendar.DiscardPreview();
        }
        UpdateCalendar();
        var date = Value?.Start ?? DateOnly.FromDateTime(DateTime.Today);
        calendar.DisplayDate = date;
        calendar.FocusedDate = date;
        popover!.Open(this, trigger, method);
    }

    /// <summary>Dismisses the range calendar and discards an uncommitted preview.</summary>
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

    private void ToggleCalendar(object? sender, RoutedEventArgs e)
    {
        if (popover?.IsOpen == true) CloseCalendar();
        else OpenCalendar(AlderDateTriggerButton.GetActivationMethod(trigger));
    }

    private void OnFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!updatingFields && (e.Property == AlderDateField.ValueProperty || e.Property == HasInvalidInputProperty))
            EvaluateDraft();
    }

    private void EvaluateDraft()
    {
        if (start is null || end is null) return;
        if (start.HasInvalidInput || end.HasInvalidInput)
        {
            UpdateInputError(true);
            return;
        }
        if (start.Value is not { } first || end.Value is not { } last)
        {
            // A single empty segment is an unfinished edit. Clear the bound value only when both endpoints are empty.
            if (start.Value is null && end.Value is null) Commit(null);
            UpdateInputError(false);
            return;
        }
        bool valid = first <= last && !(first < Minimum || last > Maximum) &&
                     IsRangeEndUnavailable?.Invoke(first, last) != true &&
                     new AlderDateRange(first, last).IsAvailable(IsDateUnavailable, AllowNonContiguousRanges);
        if (valid) Commit(new AlderDateRange(first, last));
        UpdateInputError(!valid);
    }

    private void Commit(AlderDateRange? value)
    {
        committingDraft = true;
        try { SetCurrentValue(ValueProperty, value); }
        finally { committingDraft = false; }
    }

    private void SyncFields()
    {
        if (start is null || end is null) return;
        updatingFields = true;
        try
        {
            start.SetCurrentValue(AlderDateField.ValueProperty, Value?.Start);
            end.SetCurrentValue(AlderDateField.ValueProperty, Value?.End);
        }
        finally { updatingFields = false; }
        EvaluateDraft();
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
        calendar.IsRangeEndUnavailable = IsRangeEndUnavailable;
        calendar.AllowNonContiguousRanges = AllowNonContiguousRanges;
        calendar.DateIndicator = DateIndicator;
        calendar.FlowDirection = FlowDirection;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (!committingDraft) SyncFields();
            UpdateCalendar();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            if (IsReadOnly || !IsEffectivelyEnabled) popover?.Hide();
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == AllowNonContiguousRangesProperty || change.Property == IsDateUnavailableProperty ||
                 change.Property == IsRangeEndUnavailableProperty)
        {
            EvaluateDraft();
            UpdateCalendar();
        }
        else if (change.Property == DateIndicatorProperty || change.Property == VisibleMonthsProperty ||
                 change.Property == CultureProperty || change.Property == ShowYearPickerProperty || change.Property == FlowDirectionProperty)
            UpdateCalendar();
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        if (property == ValueProperty) DataValidationErrors.SetError(this, error);
        base.UpdateDataValidation(property, state, error);
    }
}
