using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class DataDisplaySurface : AvaloniaObject
{
    public static readonly AttachedProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.RegisterAttached<DataDisplaySurface, Control, BoxShadows>("Shadow");
    public static readonly AttachedProperty<bool> IsGroupedProperty =
        AvaloniaProperty.RegisterAttached<DataDisplaySurface, StyledElement, bool>("IsGrouped", inherits: true);

    public static BoxShadows GetShadow(Control control) => control.GetValue(ShadowProperty);
    public static void SetShadow(Control control, BoxShadows value) => control.SetValue(ShadowProperty, value);
    public static bool GetIsGrouped(StyledElement element) => element.GetValue(IsGroupedProperty);
    public static void SetIsGrouped(StyledElement element, bool value) => element.SetValue(IsGroupedProperty, value);
}
