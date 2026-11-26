using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Alder.Avalonia.Controls;

/// <summary>Provides binding converters for displaying Alder color values and transparency.</summary>
public static class AlderColorRendering
{
    /// <summary>The binding converter from an Alder color value to an Avalonia color.</summary>
    public static IValueConverter ToColor { get; } = new FuncValueConverter<AlderColorValue, Color>(value => value.ToColor());

    /// <summary>The binding converter from an Alder color value to a solid color brush.</summary>
    public static IValueConverter ToBrush { get; } = new FuncValueConverter<AlderColorValue, IBrush>(value => new SolidColorBrush(value.ToColor()));

    /// <summary>The binding converter for a swatch fill that can show transparency.</summary>
    public static IValueConverter SwatchBrush { get; } = new FuncValueConverter<Color, IBrush>(color => new ImmutableSolidColorBrush(color));

    /// <summary>The binding converter that chooses a foreground for the selected swatch indicator.</summary>
    public static IValueConverter SwatchIndicatorBrush { get; } = new FuncValueConverter<Color, IBrush>(color =>
        (color.R * .299 + color.G * .587 + color.B * .114) * (color.A / 255d) + 239 * (1 - color.A / 255d) > 165
            ? Brushes.Black : Brushes.White);

    /// <summary>The binding converter that provides a checkerboard surface for showing transparency.</summary>
    public static IBrush Checkerboard { get; } = new DrawingBrush
    {
        SourceRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
        DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
        TileMode = TileMode.Tile,
        Drawing = new DrawingGroup
        {
            Children =
            {
                Tile(new ImmutableSolidColorBrush(Color.FromRgb(247, 247, 247)), 0, 0, 16),
                Tile(new ImmutableSolidColorBrush(Color.FromRgb(239, 239, 239)), 0, 0, 8),
                Tile(new ImmutableSolidColorBrush(Color.FromRgb(239, 239, 239)), 8, 8, 8)
            }
        }
    };

    private static GeometryDrawing Tile(IBrush brush, double x, double y, double size) =>
        new() { Brush = brush, Geometry = new RectangleGeometry { Rect = new Rect(x, y, size, size) } };
}
