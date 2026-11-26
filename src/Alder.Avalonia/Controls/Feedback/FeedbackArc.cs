using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;

namespace Alder.Avalonia.Controls;

internal sealed class FeedbackArc : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<FeedbackArc, double>(nameof(Fraction), 1);
    public static readonly StyledProperty<double> StrokeThicknessProperty = AlderProgressCircle.StrokeThicknessProperty.AddOwner<FeedbackArc>();
    public static readonly StyledProperty<double> TrackStrokeThicknessProperty = AlderProgressCircle.TrackStrokeThicknessProperty.AddOwner<FeedbackArc>();
    public static readonly StyledProperty<PenLineCap> StrokeLineCapProperty = AlderProgressCircle.StrokeLineCapProperty.AddOwner<FeedbackArc>();
    public static readonly StyledProperty<bool> IsTrackProperty = AvaloniaProperty.Register<FeedbackArc, bool>(nameof(IsTrack));
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<FeedbackArc, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<FeedbackArc, bool>(nameof(IsIndeterminate));

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double TrackStrokeThickness { get => GetValue(TrackStrokeThicknessProperty); set => SetValue(TrackStrokeThicknessProperty, value); }
    public PenLineCap StrokeLineCap { get => GetValue(StrokeLineCapProperty); set => SetValue(StrokeLineCapProperty, value); }
    public bool IsTrack { get => GetValue(IsTrackProperty); set => SetValue(IsTrackProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    private CompositionVisual? visual;
    private bool spinning;
    private bool withinViewport = true;
    private (Size Size, double Radius, double Fraction) geometryKey;
    private StreamGeometry? arc;

    static FeedbackArc() => AffectsRender<FeedbackArc>(FractionProperty, StrokeThicknessProperty, TrackStrokeThicknessProperty,
        StrokeLineCapProperty, IsTrackProperty, StrokeProperty, IsIndeterminateProperty);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        visual = ElementComposition.GetElementVisual(this);
        withinViewport = true;
        EffectiveViewportChanged += ViewportChanged;
        UpdateRotation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= ViewportChanged;
        StopSpin();
        visual = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        withinViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
        UpdateRotation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty || change.Property == IsVisibleProperty ||
            change.Property == IsIndeterminateProperty || change.Property == AlderMotion.ReducedProperty)
            UpdateRotation();
    }

    private void UpdateRotation()
    {
        if (visual is null) return;
        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
        if (!IsIndeterminate || !IsEffectivelyVisible || !withinViewport || AlderMotion.GetReduced(this))
        {
            StopSpin();
            return;
        }
        if (spinning) return;
        FeedbackMotion.Spin(visual, FeedbackMotion.RingSpinDuration);
        spinning = true;
    }

    private void StopSpin()
    {
        if (!spinning || visual is null) return;
        FeedbackMotion.StopSpin(visual);
        spinning = false;
    }

    public override void Render(DrawingContext context)
    {
        double diameter = Math.Min(Bounds.Width, Bounds.Height);
        double fraction = IsIndeterminate ? .25 : Math.Clamp(Fraction, 0, 1);
        if (Stroke is not { } stroke || diameter <= 0 || fraction <= 0) return;
        double fillThickness = double.IsNaN(StrokeThickness) ? diameter / 9 : Math.Min(StrokeThickness, diameter);
        double trackThickness = double.IsNaN(TrackStrokeThickness) ? fillThickness : Math.Min(TrackStrokeThickness, diameter);
        double radius = (diameter - Math.Max(fillThickness, trackThickness)) / 2;
        double thickness = IsTrack ? trackThickness : fillThickness;
        var center = new Rect(Bounds.Size).Center;
        var pen = new ImmutablePen(stroke.ToImmutable(), thickness, lineCap: StrokeLineCap);
        if (fraction >= 1 || radius <= 0)
        {
            context.DrawEllipse(null, pen, center, Math.Max(.01, radius), Math.Max(.01, radius));
            return;
        }

        var key = (Bounds.Size, radius, fraction);
        if (arc is null || key != geometryKey)
        {
            double angle = Math.Tau * fraction;
            arc = new StreamGeometry();
            using var path = arc.Open();
            path.BeginFigure(center + new Vector(0, -radius), false);
            path.ArcTo(center + new Vector(Math.Sin(angle) * radius, -Math.Cos(angle) * radius),
                new Size(radius, radius), 0, fraction > .5, SweepDirection.Clockwise);
            path.EndFigure(false);
            geometryKey = key;
        }
        context.DrawGeometry(null, pen, arc);
    }
}
