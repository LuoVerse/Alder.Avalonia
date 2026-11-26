using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Draws a continuous track beneath the native slider's input parts.</summary>
public sealed class SliderSurface : Control
{
    internal const double Thickness = 20;
    internal const double ThumbLength = 28;

    /// <summary>Identifies the <see cref="Minimum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<SliderSurface, double>(nameof(Minimum));
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<SliderSurface, double>(nameof(Maximum), 100);
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<SliderSurface, double>(nameof(Value));
    /// <summary>Identifies the <see cref="LowerValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<double?> LowerValueProperty = AvaloniaProperty.Register<SliderSurface, double?>(nameof(LowerValue));
    /// <summary>Identifies the <see cref="TickFrequency"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> TickFrequencyProperty = AvaloniaProperty.Register<SliderSurface, double>(nameof(TickFrequency));
    /// <summary>Identifies the <see cref="ShowSteps"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowStepsProperty = AvaloniaProperty.Register<SliderSurface, bool>(nameof(ShowSteps));
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<SliderSurface, Orientation>(nameof(Orientation));
    /// <summary>Identifies the <see cref="IsDirectionReversed"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDirectionReversedProperty = AvaloniaProperty.Register<SliderSurface, bool>(nameof(IsDirectionReversed));
    /// <summary>Identifies the <see cref="TrackBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<SliderSurface, IBrush?>(nameof(TrackBrush));
    /// <summary>Identifies the <see cref="FillBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty = AvaloniaProperty.Register<SliderSurface, IBrush?>(nameof(FillBrush));
    /// <summary>Identifies the <see cref="StepBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> StepBrushProperty = AvaloniaProperty.Register<SliderSurface, IBrush?>(nameof(StepBrush));
    /// <summary>Identifies the <see cref="ActiveStepBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> ActiveStepBrushProperty = AvaloniaProperty.Register<SliderSurface, IBrush?>(nameof(ActiveStepBrush));
    internal static readonly StyledProperty<CornerRadius?> RowCornerRadiusProperty =
        AvaloniaProperty.Register<SliderSurface, CornerRadius?>(nameof(RowCornerRadius));

    static SliderSurface() => AffectsRender<SliderSurface>(MinimumProperty, MaximumProperty, ValueProperty, LowerValueProperty,
        TickFrequencyProperty, ShowStepsProperty, OrientationProperty, IsDirectionReversedProperty, TrackBrushProperty,
        FillBrushProperty, StepBrushProperty, ActiveStepBrushProperty, RowCornerRadiusProperty);

    /// <summary>The lower bound of the supported value range.</summary>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>The upper bound of the supported value range.</summary>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>The value used to position the primary thumb and fill the track.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The optional lower bound used when drawing a two-thumb fill.</summary>
    public double? LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    /// <summary>The spacing between numeric tick values.</summary>
    public double TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }
    /// <summary>Whether to show the steps.</summary>
    public bool ShowSteps { get => GetValue(ShowStepsProperty); set => SetValue(ShowStepsProperty, value); }
    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Whether the minimum and maximum swap ends along the track.</summary>
    public bool IsDirectionReversed { get => GetValue(IsDirectionReversedProperty); set => SetValue(IsDirectionReversedProperty, value); }
    /// <summary>The brush used to draw the track.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    /// <summary>The brush used to draw the fill.</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    /// <summary>The brush used for marks outside the filled portion of the track.</summary>
    public IBrush? StepBrush { get => GetValue(StepBrushProperty); set => SetValue(StepBrushProperty, value); }
    /// <summary>The brush used for marks inside the filled portion of the track.</summary>
    public IBrush? ActiveStepBrush { get => GetValue(ActiveStepBrushProperty); set => SetValue(ActiveStepBrushProperty, value); }
    internal CornerRadius? RowCornerRadius { get => GetValue(RowCornerRadiusProperty); set => SetValue(RowCornerRadiusProperty, value); }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? Bounds.Height : Bounds.Width;
        var cross = vertical ? Bounds.Width : Bounds.Height;
        var rowCorners = RowCornerRadius;
        var thickness = rowCorners.HasValue ? cross : Math.Min(Thickness, cross);
        var inset = (cross - thickness) / 2;
        Rect Segment(double start, double end) => vertical
            ? new Rect(inset, start, thickness, end - start)
            : new Rect(start, inset, end - start, thickness);

        if (length <= 0 || thickness <= 0) return;
        var track = new RoundedRect(Segment(0, length), rowCorners ?? new CornerRadius(thickness / 2));
        context.DrawRectangle(TrackBrush, null, track);

        var range = Maximum - Minimum;
        if (!double.IsFinite(range) || range <= 0) return;
        var reversed = vertical != IsDirectionReversed;

        var lower = Math.Clamp(((LowerValue ?? Minimum) - Minimum) / range, 0, 1);
        var upper = Math.Clamp((Value - Minimum) / range, lower, 1);
        var thumbLength = rowCorners.HasValue ? 0 : ThumbLength;
        var low = lower == 0 ? reversed ? length : 0 : Position(length, lower, reversed, thumbLength);
        var high = upper == 1 ? reversed ? 0 : length : Position(length, upper, reversed, thumbLength);
        var start = Math.Min(low, high);
        var end = Math.Max(low, high);
        if (upper > lower && end > start)
        {
            using var clip = context.PushClip(track);
            context.DrawRectangle(FillBrush, null, Segment(start, end));
        }

        var step = TickFrequency / range;
        if (!ShowSteps || !double.IsFinite(step) || step <= 0) return;
        var count = Math.Floor(1 / step + 1e-9) + 1;
        if (count > 1000) return;
        for (int index = 0; index < count; index++)
        {
            double value = index * step;
            var position = Position(length, Math.Min(value, 1), reversed, thumbLength);
            var point = vertical ? new Point(cross / 2, position) : new Point(position, cross / 2);
            context.DrawEllipse(value >= lower - 1e-9 && value <= upper + 1e-9 ? ActiveStepBrush : StepBrush, null, point, 1.5, 1.5);
        }
    }

    internal static double Position(double length, double ratio, bool reversed, double thumbLength = ThumbLength)
    {
        var halfThumb = Math.Min(thumbLength, length) / 2;
        return halfThumb + (length - 2 * halfThumb) * (reversed ? 1 - ratio : ratio);
    }
}
