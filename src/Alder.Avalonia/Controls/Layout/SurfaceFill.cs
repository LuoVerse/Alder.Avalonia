using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Draws a surface and its shadow to the outer edge of its outline.</summary>
internal sealed class SurfaceFill : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<SurfaceFill>();
    public static readonly StyledProperty<Thickness> BorderThicknessProperty = Border.BorderThicknessProperty.AddOwner<SurfaceFill>();
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = Border.CornerRadiusProperty.AddOwner<SurfaceFill>();
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty = Border.BoxShadowProperty.AddOwner<SurfaceFill>();

    static SurfaceFill() => AffectsRender<SurfaceFill>(BackgroundProperty, BorderThicknessProperty, CornerRadiusProperty, BoxShadowProperty);

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public override void Render(DrawingContext context)
    {
        var corner = CornerRadius;
        var border = UseLayoutRounding
            ? LayoutHelper.RoundLayoutThickness(BorderThickness, LayoutHelper.GetLayoutScale(this))
            : BorderThickness;
        // Avalonia measures the corner radius at the center of the border stroke.
        var outline = new RoundedRect(new Rect(Bounds.Size),
            OuterRadius(corner.TopLeft, border.Left, border.Top),
            OuterRadius(corner.TopRight, border.Right, border.Top),
            OuterRadius(corner.BottomRight, border.Right, border.Bottom),
            OuterRadius(corner.BottomLeft, border.Left, border.Bottom));
        context.DrawRectangle(Background, null, outline, BoxShadow);
    }

    private static Vector OuterRadius(double radius, double horizontalBorder, double verticalBorder) =>
        radius == 0 ? default : new Vector(radius + horizontalBorder / 2, radius + verticalBorder / 2);
}
