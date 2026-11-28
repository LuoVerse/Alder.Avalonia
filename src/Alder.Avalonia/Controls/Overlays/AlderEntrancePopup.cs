using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Alder.Avalonia.Controls;

/// <summary>Animates the surface without delaying native popup dismissal or changing focus handling.</summary>
internal sealed class AlderEntrancePopup : AlderPopup
{
    private MenuPopupMotion? motion;

    public AlderEntrancePopup()
    {
        Opened += (_, _) =>
        {
            if (Child is not { } child ||
                (PlacementTarget ?? TemplatedParent as Control ?? this.FindLogicalAncestorOfType<Control>()) is not { } target ||
                AlderMotion.GetReduced(target))
                return;

            // The submenu positioner can flip the side. Use actual placement to determine the transform origin.
            var direction = Placement == PlacementMode.Custom ? PlacementMode.RightEdgeAlignedTop : (PlacementMode?)null;
            (motion ??= new()).Opened(this, child, target, preferredPlacement: direction,
                entranceStyle: FadeOnly ? PopupEntranceStyle.Fade : PopupEntranceStyle.Compact);
        };
        Closed += (_, _) => motion?.Closed();
    }

    public bool FadeOnly { get; set; }
}
