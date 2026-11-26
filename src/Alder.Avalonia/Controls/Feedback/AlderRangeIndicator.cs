using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the semantic color palette for feedback and progress controls.</summary>
public enum FeedbackColor
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the accent color.</summary>
    Accent,
    /// <summary>Uses the success treatment.</summary>
    Success,
    /// <summary>Uses the warning treatment.</summary>
    Warning,
    /// <summary>Uses the danger treatment.</summary>
    Danger
}

/// <summary>A read-only visual indicator for a value in a known range.</summary>
public abstract class AlderRangeIndicator : RangeBase
{
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<AlderRangeIndicator, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="ShowValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowValueProperty = AvaloniaProperty.Register<AlderRangeIndicator, bool>(nameof(ShowValue), true);
    /// <summary>Identifies the <see cref="ValueFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ValueFormatProperty = AvaloniaProperty.Register<AlderRangeIndicator, string?>(nameof(ValueFormat));
    /// <summary>Identifies the <see cref="CustomValueText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> CustomValueTextProperty = AvaloniaProperty.Register<AlderRangeIndicator, string?>(nameof(CustomValueText));
    /// <summary>Identifies the <see cref="FormatCulture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> FormatCultureProperty = AvaloniaProperty.Register<AlderRangeIndicator, CultureInfo?>(nameof(FormatCulture));
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<FeedbackColor> ColorProperty = AvaloniaProperty.Register<AlderRangeIndicator, FeedbackColor>(nameof(Color), FeedbackColor.Accent);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AvaloniaProperty.Register<AlderRangeIndicator, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="TrackBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<AlderRangeIndicator, IBrush?>(nameof(TrackBrush));
    /// <summary>Identifies the <see cref="IndicatorBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty = AvaloniaProperty.Register<AlderRangeIndicator, IBrush?>(nameof(IndicatorBrush));
    /// <summary>Identifies the <see cref="Fraction"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderRangeIndicator, double> FractionProperty =
        AvaloniaProperty.RegisterDirect<AlderRangeIndicator, double>(nameof(Fraction), indicator => indicator.Fraction);
    /// <summary>Identifies the <see cref="ValueText"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderRangeIndicator, string> ValueTextProperty =
        AvaloniaProperty.RegisterDirect<AlderRangeIndicator, string>(nameof(ValueText), indicator => indicator.ValueText);

    static AlderRangeIndicator()
    {
        FocusableProperty.OverrideDefaultValue<AlderRangeIndicator>(false);
        MaximumProperty.OverrideDefaultValue<AlderRangeIndicator>(100);
    }

    /// <summary>The text that identifies the control or value.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Whether to display the formatted value text.</summary>
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    /// <summary>A standard .NET numeric format for the raw value. The default formats the fraction as a percentage.</summary>
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    /// <summary>Optional text that replaces the default format of the current value.</summary>
    public string? CustomValueText { get => GetValue(CustomValueTextProperty); set => SetValue(CustomValueTextProperty, value); }
    /// <summary>The culture used to format the displayed value.</summary>
    public CultureInfo? FormatCulture { get => GetValue(FormatCultureProperty); set => SetValue(FormatCultureProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public FeedbackColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The brush used to draw the track.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    /// <summary>The brush used to draw the indicator.</summary>
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }

    /// <summary>The normalized position of the value in the range, from zero to one.</summary>
    public double Fraction
    {
        get => field;
        private set => SetAndRaise(FractionProperty, ref field, value);
    }

    /// <summary>The formatted text presented for the current value.</summary>
    public string ValueText
    {
        get => field;
        private set => SetAndRaise(ValueTextProperty, ref field, value);
    } = 0d.ToString("P0", CultureInfo.CurrentCulture);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            double span = Maximum - Minimum;
            double fraction = span <= 0 ? 0 : double.IsFinite(span)
                ? (Value - Minimum) / span
                : (Value / 2 - Minimum / 2) / (Maximum / 2 - Minimum / 2);
            Fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) : 0;
            UpdateValueText();
        }
        else if (change.Property == ValueFormatProperty || change.Property == FormatCultureProperty ||
                 change.Property == CustomValueTextProperty)
            UpdateValueText();
    }

    private void UpdateValueText()
    {
        var culture = FormatCulture ?? CultureInfo.CurrentCulture;
        try
        {
            ValueText = CustomValueText ?? (ValueFormat is { Length: > 0 } format
                ? Value.ToString(format, culture)
                : Fraction.ToString("P0", culture));
        }
        catch (FormatException)
        {
            ValueText = Value.ToString("G", culture);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ValuePeer(this);

    private sealed class ValuePeer(AlderRangeIndicator owner) : RangeBaseAutomationPeer(owner), IRangeValueProvider
    {
        public override bool IsReadOnly => true;
        void IRangeValueProvider.SetValue(double value) => throw new InvalidOperationException("The indicator is read-only.");
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Label;
        protected override string? GetHelpTextCore() => base.GetHelpTextCore() ??
            (owner is AlderProgressIndicator { IsIndeterminate: true } ? null : owner.ValueText);
        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IRangeValueProvider) && owner is AlderProgressIndicator { IsIndeterminate: true }
                ? null : base.GetProviderCore(providerType);
    }
}

/// <summary>Shows a determinate measurement within a specified range.</summary>
public sealed class AlderMeter : AlderRangeIndicator;

/// <summary>Shares range feedback and indeterminate state between progress presentations.</summary>
public abstract class AlderProgressIndicator : AlderRangeIndicator
{
    /// <summary>Identifies the <see cref="IsIndeterminate"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<AlderProgressIndicator, bool>(nameof(IsIndeterminate));

    /// <summary>Whether to show an ongoing operation with unknown completion.</summary>
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
}

/// <summary>Presents a value or an indeterminate operation as a horizontal progress track.</summary>
public sealed class AlderProgressBar : AlderProgressIndicator;

/// <summary>Presents a value or an indeterminate operation around a circular track.</summary>
public sealed class AlderProgressCircle : AlderProgressIndicator
{
    /// <summary>Identifies the <see cref="Diameter"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<AlderProgressCircle, double>(nameof(Diameter), 28,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="StrokeThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<AlderProgressCircle, double>(nameof(StrokeThickness), double.NaN,
            validate: value => double.IsNaN(value) || double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="TrackStrokeThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> TrackStrokeThicknessProperty =
        AvaloniaProperty.Register<AlderProgressCircle, double>(nameof(TrackStrokeThickness), double.NaN,
            validate: value => double.IsNaN(value) || double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="StrokeLineCap"/> Avalonia property.</summary>
    public static readonly StyledProperty<PenLineCap> StrokeLineCapProperty =
        AvaloniaProperty.Register<AlderProgressCircle, PenLineCap>(nameof(StrokeLineCap), PenLineCap.Round);

    static AlderProgressCircle() => ShowValueProperty.OverrideDefaultValue<AlderProgressCircle>(false);

    /// <summary>The diameter of the circular progress indicator in device-independent pixels.</summary>
    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    /// <summary>The width of the circular progress stroke in device-independent pixels.</summary>
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    /// <summary>When unset, the track uses the same thickness as the progress stroke.</summary>
    public double TrackStrokeThickness { get => GetValue(TrackStrokeThicknessProperty); set => SetValue(TrackStrokeThicknessProperty, value); }
    /// <summary>The cap applied to the endpoints of the progress stroke.</summary>
    public PenLineCap StrokeLineCap { get => GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
}
