using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Plots series on category spokes around a shared radial value scale.</summary>
public sealed class AlderRadarChart : AlderPlotChart
{
    /// <summary>Identifies the <see cref="Categories"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> CategoriesProperty = AlderCartesianChart.CategoriesProperty.AddOwner<AlderRadarChart>();
    /// <summary>Identifies the <see cref="Series"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderChartSeries>> SeriesProperty = AlderCartesianChart.SeriesProperty.AddOwner<AlderRadarChart>();
    /// <summary>Identifies the <see cref="ShowRadiusAxis"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowRadiusAxisProperty =
        AvaloniaProperty.Register<AlderRadarChart, bool>(nameof(ShowRadiusAxis));
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<AlderRadarChart, double>(nameof(Maximum), 100,
            validate: value => value > 0 && double.IsFinite(value));
    /// <summary>Identifies the <see cref="FillOpacity"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> FillOpacityProperty =
        AvaloniaProperty.Register<AlderRadarChart, double>(nameof(FillOpacity), .2, validate: value => value >= 0 && value <= 1);

    /// <summary>The labels of the spokes around the radar chart.</summary>
    public IReadOnlyList<string> Categories { get => GetValue(CategoriesProperty) ?? []; set => SetValue(CategoriesProperty, value); }
    /// <summary>The data series indexed by the chart's categories.</summary>
    public IReadOnlyList<AlderChartSeries> Series { get => GetValue(SeriesProperty) ?? []; set => SetValue(SeriesProperty, value); }
    /// <summary>Whether to show the radial value axis.</summary>
    public bool ShowRadiusAxis { get => GetValue(ShowRadiusAxisProperty); set => SetValue(ShowRadiusAxisProperty, value); }
    /// <summary>The upper bound of the radial scale.</summary>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>The opacity of the filled area inside each series outline.</summary>
    public double FillOpacity { get => GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }

    private protected override Rect TooltipBounds => new(Bounds.Size);
    private protected override int ItemCount => Categories.Count;
    private double Radius
    {
        get
        {
            double labelWidth = Categories.Count == 0 ? 0 : Categories.Max(name => Text(name, 11).Width);
            return Math.Max(0, Math.Min(Bounds.Width / 2 - labelWidth - 10, Bounds.Height / 2 - 28));
        }
    }

    static AlderRadarChart() => AffectsRender<AlderRadarChart>(ShowRadiusAxisProperty, FillOpacityProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CategoriesProperty || change.Property == SeriesProperty || change.Property == MaximumProperty)
            ResetLayout();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var center = TooltipBounds.Center;
        double radius = Radius;
        if (ItemCount < 3 || radius <= 0)
            return;

        var grid = new Pen(GridBrush ?? Brushes.LightGray);
        Point PointAt(int index, double fraction) => At(center, radius * fraction, index);
        for (int ring = 1; ring <= 4; ring++)
        {
            double fraction = ring / 4d;
            context.DrawGeometry(null, grid, Polygon(Enumerable.Range(0, ItemCount).Select(i => PointAt(i, fraction)).ToArray()));
            if (ShowRadiusAxis)
            {
                var label = Text(FormatValue(Maximum * fraction));
                context.DrawText(label, PointAt(0, fraction) + new Vector(4, -label.Height / 2));
            }
        }

        if (ShowRadiusAxis)
        {
            var zero = Text(FormatValue(0));
            context.DrawText(zero, center + new Vector(4, -zero.Height / 2));
        }

        for (int i = 0; i < ItemCount; i++)
        {
            context.DrawLine(grid, center, PointAt(i, 1));
            var label = Text(Categories[i], 11);
            var point = PointAt(i, 1) - center;
            double x = point.X > 1 ? 7 : point.X < -1 ? -label.Width - 7 : -label.Width / 2;
            double y = Math.Abs(point.X) < 1 ? (point.Y < 0 ? -label.Height - 5 : 5) : -label.Height / 2;
            context.DrawText(label, center + point + new Vector(x, y));
        }

        for (int s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            var color = ResolveSeriesBrush(s, series.Brush, series.BrushResourceKey);
            var polygon = new Point[ItemCount];
            for (int i = 0; i < ItemCount; i++)
                polygon[i] = PointAt(i, Fraction(Value(series, i)) * RevealProgress);

            var geometry = Polygon(polygon);
            using (context.PushOpacity(FillOpacity))
                context.DrawGeometry(color, null, geometry);
            context.DrawGeometry(null, new Pen(color, 2, lineJoin: PenLineJoin.Round), geometry);
            if (series.ShowDots)
                foreach (var point in polygon)
                    context.DrawEllipse(color, new Pen(ResolveBrush("AlderSurface", Brushes.White)), point, 4, 4);
            if (ActiveIndex >= 0 && Value(series, ActiveIndex) is not null)
                context.DrawEllipse(color, new Pen(ResolveBrush("AlderSurface", Brushes.White), 2), polygon[ActiveIndex], 5, 5);
        }
    }

    private protected override int HitTestIndex(Point position)
    {
        if (ItemCount < 3 || ChartPolarGeometry.Distance(position, TooltipBounds.Center) > Radius + 6)
            return -1;
        double angle = ChartPolarGeometry.GetAngle(TooltipBounds.Center, position);
        var offset = ChartPolarGeometry.Offset(angle, -90, 1);
        return (int)Math.Round(offset / 360 * ItemCount, MidpointRounding.AwayFromZero) % ItemCount;
    }

    private protected override AlderChartTooltipData? GetTooltipData(int index)
    {
        List<AlderChartTooltipEntry> entries = [];
        for (int s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            if (Value(series, index) is { } value)
                entries.Add(new(series.Name, FormatValue(value, series.ValueFormat), ResolveSeriesBrush(s, series.Brush, series.BrushResourceKey)));
        }
        return entries.Count == 0 ? null : new(Categories[index], entries, "");
    }

    private protected override Point GetKeyboardAnchor(int index) => At(TooltipBounds.Center, Radius * .7, index);

    private double Fraction(double? value) => Math.Clamp((value ?? 0) / Maximum, 0, 1);
    private Point At(Point center, double radius, int category) =>
        ChartPolarGeometry.At(center, radius, -90 + category * (360d / ItemCount));

    private static double? Value(AlderChartSeries series, int category) =>
        category < series.Values.Count && series.Values[category] is { } value && double.IsFinite(value) ? value : null;

    private static StreamGeometry Polygon(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(points[0], true);
        for (int i = 1; i < points.Count; i++)
            path.LineTo(points[i]);
        path.EndFigure(true);
        return geometry;
    }
}
