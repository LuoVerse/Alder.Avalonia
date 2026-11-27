using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects a semantic color treatment for status indicators such as badges and chips.</summary>
public enum AlderStatusColor
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the accent color.</summary>
    Accent,
    /// <summary>Uses the success treatment.</summary>
    Success,
    /// <summary>Uses the warning treatment.</summary>
    Warning,
    /// <summary>Uses the danger treatment.</summary>
    Danger
}
/// <summary>Controls the visual emphasis of a badge.</summary>
public enum BadgeVariant
{
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary,
    /// <summary>Uses the lower-contrast treatment.</summary>
    Soft
}
/// <summary>Specifies the corner of an anchor at which its badge is positioned.</summary>
public enum BadgePlacement
{
    /// <summary>Positions the badge at the top right corner.</summary>
    TopRight,
    /// <summary>Positions the badge at the top left corner.</summary>
    TopLeft,
    /// <summary>Positions the badge at the bottom right corner.</summary>
    BottomRight,
    /// <summary>Positions the badge at the bottom left corner.</summary>
    BottomLeft
}

/// <summary>A count, label, icon or dot placed over an element in a <see cref="AlderBadgeAnchor"/>.</summary>
public sealed partial class AlderBadge : ContentControl
{
    /// <summary>The size preset used for the control.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }
    /// <summary>The color applied to the control.</summary>
    [GeneratedStyledProperty]
    public partial AlderStatusColor Color { get; set; }
    /// <summary>The visual treatment applied to the control.</summary>
    [GeneratedStyledProperty]
    public partial BadgeVariant Variant { get; set; }
    /// <summary>The anchor corner used to position this badge.</summary>
    [GeneratedStyledProperty]
    public partial BadgePlacement Placement { get; set; }
    /// <summary>An optional geometry or icon control displayed with the content.</summary>
    [GeneratedStyledProperty]
    public partial object? Icon { get; set; }
    /// <summary>The size of the optional badge icon in device-independent pixels.</summary>
    [GeneratedStyledProperty(DefaultValue = 12d)]
    public partial double IconSize { get; set; }

    static AlderBadge() => ZIndexProperty.OverrideDefaultValue<AlderBadge>(1);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlacementProperty && this.GetVisualParent() is AlderBadgeAnchor anchor)
            anchor.InvalidateArrange();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgePeer(this);

    private sealed class BadgePeer(AlderBadge owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() ?? owner.Content?.ToString();
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}

/// <summary>Lays out its content and positions badge children without changing the content footprint.</summary>
public sealed class AlderBadgeAnchor : Panel
{
    static AlderBadgeAnchor()
    {
        HorizontalAlignmentProperty.OverrideDefaultValue<AlderBadgeAnchor>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<AlderBadgeAnchor>(VerticalAlignment.Top);
        AffectsArrange<AlderBadgeAnchor>(FlowDirectionProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            if (child is AlderBadge)
            {
                child.Measure(Size.Infinity);
                continue;
            }

            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is not AlderBadge badge)
            {
                child.Arrange(new Rect(finalSize));
                continue;
            }

            var size = badge.DesiredSize;
            bool right = badge.Placement is BadgePlacement.TopRight or BadgePlacement.BottomRight;
            bool bottom = badge.Placement is BadgePlacement.BottomLeft or BadgePlacement.BottomRight;
            if (FlowDirection == FlowDirection.RightToLeft) right = !right;
            double x = right ? finalSize.Width - size.Width * .75 : -size.Width * .25;
            double y = bottom ? finalSize.Height - size.Height * .75 : -size.Height * .25;
            badge.Arrange(new Rect(x, y, size.Width, size.Height));
        }
        return finalSize;
    }
}
