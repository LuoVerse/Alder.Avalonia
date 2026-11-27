using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Provides shared category, value axis, and series handling for Cartesian plots.</summary>
public abstract partial class AlderCartesianChart : AlderPlotChart
{
    /// <summary>Identifies the <see cref="Categories"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> CategoriesProperty =
        AvaloniaProperty.Register<AlderCartesianChart, IReadOnlyList<string>>(nameof(Categories), []);
    /// <summary>Identifies the <see cref="Series"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderChartSeries>> SeriesProperty =
        AvaloniaProperty.Register<AlderCartesianChart, IReadOnlyList<AlderChartSeries>>(nameof(Series), []);
    /// <summary>Identifies the <see cref="AnimateDataChanges"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AnimateDataChangesProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(AnimateDataChanges));
    /// <summary>Identifies the <see cref="IncludeZero"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IncludeZeroProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(IncludeZero), true);
    /// <summary>Identifies the <see cref="IsStacked"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(IsStacked));
    /// <summary>Identifies the <see cref="ShowXAxis"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowXAxisProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(ShowXAxis), true);
    /// <summary>Identifies the <see cref="ShowYAxis"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowYAxisProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(ShowYAxis), true);
    /// <summary>Identifies the <see cref="ShowGrid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(ShowGrid), true);
    /// <summary>Identifies the <see cref="ShowHoverMarker"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowHoverMarkerProperty =
        AvaloniaProperty.Register<AlderCartesianChart, bool>(nameof(ShowHoverMarker), true);
    /// <summary>Identifies the <see cref="AxisValueFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> AxisValueFormatProperty =
        AvaloniaProperty.Register<AlderCartesianChart, string?>(nameof(AxisValueFormat));

    /// <summary>The category labels indexed by each series' values.</summary>
    public IReadOnlyList<string> Categories { get => GetValue(CategoriesProperty) ?? []; set => SetValue(CategoriesProperty, value); }
    /// <summary>The series to draw against the category and value axes.</summary>
    public IReadOnlyList<AlderChartSeries> Series { get => GetValue(SeriesProperty) ?? []; set => SetValue(SeriesProperty, value); }
    /// <summary>Whether replacing the data animates the chart update.</summary>
    public bool AnimateDataChanges { get => GetValue(AnimateDataChangesProperty); set => SetValue(AnimateDataChangesProperty, value); }
    /// <summary>Whether the computed value axis includes zero.</summary>
    public bool IncludeZero { get => GetValue(IncludeZeroProperty); set => SetValue(IncludeZeroProperty, value); }
    /// <summary>Whether compatible series accumulate at each category.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }
    /// <summary>Whether to show the x axis.</summary>
    public bool ShowXAxis { get => GetValue(ShowXAxisProperty); set => SetValue(ShowXAxisProperty, value); }
    /// <summary>Whether to show the y axis.</summary>
    public bool ShowYAxis { get => GetValue(ShowYAxisProperty); set => SetValue(ShowYAxisProperty, value); }
    /// <summary>Whether to show the grid.</summary>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    /// <summary>Highlights the nearest line or area data points independently of tooltip visibility.</summary>
    public bool ShowHoverMarker { get => GetValue(ShowHoverMarkerProperty); set => SetValue(ShowHoverMarkerProperty, value); }
    /// <summary>The numeric format string used on the primary value axis.</summary>
    public string? AxisValueFormat { get => GetValue(AxisValueFormatProperty); set => SetValue(AxisValueFormatProperty, value); }

    /// <summary>Optional increasing, finite ticks defining the primary value axis and its displayed range.</summary>
    [GeneratedStyledProperty]
    public partial IReadOnlyList<double>? ValueAxisTicks { get; set; }

    /// <summary>The minimum interval between category labels. Values below two use the automatic spacing.</summary>
    [GeneratedStyledProperty(DefaultValue = 1)]
    public partial int CategoryLabelInterval { get; set; }

    private static readonly double[] TickSteps = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10];
    private ChartLayout? layout;
    private Panel? plotLayers;
    private PlotLayer? cursorLayer;
    private PlotLayer? seriesLayer;
    private PlotLayer? pointsLayer;
    private TopLevel? viewRoot;
    private double lineRenderScale = 1;

    static AlderCartesianChart() => AffectsRender<AlderCartesianChart>(ShowGridProperty, CategoryLabelIntervalProperty);

    private protected virtual bool IsHorizontal => false;
    private protected virtual bool UsesBands => false;
    private protected virtual bool HasPointSeries => true;
    private protected virtual double? SecondaryMaximum => null;
    private protected virtual string? SecondaryAxisFormat => null;
    private protected virtual bool StackSeries(int seriesIndex) => IsStacked;

    private protected override Rect TooltipBounds => GetLayout()?.Plot ?? default;
    private protected override int ItemCount => Categories.Count;
    private protected override bool UsesVerticalNavigation => IsHorizontal;
    private protected override bool TracksPointerWithoutTooltip => ShowHoverMarker && HasPointSeries;

    private protected override int HitTestIndex(Point position) =>
        GetLayout() is { } chart && (!ShowTooltip && !ShowXAxis && !ShowYAxis ? new Rect(Bounds.Size) : chart.Plot).Contains(position)
            ? chart.IndexAt(position) : -1;

    private protected override Point GetKeyboardAnchor(int index) => GetTooltipAnchor(index, TooltipBounds.Center);

    private protected override Point GetTooltipAnchor(int index, Point pointer) =>
        GetLayout() is not { } chart ? pointer : IsHorizontal
            ? new Point(pointer.X, chart.Category(index))
            : new Point(chart.Category(index), pointer.Y);

    private protected override AlderChartTooltipData? GetTooltipData(int index)
    {
        if (GetLayout() is not { } chart)
            return null;
        double total = 0;
        var entries = new List<AlderChartTooltipEntry>(Series.Count);
        for (int s = 0; s < Series.Count; s++)
        {
            if (chart.Spans[s][index] is not { } span)
                continue;
            total += span.Value;
            entries.Add(new(Series[s].Name, FormatValue(span.Value, Series[s].ValueFormat), chart.Brushes[s]));
        }

        return entries.Count == 0 ? null : new(Categories[index], entries, FormatValue(total));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        plotLayers?.Children.Clear();
        plotLayers = e.NameScope.Find<Panel>("PART_PlotLayers");
        cursorLayer = null;
        seriesLayer = null;
        pointsLayer = null;
        if (plotLayers is not null)
        {
            cursorLayer = new(DrawPlotCursor);
            seriesLayer = new(DrawPlotSeries);
            pointsLayer = new(DrawPlotPoints);
            plotLayers.Children.Add(cursorLayer);
            plotLayers.Children.Add(seriesLayer);
            plotLayers.Children.Add(pointsLayer);
        }
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        viewRoot = TopLevel.GetTopLevel(this);
        if (viewRoot is not null) viewRoot.ScalingChanged += UpdateLineRenderScale;
        UpdateLineRenderScale(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (viewRoot is not null) viewRoot.ScalingChanged -= UpdateLineRenderScale;
        viewRoot = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateLineRenderScale(object? sender, EventArgs e)
    {
        double scale = viewRoot?.RenderScaling ?? 1;
        if (lineRenderScale == scale) return;
        lineRenderScale = scale;
        InvalidateSeries();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CategoriesProperty || change.Property == SeriesProperty ||
            change.Property == IsStackedProperty || change.Property == IncludeZeroProperty || change.Property == ShowXAxisProperty ||
            change.Property == ShowYAxisProperty || change.Property == AxisValueFormatProperty ||
            change.Property == ValueFormatProperty || change.Property == ValueAxisTicksProperty)
            ResetLayout();
        if (change.Property == SeriesProperty && AnimateDataChanges)
            RestartReveal();
        if (change.Property == ShowHoverMarkerProperty && !ShowHoverMarker && !ShowTooltip)
            ClearActive();
        if (change.Property == ShowTooltipProperty || change.Property == ShowHoverMarkerProperty ||
            change.Property == CursorBrushProperty || change.Property == BackgroundProperty)
            InvalidateActiveIndex();
        if (change.Property == ShowGridProperty)
            InvalidateSeries();
    }

    private protected override void ResetLayout()
    {
        layout = null;
        base.ResetLayout();
        InvalidateSeries();
        InvalidateActiveIndex();
    }

    private protected void InvalidateSeries()
    {
        if (seriesLayer is { } layer) layer.InvalidateVisual();
        else InvalidateVisual();
    }

    private protected override void InvalidateReveal() => InvalidateSeries();

    private protected override void InvalidateActiveIndex()
    {
        if (cursorLayer is { } cursor && pointsLayer is { } points)
        {
            cursor.InvalidateVisual();
            points.InvalidateVisual();
        }
        else InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var chart = GetLayout();
        if (chart is null)
            return;

        DrawAxes(context, chart);
        // Keep rendering functional with templates that don't declare the retained plot layers.
        if (plotLayers is not null) return;
        DrawPlotCursor(context);
        DrawPlotSeries(context);
        DrawPlotPoints(context);
    }

    private void DrawPlotCursor(DrawingContext context)
    {
        if (!ShowTooltip || ActiveIndex < 0 || GetLayout() is not { } chart) return;
        using var clip = context.PushClip(chart.Plot.Inflate(.5));
        DrawCursor(context, chart, ActiveIndex);
    }

    private void DrawPlotSeries(DrawingContext context)
    {
        if (GetLayout() is not { } chart) return;
        using var clip = context.PushClip(chart.Plot.Inflate(.5));
        DrawSeries(context, chart);
    }

    private void DrawPlotPoints(DrawingContext context)
    {
        if (ShowHoverMarker && ActiveIndex >= 0 && GetLayout() is { } chart)
            DrawActivePoints(context, chart, ActiveIndex);
    }

    private sealed class PlotLayer(Action<DrawingContext> draw) : Control
    {
        public override void Render(DrawingContext context) => draw(context);
    }

    private protected abstract void DrawSeries(DrawingContext context, ChartLayout chart);
    private protected virtual void DrawCursor(DrawingContext context, ChartLayout chart, int index) { }
    private protected virtual void DrawActivePoints(DrawingContext context, ChartLayout chart, int index) { }

    private ChartLayout? GetLayout()
    {
        if (layout is not null)
            return layout;
        if (Categories.Count == 0 || Series.Count == 0)
            return null;

        var spans = new ChartSpan?[Series.Count][];
        var colors = new IBrush[Series.Count];
        var positive = new double[Categories.Count];
        var negative = new double[Categories.Count];
        var min = new double[2];
        var max = new double[2];
        if (!IncludeZero && !IsStacked)
        {
            Array.Fill(min, double.PositiveInfinity);
            Array.Fill(max, double.NegativeInfinity);
        }
        bool hasSecondary = false;

        for (int s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            colors[s] = ResolveSeriesBrush(s, series.Brush, series.BrushResourceKey);
            bool stacked = StackSeries(s);
            int axis = series.UseSecondaryAxis ? 1 : 0;
            hasSecondary |= axis == 1;
            spans[s] = new ChartSpan?[Categories.Count];
            for (int c = 0; c < Math.Min(series.Values.Count, Categories.Count); c++)
            {
                if (series.Values[c] is not { } value || !double.IsFinite(value))
                    continue;
                ref var stack = ref (value < 0 ? ref negative[c] : ref positive[c]);
                double start = stacked ? stack : 0;
                double end = start + value;
                if (!double.IsFinite(end))
                    continue;
                if (stacked)
                    stack = end;
                spans[s][c] = new ChartSpan(start, end, value);
                min[axis] = Math.Min(min[axis], end);
                max[axis] = Math.Max(max[axis], end);
            }
        }

        var primary = MakeFixedScale(ValueAxisTicks) ?? MakeScale(min[0], max[0]);
        var secondary = hasSecondary ? MakeScale(min[1], Math.Max(max[1], SecondaryMaximum ?? max[1])) : null;
        var (magnitude, ticks) = primary;
        var xLabels = IsHorizontal ? ticks.Select(tick => FormatTick(tick, magnitude)) : Categories;
        var yLabels = IsHorizontal ? Categories : ticks.Select(tick => FormatTick(tick, magnitude));
        var left = ShowYAxis ? Math.Min(Bounds.Width * .3, yLabels.Max(label => Text(label).Width) + 9) : 0;
        var right = secondary is not null && ShowYAxis && !IsHorizontal
            ? Math.Min(Bounds.Width * .22, secondary.Ticks.Max(tick =>
                Text(FormatTick(tick, secondary.Magnitude, SecondaryAxisFormat)).Width) + 8)
            : 0;
        if (ShowXAxis)
        {
            var labels = xLabels as IReadOnlyList<string> ?? xLabels.ToArray();
            // The endpoint labels are centered on the plot endpoints for continuous axes.
            if (!UsesBands || IsHorizontal)
            {
                left = Math.Max(left, Math.Min(Bounds.Width * .15, Text(labels[0]).Width / 2));
                right = Math.Max(right, Math.Min(Bounds.Width * .15, Text(labels[^1]).Width / 2));
            }
        }

        var padding = Padding;
        var plot = new Rect(padding.Left + left, padding.Top,
            Math.Max(0, Bounds.Width - padding.Left - padding.Right - left - right),
            Math.Max(0, Bounds.Height - padding.Top - padding.Bottom - (ShowXAxis ? FontSize + 9 : 0)));
        if (plot.Width < 1 || plot.Height < 1)
            return null;
        return layout = new ChartLayout(plot, Categories.Count, UsesBands, IsHorizontal, spans, colors, primary, secondary, Series);
    }

    private static ValueScale? MakeFixedScale(IReadOnlyList<double>? values)
    {
        if (values is not { Count: > 1 } || !double.IsFinite(values[0]) || !double.IsFinite(values[^1]) ||
            values[0] >= values[^1]) return null;

        double magnitude = Math.Max(Math.Abs(values[0]), Math.Abs(values[^1]));
        var ticks = new double[values.Count];
        ticks[0] = values[0] / magnitude;
        for (int i = 1; i < ticks.Length; i++)
        {
            ticks[i] = values[i] / magnitude;
            if (!double.IsFinite(ticks[i]) || ticks[i] <= ticks[i - 1]) return null;
        }
        return new(magnitude, ticks);
    }

    private static ValueScale MakeScale(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max))
            (min, max) = (0, 0);

        // Scale before subtracting so finite values near the limits of double remain drawable.
        double magnitude = Math.Max(Math.Abs(min), max);
        if (magnitude == 0)
            magnitude = 1;
        double order = Math.Pow(10, Math.Floor(Math.Log10(magnitude)));
        if (order > 0)
            magnitude = order;

        double low = min / magnitude;
        double high = max / magnitude;
        if (high <= low)
        {
            if (max == 0)
                high = 1;
            else
                (low, high) = (low - .5, high + .5);
        }
        double ideal = (high - low) / 4;
        double power = Math.Pow(10, Math.Floor(Math.Log10(ideal)));
        double ratio = ideal / power;
        double step = power * TickSteps.First(factor => factor >= ratio - 1e-10);
        int firstTick = (int)Math.Floor(low / step);
        int lastTick = (int)Math.Ceiling(high / step);
        var ticks = Enumerable.Range(firstTick, lastTick - firstTick + 1).Select(i => i * step).ToArray();
        return new(magnitude, ticks);
    }

    private void DrawAxes(DrawingContext context, ChartLayout chart)
    {
        var plot = chart.Plot;
        var grid = ShowGrid && GridBrush is { } brush ? new Pen(brush) : null;
        foreach (var tick in chart.Ticks)
        {
            double coordinate = chart.ScaledCoordinate(tick);
            if (grid is not null)
            {
                if (IsHorizontal)
                    context.DrawLine(grid, new Point(coordinate, plot.Top), new Point(coordinate, plot.Bottom));
                else
                    context.DrawLine(grid, new Point(plot.Left, coordinate), new Point(plot.Right, coordinate));
            }

            if (IsHorizontal ? ShowXAxis : ShowYAxis)
            {
                // Keep labels legible if the chart is resized below its normal axes width or height.
                double spacing = (IsHorizontal ? plot.Width : plot.Height) / Math.Max(1, chart.Ticks.Length - 1);
                if (spacing < (IsHorizontal ? 30 : FontSize + 4) && tick != chart.Ticks[0] && tick != chart.Ticks[^1])
                    continue;
                if (IsHorizontal)
                    DrawXAxisLabel(context, FormatTick(tick, chart.Magnitude), coordinate, plot.Bottom);
                else
                    DrawYAxisLabel(context, FormatTick(tick, chart.Magnitude), plot.Left, coordinate);
            }
        }

        if (!IsHorizontal && ShowYAxis && chart.Secondary is { } secondary)
        {
            foreach (var tick in secondary.Ticks)
            {
                var label = Text(FormatTick(tick, secondary.Magnitude, SecondaryAxisFormat));
                double y = plot.Bottom - secondary.GetFraction(tick) * plot.Height;
                context.DrawText(label, new Point(plot.Right + 6, y - label.Height / 2));
            }
        }

        if (!(IsHorizontal ? ShowYAxis : ShowXAxis))
            return;
        int interval = Math.Clamp(CategoryLabelInterval, 1, Categories.Count);
        int stride = GetCategoryLabelStride(chart.CategorySpacing, interval);
        for (int i = interval - 1; i < Categories.Count; i += stride)
        {
            if (IsHorizontal)
                DrawYAxisLabel(context, Categories[i], plot.Left, chart.Category(i));
            else
                DrawXAxisLabel(context, Categories[i], chart.Category(i), plot.Bottom);
        }
    }

    private int GetCategoryLabelStride(double categorySpacing, int interval)
    {
        int count = Categories.Count;
        if (IsHorizontal)
            return Math.Max(interval, Math.Clamp((int)Math.Ceiling((FontSize + 6) / categorySpacing), 1, count));

        // At most one label can fit in each roughly font-sized band. Only shape labels
        // that could be drawn, then widen the stride if one of those labels needs room.
        int stride = Math.Max(interval, Math.Clamp((int)Math.Ceiling((FontSize + 8) / categorySpacing), 1, count));
        while (stride < count)
        {
            double labelWidth = 0;
            for (int i = interval - 1; i < count; i += stride)
            {
                labelWidth = Math.Max(labelWidth, Text(Categories[i]).Width);
                if (labelWidth >= 92) break;
            }
            int required = Math.Clamp((int)Math.Ceiling(Math.Min(100, labelWidth + 8) / categorySpacing), 1, count);
            if (required <= stride) break;
            stride = required;
        }
        return stride;
    }

    private void DrawXAxisLabel(DrawingContext context, string label, double x, double bottom)
    {
        var text = Text(label);
        var left = Math.Clamp(x - text.Width / 2, 0, Math.Max(0, Bounds.Width - text.Width));
        context.DrawText(text, new Point(left, bottom + 5));
    }

    private void DrawYAxisLabel(DrawingContext context, string label, double left, double y)
    {
        var text = Text(label);
        text.MaxTextWidth = Math.Max(1, left - 7);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(text, new Point(Math.Max(0, left - text.Width - 7), y - text.Height / 2));
    }

    private string FormatTick(double tick, double magnitude, string? axisFormat = null)
    {
        var value = tick * magnitude;
        if (double.IsFinite(value))
            return (axisFormat ?? AxisValueFormat) is { } format ? value.ToString(format, CultureInfo.CurrentCulture) : FormatValue(value);
        return value.ToString(CultureInfo.CurrentCulture);
    }

    private protected readonly record struct ChartSpan(double Start, double End, double Value);

    private protected sealed record ValueScale(double Magnitude, double[] Ticks)
    {
        public double GetFraction(double tick) => (tick - Ticks[0]) / (Ticks[^1] - Ticks[0]);
    }

    private protected sealed class ChartLayout(Rect plot, int count, bool usesBands, bool horizontal,
        ChartSpan?[][] spans, IBrush[] brushes, ValueScale primary, ValueScale? secondary, IReadOnlyList<AlderChartSeries> series)
    {
        private readonly (bool Smooth, double Scale, LineSegment[]? Segments)[] lineCache =
            new (bool, double, LineSegment[]?)[series.Count];

        public IReadOnlyList<LineSegment> GetLineSegments(int seriesIndex, bool smooth, double scale)
        {
            ref var cache = ref lineCache[seriesIndex];
            if (cache.Segments is { } segments && cache.Smooth == smooth && cache.Scale == scale)
                return segments;

            var options = series[seriesIndex];
            var points = new List<Point>();
            var lines = new List<LineSegment>();
            for (int index = 0; index < Spans[seriesIndex].Length; index++)
            {
                if (Spans[seriesIndex][index] is { } span)
                    points.Add(new Point(Category(index), ValueCoordinate(span.End, seriesIndex)));
                else if (!options.ConnectGaps)
                    AddSegment();
            }
            AddSegment();
            cache = (smooth, scale, lines.ToArray());
            return cache.Segments;

            void AddSegment()
            {
                if (points.Count == 0) return;
                StreamGeometry? geometry = null;
                if (points.Count > 1)
                {
                    if (!options.ShowDots && !options.IsDashed)
                        ReduceDenseLine(points, scale);
                    geometry = new StreamGeometry();
                    using var path = geometry.Open();
                    path.BeginFigure(points[0], false);
                    AppendCurve(path, points, smooth);
                    path.EndFigure(false);
                }
                lines.Add(new LineSegment(points[0], geometry, options.ShowDots ? points.ToArray() : null));
                points.Clear();
            }
        }

        public Rect Plot { get; } = plot;
        public ChartSpan?[][] Spans { get; } = spans;
        public IBrush[] Brushes { get; } = brushes;
        public double Magnitude => primary.Magnitude;
        public double[] Ticks => primary.Ticks;
        public ValueScale? Secondary => secondary;
        public double CategorySpacing => (horizontal ? Plot.Height : Plot.Width) / (usesBands ? count : Math.Max(1, count - 1));

        public double Category(int index)
        {
            double origin = horizontal ? Plot.Top : Plot.Left;
            if (count == 1)
                return origin + CategorySpacing / 2;
            return origin + CategorySpacing * (index + (usesBands ? .5 : 0));
        }

        public int IndexAt(Point position)
        {
            double offset = horizontal ? position.Y - Plot.Top : position.X - Plot.Left;
            return Math.Clamp((int)Math.Floor(offset / CategorySpacing + (usesBands ? 0 : .5)), 0, count - 1);
        }

        public double ValueCoordinate(double value, int seriesIndex = 0)
        {
            var scale = series[seriesIndex].UseSecondaryAxis && secondary is not null ? secondary : primary;
            return Project(scale.GetFraction(value / scale.Magnitude));
        }

        public double ScaledCoordinate(double value) => Project(primary.GetFraction(value));

        private double Project(double fraction) =>
            horizontal ? Plot.Left + fraction * Plot.Width : Plot.Bottom - fraction * Plot.Height;
    }
}
