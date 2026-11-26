using Avalonia;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Controls how many independent dates the calendar can select.</summary>
public enum AlderCalendarSelection
{
    /// <summary>Selects a single date.</summary>
    Single,
    /// <summary>Selects several independent dates.</summary>
    Multiple
}

/// <summary>Displays a calendar for selecting one date or a set of independent dates.</summary>
public sealed class AlderCalendar : AlderCalendarBase
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> ValueProperty =
        AvaloniaProperty.Register<AlderCalendar, DateOnly?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="SelectedDates"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<DateOnly>> SelectedDatesProperty =
        AvaloniaProperty.Register<AlderCalendar, IReadOnlyList<DateOnly>>(nameof(SelectedDates), Array.Empty<DateOnly>(),
            defaultBindingMode: BindingMode.TwoWay, coerce: (_, dates) => dates ?? Array.Empty<DateOnly>());
    /// <summary>Identifies the <see cref="SelectionMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderCalendarSelection> SelectionModeProperty =
        AvaloniaProperty.Register<AlderCalendar, AlderCalendarSelection>(nameof(SelectionMode));

    /// <summary>The selected date in single-selection mode. Multiple selection is represented by SelectedDates.</summary>
    public DateOnly? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The current list of selected dates in multiple-selection mode. The calendar replaces the list when a date is toggled.</summary>
    public IReadOnlyList<DateOnly> SelectedDates { get => GetValue(SelectedDatesProperty); set => SetValue(SelectedDatesProperty, value); }
    /// <summary>Whether the calendar selects a single date or multiple independent dates.</summary>
    public AlderCalendarSelection SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>Raised after a user selects or toggles a date.</summary>
    public event EventHandler? DateSelected;

    /// <inheritdoc />
    protected override DateOnly? SelectionDate => SelectionMode == AlderCalendarSelection.Single
        ? Value : SelectedDates.Count > 0 ? SelectedDates[0] : null;

    /// <inheritdoc />
    protected override bool IsSelected(DateOnly date) => SelectionMode == AlderCalendarSelection.Single
        ? Value == date : SelectedDates.Contains(date);

    /// <inheritdoc />
    protected override void SelectDate(DateOnly date)
    {
        if (SelectionMode == AlderCalendarSelection.Single) SetCurrentValue(ValueProperty, date);
        else
            SetCurrentValue(SelectedDatesProperty, SelectedDates.Contains(date)
                ? SelectedDates.Where(value => value != date).ToArray()
                : [.. SelectedDates, date]);
        DateSelected?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == SelectedDatesProperty || change.Property == SelectionModeProperty)
            SelectionChanged();
    }
}
