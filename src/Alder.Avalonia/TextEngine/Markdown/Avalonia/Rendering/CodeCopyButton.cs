using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace Alder.NativeMarkdown.UI.Rendering;

/// <summary>Native button input with the small, directly drawn code header surface.</summary>
internal sealed class CodeCopyButton : Button
{
    private const double GlyphSize = 12;
    private static readonly TimeSpan CopyDuration = TimeSpan.FromMilliseconds(300);
    private static readonly Easing CopyEase = new SplineEasing(.32, .72, 0, 1);
    internal static readonly StyledProperty<Geometry?> CopyIconProperty = AvaloniaProperty.Register<CodeCopyButton, Geometry?>("CopyIcon");
    private static readonly StyledProperty<Geometry?> CheckIconProperty = AvaloniaProperty.Register<CodeCopyButton, Geometry?>("CheckIcon");
    private Glyph? _copyGlyph;
    private Glyph? _checkGlyph;
    private ButtonTint? _hoverTint;
    private CompositionVisual? _pressVisual;
    private IPen? _borderPen;

    public CodeCopyButton()
    {
        Name = "PART_CopyCode";
        this[!ThemeProperty] = new DynamicResourceExtension("AlderCodeCopyTheme");
        this[!global::Avalonia.Automation.AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.AiCopyCode");
        this[!CopyIconProperty] = new DynamicResourceExtension("AlderIconCopy");
    }

    internal bool IsCopied { get; private set; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _hoverTint?.Arrange(rect);
        _copyGlyph?.Arrange(GlyphBounds(finalSize));
        _checkGlyph?.Arrange(GlyphBounds(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, rect);
        if (BorderBrush is { } brush)
        {
            if (!ReferenceEquals(_borderPen?.Brush, brush))
                _borderPen = new Pen(brush, 2);
            context.DrawRectangle(null, _borderPen, rect.Inflate(2), CornerRadius.TopLeft, CornerRadius.TopLeft);
        }
        if (_copyGlyph is null)
            DrawGlyph(context, GlyphBounds(Bounds.Size), GetValue(IsCopied ? CheckIconProperty : CopyIconProperty), Foreground);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateHover();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateHover();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == CopyIconProperty || change.Property == CheckIconProperty)
        {
            InvalidateVisual();
            _copyGlyph?.InvalidateVisual();
            _checkGlyph?.InvalidateVisual();
        }
        else if (change.Property == BorderBrushProperty)
            InvalidateVisual();
        else if (change.Property == IsPressedProperty)
        {
            UpdateHover();
            UpdatePress();
        }
        else if (change.Property == IsPointerOverProperty || change.Property == IsEffectivelyEnabledProperty)
            UpdateHover();
        else if (change.Property == BackgroundProperty && _hoverTint is not null)
            _hoverTint.Background = Background;
        else if (change.Property == AlderMotion.ReducedProperty && _pressVisual is { } visual)
        {
            AlderPressableFeedback.ConfigureSurfaceScale(visual, AlderMotion.GetReduced(this));
            UpdatePress();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_pressVisual is { } visual)
        {
            AlderPressableFeedback.ConfigureSurfaceScale(visual, reducedMotion: true);
            AlderPressableFeedback.UpdateSurfaceScale(visual, 1, 1, reducedMotion: true);
            _pressVisual = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateHover()
    {
        var show = IsEffectivelyEnabled && (IsPointerOver || IsPressed || Classes.Contains(":focus-visible"));
        if (_hoverTint is null && show)
        {
            _hoverTint = new ButtonTint
            {
                Background = Background, CornerRadius = CornerRadius,
                DeferMotion = true, IsHitTestVisible = false, Opacity = 0,
            };
            LogicalChildren.Add(_hoverTint);
            VisualChildren.Insert(0, _hoverTint);
            _hoverTint.Measure(Bounds.Size);
            _hoverTint.Arrange(new Rect(Bounds.Size));
            // Put the glyph above the animated tint once there is a tint to draw.
            EnsureGlyphs(IsCopied);
        }
        if (_hoverTint is { } tint)
            tint.Opacity = show ? 1 : 0;
    }

    private void UpdatePress()
    {
        var reduced = AlderMotion.GetReduced(this);
        if (_pressVisual is null && IsPressed && !reduced)
        {
            _pressVisual = ElementComposition.GetElementVisual(this);
            if (_pressVisual is not null)
                AlderPressableFeedback.ConfigureSurfaceScale(_pressVisual, reduced);
        }
        if (_pressVisual is { } visual)
        {
            visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
            AlderPressableFeedback.UpdateSurfaceScale(visual, IsPressed ? .98 : 1, 1, reduced);
        }
    }

    internal void SetCopied(bool copied, bool animate)
    {
        if (IsCopied == copied) return;
        IsCopied = copied;
        if (copied && !IsSet(CheckIconProperty))
            this[!CheckIconProperty] = new DynamicResourceExtension("AlderIconCheck");
        var playMotion = animate && !AlderMotion.GetReduced(this);
        if (_copyGlyph is null)
        {
            InvalidateVisual();
            if (!playMotion) return;
            EnsureGlyphs(!copied);
        }
        var incoming = (copied ? _checkGlyph : _copyGlyph)!;
        var outgoing = (copied ? _copyGlyph : _checkGlyph)!;
        if (playMotion)
        {
            var pivot = new Rect(incoming.Bounds.Size).Center;
            OverlayMotion.Animate(outgoing, false, default, .55, CopyDuration, TimeSpan.FromMilliseconds(130), pivot, CopyEase);
            OverlayMotion.Animate(incoming, true, default, .65, CopyDuration, CopyDuration, pivot, CopyEase);
        }
        else
        {
            OverlayMotion.Reset(incoming);
            OverlayMotion.Reset(outgoing);
            incoming.Opacity = 1;
            outgoing.Opacity = 0;
            if (ElementComposition.GetElementVisual(outgoing) is { } visual)
                visual.Opacity = 0;
        }
    }

    private void EnsureGlyphs(bool showCheck)
    {
        if (_copyGlyph is not null) return;
        _copyGlyph = AddGlyph(CopyIconProperty, showCheck ? 0 : 1);
        _checkGlyph = AddGlyph(CheckIconProperty, showCheck ? 1 : 0);
        InvalidateVisual();
    }

    private Glyph AddGlyph(StyledProperty<Geometry?> property, double opacity)
    {
        var glyph = new Glyph(this, property) { Opacity = opacity, IsHitTestVisible = false };
        VisualChildren.Add(glyph);
        glyph.Measure(new Size(GlyphSize, GlyphSize));
        glyph.Arrange(GlyphBounds(Bounds.Size));
        return glyph;
    }

    private static Rect GlyphBounds(Size size) => new(
        (size.Width - GlyphSize) / 2, (size.Height - GlyphSize) / 2, GlyphSize, GlyphSize);

    private static void DrawGlyph(DrawingContext context, Rect target, Geometry? geometry, IBrush? foreground)
    {
        if (geometry?.Bounds is not { Width: > 0, Height: > 0 } bounds)
            return;
        var scale = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-(Vector)bounds.Position) *
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Vector)target.Position));
        context.DrawGeometry(foreground, null, geometry);
    }

    private sealed class Glyph(CodeCopyButton owner, StyledProperty<Geometry?> property) : Control
    {
        public override void Render(DrawingContext context) =>
            DrawGlyph(context, new Rect(Bounds.Size), owner.GetValue(property), owner.Foreground);
    }
}
