using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Identifies the background treatment a surface makes available to its descendants.</summary>
public enum SurfaceVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses a transparent surface.</summary>
    Transparent,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary,
    /// <summary>Uses the tertiary treatment.</summary>
    Tertiary
}

/// <summary>A themed container whose surface variant is available to descendant controls.</summary>
public class AlderSurface : ContentControl
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<SurfaceVariant> VariantProperty =
        AvaloniaProperty.Register<AlderSurface, SurfaceVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="BoxShadow"/> Avalonia property.</summary>
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<AlderSurface>();
    /// <summary>Identifies the <see cref="GetContextVariant"/> attached property.</summary>
    public static readonly AttachedProperty<SurfaceVariant> ContextVariantProperty =
        AvaloniaProperty.RegisterAttached<AlderSurface, StyledElement, SurfaceVariant>("ContextVariant", inherits: true);

    /// <summary>Initializes a new instance of <see cref="AlderSurface"/>.</summary>
    public AlderSurface() => SetValue(ContextVariantProperty, Variant);

    /// <summary>The visual treatment applied to the control.</summary>
    public SurfaceVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The shadows drawn around the surface.</summary>
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    /// <summary>Gets the effective surface variant inherited by a descendant.</summary>
    public static SurfaceVariant GetContextVariant(StyledElement element) => element.GetValue(ContextVariantProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
            SetValue(ContextVariantProperty, change.GetNewValue<SurfaceVariant>());
    }
}
