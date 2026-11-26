using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

internal sealed class CardSlotsPanel : Panel
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AlderCard.OrientationProperty.AddOwner<CardSlotsPanel>();
    public static readonly StyledProperty<double> SpacingProperty = AlderCard.SpacingProperty.AddOwner<CardSlotsPanel>();

    static CardSlotsPanel() => AffectsMeasure<CardSlotsPanel>(OrientationProperty, SpacingProperty);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    private bool IsHorizontal => Orientation == Orientation.Horizontal;

    protected override Size MeasureOverride(Size availableSize)
    {
        var header = Children[0];
        var body = Children[1];
        var footer = Children[2];

        header.Measure(availableSize);
        var afterHeader = Math.Max(0, Length(availableSize) - Length(header.DesiredSize) - GapAfterHeader);
        footer.Measure(AxisSize(afterHeader, Breadth(availableSize)));
        var availableBody = Math.Max(0, afterHeader - Length(footer.DesiredSize) - GapBeforeFooter);
        body.Measure(AxisSize(availableBody, Breadth(availableSize)));

        var length = Length(header.DesiredSize) + GapAfterHeader + Length(body.DesiredSize)
            + GapBeforeFooter + Length(footer.DesiredSize);
        var breadth = Math.Max(Breadth(header.DesiredSize), Math.Max(Breadth(body.DesiredSize), Breadth(footer.DesiredSize)));
        return AxisSize(length, breadth);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var length = Length(finalSize);
        var breadth = Breadth(finalSize);
        var header = Math.Min(length, Length(Children[0].DesiredSize));
        var afterHeader = Math.Min(length, header + GapAfterHeader);
        var footer = Math.Min(length - afterHeader, Length(Children[2].DesiredSize));
        var body = Math.Max(0, length - afterHeader - GapBeforeFooter - footer);

        Children[0].Arrange(AxisRect(0, header, breadth));
        Children[1].Arrange(AxisRect(afterHeader, body, breadth));
        Children[2].Arrange(AxisRect(length - footer, footer, breadth));
        return finalSize;
    }

    private double GapAfterHeader => Children[0].IsVisible && (Children[1].IsVisible || Children[2].IsVisible) ? Spacing : 0;
    private double GapBeforeFooter => Children[1].IsVisible && Children[2].IsVisible ? Spacing : 0;
    private double Length(Size size) => IsHorizontal ? size.Width : size.Height;
    private double Breadth(Size size) => IsHorizontal ? size.Height : size.Width;
    private Size AxisSize(double length, double breadth) => IsHorizontal ? new Size(length, breadth) : new Size(breadth, length);
    private Rect AxisRect(double start, double length, double breadth) =>
        IsHorizontal ? new Rect(start, 0, length, breadth) : new Rect(0, start, breadth, length);
}
