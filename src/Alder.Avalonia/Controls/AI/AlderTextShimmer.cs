using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Sweeps a highlight in the current foreground color across a readable label.</summary>
public sealed class AlderTextShimmer : TextBlock
{
    /// <summary>Identifies the <see cref="Spread"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SpreadProperty =
        AvaloniaProperty.Register<AlderTextShimmer, double>(nameof(Spread), 4, validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>The falloff on either side of the highlight, in widths of the character zero.</summary>
    public double Spread { get => GetValue(SpreadProperty); set => SetValue(SpreadProperty, value); }

    private static readonly SplineEasing SweepEase = new(.25, .1, .25, 1);
    private static readonly Color Rest = Color.FromArgb(115, 0, 0, 0);
    private readonly LinearGradientBrush band = new()
    {
        GradientStops = [new GradientStop(Rest, 0), new GradientStop(Colors.Black, .5), new GradientStop(Rest, 1)]
    };
    private readonly Action<TimeSpan> tick;
    private bool pending;
    private bool inViewport = true;
    private TimeSpan? started;
    private double position;
    private double zeroWidth;
    private double measuredSize;
    private Typeface measuredTypeface;

    static AlderTextShimmer() => AffectsRender<AlderTextShimmer>(SpreadProperty, AlderMotion.ReducedProperty);

    /// <summary>Creates a label with an animated foreground highlight.</summary>
    public AlderTextShimmer()
    {
        tick = Advance;
        EffectiveViewportChanged += (_, e) =>
        {
            bool visible = new Rect(Bounds.Size).Intersects(e.EffectiveViewport);
            if (inViewport == visible) return;
            inViewport = visible;
            InvalidateVisual();
        };
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        pending = false;
        started = null;
        position = 0;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlderMotion.ReducedProperty || change.Property == IsVisibleProperty)
        {
            started = null;
            position = 0;
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var layout = TextLayout;
        if (AlderMotion.GetReduced(this) || layout.Width <= 0)
        {
            base.RenderTextLayout(context, origin);
            return;
        }

        double width = layout.WidthIncludingTrailingWhitespace;
        double spread = Spread == 0 ? 0 : MeasureZero() * Spread / width;
        if (spread == 0 || position is <= 0 or >= 1)
        {
            using (context.PushOpacity(.45)) base.RenderTextLayout(context, origin);
        }
        else
        {
            // Move the falloff across the cached glyph layout, leaving the background unmasked.
            double center = -.5 + 2 * position;
            band.StartPoint = new RelativePoint(center - spread, .5, RelativeUnit.Relative);
            band.EndPoint = new RelativePoint(center + spread, .5, RelativeUnit.Relative);
            using (context.PushOpacityMask(band, new Rect(origin, new Size(width, layout.Height))))
                base.RenderTextLayout(context, origin);
        }

        if (spread > 0 && !pending && CanAnimate && TopLevel.GetTopLevel(this) is { } root)
        {
            pending = true;
            root.RequestAnimationFrame(tick);
        }
    }

    private bool CanAnimate => inViewport && IsEffectivelyVisible && !AlderMotion.GetReduced(this);

    private double MeasureZero()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        if (zeroWidth > 0 && typeface.Equals(measuredTypeface) && FontSize == measuredSize) return zeroWidth;

        using var sample = new TextLayout("0", typeface, FontSize, Brushes.Black);
        measuredTypeface = typeface;
        measuredSize = FontSize;
        return zeroWidth = sample.WidthIncludingTrailingWhitespace;
    }

    private void Advance(TimeSpan time)
    {
        pending = false;
        if (!CanAnimate || !this.IsAttachedToVisualTree())
        {
            started = null;
            position = 0;
            return;
        }
        started ??= time;
        position = SweepEase.Ease((time - started.Value).TotalSeconds % 2.5 / 2.5);
        InvalidateVisual();
    }
}
