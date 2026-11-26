using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Draws one contour for the selected header and its panel.</summary>
internal sealed class AlderMorphingTabSurface : Control
{
    public static readonly StyledProperty<IBrush?> RailBrushProperty =
        AvaloniaProperty.Register<AlderMorphingTabSurface, IBrush?>(nameof(RailBrush));
    public static readonly StyledProperty<IBrush?> PanelBrushProperty =
        AvaloniaProperty.Register<AlderMorphingTabSurface, IBrush?>(nameof(PanelBrush));

    private const double RailCorner = 32;
    private const double PanelCorner = 28;
    private const double PanelInset = 16;
    private const double TabCorner = 24;
    private const double Shoulder = 24;
    private const double ArcControl = .55228475;
    private readonly AlderTabs.HeaderSpring leftEdge = new();
    private readonly AlderTabs.HeaderSpring rightEdge = new();
    private readonly AlderTabs.HeaderSpring topEdge = new();
    private Rect? target;
    private double railHeight;
    private bool framePending;
    private StreamGeometry? contour;

    private bool IsMoving => leftEdge.IsMoving || rightEdge.IsMoving || topEdge.IsMoving;

    static AlderMorphingTabSurface() =>
        AffectsRender<AlderMorphingTabSurface>(RailBrushProperty, PanelBrushProperty);

    public IBrush? RailBrush { get => GetValue(RailBrushProperty); set => SetValue(RailBrushProperty, value); }
    public IBrush? PanelBrush { get => GetValue(PanelBrushProperty); set => SetValue(PanelBrushProperty, value); }

    internal void Update(double height, Rect? selected, bool animate)
    {
        bool geometryChanged = railHeight != height || target != selected;
        if (!geometryChanged && (animate || !IsMoving)) return;

        bool hadHeader = target is not null || IsMoving;
        railHeight = height;
        target = selected;
        double center = (leftEdge.Offset + rightEdge.Offset) / 2;
        var end = selected ?? new Rect(center, height, 0, 0);
        if (animate && hadHeader && this.IsAttachedToVisualTree())
        {
            // Retarget the moving edges without losing their existing velocity.
            long time = Stopwatch.GetTimestamp();
            leftEdge.MoveTo(end.X, time);
            rightEdge.MoveTo(end.Right, time);
            topEdge.MoveTo(end.Y, time);
            RequestFrame();
        }
        else
            JumpToTarget();
        InvalidateContour();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        double top = Math.Clamp(railHeight, 0, size.Height);
        var bounds = new Rect(size);
        context.DrawRectangle(RailBrush, null, bounds, RailCorner, RailCorner);
        if (PanelBrush is not null && size.Height > top)
        {
            using var clip = context.PushClip(new RoundedRect(bounds, RailCorner));
            var header = target is not null || IsMoving
                ? new Rect(leftEdge.Offset, topEdge.Offset, Math.Max(0, rightEdge.Offset - leftEdge.Offset), 0)
                : (Rect?)null;
            contour ??= CreateContour(size, top, header);
            context.DrawGeometry(PanelBrush, null, contour);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateContour();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        framePending = false;
        JumpToTarget();
        contour = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void RequestFrame()
    {
        if (framePending || !IsMoving || TopLevel.GetTopLevel(this) is not { } root) return;
        framePending = true;
        root.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        framePending = false;
        if (!IsMoving) return;
        long time = Stopwatch.GetTimestamp();
        leftEdge.Advance(time);
        rightEdge.Advance(time);
        topEdge.Advance(time);
        InvalidateContour();
        RequestFrame();
    }

    private void JumpToTarget()
    {
        leftEdge.Jump(target?.X ?? 0);
        rightEdge.Jump(target?.Right ?? 0);
        topEdge.Jump(target?.Y ?? railHeight);
    }

    private void InvalidateContour()
    {
        contour = null;
        InvalidateVisual();
    }

    private static StreamGeometry CreateContour(Size size, double rail, Rect? selected)
    {
        double panelLeft = Math.Min(PanelInset, size.Width / 4);
        double panelRight = size.Width - panelLeft;
        double bottom = size.Height;
        double radius = Math.Min(PanelCorner, Math.Min(panelRight - panelLeft, bottom - rail) / 2);
        double left = Math.Clamp(selected?.X ?? panelLeft, panelLeft, panelRight);
        double right = Math.Clamp(selected?.Right ?? left, left, panelRight);
        double top = Math.Clamp(selected?.Y ?? rail, 0, rail);
        double rise = rail - top;
        bool hasHeader = right - left > 1 && rise > 1;
        double shoulderLimit = Math.Min(Shoulder, rise / 2);
        double shoulderShare = shoulderLimit / (shoulderLimit + radius);
        double leftShoulder = Math.Min(shoulderLimit, (left - panelLeft) * shoulderShare);
        double rightShoulder = Math.Min(shoulderLimit, (panelRight - right) * shoulderShare);
        double leftJoin = left - leftShoulder;
        double rightJoin = right + rightShoulder;
        // Near an edge, the panel corner and the shoulder share the remaining width.
        double panelLeftRadius = hasHeader ? Math.Min(radius, leftJoin - panelLeft) : radius;
        double panelRightRadius = hasHeader ? Math.Min(radius, panelRight - rightJoin) : radius;

        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(new Point(panelLeft, rail + panelLeftRadius), true);
        path.QuadraticBezierTo(new Point(panelLeft, rail), new Point(panelLeft + panelLeftRadius, rail));
        if (hasHeader)
        {
            double cap = Math.Min(TabCorner, Math.Min((right - left) / 2, rise - Math.Max(leftShoulder, rightShoulder)));
            path.LineTo(new Point(leftJoin, rail));
            path.CubicBezierTo(new Point(leftJoin + leftShoulder * ArcControl, rail),
                new Point(left, rail - leftShoulder * (1 - ArcControl)), new Point(left, rail - leftShoulder));
            path.LineTo(new Point(left, top + cap));
            path.CubicBezierTo(new Point(left, top + cap * (1 - ArcControl)),
                new Point(left + cap * (1 - ArcControl), top), new Point(left + cap, top));
            path.LineTo(new Point(right - cap, top));
            path.CubicBezierTo(new Point(right - cap * (1 - ArcControl), top),
                new Point(right, top + cap * (1 - ArcControl)), new Point(right, top + cap));
            path.LineTo(new Point(right, rail - rightShoulder));
            path.CubicBezierTo(new Point(right, rail - rightShoulder * (1 - ArcControl)),
                new Point(rightJoin - rightShoulder * ArcControl, rail), new Point(rightJoin, rail));
        }
        path.LineTo(new Point(panelRight - panelRightRadius, rail));
        path.QuadraticBezierTo(new Point(panelRight, rail), new Point(panelRight, rail + panelRightRadius));
        path.LineTo(new Point(panelRight, bottom - radius));
        var corner = new Size(radius, radius);
        path.ArcTo(new Point(panelRight - radius, bottom), corner, 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(panelLeft + radius, bottom));
        path.ArcTo(new Point(panelLeft, bottom - radius), corner, 0, false, SweepDirection.Clockwise);
        path.EndFigure(true);
        return geometry;
    }
}
