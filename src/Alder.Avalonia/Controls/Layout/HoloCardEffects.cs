using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class HoloCardEffects : Control
{
    public static readonly StyledProperty<IBrush?> PatternProperty = AlderHoloCard.PatternProperty.AddOwner<HoloCardEffects>();
    public static readonly StyledProperty<IBrush?> ShinePatternProperty = AlderHoloCard.ShinePatternProperty.AddOwner<HoloCardEffects>();
    public static readonly StyledProperty<double> TextureOpacityProperty = AlderHoloCard.TextureOpacityProperty.AddOwner<HoloCardEffects>();
    public static readonly StyledProperty<double> ShineOpacityProperty = AlderHoloCard.ShineOpacityProperty.AddOwner<HoloCardEffects>();

    public IBrush? Pattern { get => GetValue(PatternProperty); set => SetValue(PatternProperty, value); }
    public IBrush? ShinePattern { get => GetValue(ShinePatternProperty); set => SetValue(ShinePatternProperty, value); }
    public double TextureOpacity { get => GetValue(TextureOpacityProperty); set => SetValue(TextureOpacityProperty, value); }
    public double ShineOpacity { get => GetValue(ShineOpacityProperty); set => SetValue(ShineOpacityProperty, value); }

    private static readonly ImmutableGradientStop[] FoilColors =
    [
        new(0, Color.FromUInt32(0xD9FF7773)),
        new(1d / 6, Color.FromUInt32(0xD9FFED5F)),
        new(2d / 6, Color.FromUInt32(0xD9A8FF5F)),
        new(3d / 6, Color.FromUInt32(0xD983FFF7)),
        new(4d / 6, Color.FromUInt32(0xD97894FF)),
        new(5d / 6, Color.FromUInt32(0xD9D875FF)),
        new(1, Color.FromUInt32(0xD9FF7773))
    ];

    private static readonly ImmutableGradientStop[] GlintColors =
    [
        new(0, Color.FromUInt32(0xFFBA6751)),
        new(1d / 6, Color.FromUInt32(0xFFC38836)),
        new(2d / 6, Color.FromUInt32(0xFF75A63E)),
        new(3d / 6, Color.FromUInt32(0xFF3FAFA7)),
        new(4d / 6, Color.FromUInt32(0xFF4265C6)),
        new(5d / 6, Color.FromUInt32(0xFF9759BC)),
        new(1, Color.FromUInt32(0xFFBA6751))
    ];

    private static readonly ImmutableGradientStop[] GlareFalloff =
    [
        new(0, Color.FromUInt32(0xC2FFFFFF)),
        new(.22, Color.FromUInt32(0x91FFFFFF)),
        new(.55, Color.FromUInt32(0x32FFFFFF)),
        new(.84, Color.FromUInt32(0x06FFFFFF)),
        new(1, Colors.Transparent)
    ];

    private static readonly ImmutableLinearGradientBrush Foil = Spectrum(FoilColors, default);
    private static readonly ImmutableLinearGradientBrush Grooves = new(
        [new(0, Color.FromUInt32(0x23000000)), new(1d / 3, Color.FromUInt32(0x23000000)),
         new(1d / 3, Colors.Transparent), new(1, Colors.Transparent)],
        1, null, null, GradientSpreadMethod.Repeat, new RelativePoint(0, 0, RelativeUnit.Absolute),
        new RelativePoint(3, 0, RelativeUnit.Absolute));
    private static readonly ImmutableSolidColorBrush GlintBase = new(Color.FromUInt32(0xFF040506));
    private Point pointer;
    private double strength;
    private RenderTargetBitmap? foilBitmap;
    private Size foilSize;

    static HoloCardEffects() =>
        AffectsRender<HoloCardEffects>(PatternProperty, ShinePatternProperty, TextureOpacityProperty, ShineOpacityProperty);

    internal void SetGlare(Point position, double amount)
    {
        if (amount == strength && (amount == 0 || pointer == position))
            return;

        pointer = position;
        strength = amount;
        if (ShineOpacity > 0) InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        double shine = strength * ShineOpacity;
        if (bounds.Width <= 0 || bounds.Height <= 0 || (TextureOpacity == 0 && shine == 0))
            return;

        var dpi = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        double scale = Math.Min(dpi, 2048 / Math.Max(bounds.Width, bounds.Height));
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(bounds.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(bounds.Height * scale)));

        if (TextureOpacity > 0)
        {
            if (Pattern is { } texture)
            {
                using var bitmap = Rasterize(pixels, bounds, paint => paint.FillRectangle(texture, bounds));
                Blend(context, bitmap, bounds, BitmapBlendingMode.Overlay, TextureOpacity);
            }
            else
            {
                // Cache the static foil independently of the moving glare.
                if (foilBitmap is null || foilBitmap.PixelSize != pixels || foilSize != bounds.Size)
                {
                    foilBitmap?.Dispose();
                    foilSize = bounds.Size;
                    foilBitmap = Rasterize(pixels, bounds, paint =>
                    {
                        paint.FillRectangle(Foil, bounds);
                        paint.FillRectangle(Grooves, bounds);
                    });
                }
                Blend(context, foilBitmap, bounds, BitmapBlendingMode.Overlay, TextureOpacity);
            }
        }
        if (shine <= 0) return;

        // Re-rasterise just the moving light. Blending the masked bitmap against the actual
        // surface lets the same glare react to a solid brush, a gradient or image artwork.
        // The moving bands are broad enough for a DIP-resolution layer even on a high-DPI display.
        double glintScale = Math.Min(1, scale);
        var glintPixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(bounds.Width * glintScale)),
            Math.Max(1, (int)Math.Ceiling(bounds.Height * glintScale)));
        using var glint = Rasterize(glintPixels, bounds, paint => DrawGlare(paint, bounds));
        Blend(context, glint, bounds, BitmapBlendingMode.ColorDodge, shine);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foilBitmap?.Dispose();
        foilBitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void DrawGlare(DrawingContext paint, Rect bounds)
    {
        var center = new RelativePoint(pointer, RelativeUnit.Relative);
        var radius = new RelativeScalar(Math.Max(bounds.Width, bounds.Height) * .43, RelativeUnit.Absolute);
        var mask = new ImmutableRadialGradientBrush(GlareFalloff, 1, null, null,
            GradientSpreadMethod.Pad, center, center, radius, radius);
        using var clip = paint.PushOpacityMask(mask, bounds);
        var phase = new Point(34 * (.5 - pointer.X), 34 * (.5 - pointer.Y));
        if ((ShinePattern ?? Pattern) is { } pattern)
        {
            // Bound the luminance before color dodge so white texels retain local detail.
            paint.FillRectangle(GlintBase, bounds);
            using var opacity = paint.PushOpacity(.62);
            using var slide = paint.PushTransform(Matrix.CreateTranslation(phase.X, phase.Y));
            paint.FillRectangle(pattern, bounds.Inflate(17));
        }
        else
        {
            paint.FillRectangle(Spectrum(GlintColors, phase), bounds);
            paint.FillRectangle(Grooves, bounds);
        }
    }

    private static RenderTargetBitmap Rasterize(PixelSize pixels, Rect bounds, Action<DrawingContext> draw)
    {
        var bitmap = new RenderTargetBitmap(pixels,
            new Vector(96 * pixels.Width / bounds.Width, 96 * pixels.Height / bounds.Height));
        using var paint = bitmap.CreateDrawingContext();
        draw(paint);
        return bitmap;
    }

    private static void Blend(DrawingContext context, Bitmap bitmap, Rect bounds, BitmapBlendingMode mode, double opacity)
    {
        using var renderOptions = context.PushRenderOptions(new RenderOptions
        {
            BitmapBlendingMode = mode,
            RequiresFullOpacityHandling = false
        });
        using var alpha = context.PushOpacity(opacity);
        context.DrawImage(bitmap, bounds);
    }

    private static ImmutableLinearGradientBrush Spectrum(IReadOnlyList<ImmutableGradientStop> colors, Point phase) => new(
        colors, 1, null, null, GradientSpreadMethod.Repeat,
        new RelativePoint(phase, RelativeUnit.Absolute),
        new RelativePoint(phase + new Vector(16.31, 7.61), RelativeUnit.Absolute));
}
