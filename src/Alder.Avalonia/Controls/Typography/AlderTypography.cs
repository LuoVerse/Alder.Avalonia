using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects a heading, body, or code style for typography content.</summary>
public enum TypographyType
{
    /// <summary>Uses the default body text style.</summary>
    Body,
    /// <summary>Uses the level 1 heading style.</summary>
    H1,
    /// <summary>Uses the level 2 heading style.</summary>
    H2,
    /// <summary>Uses the level 3 heading style.</summary>
    H3,
    /// <summary>Uses the level 4 heading style.</summary>
    H4,
    /// <summary>Uses the level 5 heading style.</summary>
    H5,
    /// <summary>Uses the level 6 heading style.</summary>
    H6,
    /// <summary>Uses the smaller body text style.</summary>
    BodySmall,
    /// <summary>Uses the smallest body text style.</summary>
    BodyExtraSmall,
    /// <summary>Uses the inline code style.</summary>
    Code
}
/// <summary>Selects the foreground emphasis of a typography control.</summary>
public enum TypographyColor
{
    /// <summary>Uses the standard text foreground.</summary>
    Default,
    /// <summary>Uses the lower-emphasis text foreground.</summary>
    Muted
}
/// <summary>Selects the body-text scale used by a paragraph.</summary>
public enum TypographyParagraphSize
{
    /// <summary>Uses the default paragraph scale.</summary>
    Base,
    /// <summary>Uses the small paragraph scale.</summary>
    Small,
    /// <summary>Uses the extra-small paragraph scale.</summary>
    ExtraSmall
}

/// <summary>A theme-aware text block with Alder's type scale and native inline content.</summary>
public class AlderTypography : TextBlock
{
    /// <summary>Identifies the <see cref="Type"/> Avalonia property.</summary>
    public static readonly StyledProperty<TypographyType> TypeProperty =
        AvaloniaProperty.Register<AlderTypography, TypographyType>(nameof(Type));
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<TypographyColor> ColorProperty =
        AvaloniaProperty.Register<AlderTypography, TypographyColor>(nameof(Color));
    /// <summary>Identifies the <see cref="Truncate"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> TruncateProperty =
        AvaloniaProperty.Register<AlderTypography, bool>(nameof(Truncate));
    /// <summary>Identifies the <see cref="CodeBackground"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> CodeBackgroundProperty =
        AvaloniaProperty.Register<AlderTypography, IBrush?>(nameof(CodeBackground));
    /// <summary>Identifies the <see cref="CodeCornerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<CornerRadius> CodeCornerRadiusProperty =
        AvaloniaProperty.Register<AlderTypography, CornerRadius>(nameof(CodeCornerRadius), new(6));

    static AlderTypography() => AffectsRender<AlderTypography>(TypeProperty, CodeBackgroundProperty, CodeCornerRadiusProperty);

    /// <summary>The typography style used for this content.</summary>
    public TypographyType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public TypographyColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>Whether overflowing text is trimmed.</summary>
    public bool Truncate { get => GetValue(TruncateProperty); set => SetValue(TruncateProperty, value); }
    /// <summary>The background brush applied to inline code presentation.</summary>
    public IBrush? CodeBackground { get => GetValue(CodeBackgroundProperty); set => SetValue(CodeBackgroundProperty, value); }
    /// <summary>The corner radius of the inline code background.</summary>
    public CornerRadius CodeCornerRadius { get => GetValue(CodeCornerRadiusProperty); set => SetValue(CodeCornerRadiusProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(AlderTypography);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // The last line can paint below its line box when the font has deep descenders.
        return size.WithHeight(size.Height + Math.Max(0, TextLayout.OverhangAfter));
    }

    /// <inheritdoc />
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (Type == TypographyType.Code && CodeBackground is { } background)
            context.DrawRectangle(background, null, new RoundedRect(new Rect(Bounds.Size), CodeCornerRadius));

        base.RenderTextLayout(context, origin);
    }
}

/// <summary>A heading with a level from 1 to 6.</summary>
public sealed class AlderHeading : AlderTypography
{
    /// <summary>Identifies the <see cref="Level"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<AlderHeading, int>(nameof(Level), 1, validate: level => level is >= 1 and <= 6);

    static AlderHeading() => TypeProperty.OverrideDefaultValue<AlderHeading>(TypographyType.H1);

    /// <summary>The heading level used to choose the type scale and accessibility semantics.</summary>
    public int Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelProperty)
            SetCurrentValue(TypeProperty, (TypographyType)Level);
    }
}

/// <summary>Body text with the base, small or extra-small type scale.</summary>
public sealed class AlderParagraph : AlderTypography
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<TypographyParagraphSize> SizeProperty =
        AvaloniaProperty.Register<AlderParagraph, TypographyParagraphSize>(nameof(Size));

    /// <summary>The size of the paragraph text.</summary>
    public TypographyParagraphSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
            SetCurrentValue(TypeProperty, Size switch
            {
                TypographyParagraphSize.Small => TypographyType.BodySmall,
                TypographyParagraphSize.ExtraSmall => TypographyType.BodyExtraSmall,
                _ => TypographyType.Body
            });
    }
}

/// <summary>Monospaced inline code. Use an InlineUIContainer to embed it in surrounding text.</summary>
public sealed class AlderCode : AlderTypography
{
    static AlderCode() => TypeProperty.OverrideDefaultValue<AlderCode>(TypographyType.Code);
}

/// <summary>A vertical layout for rich native content with scoped prose styles.</summary>
public sealed class AlderProse : StackPanel
{
}
