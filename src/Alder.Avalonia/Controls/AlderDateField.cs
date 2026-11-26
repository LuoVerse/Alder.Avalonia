using System.Globalization;
using Avalonia;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Edits a nullable date through culture-aware day, month, and year segments.</summary>
public class AlderDateField : AlderTemporalField
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> ValueProperty =
        AvaloniaProperty.Register<AlderDateField, DateOnly?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);
    /// <summary>Identifies the <see cref="Minimum"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> MinimumProperty = AvaloniaProperty.Register<AlderDateField, DateOnly?>(nameof(Minimum));
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> MaximumProperty = AvaloniaProperty.Register<AlderDateField, DateOnly?>(nameof(Maximum));
    /// <summary>Identifies the <see cref="PlaceholderDate"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> PlaceholderDateProperty =
        AvaloniaProperty.Register<AlderDateField, DateOnly?>(nameof(PlaceholderDate));
    /// <summary>Identifies the <see cref="IsDateUnavailable"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> IsDateUnavailableProperty =
        AvaloniaProperty.Register<AlderDateField, Func<DateOnly, bool>?>(nameof(IsDateUnavailable));

    /// <summary>The date committed by the editor. Null represents an empty field.</summary>
    public DateOnly? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The earliest accepted date, inclusive.</summary>
    public DateOnly? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>The latest accepted date, inclusive.</summary>
    public DateOnly? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>The reference date used to display segment placeholders before a value is entered.</summary>
    public DateOnly? PlaceholderDate { get => GetValue(PlaceholderDateProperty); set => SetValue(PlaceholderDateProperty, value); }
    /// <summary>A predicate that rejects specific dates independently of the date bounds.</summary>
    public Func<DateOnly, bool>? IsDateUnavailable { get => GetValue(IsDateUnavailableProperty); set => SetValue(IsDateUnavailableProperty, value); }

    /// <inheritdoc />
    protected override DateTime ReferenceValue => PlaceholderDate?.ToDateTime(TimeOnly.MinValue) ?? base.ReferenceValue;
    /// <inheritdoc />
    protected override string InputPattern => InputCulture.DateTimeFormat.ShortDatePattern;
    /// <inheritdoc />
    protected override DateTime? ReadValue() => Value?.ToDateTime(TimeOnly.MinValue);
    /// <inheritdoc />
    protected override void WriteValue(DateTime? value) =>
        SetCurrentValue(ValueProperty, value is { } date ? DateOnly.FromDateTime(date) : null);

    /// <inheritdoc />
    protected override bool IsAllowed(DateTime value)
    {
        var date = DateOnly.FromDateTime(value);
        var calendar = InputCulture.Calendar;
        return value.Date >= calendar.MinSupportedDateTime.Date && value.Date <= calendar.MaxSupportedDateTime.Date &&
               !(date < Minimum || date > Maximum) && IsDateUnavailable?.Invoke(date) != true;
    }

    /// <inheritdoc />
    protected override bool TryParseValue(string text, out DateTime value)
    {
        bool parsed = DateOnly.TryParse(text, InputCulture, DateTimeStyles.AllowWhiteSpaces, out var date);
        value = date.ToDateTime(TimeOnly.MinValue);
        return parsed;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty ||
            change.Property == MaximumProperty || change.Property == IsDateUnavailableProperty)
            SyncSegments();
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        if (property == ValueProperty) global::Avalonia.Controls.DataValidationErrors.SetError(this, error);
        base.UpdateDataValidation(property, state, error);
    }
}
