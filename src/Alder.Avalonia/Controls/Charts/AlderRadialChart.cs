using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Supplies a label, value, maximum, and optional brush for one radial ring.</summary>
/// <param name="Name">The ring label.</param>
/// <param name="Value">The progress to display.</param>
/// <param name="Maximum">The value represented by a full ring.</param>
/// <param name="Brush">An optional ring brush.</param>
/// <param name="BrushResourceKey">An optional resource key used to resolve the ring brush.</param>
public sealed record AlderRadialSeries(string Name, double Value, double Maximum = 100,
    IBrush? Brush = null, string? BrushResourceKey = null)
{
    /// <summary>The optional numeric format to use for this ring's displayed value.</summary>
    public string? ValueFormat { get; init; }
}

/// <summary>Presents values as concentric progress rings with optional shared center content.</summary>
public sealed class AlderRadialChart : AlderCircularChart
{
    /// <summary>Identifies the <see cref="Rings"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderRadialSeries>> RingsProperty =
        AvaloniaProperty.Register<AlderRadialChart, IReadOnlyList<AlderRadialSeries>>(nameof(Rings), []);
    /// <summary>Identifies the <see cref="RingThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<AlderRadialChart, double>(nameof(RingThickness), 10, validate: Nonnegative);
    /// <summary>Identifies the <see cref="RingSpacing"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> RingSpacingProperty =
        AvaloniaProperty.Register<AlderRadialChart, double>(nameof(RingSpacing), 6, validate: Nonnegative);
    /// <summary>Identifies the <see cref="TrackBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AlderRadialChart, IBrush?>(nameof(TrackBrush));
    /// <summary>Identifies the <see cref="RoundCaps"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> RoundCapsProperty =
        AvaloniaProperty.Register<AlderRadialChart, bool>(nameof(RoundCaps), true);

    /// <summary>The series drawn as concentric rings.</summary>
    public IReadOnlyList<AlderRadialSeries> Rings { get => GetValue(RingsProperty) ?? []; set => SetValue(RingsProperty, value); }
    /// <summary>The thickness of each ring in device-independent pixels.</summary>
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }
    /// <summary>The gap between adjacent rings in device-independent pixels.</summary>
    public double RingSpacing { get => GetValue(RingSpacingProperty); set => SetValue(RingSpacingProperty, value); }
    /// <summary>The brush used to draw the track.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    /// <summary>Whether each ring's stroke ends are rounded.</summary>
    public bool RoundCaps { get => GetValue(RoundCapsProperty); set => SetValue(RoundCapsProperty, value); }

    private protected override int ItemCount => Rings.Count;

    static AlderRadialChart()
    {
        StartAngleProperty.OverrideDefaultValue<AlderRadialChart>(-90);
        SweepAngleProperty.OverrideDefaultValue<AlderRadialChart>(360);
        AffectsRender<AlderRadialChart>(TrackBrushProperty, RoundCapsProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RingsProperty || change.Property == RingThicknessProperty || change.Property == RingSpacingProperty)
            ResetLayout();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var (thickness, spacing) = GetMetrics();
        var center = Center;
        for (int i = 0; i < Rings.Count; i++)
        {
            var ring = Rings[i];
            double radius = GetRadius(i, thickness, spacing);
            if (TrackBrush is { } track)
                ChartPolarGeometry.DrawArc(context, center, radius, StartAngle, SweepAngle, track, thickness, RoundCaps);
            using var opacity = context.PushOpacity(ActiveData is not null && ActiveIndex != i ? .72 : 1);
            ChartPolarGeometry.DrawArc(context, center, radius, StartAngle, SweepAngle * Fraction(ring) * RevealProgress,
                ResolveSeriesBrush(i, ring.Brush, ring.BrushResourceKey), thickness, RoundCaps);
        }
    }

    private protected override int HitTestIndex(Point position)
    {
        if (SweepAngle == 0)
            return -1;
        var (thickness, spacing) = GetMetrics();
        if (thickness <= 0)
            return -1;
        double distance = ChartPolarGeometry.Distance(position, Center);
        double offset = ChartPolarGeometry.Offset(ChartPolarGeometry.GetAngle(Center, position), StartAngle, Math.Sign(SweepAngle));
        for (int i = 0; i < Rings.Count; i++)
            if (Math.Abs(distance - GetRadius(i, thickness, spacing)) <= thickness / 2 &&
                offset <= Math.Abs(SweepAngle) * Fraction(Rings[i]) * RevealProgress)
                return i;
        return -1;
    }

    private protected override AlderChartTooltipData? GetTooltipData(int index)
    {
        var ring = Rings[index];
        if (!double.IsFinite(ring.Value))
            return null;
        string value = FormatValue(ring.Value, ring.ValueFormat);
        return new("", [new(ring.Name, value, ResolveSeriesBrush(index, ring.Brush, ring.BrushResourceKey))], value);
    }

    private protected override Point GetKeyboardAnchor(int index)
    {
        var (thickness, spacing) = GetMetrics();
        return ChartPolarGeometry.At(Center, GetRadius(index, thickness, spacing),
            StartAngle + SweepAngle * Fraction(Rings[index]) / 2);
    }

    private (double Thickness, double Spacing) GetMetrics()
    {
        double required = Rings.Count * RingThickness + Math.Max(0, Rings.Count - 1) * RingSpacing;
        double scale = required > Radius && required > 0 ? Radius / required : 1;
        return (RingThickness * scale, RingSpacing * scale);
    }

    private double GetRadius(int index, double thickness, double spacing) =>
        Radius - thickness / 2 - (thickness + spacing) * index;

    private static double Fraction(AlderRadialSeries ring) =>
        double.IsFinite(ring.Value) && double.IsFinite(ring.Maximum) && ring.Maximum > 0
            ? Math.Clamp(ring.Value / ring.Maximum, 0, 1) : 0;

    private static bool Nonnegative(double value) => double.IsFinite(value) && value >= 0;
}
