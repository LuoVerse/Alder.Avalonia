using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

/// <summary>A text editor with a label, supporting description, and inline validation.</summary>
public sealed class AlderTextField : TemplatedControl
{
    /// <summary>Identifies the two-way, validation-enabled <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<AlderTextField, string?>(
        nameof(Text), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);
    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<AlderTextField, string?>(nameof(Label));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderTextField, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="ErrorMessage"/> property.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty = AvaloniaProperty.Register<AlderTextField, string?>(nameof(ErrorMessage));
    /// <summary>Identifies the <see cref="RequiredErrorMessage"/> property.</summary>
    public static readonly StyledProperty<string> RequiredErrorMessageProperty = AvaloniaProperty.Register<AlderTextField, string>(nameof(RequiredErrorMessage), "This field is required.");
    /// <summary>Identifies the <see cref="PlaceholderText"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty = TextBox.PlaceholderTextProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="IsReadOnly"/> property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = TextBox.IsReadOnlyProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="AcceptsReturn"/> property.</summary>
    public static readonly StyledProperty<bool> AcceptsReturnProperty = TextBox.AcceptsReturnProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="MinLines"/> property.</summary>
    public static readonly StyledProperty<int> MinLinesProperty = TextBox.MinLinesProperty.AddOwner<AlderTextField>(new StyledPropertyMetadata<int>(3));
    /// <summary>Identifies the <see cref="MaxLength"/> property.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty = TextBox.MaxLengthProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="IsRequired"/> property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AlderInput.IsRequiredProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="IsInvalid"/> property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AlderInput.IsInvalidProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="Type"/> property.</summary>
    public static readonly StyledProperty<InputType> TypeProperty = AlderInput.TypeProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the <see cref="ShowClearButton"/> property.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty = AlderInput.ShowClearButtonProperty.AddOwner<AlderTextField>();
    /// <summary>Identifies the computed <see cref="HasValidationError"/> property.</summary>
    public static readonly DirectProperty<AlderTextField, bool> HasValidationErrorProperty =
        AvaloniaProperty.RegisterDirect<AlderTextField, bool>(nameof(HasValidationError), field => field.HasValidationError);
    /// <summary>Identifies the computed <see cref="ValidationMessage"/> property.</summary>
    public static readonly DirectProperty<AlderTextField, string?> ValidationMessageProperty =
        AvaloniaProperty.RegisterDirect<AlderTextField, string?>(nameof(ValidationMessage), field => field.ValidationMessage);

    private TextBox? editor;
    private bool validateRequired;
    private bool hasValidationError;
    private string? validationMessage;

    /// <summary>Gets or sets the current text. Bind this property to the source that supplies validation errors.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>Gets or sets the label associated with the editor. Use an underscore to define an access key.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>Gets or sets the supporting text shown below the editor when there is no error.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the message for an explicit invalid state.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    /// <summary>Gets or sets the message for an empty required field.</summary>
    public string RequiredErrorMessage { get => GetValue(RequiredErrorMessageProperty); set => SetValue(RequiredErrorMessageProperty, value); }
    /// <summary>Gets or sets the hint visible inside an empty editor.</summary>
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    /// <summary>Gets or sets whether text editing is disabled while the editor remains focusable.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Gets or sets whether the editor accepts line breaks.</summary>
    public bool AcceptsReturn { get => GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
    /// <summary>Gets or sets the minimum number of visible lines in multiline mode.</summary>
    public int MinLines { get => GetValue(MinLinesProperty); set => SetValue(MinLinesProperty, value); }
    /// <summary>Gets or sets the maximum number of characters accepted by the editor.</summary>
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    /// <summary>Gets or sets whether the form requires a non-blank value.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Gets or sets an explicit invalid state supplied by the application.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Gets or sets the visual treatment of the editor.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Gets or sets the editor's expected type, including password masking.</summary>
    public InputType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    /// <summary>Gets or sets whether the editor offers a clear button.</summary>
    public bool ShowClearButton { get => GetValue(ShowClearButtonProperty); set => SetValue(ShowClearButtonProperty, value); }
    /// <summary>Gets whether an explicit, binding, or required validation error is active.</summary>
    public bool HasValidationError => hasValidationError;
    /// <summary>Gets the current error text in display order.</summary>
    public string? ValidationMessage => validationMessage;

    /// <summary>Checks the required value and refreshes its error until validation is reset.</summary>
    public bool Validate()
    {
        validateRequired = true;
        RefreshValidation();
        return !HasValidationError;
    }

    /// <summary>Clears the active required check while retaining binding and application validation.</summary>
    public void ResetValidation()
    {
        validateRequired = false;
        RefreshValidation();
    }

    /// <summary>Moves keyboard focus to the native editor.</summary>
    public bool FocusEditor()
    {
        ApplyTemplate();
        return editor?.Focus() == true;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        editor = e.NameScope.Find<TextBox>("PART_Input");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsRequiredProperty || change.Property == IsInvalidProperty ||
            change.Property == IsReadOnlyProperty || change.Property == ErrorMessageProperty ||
            change.Property == RequiredErrorMessageProperty || change.Property == DataValidationErrors.ErrorsProperty ||
            change.Property == DataValidationErrors.HasErrorsProperty)
            RefreshValidation();
    }

    private void RefreshValidation()
    {
        var bindingMessage = FormValidation.GetErrorText(this);
        var requiredError = validateRequired && IsRequired && !IsReadOnly && string.IsNullOrWhiteSpace(Text);
        var message = bindingMessage ?? (requiredError ? RequiredErrorMessage : IsInvalid ? ErrorMessage : null);
        SetAndRaise(ValidationMessageProperty, ref validationMessage, message);
        SetAndRaise(HasValidationErrorProperty, ref hasValidationError,
            IsInvalid || requiredError || bindingMessage is not null || DataValidationErrors.GetHasErrors(this));
    }
}
