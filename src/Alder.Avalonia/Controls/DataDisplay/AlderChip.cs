using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual emphasis of a compact label or status chip.</summary>
public enum ChipVariant
{
    /// <summary>Uses the secondary treatment.</summary>
    Secondary,
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the tertiary treatment.</summary>
    Tertiary,
    /// <summary>Uses the lower-contrast treatment.</summary>
    Soft
}

/// <summary>A compact label for a status or category with optional icon and templated content.</summary>
public sealed class AlderChip : ContentControl
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AlderBadge.SizeProperty.AddOwner<AlderChip>();
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderStatusColor> ColorProperty = AlderBadge.ColorProperty.AddOwner<AlderChip>();
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<ChipVariant> VariantProperty =
        AvaloniaProperty.Register<AlderChip, ChipVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AlderBadge.IconProperty.AddOwner<AlderChip>();
    /// <summary>Identifies the <see cref="IconSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> IconSizeProperty = AlderBadge.IconSizeProperty.AddOwner<AlderChip>();
    /// <summary>Identifies the <see cref="TrailingIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> TrailingIconProperty =
        AvaloniaProperty.Register<AlderChip, Geometry?>(nameof(TrailingIcon));

    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public AlderStatusColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public ChipVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The optional geometry displayed with the content.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>The width and height of the icon in device-independent pixels.</summary>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    /// <summary>The geometry used for the trailing icon.</summary>
    public Geometry? TrailingIcon { get => GetValue(TrailingIconProperty); set => SetValue(TrailingIconProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ChipPeer(this);

    private sealed class ChipPeer(AlderChip owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Content?.ToString();
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
