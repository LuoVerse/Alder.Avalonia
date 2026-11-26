using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>Displays independent switches with a shared heading and layout.</summary>
public sealed class AlderSwitchGroup : HeaderedItemsControl
{
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderSwitchGroup, Orientation>(nameof(Orientation), Orientation.Vertical);
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderSwitchGroup, string?>(nameof(Description));

    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SwitchGroupPeer(this);

    private sealed class SwitchGroupPeer(AlderSwitchGroup owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Header as string;
    }
}
