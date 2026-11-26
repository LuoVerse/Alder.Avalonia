using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment applied to the checkbox indicator.</summary>
public enum CheckBoxVariant
{
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary
}

/// <summary>Provides a checkable input with optional field description, validation, and a custom check indicator.</summary>
public class AlderCheckBox : CheckBox
{
    /// <summary>Identifies the animated feedback settings.</summary>
    public static readonly StyledProperty<AlderPressableFeedback?> FeedbackProperty =
        AlderButton.FeedbackProperty.AddOwner<AlderCheckBox>();

    /// <summary>Optional motion settings for this control's press and hover feedback.</summary>
    public AlderPressableFeedback? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<CheckBoxVariant> VariantProperty =
        AvaloniaProperty.Register<AlderCheckBox, CheckBoxVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<AlderCheckBox, bool>(nameof(IsReadOnly));
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<AlderCheckBox, bool>(nameof(IsRequired));
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<AlderCheckBox, bool>(nameof(IsInvalid));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderCheckBox, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="ErrorMessage"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<AlderCheckBox, string?>(nameof(ErrorMessage));
    /// <summary>Identifies the <see cref="IndicatorSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> IndicatorSizeProperty =
        AvaloniaProperty.Register<AlderCheckBox, double>(nameof(IndicatorSize), 16,
            validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="IndicatorCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<CornerRadius> IndicatorCornerRadiusProperty =
        AvaloniaProperty.Register<AlderCheckBox, CornerRadius>(nameof(IndicatorCornerRadius), new CornerRadius(6));
    /// <summary>Identifies the <see cref="CheckedIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> CheckedIconProperty =
        AvaloniaProperty.Register<AlderCheckBox, Geometry?>(nameof(CheckedIcon));
    /// <summary>Identifies the <see cref="IndeterminateIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndeterminateIconProperty =
        AvaloniaProperty.Register<AlderCheckBox, Geometry?>(nameof(IndeterminateIcon));

    /// <summary>The visual treatment applied to the control.</summary>
    public CheckBoxVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the value is protected from user edits.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Supporting text displayed beside the checkbox label.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>The error text visible in the invalid state.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    /// <summary>The size of the checkbox mark container in device-independent pixels.</summary>
    public double IndicatorSize { get => GetValue(IndicatorSizeProperty); set => SetValue(IndicatorSizeProperty, value); }
    /// <summary>The corner radius of the checkbox mark container.</summary>
    public CornerRadius IndicatorCornerRadius { get => GetValue(IndicatorCornerRadiusProperty); set => SetValue(IndicatorCornerRadiusProperty, value); }
    /// <summary>Overrides the built-in animated checkmark when supplied.</summary>
    public Geometry? CheckedIcon { get => GetValue(CheckedIconProperty); set => SetValue(CheckedIconProperty, value); }
    /// <summary>The geometry displayed when the checkbox state is indeterminate.</summary>
    public Geometry? IndeterminateIcon { get => GetValue(IndeterminateIconProperty); set => SetValue(IndeterminateIconProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        AlderPressableFeedback.OnOwnerPropertyChanged(this, change);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (!IsReadOnly) base.OnClick();
    }

    /// <inheritdoc />
    protected override void Toggle()
    {
        if (IsChecked is null && !IsThreeState)
            SetCurrentValue(IsCheckedProperty, true);
        else
            base.Toggle();
    }
}
