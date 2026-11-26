using Avalonia;
using Avalonia.Collections;

namespace Alder.Avalonia.Controls;

/// <summary>Labels a particular position on a slider track.</summary>
public sealed class SliderMark : AvaloniaObject
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SliderMark, double>(nameof(Value));
    /// <summary>Identifies the <see cref="Label"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SliderMark, string?>(nameof(Label));

    /// <summary>The value at which this mark appears on the slider track.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The text to show at the mark in place of a formatted value.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>A collection of marks that may be declared inline in AXAML.</summary>
public sealed class SliderMarkCollection : AvaloniaList<SliderMark>;
