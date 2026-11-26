using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A horizontal slider displayed as a settings row with a live numeric value.</summary>
public sealed class AlderCellSlider : Slider
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderCellSlider>();

    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty = AlderColorPicker.LabelProperty.AddOwner<AlderCellSlider>();

    /// <summary>Identifies the <see cref="Step"/> property.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<AlderCellSlider, double>(nameof(Step), .01, validate: step => double.IsFinite(step) && step > 0);

    /// <summary>Identifies the <see cref="ValueFormat"/> property.</summary>
    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<AlderCellSlider, string>(nameof(ValueFormat), "0.00");

    /// <summary>Identifies the <see cref="ValueCulture"/> property.</summary>
    public static readonly StyledProperty<CultureInfo?> ValueCultureProperty =
        AvaloniaProperty.Register<AlderCellSlider, CultureInfo?>(nameof(ValueCulture));

    /// <summary>Identifies the <see cref="ValueText"/> property.</summary>
    public static readonly DirectProperty<AlderCellSlider, string> ValueTextProperty =
        AvaloniaProperty.RegisterDirect<AlderCellSlider, string>(nameof(ValueText), slider => slider.ValueText);

    static AlderCellSlider() => OrientationProperty.OverrideMetadata<AlderCellSlider>(
        new StyledPropertyMetadata<Orientation>(Orientation.Horizontal, coerce: (_, _) => Orientation.Horizontal));

    private IPointer? dragPointer;

    /// <summary>Initializes a settings row that tracks pointer interaction across the whole slider.</summary>
    public AlderCellSlider()
    {
        AddHandler(PointerPressedEvent, StartDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, e) => EndDrag(e.Pointer), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, e) => EndDrag(e.Pointer), RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The visual treatment of the row.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>The text describing the value on the leading side of the row.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The increment for keyboard, track and drag input.</summary>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>The numeric format used on the trailing side of the row.</summary>
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>The culture used to format the value. Null uses the current culture.</summary>
    public CultureInfo? ValueCulture { get => GetValue(ValueCultureProperty); set => SetValue(ValueCultureProperty, value); }

    /// <summary>The current value formatted for display.</summary>
    public string ValueText => Value.ToString(ValueFormat, ValueCulture ?? CultureInfo.CurrentCulture);

    private void StartDrag(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || dragPointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        dragPointer = e.Pointer;
        PseudoClasses.Set(":dragging", true);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (pointer is null || pointer != dragPointer) return;
        dragPointer = null;
        PseudoClasses.Set(":dragging", false);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndDrag(dragPointer);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            EndDrag(dragPointer);
        if (change.Property == ValueProperty || change.Property == ValueFormatProperty || change.Property == ValueCultureProperty)
        {
            var oldValue = change.Property == ValueProperty ? change.GetOldValue<double>() : Value;
            var oldFormat = change.Property == ValueFormatProperty ? change.GetOldValue<string>() : ValueFormat;
            var oldCulture = change.Property == ValueCultureProperty ? change.GetOldValue<CultureInfo?>() : ValueCulture;
            RaisePropertyChanged(ValueTextProperty, oldValue.ToString(oldFormat, oldCulture ?? CultureInfo.CurrentCulture), ValueText);
        }
    }
}
