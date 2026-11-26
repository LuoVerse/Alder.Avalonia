using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A switch whose entire settings row toggles the value. Content supplies the label.</summary>
public sealed class AlderCellSwitch : ToggleSwitch
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<InputVariant> VariantProperty = AlderInput.VariantProperty.AddOwner<AlderCellSwitch>();
    /// <summary>Identifies the <see cref="BoxShadow"/> property.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<AlderCellSwitch>();

    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderCellSwitch, string?>(nameof(Description));

    /// <summary>The visual treatment of the row.</summary>
    public InputVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The shadow drawn outside the settings row.</summary>
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    /// <summary>Optional supporting text underneath the label.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}
