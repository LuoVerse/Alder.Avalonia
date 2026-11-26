using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the semantic line treatment for a separator.</summary>
public enum SeparatorVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary,
    /// <summary>Uses the tertiary treatment.</summary>
    Tertiary
}

/// <summary>A horizontal or vertical divider with optional decorative semantics.</summary>
public sealed class AlderSeparator : Control
{
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderSeparator, Orientation>(nameof(Orientation), Orientation.Horizontal);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<SeparatorVariant> VariantProperty = AvaloniaProperty.Register<AlderSeparator, SeparatorVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="IsDecorative"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDecorativeProperty = AvaloniaProperty.Register<AlderSeparator, bool>(nameof(IsDecorative));
    /// <summary>Identifies the <see cref="Thickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<AlderSeparator, double>(nameof(Thickness), 1, validate: x => double.IsFinite(x) && x > 0);
    /// <summary>Identifies the <see cref="Background"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<AlderSeparator>();

    static AlderSeparator()
    {
        AffectsMeasure<AlderSeparator>(OrientationProperty, ThicknessProperty);
        AffectsRender<AlderSeparator>(OrientationProperty, ThicknessProperty, BackgroundProperty);
    }

    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public SeparatorVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether this line is purely decorative for accessibility.</summary>
    public bool IsDecorative { get => GetValue(IsDecorativeProperty); set => SetValue(IsDecorativeProperty, value); }
    /// <summary>The line thickness in device-independent pixels.</summary>
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    /// <summary>The brush painted behind the content.</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) =>
        Orientation == Orientation.Horizontal ? new Size(0, Thickness) : new Size(Thickness, 8);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Background is not { } brush || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var horizontal = Orientation == Orientation.Horizontal;
        var thickness = Math.Min(Thickness, horizontal ? Bounds.Height : Bounds.Width);
        var rect = horizontal
            ? new Rect(0, (Bounds.Height - thickness) / 2, Bounds.Width, thickness)
            : new Rect((Bounds.Width - thickness) / 2, 0, thickness, Bounds.Height);
        context.DrawRectangle(brush, null, new RoundedRect(rect, thickness / 2));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SeparatorPeer(this);

    private sealed class SeparatorPeer(AlderSeparator owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;
    }
}
