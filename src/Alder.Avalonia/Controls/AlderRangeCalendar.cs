using Avalonia;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>An inclusive date interval. Reversed endpoints are normalized.</summary>
public readonly record struct AlderDateRange
{
    /// <summary>The earlier endpoint of the inclusive interval.</summary>
    public DateOnly Start { get; }
    /// <summary>The later endpoint of the inclusive interval.</summary>
    public DateOnly End { get; }

    /// <summary>Creates an inclusive interval, ordering the two supplied dates if necessary.</summary>
    /// <param name="start">The first supplied endpoint.</param>
    /// <param name="end">The second supplied endpoint.</param>
    public AlderDateRange(DateOnly start, DateOnly end)
    {
        Start = start <= end ? start : end;
        End = start <= end ? end : start;
    }

    /// <summary>Determines whether the specified date is within the inclusive interval.</summary>
    /// <param name="date">The date to look for within the interval.</param>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    internal bool IsAvailable(Func<DateOnly, bool>? unavailable, bool allowGaps)
    {
        if (unavailable is null) return true;
        if (unavailable(Start) || unavailable(End)) return false;
        if (allowGaps) return true;
        for (int n = Start.DayNumber + 1; n < End.DayNumber; n++)
            if (unavailable(DateOnly.FromDayNumber(n))) return false;
        return true;
    }
}

/// <summary>Selects a date range and can preview its end before the selection is committed.</summary>
public sealed class AlderRangeCalendar : AlderCalendarBase
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderDateRange?> ValueProperty =
        AvaloniaProperty.Register<AlderRangeCalendar, AlderDateRange?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="AllowNonContiguousRanges"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowNonContiguousRangesProperty =
        AvaloniaProperty.Register<AlderRangeCalendar, bool>(nameof(AllowNonContiguousRanges));
    /// <summary>Identifies the <see cref="IsRangeEndUnavailable"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, DateOnly, bool>?> IsRangeEndUnavailableProperty =
        AvaloniaProperty.Register<AlderRangeCalendar, Func<DateOnly, DateOnly, bool>?>(nameof(IsRangeEndUnavailable));

    /// <summary>The committed inclusive date interval. A range being previewed is kept separately.</summary>
    public AlderDateRange? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>Whether the interval may contain unavailable interior dates.</summary>
    public bool AllowNonContiguousRanges { get => GetValue(AllowNonContiguousRangesProperty); set => SetValue(AllowNonContiguousRangesProperty, value); }
    /// <summary>A predicate that rejects a proposed second date for the current anchor date.</summary>
    public Func<DateOnly, DateOnly, bool>? IsRangeEndUnavailable { get => GetValue(IsRangeEndUnavailableProperty); set => SetValue(IsRangeEndUnavailableProperty, value); }

    /// <summary>Raised when the user commits a completed date range.</summary>
    public event EventHandler? RangeSelected;

    private DateOnly? anchor;
    private AlderDateRange? preview;

    /// <inheritdoc />
    protected override DateOnly? SelectionDate => Value?.Start;
    /// <inheritdoc />
    protected override AlderDateRange? HighlightedRange => anchor is null ? Value : preview;
    /// <inheritdoc />
    protected override bool IsSelected(DateOnly date) => false;
    /// <inheritdoc />
    protected override bool IsUnavailable(DateOnly date) =>
        base.IsUnavailable(date) || anchor is { } start && date != start && IsRangeEndUnavailable?.Invoke(start, date) == true;

    /// <inheritdoc />
    protected override void SelectDate(DateOnly date)
    {
        if (anchor is not { } start)
        {
            anchor = date;
            preview = new AlderDateRange(date, date);
            RefreshDates();
            return;
        }
        var range = new AlderDateRange(start, date);
        if (!range.IsAvailable(IsDateUnavailable, AllowNonContiguousRanges)) return;
        anchor = null;
        preview = null;
        SetCurrentValue(ValueProperty, range);
        RefreshDates();
        RangeSelected?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void HoverDate(DateOnly? date)
    {
        if (anchor is not { } start) return;
        var target = date is { } d && IsInBounds(d) && !IsUnavailable(d) ? d : start;
        var proposed = new AlderDateRange(start, target);
        var next = proposed.IsAvailable(IsDateUnavailable, AllowNonContiguousRanges) ? proposed : new AlderDateRange(start, start);
        if (preview == next) return;
        preview = next;
        RefreshDates();
    }

    /// <inheritdoc />
    protected override bool CancelPreview()
    {
        if (anchor is null) return false;
        anchor = null;
        preview = null;
        RefreshDates();
        return true;
    }

    internal void DiscardPreview() => CancelPreview();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            anchor = null;
            preview = null;
            SelectionChanged();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsDateUnavailableProperty ||
                 change.Property == IsRangeEndUnavailableProperty || change.Property == AllowNonContiguousRangesProperty)
            CancelPreview();
    }
}
