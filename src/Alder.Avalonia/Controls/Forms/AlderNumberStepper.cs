using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace Alder.Avalonia.Controls;

/// <summary>Adjusts a decimal value with bounded increment and decrement buttons and an animated value.</summary>
public sealed partial class AlderNumberStepper : AlderNumberField
{
    /// <summary>The size of the stepper and its buttons.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }

    /// <summary>Places the increment button before the value and the decrement button after it.</summary>
    [GeneratedStyledProperty]
    public partial bool IsReversed { get; set; }

    /// <summary>Whether the editable value accepts text as well as button and keyboard stepping.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool IsTextEditable { get; set; }

    /// <summary>An optional read-only presentation of the current value.</summary>
    [GeneratedStyledProperty]
    public partial IDataTemplate? ValueTemplate { get; set; }

    private AlderInputSpinner? spinner;

    static AlderNumberStepper()
    {
        ClipValueToMinMaxProperty.OverrideDefaultValue<AlderNumberStepper>(true);
        IncrementProperty.OverrideDefaultValue<AlderNumberStepper>(1m);
        FormatStringProperty.OverrideDefaultValue<AlderNumberStepper>("0.############################");
    }

    private protected override bool FocusEditorOnSpin => false;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        spinner = e.NameScope.Find<AlderInputSpinner>("PART_Spinner");
        UpdatePresentation();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == IsReversedProperty || change.Property == ValueTemplateProperty)
            UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        PseudoClasses.Set(":custom-value", ValueTemplate is not null);
        spinner?.Classes.Set("stepper-medium", Size == AlderSize.Medium);
        spinner?.Classes.Set("stepper-large", Size == AlderSize.Large);
        spinner?.Classes.Set("stepper-reversed", IsReversed);
    }
}
