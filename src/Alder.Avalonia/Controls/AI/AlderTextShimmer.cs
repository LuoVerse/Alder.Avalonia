using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
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

    private static readonly TimeSpan SweepDuration = TimeSpan.FromSeconds(2.5);
    private static readonly SplineEasing SweepEase = new(.25, .1, .25, 1);
    private static readonly Color Rest = Color.FromArgb(115, 0, 0, 0);
    private CompositionLinearGradientBrush? band;
    private Visual[]? visibilityAncestors;
    private double? animatedSpread;
    private long startedAt;
    private bool inViewport = true;
    private double zeroWidth;
    private double measuredSize;
    private Typeface measuredTypeface;

    static AlderTextShimmer() => AffectsRender<AlderTextShimmer>(SpreadProperty, AlderMotion.ReducedProperty);

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        inViewport = true;
        EffectiveViewportChanged += OnViewportChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= OnViewportChanged;
        if (visibilityAncestors is not null)
        {
            foreach (var ancestor in visibilityAncestors) ancestor.PropertyChanged -= OnAncestorChanged;
            visibilityAncestors = null;
        }
        StopSweep();
        band = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this)) ||
            (change.Property == SpreadProperty && Spread == 0))
            StopSweep();
        else if (change.Property == IsVisibleProperty)
            UpdateVisibility(IsVisible);
    }

    /// <inheritdoc/>
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        var layout = TextLayout;
        if (AlderMotion.GetReduced(this) || layout.Width <= 0)
        {
            StopSweep();
            base.RenderTextLayout(context, origin);
            return;
        }

        double width = layout.WidthIncludingTrailingWhitespace;
        double spread = Spread == 0 || width <= 0 ? 0 : MeasureZero() * Spread / width;
        if (!inViewport || !IsEffectivelyVisible || !double.IsFinite(spread) || spread <= 0 ||
            ElementComposition.GetElementVisual(this)?.Compositor is not { } compositor)
        {
            StopSweep();
            using (context.PushOpacity(.45)) base.RenderTextLayout(context, origin);
            return;
        }

        if (band is null)
        {
            band = compositor.CreateLinearGradientBrush();
            band.GradientStops = [new GradientStop(Rest, 0), new GradientStop(Colors.Black, .5), new GradientStop(Rest, 1)];
            // Avalonia's effective-visibility event is internal. Observe ancestors only once a sweep exists.
            visibilityAncestors = this.GetVisualAncestors().ToArray();
            foreach (var ancestor in visibilityAncestors) ancestor.PropertyChanged += OnAncestorChanged;
        }
        if (animatedSpread != spread)
        {
            // Resizing a wrap or changing the font keeps the highlight on its existing timeline.
            var delay = animatedSpread is null ? TimeSpan.Zero
                : TimeSpan.FromTicks(-(Stopwatch.GetElapsedTime(startedAt).Ticks % SweepDuration.Ticks));
            if (animatedSpread is null) startedAt = Stopwatch.GetTimestamp();
            animatedSpread = spread;
            StartEndpoint(band, nameof(CompositionLinearGradientBrush.StartPoint), -spread, delay);
            StartEndpoint(band, nameof(CompositionLinearGradientBrush.EndPoint), spread, delay);
        }

        // The recorded mask and glyphs stay in place while the compositor advances the gradient.
        using (context.PushOpacityMask(band, new Rect(origin, new Size(width, layout.Height))))
            base.RenderTextLayout(context, origin);
    }

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        bool visible = new Rect(Bounds.Size).Intersects(e.EffectiveViewport);
        if (visible == inViewport) return;
        inViewport = visible;
        if (!visible) StopSweep();
        InvalidateVisual();
    }

    private void OnAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty) UpdateVisibility(change.GetNewValue<bool>());
    }

    private void UpdateVisibility(bool visible)
    {
        if (!visible) StopSweep();
        else InvalidateVisual();
    }

    private static void StartEndpoint(CompositionLinearGradientBrush brush, string property, double spreadOffset, TimeSpan delay)
    {
        var animation = brush.Compositor.CreateRelativePointKeyFrameAnimation();
        animation.Duration = SweepDuration;
        animation.DelayTime = delay;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.InsertKeyFrame(0, new RelativePoint(-.5 + spreadOffset, .5, RelativeUnit.Relative));
        animation.InsertKeyFrame(1, new RelativePoint(1.5 + spreadOffset, .5, RelativeUnit.Relative), SweepEase);
        brush.StartAnimation(property, animation);
    }

    private void StopSweep()
    {
        if (animatedSpread is null || band is null) return;
        band.StopAnimation(nameof(CompositionLinearGradientBrush.StartPoint));
        band.StopAnimation(nameof(CompositionLinearGradientBrush.EndPoint));
        animatedSpread = null;
    }

    private double MeasureZero()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        if (zeroWidth > 0 && typeface.Equals(measuredTypeface) && FontSize == measuredSize) return zeroWidth;

        using var sample = new TextLayout("0", typeface, FontSize, Brushes.Black);
        measuredTypeface = typeface;
        measuredSize = FontSize;
        return zeroWidth = sample.WidthIncludingTrailingWhitespace;
    }
}
