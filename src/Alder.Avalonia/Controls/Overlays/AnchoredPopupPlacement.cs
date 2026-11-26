using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal static class AnchoredPopupPlacement
{
    public static bool IsVertical(PlacementMode placement) => placement is PlacementMode.Top or PlacementMode.Bottom or
        PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight or
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight;

    public static bool PrefersTop(PlacementMode placement) => placement is PlacementMode.Top or
        PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight;

    public static void PlaceVertical(CustomPopupPlacement placement, PlacementMode mode, double gap, double crossOffset = 0)
    {
        var anchor = placement.AnchorRectangle;
        var size = placement.PopupSize.Deflate(placement.Deflate);
        bool rtl = placement.Target.FlowDirection == FlowDirection.RightToLeft;
        bool above = PrefersTop(mode);
        double left = crossOffset + (mode switch
        {
            PlacementMode.TopEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedLeft => rtl ? anchor.Right - size.Width : anchor.Left,
            PlacementMode.TopEdgeAlignedRight or PlacementMode.BottomEdgeAlignedRight => rtl ? anchor.Left : anchor.Right - size.Width,
            _ => anchor.Center.X - size.Width / 2
        });
        double top = above ? anchor.Top - gap - size.Height : anchor.Bottom + gap;
        if (TopLevel.GetTopLevel(placement.Target) is { } root)
        {
            var viewport = new Rect(root.ClientSize).Deflate(root.InsetsManager?.SafeAreaPadding ?? default).Deflate(new Thickness(12));
            double roomAbove = anchor.Top - viewport.Top;
            double roomBelow = viewport.Bottom - anchor.Bottom;
            double preferredRoom = above ? roomAbove : roomBelow;
            double otherRoom = above ? roomBelow : roomAbove;
            if (size.Height + gap > preferredRoom && otherRoom > preferredRoom)
                top = above ? anchor.Bottom + gap : anchor.Top - gap - size.Height;

            left = Math.Clamp(left, viewport.Left, Math.Max(viewport.Left, viewport.Right - size.Width));
            top = Math.Clamp(top, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - size.Height));
        }

        placement.Anchor = PopupAnchor.TopLeft;
        placement.Gravity = PopupGravity.BottomRight;
        // Avalonia mirrors the horizontal anchor/gravity for RTL after the callback.
        placement.Offset = new Point(left - (rtl ? anchor.Right - size.Width : anchor.Left), top - anchor.Top);
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY |
                                         PopupPositionerConstraintAdjustment.ResizeX | PopupPositionerConstraintAdjustment.ResizeY;
    }
}
