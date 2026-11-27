using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

// Draw at the animated size so the corner radii, border and shadow keep their DIP sizes.
internal sealed class AlderBloomMenuChrome : Decorator
{
    private CompositionCustomVisual? chrome;
    private Size capacity;

    internal CompositionVisual? GetChromeVisual(Size maximumSize)
    {
        if (chrome is null)
        {
            if (Child is not Border border || ElementComposition.GetElementVisual(this) is not { Compositor: var compositor })
                return null;

            chrome = compositor.CreateCustomVisual(new ChromeRenderer());
            ElementComposition.SetElementChildVisual(this, chrome);
            border.PropertyChanged += OnPaintChanged;
            UpdateChrome(border);
        }
        if (capacity != maximumSize)
        {
            capacity = maximumSize;
            chrome.SendHandlerMessage(capacity);
        }
        return chrome;
    }

    internal void ShowAnimatedChrome(bool animated)
    {
        if (chrome is null || Child is not Border border) return;
        chrome.Visible = animated;
        border.Opacity = animated ? 0 : 1;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ShowAnimatedChrome(false);
        if (Child is Border border) border.PropertyChanged -= OnPaintChanged;
        ElementComposition.SetElementChildVisual(this, null);
        chrome = null;
        capacity = default;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPaintChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (sender is Border border && (change.Property == Border.BackgroundProperty || change.Property == Border.BorderBrushProperty ||
            change.Property == Border.BorderThicknessProperty || change.Property == Border.CornerRadiusProperty ||
            change.Property == Border.BoxShadowProperty))
            UpdateChrome(border);
    }

    private void UpdateChrome(Border border) => chrome?.SendHandlerMessage(new Paint(border.Background?.ToImmutable(),
        border.BorderBrush?.ToImmutable(), border.BorderThickness, border.CornerRadius, border.BoxShadow));

    private sealed record Paint(IImmutableBrush? Fill, IImmutableBrush? Border, Thickness Stroke, CornerRadius Radius, BoxShadows Shadow);

    private sealed class ChromeRenderer : CompositionCustomVisualHandler
    {
        private Paint? paint;
        private ImmutablePen? pen;
        private Size renderSize;

        public override void OnMessage(object message)
        {
            if (message is Size maximumSize) renderSize = maximumSize;
            else if (message is Paint updated)
            {
                paint = updated;
                pen = updated.Stroke.IsUniform && updated.Stroke.Top > 0 && updated.Border is { } stroke
                    ? new ImmutablePen(stroke, updated.Stroke.Top) : null;
            }
            Invalidate();
        }

        // The compositor caches content bounds, so declare the whole animation's
        // paint area instead of only the first (collapsed) frame.
        public override Rect GetRenderBounds() => new(renderSize);

        public override void OnRender(ImmediateDrawingContext context)
        {
            if (paint is not { } style) return;
            var bounds = new Rect(new Size(EffectiveSize.X, EffectiveSize.Y)).Deflate(AlderBloomMenuSurface.ShadowGutter);
            var radius = style.Radius;
            if (radius.IsUniform && style.Stroke.IsUniform)
            {
                context.DrawRectangle(style.Fill, pen, bounds.Deflate(style.Stroke.Top / 2),
                    radius.TopLeft, radius.TopLeft, style.Shadow);
                return;
            }

            // ImmediateDrawingContext only takes a uniform radius for a painted box.
            // Clip the uncommon asymmetric outline explicitly during the resize.
            using (context.PushClip(new RoundedRect(bounds, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft)))
            {
                if (style.Border is not null) context.FillRectangle(style.Border, bounds);
                var inner = bounds.Deflate(style.Stroke);
                if (style.Fill is not null)
                    using (context.PushClip(new RoundedRect(inner,
                        Math.Max(0, radius.TopLeft - Math.Max(style.Stroke.Top, style.Stroke.Left)),
                        Math.Max(0, radius.TopRight - Math.Max(style.Stroke.Top, style.Stroke.Right)),
                        Math.Max(0, radius.BottomRight - Math.Max(style.Stroke.Bottom, style.Stroke.Right)),
                        Math.Max(0, radius.BottomLeft - Math.Max(style.Stroke.Bottom, style.Stroke.Left)))))
                        context.FillRectangle(style.Fill, inner);
            }
        }
    }
}
