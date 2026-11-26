using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A native decimal editor with the Alder inline stepper layouts.</summary>
public class AlderNumberField : NumericUpDown
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderNumberField>();
    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty = AlderInput.FullWidthProperty.AddOwner<AlderNumberField>();
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AlderInput.IsInvalidProperty.AddOwner<AlderNumberField>();
    /// <summary>Identifies the <see cref="IsRequired"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AlderInput.IsRequiredProperty.AddOwner<AlderNumberField>();
    /// <summary>Identifies the <see cref="VerticalStepper"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> VerticalStepperProperty =
        AvaloniaProperty.Register<AlderNumberField, bool>(nameof(VerticalStepper));
    /// <summary>Identifies the <see cref="DecreaseIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> DecreaseIconProperty =
        AvaloniaProperty.Register<AlderNumberField, Geometry?>(nameof(DecreaseIcon));
    /// <summary>Identifies the <see cref="IncreaseIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IncreaseIconProperty =
        AvaloniaProperty.Register<AlderNumberField, Geometry?>(nameof(IncreaseIcon));

    private TextBox? editor;
    private AlderNumberStepValue? stepValue;
    private bool spinning;
    private bool changingContext;

    /// <summary>The visual treatment applied to the control.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }
    /// <summary>Whether the control presents its invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>Whether the field is presented as required.</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    /// <summary>Whether the increase and decrease buttons use a vertical layout.</summary>
    public bool VerticalStepper { get => GetValue(VerticalStepperProperty); set => SetValue(VerticalStepperProperty, value); }
    /// <summary>The geometry used for the decrease icon.</summary>
    public Geometry? DecreaseIcon { get => GetValue(DecreaseIconProperty); set => SetValue(DecreaseIconProperty, value); }
    /// <summary>The geometry used for the increase icon.</summary>
    public Geometry? IncreaseIcon { get => GetValue(IncreaseIconProperty); set => SetValue(IncreaseIconProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        stepValue?.Stop();
        base.OnApplyTemplate(e);
        editor = e.NameScope.Find<TextBox>("PART_TextBox");
        stepValue = e.NameScope.Find<AlderNumberStepValue>("PART_StepValue");
    }

    /// <inheritdoc />
    protected override void OnSpin(SpinEventArgs e)
    {
        spinning = true;
        try { base.OnSpin(e); }
        finally { spinning = false; }
        if (!e.UsingMouseWheel && FocusEditorOnSpin && editor is { IsFocused: false }) editor.Focus(NavigationMethod.Pointer);
    }

    private protected virtual bool FocusEditorOnSpin => true;

    /// <inheritdoc />
    protected override void OnValueChanged(decimal? oldValue, decimal? newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (stepValue is not { } display || editor is not { } input) return;
        if (changingContext || !input.IsVisible || !spinning && input.IsFocused || AlderMotion.GetReduced(this) || !this.IsAttachedToVisualTree() ||
            oldValue is not { } before || newValue is not { } after || before == after)
        {
            display.Stop();
            return;
        }
        display.Roll(input, StepText(before), StepText(after), after > before ? 1 : -1, this is AlderNumberStepper);
    }

    /// <inheritdoc />
    protected override void OnDataContextBeginUpdate()
    {
        changingContext = true;
        stepValue?.Stop();
        base.OnDataContextBeginUpdate();
    }

    /// <inheritdoc />
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        changingContext = false;
    }

    private string StepText(decimal value) => FormatString is { } format && format.Contains("{0", StringComparison.Ordinal)
        ? string.Format(NumberFormat, format, value)
        : value.ToString(FormatString, NumberFormat);
}
