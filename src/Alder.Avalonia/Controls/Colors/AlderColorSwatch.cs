using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects a circular or square outline for a swatch.</summary>
public enum ColorSwatchShape
{
    /// <summary>Draws a circle swatch.</summary>
    Circle,
    /// <summary>Draws a square swatch.</summary>
    Square
}
/// <summary>Selects one of the supported swatch dimensions.</summary>
public enum ColorSwatchSize
{
    /// <summary>Uses the extra-small size.</summary>
    ExtraSmall,
    /// <summary>Uses the small size.</summary>
    Small,
    /// <summary>Uses the medium size.</summary>
    Medium,
    /// <summary>Uses the large size.</summary>
    Large,
    /// <summary>Uses the extra-large size.</summary>
    ExtraLarge
}

/// <summary>Displays an sRGB color, with a checkerboard behind transparent colors.</summary>
public sealed class AlderColorSwatch : TemplatedControl
{
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<AlderColorSwatch, Color>(nameof(Color), AlderColorValue.Default.ToColor());
    /// <summary>Identifies the <see cref="ColorName"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ColorNameProperty =
        AvaloniaProperty.Register<AlderColorSwatch, string?>(nameof(ColorName));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchSize> SizeProperty =
        AvaloniaProperty.Register<AlderColorSwatch, ColorSwatchSize>(nameof(Size), ColorSwatchSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="Shape"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSwatchShape> ShapeProperty =
        AvaloniaProperty.Register<AlderColorSwatch, ColorSwatchShape>(nameof(Shape), inherits: true);

    /// <summary>The color applied to the control.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>An optional name associated with the displayed color.</summary>
    public string? ColorName { get => GetValue(ColorNameProperty); set => SetValue(ColorNameProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public ColorSwatchSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The outline of the drawn swatch.</summary>
    public ColorSwatchShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    static AlderColorSwatch() => AffectsRender<AlderColorSwatch>(ColorProperty, ShapeProperty, CornerRadiusProperty, BorderBrushProperty);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var radius = Shape == ColorSwatchShape.Circle ? new CornerRadius(Math.Min(rect.Width, rect.Height) / 2) : CornerRadius;
        var rounded = new RoundedRect(rect, radius);
        using (context.PushClip(rounded))
        {
            if (Color.A < byte.MaxValue)
                context.FillRectangle(AlderColorRendering.Checkerboard, rect);
            context.FillRectangle(new SolidColorBrush(Color), rect);
            if (BorderBrush is { } brush)
                context.DrawRectangle(null, new Pen(brush, 2), rounded);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SwatchPeer(this);

    private sealed class SwatchPeer(AlderColorSwatch owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string? GetNameCore() =>
            base.GetNameCore() ?? owner.ColorName ?? AlderColorValue.FormatHex(owner.Color);
    }
}
