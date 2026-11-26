using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Centers the actual geometry bounds inside a fixed icon slot, with room for antialiasing.</summary>
internal sealed class AlderFittedIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty = PathIcon.DataProperty.AddOwner<AlderFittedIcon>();
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AlderFittedIcon, IBrush?>(nameof(Fill));

    static AlderFittedIcon() => AffectsRender<AlderFittedIcon>(DataProperty, FillProperty);

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public override void Render(DrawingContext context)
    {
        var geometry = Data;
        var fill = Fill;
        if (geometry is null || fill is null) return;

        var ink = geometry.Bounds;
        var slot = new Rect(Bounds.Size).Deflate(1);
        if (ink.Width <= 0 || ink.Height <= 0 || slot.Width <= 0 || slot.Height <= 0) return;

        var scale = Math.Min(slot.Width / ink.Width, slot.Height / ink.Height);
        var offset = new Vector(slot.Center.X - ink.Center.X * scale, slot.Center.Y - ink.Center.Y * scale);
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y));
        context.DrawGeometry(fill, null, geometry);
    }
}
