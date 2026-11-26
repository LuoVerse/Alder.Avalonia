using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

/// <summary>Shared visual and validation state for date and time inputs.</summary>
public abstract class AlderTemporalInput : TemplatedControl
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderTemporalInput>();
    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty = AlderInput.FullWidthProperty.AddOwner<AlderTemporalInput>();
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AlderInput.IsInvalidProperty.AddOwner<AlderTemporalInput>();
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AlderInput.IsRequiredProperty.AddOwner<AlderTemporalInput>();
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = TextBox.IsReadOnlyProperty.AddOwner<AlderTemporalInput>();
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<AlderTemporalInput, CultureInfo?>(nameof(Culture), inherits: true);
    /// <summary>Identifies the <see cref="Prefix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        AvaloniaProperty.Register<AlderTemporalInput, object?>(nameof(Prefix));
    /// <summary>Identifies the <see cref="Suffix"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SuffixProperty =
        AvaloniaProperty.Register<AlderTemporalInput, object?>(nameof(Suffix));
    /// <summary>Identifies the <see cref="HasInvalidInput"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderTemporalInput, bool> HasInvalidInputProperty =
        AvaloniaProperty.RegisterDirect<AlderTemporalInput, bool>(nameof(HasInvalidInput), field => field.HasInvalidInput);

    /// <summary>The visual treatment applied to the control.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Whether the value is protected from user edits.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>The culture used to format or interpret displayed values.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>Optional leading content.</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <summary>Optional trailing content.</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <summary>Whether the text currently in the editor fails parsing or validation.</summary>
    public bool HasInvalidInput => hasInvalidInput;

    /// <summary>The effective culture used for formatting and interpreting user input.</summary>
    protected CultureInfo InputCulture => Culture ?? CultureInfo.CurrentCulture;
    /// <summary>The template element that should receive focus when editing starts.</summary>
    protected virtual Control? FocusTarget => null;

    private bool hasInvalidInput;

    static AlderTemporalInput()
    {
        FocusableProperty.OverrideDefaultValue<AlderTemporalInput>(true);
        IsTabStopProperty.OverrideDefaultValue<AlderTemporalInput>(false);
    }

    /// <summary>Updates the input validation state exposed by the temporal editor.</summary>
    protected void UpdateInputError(bool value)
    {
        SetAndRaise(HasInvalidInputProperty, ref hasInvalidInput, value);
        UpdateInvalidState();
    }

    private void UpdateInvalidState()
    {
        bool invalid = IsInvalid || HasInvalidInput || DataValidationErrors.GetHasErrors(this);
        PseudoClasses.Set(":alder-invalid", invalid);
        AlderLabel.SetIsInvalid(this, invalid);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source == this) FocusTarget?.Focus(e.NavigationMethod);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRequiredProperty) AutomationProperties.SetIsRequiredForForm(this, IsRequired);
        else if (change.Property == IsInvalidProperty || change.Property == DataValidationErrors.HasErrorsProperty)
            UpdateInvalidState();
    }
}
