using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Supplies the label, magnitude, and optional brush for one pie segment.</summary>
/// <param name="Name">The segment label.</param>
/// <param name="Value">The magnitude used to size the segment.</param>
/// <param name="Brush">An optional segment brush.</param>
/// <param name="BrushResourceKey">An optional resource key used to resolve the segment brush.</param>
public sealed record AlderChartSlice(string Name, double Value, IBrush? Brush = null, string? BrushResourceKey = null);

/// <summary>Draws one or two rings of proportional segments and exposes segment tooltip data.</summary>
public sealed class AlderPieChart : AlderCircularChart
{
    /// <summary>Identifies the <see cref="Slices"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderChartSlice>> SlicesProperty =
        AvaloniaProperty.Register<AlderPieChart, IReadOnlyList<AlderChartSlice>>(nameof(Slices), []);
    /// <summary>Identifies the <see cref="InnerSlices"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderChartSlice>> InnerSlicesProperty =
        AvaloniaProperty.Register<AlderPieChart, IReadOnlyList<AlderChartSlice>>(nameof(InnerSlices), []);
    /// <summary>Identifies the <see cref="InnerRadius"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<AlderPieChart, double>(nameof(InnerRadius), 0, validate: value => value >= 0 && value < 1);
    /// <summary>Identifies the <see cref="GapAngle"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> GapAngleProperty =
        AvaloniaProperty.Register<AlderPieChart, double>(nameof(GapAngle), 0, validate: value => value >= 0 && value < 360);
    /// <summary>Identifies the <see cref="RoundCaps"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> RoundCapsProperty =
        AvaloniaProperty.Register<AlderPieChart, bool>(nameof(RoundCaps));

    /// <summary>The source values for the outer ring.</summary>
    public IReadOnlyList<AlderChartSlice> Slices { get => GetValue(SlicesProperty) ?? []; set => SetValue(SlicesProperty, value); }
    /// <summary>The source values for the optional inner ring.</summary>
    public IReadOnlyList<AlderChartSlice> InnerSlices { get => GetValue(InnerSlicesProperty) ?? []; set => SetValue(InnerSlicesProperty, value); }
    /// <summary>The radius of the donut opening as a fraction of the outer radius.</summary>
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    /// <summary>The angular gap between adjacent segments, in degrees.</summary>
    public double GapAngle { get => GetValue(GapAngleProperty); set => SetValue(GapAngleProperty, value); }
    /// <summary>Whether ring segments use rounded ends.</summary>
    public bool RoundCaps { get => GetValue(RoundCapsProperty); set => SetValue(RoundCapsProperty, value); }

    private readonly record struct Sector(AlderChartSlice Item, IBrush Brush, double Start, double Size, double Inner, double Outer);
    private Sector[]? sectors;
    private Sector[] Sectors => sectors ??= BuildSectors();
    private protected override int ItemCount => Sectors.Length;
    private protected override TimeSpan RevealDelay => TimeSpan.FromMilliseconds(400);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlicesProperty || change.Property == InnerSlicesProperty ||
            change.Property == InnerRadiusProperty || change.Property == GapAngleProperty ||
            change.Property == RoundCapsProperty)
            ResetLayout();
    }

    private protected override void ResetLayout()
    {
        sectors = null;
        base.ResetLayout();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = TooltipBounds;
        double direction = Math.Sign(SweepAngle);
        for (int i = 0; i < Sectors.Length; i++)
        {
            var sector = Sectors[i];
            double start = StartAngle + direction * sector.Start * RevealProgress;
            double sweep = direction * sector.Size * RevealProgress;
            if (sweep == 0)
                continue;
            using var opacity = context.PushOpacity(ActiveData is not null && ActiveIndex != i ? .68 : 1);
            if (sector.Inner > 0)
                ChartPolarGeometry.DrawArc(context, bounds.Center, (sector.Outer + sector.Inner) / 2,
                    start, sweep, sector.Brush, sector.Outer - sector.Inner, RoundCaps);
            else
                context.DrawGeometry(sector.Brush, null, ChartPolarGeometry.Arc(bounds.Center, sector.Outer, start, sweep, true));
        }
    }

    private Sector[] BuildSectors()
    {
        var result = new List<Sector>();
        double radius = Radius;
        if (radius <= 0 || SweepAngle == 0)
            return [];
        double hole = radius * InnerRadius;
        if (InnerSlices.Count == 0)
            AddRing(Slices, hole, radius);
        else
        {
            double thickness = (radius - hole) * .42;
            AddRing(Slices, radius - thickness, radius);
            AddRing(InnerSlices, hole, hole + thickness);
        }
        return result.ToArray();

        void AddRing(IReadOnlyList<AlderChartSlice> items, double inner, double outer)
        {
            var valid = items.Where(i => double.IsFinite(i.Value) && i.Value > 0).ToArray();
            if (valid.Length == 0)
                return;
            double max = valid.Max(item => item.Value);
            double total = valid.Sum(item => item.Value / max);
            double angle = Math.Abs(SweepAngle);
            double gap = valid.Length > 1 ? Math.Min(GapAngle, angle / valid.Length / 2) : 0;
            double available = angle - gap * valid.Length;
            double offset = gap / 2;
            for (int i = 0; i < valid.Length; i++)
            {
                var item = valid[i];
                double size = (item.Value / max / total) * available;
                result.Add(new(item, ResolveSeriesBrush(i, item.Brush, item.BrushResourceKey), offset, size, inner, outer));
                offset += size + gap;
            }
        }
    }

    private protected override int HitTestIndex(Point position)
    {
        double distance = ChartPolarGeometry.Distance(position, Center);
        double offset = ChartPolarGeometry.Offset(ChartPolarGeometry.GetAngle(Center, position), StartAngle, Math.Sign(SweepAngle));
        for (int i = 0; i < Sectors.Length; i++)
        {
            var sector = Sectors[i];
            if (distance >= sector.Inner && distance <= sector.Outer &&
                offset >= sector.Start * RevealProgress && offset <= (sector.Start + sector.Size) * RevealProgress)
                return i;
        }
        return -1;
    }

    private protected override AlderChartTooltipData GetTooltipData(int index)
    {
        var sector = Sectors[index];
        string value = FormatValue(sector.Item.Value);
        return new("", [new(sector.Item.Name, value, sector.Brush)], value);
    }

    private protected override Point GetKeyboardAnchor(int index)
    {
        var sector = Sectors[index];
        return ChartPolarGeometry.At(Center, (sector.Inner + sector.Outer) / 2,
            StartAngle + Math.Sign(SweepAngle) * (sector.Start + sector.Size / 2));
    }
}
