using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Controls when the link text shows an underline.</summary>
public enum AlderLinkUnderline
{
    /// <summary>Underlines the link when it is hovered.</summary>
    Hover,
    /// <summary>Keeps the link text underlined.</summary>
    Always,
    /// <summary>Suppresses the link underline.</summary>
    None
}
/// <summary>Places the link icon at the logical start or end of its content.</summary>
public enum AlderLinkIconPlacement
{
    /// <summary>Places the icon before the link text in reading order.</summary>
    Start,
    /// <summary>Places the icon after the link text in reading order.</summary>
    End
}

/// <summary>A hyperlink with native URI launching, command support and an optional icon.</summary>
public sealed class AlderLink : HyperlinkButton
{
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderLink, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="ShowExternalIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowExternalIconProperty =
        AvaloniaProperty.Register<AlderLink, bool>(nameof(ShowExternalIcon));
    /// <summary>Identifies the <see cref="IconPlacement"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderLinkIconPlacement> IconPlacementProperty =
        AvaloniaProperty.Register<AlderLink, AlderLinkIconPlacement>(nameof(IconPlacement), AlderLinkIconPlacement.End);
    /// <summary>Identifies the <see cref="Underline"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderLinkUnderline> UnderlineProperty =
        AvaloniaProperty.Register<AlderLink, AlderLinkUnderline>(nameof(Underline));
    /// <summary>Identifies the <see cref="UnderlineBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<AlderLink, IBrush?>(nameof(UnderlineBrush));
    /// <summary>Identifies the <see cref="UnderlineOffset"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> UnderlineOffsetProperty =
        AvaloniaProperty.Register<AlderLink, double>(nameof(UnderlineOffset), 3, validate: NonnegativeFinite);
    /// <summary>Identifies the <see cref="UnderlineThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> UnderlineThicknessProperty =
        AvaloniaProperty.Register<AlderLink, double>(nameof(UnderlineThickness), 1.5, validate: NonnegativeFinite);

    /// <summary>An optional geometry or icon control displayed with the content.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Whether the link displays the built-in external navigation icon.</summary>
    public bool ShowExternalIcon { get => GetValue(ShowExternalIconProperty); set => SetValue(ShowExternalIconProperty, value); }
    /// <summary>Where the icon is placed relative to the link text.</summary>
    public AlderLinkIconPlacement IconPlacement { get => GetValue(IconPlacementProperty); set => SetValue(IconPlacementProperty, value); }
    /// <summary>When the underline is visible.</summary>
    public AlderLinkUnderline Underline { get => GetValue(UnderlineProperty); set => SetValue(UnderlineProperty, value); }
    /// <summary>The brush used to draw the underline.</summary>
    public IBrush? UnderlineBrush { get => GetValue(UnderlineBrushProperty); set => SetValue(UnderlineBrushProperty, value); }
    /// <summary>The offset of the underline from its normal position.</summary>
    public double UnderlineOffset { get => GetValue(UnderlineOffsetProperty); set => SetValue(UnderlineOffsetProperty, value); }
    /// <summary>The thickness of the underline.</summary>
    public double UnderlineThickness { get => GetValue(UnderlineThicknessProperty); set => SetValue(UnderlineThicknessProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    private static bool NonnegativeFinite(double value) => double.IsFinite(value) && value >= 0;

    private sealed class LinkPeer(AlderLink owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}
