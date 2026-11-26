using Avalonia.Controls;
using Avalonia.Input;

namespace Alder.Avalonia.Controls;

internal sealed class AlderDateTriggerButton : Button
{
    private NavigationMethod activationMethod;

    protected override Type StyleKeyOverride => typeof(Button);

    internal static NavigationMethod GetActivationMethod(Button? trigger) => trigger switch
    {
        AlderDateTriggerButton button => button.activationMethod,
        _ when trigger?.Classes.Contains(":focus-visible") == true => NavigationMethod.Directional,
        _ => NavigationMethod.Unspecified
    };

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) activationMethod = NavigationMethod.Pointer;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space) activationMethod = NavigationMethod.Directional;
        base.OnKeyDown(e);
    }

    protected override void OnClick()
    {
        try { base.OnClick(); }
        finally { activationMethod = NavigationMethod.Unspecified; }
    }
}
