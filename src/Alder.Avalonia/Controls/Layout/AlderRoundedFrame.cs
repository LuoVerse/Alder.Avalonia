using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class AlderRoundedFrame : Decorator
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<AlderRoundedFrame>();

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty ||
            (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != Bounds.Size))
            OpacityMask = CreateMask(Bounds.Size, CornerRadius);
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
