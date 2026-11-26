using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>An Alder label with required and validation states.</summary>
public sealed class AlderLabel : Label
{
    /// <summary>Identifies the <see cref="GetIsRequired"/> attached property.</summary>
    public static readonly AttachedProperty<bool> IsRequiredProperty =
        AvaloniaProperty.RegisterAttached<AlderLabel, Label, bool>("IsRequired");

    /// <summary>Can also be set on a target field that supplies custom validation.</summary>
    public static readonly AttachedProperty<bool> IsInvalidProperty =
        AvaloniaProperty.RegisterAttached<AlderLabel, Control, bool>("IsInvalid");

    /// <summary>Whether the associated field is required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>Whether the associated field is invalid.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>Whether the field is presented as required.</remarks>
    public static bool GetIsRequired(Label target) => target.GetValue(IsRequiredProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>Whether the field is presented as required.</remarks>
    public static void SetIsRequired(Label target, bool value) => target.SetValue(IsRequiredProperty, value);
    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>Whether the control presents its invalid state.</remarks>
    public static bool GetIsInvalid(Control target) => target.GetValue(IsInvalidProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>Whether the control presents its invalid state.</remarks>
    public static void SetIsInvalid(Control target, bool value) => target.SetValue(IsInvalidProperty, value);
}
