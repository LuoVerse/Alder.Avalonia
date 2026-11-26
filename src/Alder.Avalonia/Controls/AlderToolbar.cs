using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Lays out related actions using the Alder toolbar style.</summary>
public class AlderToolbar : StackPanel
{
    static AlderToolbar() => SpacingProperty.OverrideDefaultValue<AlderToolbar>(8);

    /// <summary>Initializes a new instance of <see cref="AlderToolbar"/>.</summary>
    public AlderToolbar()
    {
        Orientation = Orientation.Horizontal;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Button current) return;
        var direction = Orientation == Orientation.Horizontal
            ? e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0
            : e.Key == Key.Down ? 1 : e.Key == Key.Up ? -1 : 0;
        if (Orientation == Orientation.Horizontal && FlowDirection == FlowDirection.RightToLeft) direction *= -1;
        if (direction == 0) return;
        var items = this.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or TextBox or Slider or ComboBox)
            .Where(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible
                && c.FindAncestorOfType<AlderToolbar>() == this
                && !c.GetVisualAncestors().TakeWhile(a => a != this).Any(a => a is Button or TextBox or Slider or ComboBox))
            .ToArray();
        var index = Array.IndexOf(items, current);
        if (index < 0) return;
        items[(index + direction + items.Length) % items.Length].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarPeer(this);
    private sealed class ToolbarPeer(AlderToolbar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
    }
}
