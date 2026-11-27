using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal enum AnchoredSurfaceMotion { Default, Autocomplete, ListBox, InlineSelect }

internal sealed class AnchoredSurface : Panel
{
    // Keep the full shadow inside the popup host. Placement compensates for this margin to align the visible frame.
    internal static readonly Thickness ShadowMargin = new(20, 18, 20, 30);

    private static readonly SplineEasing Ease = new(.25, .1, .25, 1);
    private static readonly SplineEasing AutocompleteEnterEase = new(.32, .72, 0, 1);
    private static readonly SplineEasing AutocompleteExitEase = new(.25, .46, .45, .94);
    private readonly Border frame;
    private Border? backdrop;
    private ArrowGlyph? arrow;
    private Point? lastAnchor;

    public AnchoredSurface(bool tooltip)
    {
        Name = tooltip ? "AlderToolTipSurface" : "AlderPopoverSurface";
        frame = new Border
        {
            CornerRadius = new CornerRadius(tooltip ? 12 : 24),
            Padding = new Thickness(tooltip ? 8 : 16)
        };
        Children.Add(frame);
        Margin = ShadowMargin;
        MaxWidth = tooltip ? 320 : 360;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        frame[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderSurface");
        frame[!Border.BoxShadowProperty] = new DynamicResourceExtension("AlderToolbarShadow");
    }

    public Control? Child { get => frame.Child; set => frame.Child = value; }

    public bool ShowArrow
    {
        get => arrow?.IsVisible == true;
        set
        {
            if (ShowArrow == value) return;
            lastAnchor = null;
            if (arrow is null)
            {
                arrow = new ArrowGlyph { IsHitTestVisible = false };
                arrow.Bind(ArrowGlyph.FillProperty, frame.GetBindingObservable(Border.BackgroundProperty));
                Children.Add(arrow);
            }
            arrow.IsVisible = value;
        }
    }

    public void ModifyAppearance(IBrush? background, CornerRadius radius, Thickness padding,
        IBrush? borderBrush, Thickness borderThickness, BoxShadows? shadow, double blurRadius = 0)
    {
        frame.CornerRadius = radius;
        frame.Padding = padding;
        frame.BorderBrush = borderBrush;
        frame.BorderThickness = borderThickness;
        if (background is null)
            frame[!Border.BackgroundProperty] = new DynamicResourceExtension(blurRadius > 0 ? "AlderPopoverBackdropTint" : "AlderSurface");
        else frame.Background = background;
        if (shadow is { } custom) frame.BoxShadow = custom;
        else frame[!Border.BoxShadowProperty] = new DynamicResourceExtension("AlderToolbarShadow");
        UpdateBackdrop(radius, blurRadius);
    }

    private void UpdateBackdrop(CornerRadius radius, double blurRadius)
    {
        if (blurRadius == 0)
        {
            if (backdrop is not null) Children.Remove(backdrop);
            backdrop = null;
            return;
        }

        if (backdrop is null)
        {
            // Clip only the sampling layer. The frame still draws its shadow outside the radius.
            backdrop = new Border { ClipToBounds = true, IsHitTestVisible = false, BackdropEffect = new BlurEffect() };
            Children.Insert(0, backdrop);
        }

        backdrop.CornerRadius = radius;
        ((BlurEffect)backdrop.BackdropEffect!).Radius = blurRadius;
    }

    public (Point Pivot, Vector3D Offset) Position(Control anchor, PlacementMode placement, Rect? anchorRect = null)
    {
        var center = (anchorRect ?? new Rect(anchor.Bounds.Size)).Center;
        var translated = anchor.TranslatePoint(center, this) ?? this.PointToClient(anchor.PointToScreen(center));
        var width = Bounds.Width;
        var height = Bounds.Height;
        var displacement = translated - new Rect(Bounds.Size).Center;
        bool vertical = placement switch
        {
            PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight or
                PlacementMode.Bottom or PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight => true,
            PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom or
                PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => false,
            _ => Math.Abs(displacement.Y) / Math.Max(1, height) >= Math.Abs(displacement.X) / Math.Max(1, width)
        };
        double x = vertical ? ClampArrow(translated.X, width) : displacement.X < 0 ? 0 : width;
        double y = vertical ? displacement.Y < 0 ? 0 : height : ClampArrow(translated.Y, height);
        var pivot = new Point(x, y);
        var direction = vertical ? new Vector(0, y == 0 ? -1 : 1) : new Vector(x == 0 ? -1 : 1, 0);
        var tip = pivot + direction * 6;
        if (arrow is { IsVisible: true } glyph && lastAnchor != tip)
        {
            lastAnchor = tip;
            var tangent = vertical ? new Vector(6, 0) : new Vector(0, 6);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                var edge = pivot - direction * .5;
                path.BeginFigure(edge - tangent, true);
                path.CubicBezierTo(edge + direction * 8, edge + direction * 8, edge + tangent);
                path.EndFigure(true);
            }
            glyph.SetGeometry(geometry);
        }
        return (pivot, new Vector3D(direction.X * 4, direction.Y * 4, 0));
    }

    public static void SetOffsets(Popup popup, PlacementMode placement, double gap, double horizontal = 0, double vertical = 0)
    {
        popup.HorizontalOffset = horizontal + (placement switch
        {
            PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => -gap,
            PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => gap,
            _ => 0
        });
        popup.VerticalOffset = vertical + (placement switch
        {
            PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => -gap,
            PlacementMode.Bottom or PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight => gap,
            _ => 0
        });
    }

    public void Play(Control anchor, PlacementMode placement, bool entering,
        AnchoredSurfaceMotion motion = AnchoredSurfaceMotion.Default, Rect? anchorRect = null)
    {
        var (pivot, offset) = Position(anchor, placement, anchorRect);
        if (motion == AnchoredSurfaceMotion.InlineSelect)
        {
            // The aligned edge stays attached to the trigger while the menu scales.
            static double AlignedEdge(double position, double length) =>
                Math.Abs(position - length / 2) <= 2 ? length / 2 : position < length / 2 ? 0 : length;

            pivot = offset.X == 0
                ? new Point(AlignedEdge(pivot.X, Bounds.Width), pivot.Y)
                : new Point(pivot.X, AlignedEdge(pivot.Y, Bounds.Height));
        }
        int enterMilliseconds = motion == AnchoredSurfaceMotion.Autocomplete ? 250 : 150;
        var duration = TimeSpan.FromMilliseconds(entering ? enterMilliseconds : 100);
        var easing = motion == AnchoredSurfaceMotion.Autocomplete ? entering ? AutocompleteEnterEase : AutocompleteExitEase : Ease;
        double scale = entering && motion == AnchoredSurfaceMotion.Default ? .9 : .95;
        OverlayMotion.Animate(this, entering, entering ? offset : default, scale, duration, duration, pivot, easing);
    }

    private sealed class ArrowGlyph : Control
    {
        public static readonly StyledProperty<IBrush?> FillProperty =
            Border.BackgroundProperty.AddOwner<ArrowGlyph>();
        private Geometry? geometry;

        static ArrowGlyph() => AffectsRender<ArrowGlyph>(FillProperty);

        public void SetGeometry(Geometry value)
        {
            geometry = value;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            // Paint the arrow over the frame's shadow so it keeps the same fill as the frame.
            if (geometry is not null && GetValue(FillProperty) is { } fill)
                context.DrawGeometry(fill, null, geometry);
        }
    }

    private static double ClampArrow(double value, double size)
    {
        double margin = Math.Min(18, size / 2);
        return Math.Clamp(value, margin, size - margin);
    }
}
