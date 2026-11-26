using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace Alder.Avalonia.Controls;

/// <summary>The unit represented by a formatted number.</summary>
public enum NumberValueStyle
{
    /// <summary>A decimal number.</summary>
    Decimal,
    /// <summary>A monetary amount.</summary>
    Currency,
    /// <summary>A fraction displayed as a percentage.</summary>
    Percent,
    /// <summary>A number followed by a unit.</summary>
    Unit
}

/// <summary>The scale used to display a number.</summary>
public enum NumberValueNotation
{
    /// <summary>Displays the full number.</summary>
    Standard,
    /// <summary>Abbreviates large numbers using a locale-appropriate scale.</summary>
    Compact,
    /// <summary>Uses a power of ten and a mantissa with one integer digit.</summary>
    Scientific,
    /// <summary>Uses a power of ten divisible by three.</summary>
    Engineering
}

/// <summary>Determines when a number is prefixed by a sign.</summary>
public enum NumberValueSignDisplay
{
    /// <summary>Displays negative signs, including for signed zero.</summary>
    Auto,
    /// <summary>Displays a sign for both negative and nonnegative values.</summary>
    Always,
    /// <summary>Displays a sign for nonzero values.</summary>
    ExceptZero,
    /// <summary>Omits both signs.</summary>
    Never,
    /// <summary>Displays a sign for negative values other than negative zero.</summary>
    Negative
}

/// <summary>The representation of a currency in a formatted monetary amount.</summary>
public enum NumberValueCurrencyDisplay
{
    /// <summary>Uses the locale's currency symbol.</summary>
    Symbol,
    /// <summary>Uses the three-letter ISO currency code.</summary>
    Code,
    /// <summary>Uses the unqualified currency symbol.</summary>
    NarrowSymbol
}

/// <summary>Optional overrides for the number formatter.</summary>
public sealed record NumberValueFormatOptions
{
    /// <summary>Overrides the style supplied on the control.</summary>
    public NumberValueStyle? Style { get; init; }
    /// <summary>Overrides the notation supplied on the control.</summary>
    public NumberValueNotation? Notation { get; init; }
    /// <summary>Overrides the sign display supplied on the control.</summary>
    public NumberValueSignDisplay? SignDisplay { get; init; }
    /// <summary>Overrides the ISO currency code supplied on the control.</summary>
    public string? Currency { get; init; }
    /// <summary>Overrides the unit supplied on the control.</summary>
    public string? Unit { get; init; }
    /// <summary>Sets the minimum number of fraction digits.</summary>
    public int? MinimumFractionDigits { get; init; }
    /// <summary>Sets the maximum number of fraction digits.</summary>
    public int? MaximumFractionDigits { get; init; }
    /// <summary>Sets the minimum number of integer digits.</summary>
    public int MinimumIntegerDigits { get; init; } = 1;
    /// <summary>Sets the minimum number of significant digits.</summary>
    public int? MinimumSignificantDigits { get; init; }
    /// <summary>Sets the maximum number of significant digits.</summary>
    public int? MaximumSignificantDigits { get; init; }
    /// <summary>Controls whether integer digits are grouped.</summary>
    public bool UseGrouping { get; init; } = true;
    /// <summary>Selects currency symbols or codes.</summary>
    public NumberValueCurrencyDisplay CurrencyDisplay { get; init; }
    /// <summary>Uses the culture's accounting layout for negative currency amounts.</summary>
    public bool Accounting { get; init; }
    /// <summary>Supplies a custom unit label, with any spacing the label requires.</summary>
    public string? UnitLabel { get; init; }
}

/// <summary>Displays a culture-aware formatted number with optional inline content.</summary>
public sealed partial class AlderNumberValue : TemplatedControl
{
    /// <summary>The source number, before formatting.</summary>
    [GeneratedStyledProperty]
    public partial double Value { get; set; }
    /// <summary>Applies decimal, currency, percentage or unit formatting.</summary>
    [GeneratedStyledProperty]
    public partial NumberValueStyle Style { get; set; }
    /// <summary>The ISO 4217 currency code.</summary>
    [GeneratedStyledProperty]
    public partial string? Currency { get; set; }
    /// <summary>The unit identifier, for example degree or kilometer.</summary>
    [GeneratedStyledProperty]
    public partial string? Unit { get; set; }
    /// <summary>Controls scaling or exponent notation.</summary>
    [GeneratedStyledProperty]
    public partial NumberValueNotation Notation { get; set; }
    /// <summary>Controls when to display the sign.</summary>
    [GeneratedStyledProperty]
    public partial NumberValueSignDisplay SignDisplay { get; set; }
    /// <summary>The minimum fraction digits, or null to use the style default.</summary>
    [GeneratedStyledProperty]
    public partial int? MinimumFractionDigits { get; set; }
    /// <summary>The maximum fraction digits, or null to use the style default.</summary>
    [GeneratedStyledProperty]
    public partial int? MaximumFractionDigits { get; set; }
    /// <summary>The formatting culture. Null uses <see cref="CultureInfo.CurrentCulture"/>.</summary>
    [GeneratedStyledProperty]
    public partial CultureInfo? Culture { get; set; }
    /// <summary>Overrides individual formatting properties.</summary>
    [GeneratedStyledProperty]
    public partial NumberValueFormatOptions? FormatOptions { get; set; }
    /// <summary>An optional host-supplied formatter for specialized formats or units.</summary>
    [GeneratedStyledProperty]
    public partial Func<double, CultureInfo, string>? Formatter { get; set; }
    /// <summary>Content positioned before the formatted value.</summary>
    [GeneratedStyledProperty]
    public partial object? Prefix { get; set; }
    /// <summary>Content positioned after the formatted value.</summary>
    [GeneratedStyledProperty]
    public partial object? Suffix { get; set; }
    /// <summary>A template that receives the formatted value as its data context.</summary>
    [GeneratedStyledProperty]
    public partial IDataTemplate? ValueTemplate { get; set; }
    /// <summary>Uses equal-width digits when the selected font supports tabular numbers.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool TabularNumbers { get; set; }

    /// <summary>Identifies the formatted value exposed to bindings.</summary>
    public static readonly DirectProperty<AlderNumberValue, string> FormattedValueProperty =
        AvaloniaProperty.RegisterDirect<AlderNumberValue, string>(nameof(FormattedValue), control => control.FormattedValue);

    /// <summary>The current formatted value without prefix or suffix content.</summary>
    public string FormattedValue { get => field; private set => SetAndRaise(FormattedValueProperty, ref field, value); } = "0";

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueTemplateProperty) PseudoClasses.Set(":custom-value", ValueTemplate is not null);
        if (change.Property == ValueProperty || change.Property == StyleProperty || change.Property == CurrencyProperty ||
            change.Property == UnitProperty || change.Property == NotationProperty || change.Property == SignDisplayProperty ||
            change.Property == MinimumFractionDigitsProperty || change.Property == MaximumFractionDigitsProperty ||
            change.Property == CultureProperty || change.Property == FormatOptionsProperty || change.Property == FormatterProperty)
        {
            var culture = Culture ?? CultureInfo.CurrentCulture;
            FormattedValue = Formatter?.Invoke(Value, culture) ?? NumberValueFormatter.Format(this, culture);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NumberValuePeer(this);

    private sealed class NumberValuePeer(AlderNumberValue owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string? GetNameCore() => base.GetNameCore() ??
            string.Join(" ", new[] { owner.Prefix as string, owner.FormattedValue, owner.Suffix as string }
                .Where(value => !string.IsNullOrEmpty(value)));
    }
}
