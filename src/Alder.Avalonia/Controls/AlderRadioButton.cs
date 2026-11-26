using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>A RadioButton with an Alder control theme and bindable appearance properties.</summary>
public class AlderRadioButton : RadioButton
{
    /// <summary>Identifies Description for styling and binding.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderRadioButton, string?>(nameof(Description));

    /// <summary>Identifies Indicator for styling and binding.</summary>
    public static readonly StyledProperty<object?> IndicatorProperty =
        AvaloniaProperty.Register<AlderRadioButton, object?>(nameof(Indicator));

    /// <summary>Identifies Footer for styling and binding.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderRadioButton, object?>(nameof(Footer));

    /// <summary>Identifies FieldError for styling and binding.</summary>
    public static readonly StyledProperty<string?> FieldErrorProperty =
        AvaloniaProperty.Register<AlderRadioButton, string?>(nameof(FieldError));

    /// <summary>Identifies IsSecondary for styling and binding.</summary>
    public static readonly StyledProperty<bool> IsSecondaryProperty =
        AvaloniaProperty.Register<AlderRadioButton, bool>(nameof(IsSecondary));

    /// <summary>Gets or sets the Description appearance setting.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the Indicator appearance setting.</summary>
    public object? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
    /// <summary>Gets or sets the Footer appearance setting.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>Gets or sets the FieldError appearance setting.</summary>
    public string? FieldError { get => GetValue(FieldErrorProperty); set => SetValue(FieldErrorProperty, value); }
    /// <summary>Gets or sets the IsSecondary appearance setting.</summary>
    public bool IsSecondary { get => GetValue(IsSecondaryProperty); set => SetValue(IsSecondaryProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldErrorProperty)
            PseudoClasses.Set(":field-error", !string.IsNullOrWhiteSpace(FieldError));
    }
}
