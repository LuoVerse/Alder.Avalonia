using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>An anchored menu that opens in the same window with Alder dropdown styling.</summary>
public sealed class AlderDropdownFlyout : AlderMenuFlyout
{
    /// <summary>Identifies the <see cref="MinMenuWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinMenuWidthProperty =
        AvaloniaProperty.Register<AlderDropdownFlyout, double>(nameof(MinMenuWidth), 160,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>The minimum width of the menu surface in device-independent pixels.</summary>
    public double MinMenuWidth { get => GetValue(MinMenuWidthProperty); set => SetValue(MinMenuWidthProperty, value); }

    /// <summary>Initializes a new instance of <see cref="AlderDropdownFlyout"/>.</summary>
    public AlderDropdownFlyout()
    {
        Placement = PlacementMode.Custom;
        CustomPopupPlacementCallback = PlaceDropdown;
        VerticalOffset = 8;
    }

    /// <inheritdoc />
    protected override Control CreatePresenter()
    {
        var presenter = base.CreatePresenter();
        presenter[!Control.MinWidthProperty] = this[!MinMenuWidthProperty];
        return presenter;
    }

    /// <inheritdoc />
    protected override void OnOpened()
    {
        MotionPlacement = null;
        if (AnchoredPopupPlacement.IsVertical(Popup.Placement))
        {
            var preferred = Popup.Placement;
            MotionPlacement = preferred;
            Popup.CustomPopupPlacementCallback = placement => AnchoredPopupPlacement.PlaceVertical(placement, preferred,
                AnchoredPopupPlacement.PrefersTop(preferred) ? -VerticalOffset : VerticalOffset, HorizontalOffset);
            Popup.Placement = PlacementMode.Custom;
        }
        base.OnOpened();
    }

    private static void PlaceDropdown(CustomPopupPlacement placement)
    {
        var anchor = placement.AnchorRectangle;
        var popup = placement.PopupSize.Deflate(placement.Deflate);
        var gap = placement.Offset.Y;
        bool rtl = placement.Target.FlowDirection == FlowDirection.RightToLeft;
        double start = rtl ? anchor.Right - popup.Width : anchor.Left;
        double left = start + placement.Offset.X;
        double top = anchor.Bottom + gap;

        if (TopLevel.GetTopLevel(placement.Target) is { } root)
        {
            var safeArea = root.InsetsManager?.SafeAreaPadding ?? default;
            var viewport = new Rect(root.ClientSize).Deflate(safeArea).Deflate(new Thickness(12));
            double below = viewport.Bottom - anchor.Bottom;
            double above = anchor.Top - viewport.Top;
            if (popup.Height + gap > below && above > below)
                top = anchor.Top - gap - popup.Height;

            left = Math.Clamp(left, viewport.Left, Math.Max(viewport.Left, viewport.Right - popup.Width));
            top = Math.Clamp(top, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - popup.Height));
        }

        placement.Anchor = PopupAnchor.TopLeft;
        placement.Gravity = PopupGravity.BottomRight;
        placement.Offset = new Point(left - start, top - anchor.Top);
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX |
                                         PopupPositionerConstraintAdjustment.SlideY;
    }
}
