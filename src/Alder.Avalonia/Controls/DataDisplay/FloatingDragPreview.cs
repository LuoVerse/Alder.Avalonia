using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class FloatingDragPreview : IDisposable
{
    private readonly OverlayLayer layer;
    private readonly Border surface;
    private readonly Image image;
    private readonly RenderTargetBitmap bitmap;
    private readonly TranslateTransform transform = new();
    private readonly Point grabOffset;
    private readonly double inset;

    public FloatingDragPreview(OverlayLayer layer, Control row, Rect source, Point grabOffset, double scale, ControlTheme? theme, string name, double inset = 1)
    {
        this.layer = layer;
        this.grabOffset = grabOffset;
        this.inset = inset;
        var pixelSize = new PixelSize((int)Math.Ceiling(source.Width * scale), (int)Math.Ceiling(source.Height * scale));
        // Image samples the snapshot in pixels. Apply the display scale while capturing it and keep its DPI at 96.
        bitmap = new RenderTargetBitmap(pixelSize);
        var brush = new VisualBrush(row) { SourceRect = new RelativeRect(source, RelativeUnit.Absolute) };
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(pixelSize.Width / source.Width, pixelSize.Height / source.Height)))
            context.FillRectangle(brush, new Rect(source.Size));

        image = new AlderImage { Source = bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
        surface = new Border
        {
            Name = name,
            Theme = theme,
            Width = source.Width + 2 * inset,
            Height = source.Height + 2 * inset,
            ClipToBounds = false,
            IsHitTestVisible = false,
            ZIndex = int.MaxValue,
            Opacity = .96,
            RenderTransform = transform
        };
        surface.Child = new Border
        {
            [!Border.CornerRadiusProperty] = surface[!Border.CornerRadiusProperty],
            ClipToBounds = true,
            Child = image
        };
        AutomationProperties.SetAccessibilityView(surface, AccessibilityView.Raw);
        layer.Children.Add(surface);
    }

    public void Move(Visual grid, Point pointer)
    {
        if (grid.TranslatePoint(pointer, layer) is not { } position) return;
        transform.X = position.X - grabOffset.X - inset;
        transform.Y = position.Y - grabOffset.Y - inset;
    }

    public Point? OriginIn(Visual relativeTo) => layer.TranslatePoint(new Point(transform.X, transform.Y), relativeTo);

    public void Dispose()
    {
        layer.Children.Remove(surface);
        image.Source = null;
        bitmap.Dispose();
    }
}
