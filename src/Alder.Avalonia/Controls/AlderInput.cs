using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment of a text input.</summary>
public enum InputVariant
{
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary
}
/// <summary>Specifies the expected text and whether the input should mask it.</summary>
public enum InputType
{
    /// <summary>Accepts ordinary visible text.</summary>
    Text,
    /// <summary>Marks the value as an email address for input handling.</summary>
    Email,
    /// <summary>Masks the entered text.</summary>
    Password
}

/// <summary>Extends the native text box with Alder field styling and validation states.</summary>
public class AlderInput : TextBox
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty =
        AvaloniaProperty.Register<AlderInput, InputVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Type"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputType> TypeProperty =
        AvaloniaProperty.Register<AlderInput, InputType>(nameof(Type));
    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty =
        AvaloniaProperty.Register<AlderInput, bool>(nameof(FullWidth));
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<AlderInput, bool>(nameof(IsInvalid));
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<AlderInput, bool>(nameof(IsRequired));
    /// <summary>Identifies the <see cref="ShowClearButton"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<AlderInput, bool>(nameof(ShowClearButton));

    /// <summary>The visual treatment applied to the control.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The expected input kind and the corresponding password masking behavior.</summary>
    public InputType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Shows a clear button when the editable input contains text.</summary>
    public bool ShowClearButton { get => GetValue(ShowClearButtonProperty); set => SetValue(ShowClearButtonProperty, value); }

    private Button? clearButton;

    static AlderInput() => AffectsMeasure<AlderInput>(FullWidthProperty);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (clearButton is not null) clearButton.Click -= OnClearClick;
        base.OnApplyTemplate(e);
        clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        if (clearButton is not null) clearButton.Click += OnClearClick;
        UpdateClearButton();
    }

    // Accessory buttons have their own focus and keyboard handling.
    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (e.Source == this) base.OnGotFocus(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (e.Source == this) base.OnLostFocus(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsFocused) base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (IsFocused) base.OnTextInput(e);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        return FullWidth && double.IsFinite(availableSize.Width) ? desired.WithWidth(availableSize.Width) : desired;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
            PseudoClasses.Set(":secondary", Variant == InputVariant.Secondary);
        else if (change.Property == IsInvalidProperty)
            PseudoClasses.Set(":alder-invalid", IsInvalid);
        else if (change.Property == TextProperty || change.Property == IsReadOnlyProperty ||
                 change.Property == ShowClearButtonProperty)
            UpdateClearButton();
    }

    internal virtual bool UsesTemplateClearButton => true;
    private bool CanClear => UsesTemplateClearButton && ShowClearButton && !IsReadOnly && !string.IsNullOrEmpty(Text);

    private void UpdateClearButton()
    {
        if (!CanClear && clearButton?.IsFocused == true) Focus();
        PseudoClasses.Set(":clearable", CanClear);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (!CanClear || !IsEffectivelyEnabled) return;
        Focus();
        SetCurrentValue(TextProperty, string.Empty);
        e.Handled = true;
    }
}
