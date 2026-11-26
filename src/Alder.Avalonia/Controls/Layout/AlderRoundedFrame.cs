using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class AlderRoundedFrame : Border
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != CornerRadiusProperty) return;

        bool rounded = CornerRadius != default;
        ClipToBounds = rounded;
        // The opaque mask opens a compositing layer inside the rounded clip.
        // The clip antialiases the combined background and content once, at the render target's scale.
        // Refresh the layer even when the old and new corner radii are both nonzero.
        OpacityMask = rounded ? new SolidColorBrush(Colors.White) : null;
    }

    internal static IBrush? CreateMask(Size size, CornerRadius radius)
    {
        if (radius == default || size.Width <= 0 || size.Height <= 0) return null;

        // Composite the background and content before applying antialiasing at the outer edge.
        var shape = new MaskShape(new RoundedRect(new Rect(size), radius)) { Width = size.Width, Height = size.Height };
        return new VisualBrush { Visual = shape, Stretch = Stretch.Fill };
    }

    private sealed class MaskShape(RoundedRect shape) : Control
    {
        public override void Render(DrawingContext context) => context.DrawRectangle(Brushes.White, null, shape);
    }
}
