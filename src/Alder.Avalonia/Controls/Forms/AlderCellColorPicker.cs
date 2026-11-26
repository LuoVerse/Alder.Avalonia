using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Alder.Avalonia.Controls;

/// <summary>Presents a color picker as a settings row with a hexadecimal value and swatch.</summary>
public sealed class AlderCellColorPicker : AlderColorPicker
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderCellColorPicker>();

    /// <summary>Identifies the <see cref="Presets"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderColorValue>?> PresetsProperty =
        AvaloniaProperty.Register<AlderCellColorPicker, IReadOnlyList<AlderColorValue>?>(nameof(Presets));

    /// <summary>Identifies the <see cref="SelectedPreset"/> property.</summary>
    public static readonly DirectProperty<AlderCellColorPicker, AlderColorValue?> SelectedPresetProperty =
        AvaloniaProperty.RegisterDirect<AlderCellColorPicker, AlderColorValue?>(nameof(SelectedPreset),
            picker => picker.SelectedPreset, (picker, value) => picker.SelectedPreset = value, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The visual treatment of the row.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>An optional palette above the color area. Replace the list to publish a new palette.</summary>
    public IReadOnlyList<AlderColorValue>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>The preset matching the draft color, or null when the draft is a custom color.</summary>
    public AlderColorValue? SelectedPreset
    {
        get => selectedPreset;
        set
        {
            if (value is { } preset && selectedPreset != preset)
                SetCurrentValue(DraftValueProperty, preset);
        }
    }

    internal override bool UseFlipSafePlacement => true;

    private AlderColorValue? selectedPreset;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PresetsProperty)
            PseudoClasses.Set(":has-presets", Presets is { Count: > 0 });

        if (change.Property == PresetsProperty || change.Property == DraftValueProperty)
        {
            var color = DraftValue.ToColor();
            AlderColorValue? match = null;
            if (Presets is { } presets)
            {
                foreach (var preset in presets)
                {
                    if (preset.ToColor() != color) continue;
                    match = preset;
                    break;
                }
            }
            SetAndRaise(SelectedPresetProperty, ref selectedPreset, match);
        }
    }
}
