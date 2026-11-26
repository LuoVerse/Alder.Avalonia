using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Alder.Avalonia.Controls;

internal sealed class InputFocusRing : Control
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        TemplatedControl.CornerRadiusProperty.AddOwner<InputFocusRing>();
    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        Border.BoxShadowProperty.AddOwner<InputFocusRing>();

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    static InputFocusRing() => AffectsRender<InputFocusRing>(CornerRadiusProperty, BoxShadowProperty);

    public override void Render(DrawingContext context)
    {
        if (BoxShadow.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        for (int i = 0; i < BoxShadow.Count; i++)
        {
            var ring = BoxShadow[i];
            if (ring.Spread <= 0 || ring.Color.A == 0) continue;
            // Center each stroke outside the field so fractional spread grows from its edge.
            var halfSpread = ring.Spread / 2;
            var outline = new RoundedRect(new Rect(Bounds.Size), CornerRadius).Inflate(halfSpread, halfSpread);
            context.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), ring.Spread), outline);
        }
    }
}
