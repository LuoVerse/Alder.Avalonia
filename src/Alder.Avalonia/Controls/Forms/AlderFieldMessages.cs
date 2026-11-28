using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Displays supporting text, such as guidance for a form field.</summary>
public sealed class AlderDescription : TextBlock;

/// <summary>Displays an application error as accessible text.</summary>
public class AlderErrorMessage : TextBlock;

/// <summary>Displays an explicit error message or follows validation on another control.</summary>
public sealed class AlderFieldError : AlderErrorMessage
{
    /// <summary>Identifies the optional <see cref="Target"/> property.</summary>
    public static readonly StyledProperty<Control?> TargetProperty = AvaloniaProperty.Register<AlderFieldError, Control?>(nameof(Target));
    /// <summary>Identifies the computed <see cref="ValidationText"/> property.</summary>
    public static readonly DirectProperty<AlderFieldError, string?> ValidationTextProperty =
        AvaloniaProperty.RegisterDirect<AlderFieldError, string?>(nameof(ValidationText), error => error.ValidationText);
    /// <summary>Identifies the computed <see cref="HasMessage"/> property.</summary>
    public static readonly DirectProperty<AlderFieldError, bool> HasMessageProperty =
        AvaloniaProperty.RegisterDirect<AlderFieldError, bool>(nameof(HasMessage), error => error.HasMessage);

    private Control? observedTarget;
    private string? validationText;
    private bool hasMessage;

    /// <summary>Gets or sets the control supplying validation. Set <see cref="TextBlock.Text"/> to override the displayed message.</summary>
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    /// <summary>Gets the validation text supplied by the target control.</summary>
    public string? ValidationText => validationText;
    /// <summary>Gets whether error text is available and the target, if supplied, is invalid.</summary>
    public bool HasMessage => hasMessage;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveTarget(Target);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ObserveTarget(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty)
        {
            if (this.IsAttachedToVisualTree()) ObserveTarget(Target);
            else Refresh();
        }
        else if (change.Property == TextProperty)
            UpdateVisibility();
    }

    private void ObserveTarget(Control? target)
    {
        if (observedTarget is not null) observedTarget.PropertyChanged -= OnTargetPropertyChanged;
        observedTarget = target;
        if (observedTarget is not null) observedTarget.PropertyChanged += OnTargetPropertyChanged;
        Refresh();
    }

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataValidationErrors.ErrorsProperty || e.Property == DataValidationErrors.HasErrorsProperty ||
            e.Property == AlderLabel.IsInvalidProperty || e.Property == AlderInput.IsInvalidProperty || e.Property == AlderCheckBox.IsInvalidProperty ||
            e.Property == AlderTextField.ValidationMessageProperty || e.Property == AlderTextField.HasValidationErrorProperty)
            Refresh();
    }

    private void Refresh()
    {
        var message = Target switch
        {
            AlderTextField field => field.ValidationMessage,
            { } target => FormValidation.GetErrorText(target),
            _ => null
        };
        SetAndRaise(ValidationTextProperty, ref validationText, message);
        UpdateVisibility();
    }

    private void UpdateVisibility() => SetAndRaise(HasMessageProperty, ref hasMessage,
        !string.IsNullOrWhiteSpace(Text) && (Target is null || FormValidation.IsInvalid(Target)));
}
