using System.Globalization;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Identifies the color model used to interpret the value's components.</summary>
public enum ColorSpace
{
    /// <summary>Uses red, green, and blue components.</summary>
    Rgb,
    /// <summary>Uses hue, saturation, and lightness.</summary>
    Hsl,
    /// <summary>Uses hue, saturation, and brightness.</summary>
    Hsb
}

/// <summary>Identifies a component that can be edited independently of the rest of a color value.</summary>
public enum ColorChannel
{
    /// <summary>Edits the red component.</summary>
    Red,
    /// <summary>Edits the green component.</summary>
    Green,
    /// <summary>Edits the blue component.</summary>
    Blue,
    /// <summary>Edits the hue component.</summary>
    Hue,
    /// <summary>Edits the saturation component.</summary>
    Saturation,
    /// <summary>Edits the lightness component.</summary>
    Lightness,
    /// <summary>Edits the brightness component.</summary>
    Brightness,
    /// <summary>Edits opacity independently of the color components.</summary>
    Alpha
}

/// <summary>A color with floating-point channels, stored in the space in which it was edited.</summary>
public readonly record struct AlderColorValue
{
    private AlderColorValue(ColorSpace space, double first, double second, double third, double alpha, double hue)
    {
        Space = space;
        First = first;
        Second = second;
        Third = third;
        Alpha = alpha;
        Hue = hue;
    }

    /// <summary>The space in which the color's floating-point channels are stored.</summary>
    public ColorSpace Space { get; }
    /// <summary>The normalized opacity in the range from zero to one.</summary>
    public double Alpha { get; private init; }
    /// <summary>The hue angle retained for subsequent edits, in degrees.</summary>
    public double Hue { get; private init; }
    private double First { get; init; }
    private double Second { get; init; }
    private double Third { get; init; }

    /// <summary>Creates an RGB color with channels from 0 to 255 and alpha from 0 to 1.</summary>
    public static AlderColorValue FromRgb(double red, double green, double blue, double alpha = 1)
    {
        double r = Clamp(red, 255) / 255, g = Clamp(green, 255) / 255, b = Clamp(blue, 255) / 255;
        return new(ColorSpace.Rgb, r, g, b, Clamp(alpha), GetHsv(r, g, b, 1, 0).H);
    }

    /// <summary>Creates an HSL color with hue in degrees and other channels from 0 to 1.</summary>
    public static AlderColorValue FromHsl(double hue, double saturation, double lightness, double alpha = 1) =>
        new(ColorSpace.Hsl, Clamp(hue, 360) / 360, Clamp(saturation), Clamp(lightness), Clamp(alpha), Clamp(hue, 360));

    /// <summary>Creates an HSB (HSV) color with hue in degrees and other channels from 0 to 1.</summary>
    public static AlderColorValue FromHsb(double hue, double saturation, double brightness, double alpha = 1) =>
        new(ColorSpace.Hsb, Clamp(hue, 360) / 360, Clamp(saturation), Clamp(brightness), Clamp(alpha), Clamp(hue, 360));

    /// <summary>Creates a floating-point RGB value from an Avalonia color.</summary>
    public static AlderColorValue FromColor(Color color) => FromRgb(color.R, color.G, color.B, color.A / 255d);
    /// <summary>Converts an Avalonia color to a floating-point Alder color value.</summary>
    public static explicit operator AlderColorValue(Color color) => FromColor(color);
    /// <summary>Converts an Alder color value to an Avalonia color by quantizing its channels.</summary>
    public static explicit operator Color(AlderColorValue value) => value.ToColor();

    /// <summary>Converts this value to eight-bit RGB channels and alpha.</summary>
    public Color ToColor()
    {
        var rgb = InSpace(ColorSpace.Rgb);
        return Color.FromArgb(ToByte(Alpha), ToByte(rgb.First), ToByte(rgb.Second), ToByte(rgb.Third));
    }

    /// <summary>Gets a channel as degrees for hue, 0–255 for RGB, or 0–100 for other channels.</summary>
    public double GetChannel(ColorChannel channel, ColorSpace? colorSpace = null)
    {
        var space = SpaceFor(channel, colorSpace ?? Space);
        return InSpace(space).At(Index(space, channel)) * Maximum(channel);
    }

    /// <summary>Formats this color as CSS hexadecimal text, including alpha when needed.</summary>
    public override string ToString() => FormatHex(ToColor());

    /// <summary>Parses a CSS or Avalonia named color and throws if the input is invalid.</summary>
    public static AlderColorValue Parse(string text) => TryParse(text, out var value)
        ? value : throw new FormatException($"Invalid color: {text}");

    /// <summary>Parses CSS hex, RGB, HSL or HSB colors, or an Avalonia named color.</summary>
    public static bool TryParse(string? text, out AlderColorValue value)
    {
        value = default;
        var input = text.AsSpan().Trim();
        if (input.IsEmpty) return false;
        if (input[0] == '#')
        {
            var hex = input[1..];
            if (hex.Length is not (3 or 4 or 6 or 8) ||
                !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint packed))
                return false;

            if (hex.Length is 3 or 4)
            {
                uint expanded = 0;
                for (int shift = (hex.Length - 1) * 4; shift >= 0; shift -= 4)
                    expanded = (expanded << 8) | ((packed >> shift) & 0xF) * 17;
                packed = expanded;
            }

            packed = hex.Length is 4 or 8 ? packed : (packed << 8) | 255;
            value = FromRgb(packed >> 24, (packed >> 16) & 255, (packed >> 8) & 255, (packed & 255) / 255d);
            return true;
        }

        int open = input.IndexOf('(');
        if (open < 0)
        {
            // Avalonia's named-color lookup uses strings, so reuse the input when it needed no trimming.
            var nameText = text?.Length == input.Length ? text : input.ToString();
            if (!Color.TryParse(nameText, out var named)) return false;
            value = FromColor(named);
            return true;
        }
        if (input[^1] != ')') return false;
        var name = input[..open].Trim();
        if (name.Length == 4 && name[3] is 'a' or 'A') name = name[..3];
        var arguments = input[(open + 1)..^1];
        // Leave room for a fifth argument so overflow cannot look like a valid four-argument color.
        Span<Range> parts = stackalloc Range[5];
        int count = arguments.Split(parts, ',', StringSplitOptions.TrimEntries);
        if (count is not (3 or 4)) return false;

        double alpha = 1;
        if (count == 4 && !TryNumber(arguments[parts[3]], 1, 1, out alpha)) return false;

        if (name.Equals("rgb", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNumber(arguments[parts[0]], 255, 255, out double red) ||
                !TryNumber(arguments[parts[1]], 255, 255, out double green) ||
                !TryNumber(arguments[parts[2]], 255, 255, out double blue)) return false;
            value = FromRgb(red, green, blue, alpha);
            return true;
        }

        bool isHsl = name.Equals("hsl", StringComparison.OrdinalIgnoreCase);
        if ((!isHsl && !name.Equals("hsb", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals("hsv", StringComparison.OrdinalIgnoreCase)) ||
            !TryNumber(arguments[parts[0]], 360, 0, out double hue) ||
            !TryNumber(arguments[parts[1]], 1, 1, out double saturation) ||
            !TryNumber(arguments[parts[2]], 1, 1, out double third))
            return false;
        value = isHsl
            ? FromHsl(hue, saturation, third, alpha)
            : FromHsb(hue, saturation, third, alpha);
        return true;
    }

    internal static readonly AlderColorValue Default = FromColor(Color.FromRgb(4, 133, 247));

    internal static string FormatHex(Color color) => color.A == byte.MaxValue
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    internal static double Maximum(ColorChannel channel) => channel switch
    {
        ColorChannel.Hue => 360,
        ColorChannel.Red or ColorChannel.Green or ColorChannel.Blue => 255,
        _ => 100
    };

    internal static ColorSpace SpaceFor(ColorChannel channel, ColorSpace space) => channel switch
    {
        ColorChannel.Red or ColorChannel.Green or ColorChannel.Blue => ColorSpace.Rgb,
        ColorChannel.Lightness => ColorSpace.Hsl,
        ColorChannel.Brightness => ColorSpace.Hsb,
        ColorChannel.Hue or ColorChannel.Saturation when space == ColorSpace.Rgb => ColorSpace.Hsb,
        _ => space
    };

    internal static int Index(ColorSpace space, ColorChannel channel) => channel switch
    {
        ColorChannel.Alpha => 3,
        ColorChannel.Red when space == ColorSpace.Rgb => 0,
        ColorChannel.Green when space == ColorSpace.Rgb => 1,
        ColorChannel.Blue when space == ColorSpace.Rgb => 2,
        ColorChannel.Hue when space != ColorSpace.Rgb => 0,
        ColorChannel.Saturation when space != ColorSpace.Rgb => 1,
        ColorChannel.Lightness when space == ColorSpace.Hsl => 2,
        ColorChannel.Brightness when space == ColorSpace.Hsb => 2,
        _ => -1
    };

    internal double At(int index) => index switch { 0 => First, 1 => Second, 2 => Third, _ => Alpha };

    // The caller converts the color space once before varying one or more of its coordinates.
    internal AlderColorValue With(int index, double value) => index switch
    {
        0 => this with { First = value, Hue = Space == ColorSpace.Rgb ? Hue : value * 360 },
        1 => this with { Second = value },
        2 => this with { Third = value },
        _ => this with { Alpha = value }
    };

    internal AlderColorValue Edit(ColorSpace space, ColorChannel channel, double value)
    {
        if (channel == ColorChannel.Alpha) return With(3, value);
        var edited = InSpace(space).With(Index(space, channel), value);
        return space == ColorSpace.Rgb ? edited.WithRgbHue(Hue) : edited;
    }

    internal AlderColorValue WithPreviousHue(double hue) => Space == ColorSpace.Rgb ? WithRgbHue(hue) : this;

    internal AlderColorValue InSpace(ColorSpace space)
    {
        if (Space == space) return this;

        if (space == ColorSpace.Rgb)
        {
            double saturation = Second, level = Third;
            double chroma = Space == ColorSpace.Hsl
                ? (1 - Math.Abs(2 * level - 1)) * saturation : level * saturation;
            double offset = Space == ColorSpace.Hsl ? level - chroma / 2 : level - chroma;
            double hue = Hue % 360 / 60;
            double middle = chroma * (1 - Math.Abs(hue % 2 - 1));
            (double r, double g, double b) = (int)hue switch
            {
                0 => (chroma, middle, 0d), 1 => (middle, chroma, 0d), 2 => (0d, chroma, middle),
                3 => (0d, middle, chroma), 4 => (middle, 0d, chroma), _ => (chroma, 0d, middle)
            };
            return new(space, Clamp(r + offset), Clamp(g + offset), Clamp(b + offset), Alpha, Hue);
        }

        var hsv = Space switch
        {
            ColorSpace.Rgb => GetHsv(First, Second, Third, Alpha, Hue),
            ColorSpace.Hsl => new HslColor(Alpha, Hue, Second, Third).ToHsv(),
            _ => new HsvColor(Alpha, Hue, Second, Third)
        };
        if (space == ColorSpace.Hsb) return FromHsb(Hue, hsv.S, hsv.V, Alpha);
        var hsl = hsv.ToHsl();
        return FromHsl(Hue, hsl.S, hsl.L, Alpha);
    }

    private AlderColorValue WithRgbHue(double fallback)
    {
        if (First == Second && Second == Third) return this with { Hue = fallback };
        return this with { Hue = GetHsv(First, Second, Third, Alpha, Hue).H };
    }

    private static HsvColor GetHsv(double red, double green, double blue, double alpha, double fallbackHue)
    {
        double maximum = Math.Max(red, Math.Max(green, blue));
        double chroma = maximum - Math.Min(red, Math.Min(green, blue));
        if (chroma == 0) return new HsvColor(alpha, fallbackHue, 0, maximum);

        double sector = maximum == red ? (green - blue) / chroma
            : maximum == green ? (blue - red) / chroma + 2 : (red - green) / chroma + 4;
        double hue = (sector < 0 ? sector + 6 : sector) * 60;
        return new HsvColor(alpha, hue, chroma / maximum, maximum);
    }

    private static bool TryNumber(ReadOnlySpan<char> text, double maximum, double percentOf, out double value)
    {
        bool percent = !text.IsEmpty && text[^1] == '%';
        if (percent) text = text[..^1];
        else if (percentOf == 0 && text.EndsWith("deg", StringComparison.OrdinalIgnoreCase)) text = text[..^3];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value) || (percent && percentOf == 0)) return false;
        if (percent) value = value / 100 * percentOf;
        return value >= 0 && value <= maximum;
    }

    private static double Clamp(double value, double maximum = 1) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, maximum);
    private static byte ToByte(double value) => (byte)Math.Round(value * 255, MidpointRounding.AwayFromZero);
}
