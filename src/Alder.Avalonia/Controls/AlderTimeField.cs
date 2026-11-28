using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Selects a locale-derived, 12-hour, or 24-hour editing pattern.</summary>
public enum TimeFieldClock
{
    /// <summary>Uses the input culture's clock convention.</summary>
    Automatic,
    /// <summary>Uses a 12-hour clock and an AM or PM period.</summary>
    TwelveHour,
    /// <summary>Uses a 24-hour clock.</summary>
    TwentyFourHour
}
/// <summary>Identifies the smallest time unit exposed by the input.</summary>
public enum TimeFieldGranularity
{
    /// <summary>Edits hours and minutes.</summary>
    Minute,
    /// <summary>Includes seconds in the editor.</summary>
    Second
}

/// <summary>Edits a nullable time through culture-aware time segments.</summary>
public partial class AlderTimeField : AlderTemporalField
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeOnly?> ValueProperty =
        AvaloniaProperty.Register<AlderTimeField, TimeOnly?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);
    /// <summary>Identifies the <see cref="Minimum"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeOnly?> MinimumProperty = AvaloniaProperty.Register<AlderTimeField, TimeOnly?>(nameof(Minimum));
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeOnly?> MaximumProperty = AvaloniaProperty.Register<AlderTimeField, TimeOnly?>(nameof(Maximum));
    /// <summary>Identifies the <see cref="Clock"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeFieldClock> ClockProperty =
        AvaloniaProperty.Register<AlderTimeField, TimeFieldClock>(nameof(Clock));
    /// <summary>Identifies the <see cref="Granularity"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeFieldGranularity> GranularityProperty =
        AvaloniaProperty.Register<AlderTimeField, TimeFieldGranularity>(nameof(Granularity));

    /// <summary>The committed time of day. Null represents an empty field.</summary>
    public TimeOnly? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The earliest time accepted by the editor.</summary>
    public TimeOnly? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>The latest time accepted by the editor.</summary>
    public TimeOnly? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>The clock convention applied to the editing pattern.</summary>
    public TimeFieldClock Clock { get => GetValue(ClockProperty); set => SetValue(ClockProperty, value); }
    /// <summary>The finest editable time unit.</summary>
    public TimeFieldGranularity Granularity { get => GetValue(GranularityProperty); set => SetValue(GranularityProperty, value); }

    [GeneratedRegex(@"[\s\u200e\u200f]*t+[\s\u200e\u200f]*")]
    private static partial Regex PeriodPattern();

    [GeneratedRegex(@"[\s:.]*s+")]
    private static partial Regex SecondsPattern();

    /// <inheritdoc />
    protected override string InputPattern
    {
        get
        {
            var format = InputCulture.DateTimeFormat;
            if (HasDisplayFormat)
                return DisplayFormat!.Trim() switch
                {
                    "t" => format.ShortTimePattern,
                    "T" => format.LongTimePattern,
                    _ => DisplayFormat!
                };
            string pattern = Granularity == TimeFieldGranularity.Second ? format.LongTimePattern : format.ShortTimePattern;
            if (Granularity == TimeFieldGranularity.Minute) pattern = SecondsPattern().Replace(pattern, "");
            if (Clock == TimeFieldClock.TwentyFourHour) return PeriodPattern().Replace(pattern.Replace('h', 'H'), "").Trim();
            if (Clock == TimeFieldClock.TwelveHour)
            {
                pattern = pattern.Replace('H', 'h');
                if (!pattern.Contains('t')) pattern += " tt";
            }
            return pattern;
        }
    }

    internal bool ShowsSeconds => HasDisplayFormat ? HasPatternPart('s') : Granularity == TimeFieldGranularity.Second;

    /// <inheritdoc />
    protected override DateTime ReferenceValue => DateTime.Now;
    /// <inheritdoc />
    protected override DateTime? ReadValue() => Value is { } time ? DateTime.Today.Add(time.ToTimeSpan()) : null;
    /// <inheritdoc />
    protected override void WriteValue(DateTime? value) =>
        SetCurrentValue(ValueProperty, value is { } date ? TimeOnly.FromDateTime(date) : null);

    /// <inheritdoc />
    protected override bool IsAllowed(DateTime value)
    {
        var time = TimeOnly.FromDateTime(value);
        return Minimum is { } min && Maximum is { } max && min > max
            ? time >= min || time <= max
            : !(time < Minimum || time > Maximum);
    }

    /// <inheritdoc />
    protected override bool TryParseValue(string text, out DateTime value)
    {
        bool parsed = HasParseFormat
            ? TimeOnly.TryParseExact(text, ParseFormat, InputCulture, DateTimeStyles.None, out var time)
            : TimeOnly.TryParse(text, InputCulture, DateTimeStyles.AllowWhiteSpaces, out time);
        if (parsed)
        {
            value = ReferenceValue.Date.Add(time.ToTimeSpan());
            return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClockProperty || change.Property == GranularityProperty) RebuildSegments();
        else if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty) SyncSegments();
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        if (property == ValueProperty) global::Avalonia.Controls.DataValidationErrors.SetError(this, error);
        base.UpdateDataValidation(property, state, error);
    }
}
