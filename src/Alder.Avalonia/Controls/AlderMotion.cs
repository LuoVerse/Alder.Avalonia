using Avalonia;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the inheritable reduced-motion preference used by Alder controls.</summary>
/// <remarks>
/// Set the preference on a window or container to apply it to descendants. Controls still commit their final
/// interactive state when optional transitions are suppressed.
/// </remarks>
public sealed class AlderMotion : AvaloniaObject
{
    /// <summary>Identifies the <see cref="GetReduced"/> attached property.</summary>
    public static readonly AttachedProperty<bool> ReducedProperty =
        AvaloniaProperty.RegisterAttached<AlderMotion, StyledElement, bool>("Reduced", inherits: true);
    /// <summary>Gets the inherited preference to suppress optional Alder motion.</summary>
    public static bool GetReduced(StyledElement target) => target.GetValue(ReducedProperty);
    /// <summary>Sets the inherited preference to suppress optional Alder motion.</summary>
    public static void SetReduced(StyledElement target, bool value) => target.SetValue(ReducedProperty, value);
}
