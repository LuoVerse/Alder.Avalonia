using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

public abstract partial class AlderCartesianChart
{
    private static readonly DashStyle ComparisonDash = new([2.5, 2.5], 0);

    private protected void DrawLineSeries(DrawingContext context, ChartLayout chart, int s, bool smooth, double progress)
    {
        if (progress <= 0)
            return;
        var series = Series[s];
        var brush = chart.Brushes[s];
        var pen = new Pen(brush, !ShowXAxis && !ShowYAxis && !ShowGrid ? 1.5 : 2,
            series.IsDashed ? ComparisonDash : null, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var points = new List<Point>();
        foreach (var (span, category) in chart.Spans[s].Select((span, index) => (span, index)))
        {
            if (span is { } value)
                points.Add(new Point(chart.Category(category), chart.ValueCoordinate(value.End, s)));
            else if (!series.ConnectGaps)
            {
                DrawLineSegment(context, points, pen, smooth, series.ShowDots, progress);
                points.Clear();
            }
        }
        DrawLineSegment(context, points, pen, smooth, series.ShowDots, progress);
    }

    private static void DrawLineSegment(DrawingContext context, List<Point> points, IPen pen,
        bool smooth, bool dots, double progress)
    {
        if (points.Count == 0)
            return;
        if (points.Count == 1)
        {
            context.DrawEllipse(pen.Brush, null, points[0], 2.5, 2.5);
            return;
        }

        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(points[0], false);
            AppendCurve(path, points, smooth);
            path.EndFigure(false);
        }

        if (progress >= 1)
            context.DrawGeometry(null, pen, geometry);
        else if (geometry.TryGetSegment(0, geometry.ContourLength * progress, true, out var segment))
            context.DrawGeometry(null, pen, segment);

        if (dots && progress >= 1)
            foreach (var point in points)
                context.DrawEllipse(pen.Brush, null, point, 3, 3);
    }

    private protected void DrawAreaSeries(DrawingContext context, ChartLayout chart, int s, bool smooth, bool stacked)
    {
        bool compact = !ShowXAxis && !ShowYAxis && !ShowGrid;
        double fillStart = stacked ? .4 : .2;
        double fillEnd = stacked ? .1 : .02;
        var brush = chart.Brushes[s];
        var stroke = new Pen(brush, compact ? 1.5 : 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var fill = brush is ISolidColorBrush solid
            ? new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                Opacity = solid.Opacity,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb((byte)(solid.Color.A * fillStart), solid.Color.R, solid.Color.G, solid.Color.B), 0),
                    new GradientStop(Color.FromArgb((byte)(solid.Color.A * fillEnd), solid.Color.R, solid.Color.G, solid.Color.B), 1)
                }
            }
            : brush;
        var top = new List<Point>();
        var bottom = new List<Point>();

        for (int c = 0; c < chart.Spans[s].Length; c++)
        {
            if (chart.Spans[s][c] is not { } span)
            {
                if (Series[s].ConnectGaps) continue;
                DrawSegment(context, top, bottom, fill, stroke, brush is ISolidColorBrush ? 1 : .2, smooth);
                top.Clear();
                bottom.Clear();
                continue;
            }

            double x = chart.Category(c);
            top.Add(new Point(x, chart.ValueCoordinate(span.End, s)));
            bottom.Add(new Point(x, chart.ValueCoordinate(span.Start, s)));
        }

        DrawSegment(context, top, bottom, fill, stroke, brush is ISolidColorBrush ? 1 : .2, smooth);
    }

    private void DrawSegment(DrawingContext context, List<Point> top, List<Point> bottom,
        IBrush fill, IPen stroke, double fillOpacity, bool smooth)
    {
        if (top.Count == 0)
            return;
        if (top.Count == 1)
        {
            context.DrawEllipse(stroke.Brush, null, top[0], 2, 2);
            return;
        }

        var area = new StreamGeometry();
        using (var path = area.Open())
        {
            path.BeginFigure(top[0], true);
            AppendCurve(path, top, smooth);
            bottom.Reverse();
            path.LineTo(bottom[0]);
            AppendCurve(path, bottom, smooth);
            path.EndFigure(true);
        }
        using (context.PushOpacity(fillOpacity))
            context.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var path = line.Open())
        {
            path.BeginFigure(top[0], false);
            AppendCurve(path, top, smooth);
            path.EndFigure(false);
        }
        context.DrawGeometry(null, stroke, line);
    }

    private static void AppendCurve(StreamGeometryContext path, IReadOnlyList<Point> points, bool smooth)
    {
        double previousTangent = 0;
        for (int i = 1; i < points.Count; i++)
        {
            var start = points[i - 1];
            var end = points[i];
            if (!smooth)
            {
                path.LineTo(end);
                continue;
            }

            double nextTangent = 0;
            double slope = Slope(start, end);
            if (i + 1 < points.Count)
            {
                double nextSlope = Slope(end, points[i + 1]);
                if (slope != 0 && Math.Sign(slope) == Math.Sign(nextSlope))
                    nextTangent = 2 * slope * nextSlope / (slope + nextSlope);
            }
            else
                nextTangent = slope;
            if (i == 1)
                previousTangent = slope;

            double dx = (end.X - start.X) / 3;
            path.CubicBezierTo(new Point(start.X + dx, start.Y + dx * previousTangent),
                new Point(end.X - dx, end.Y - dx * nextTangent), end);
            previousTangent = nextTangent;
        }
    }

    private static double Slope(Point start, Point end) => (end.Y - start.Y) / (end.X - start.X);

    private protected void DrawBarSeries(DrawingContext context, ChartLayout chart, IReadOnlyList<int> indices,
        bool stacked, double maxWidth, double cornerRadius, bool roundBaseAtZero, double progress)
    {
        int bars = stacked ? 1 : indices.Count;
        double group = Math.Min(chart.CategorySpacing * .76, (Math.Min(maxWidth, 42) + (bars == 1 ? 0 : 4)) * bars);
        double slot = group / bars;
        double width = slot - (bars == 1 ? 0 : Math.Min(3, slot * .25));
        width = Math.Min(maxWidth, width);

        for (int c = 0; c < Categories.Count; c++)
        {
            int lastPositive = -1;
            int lastNegative = -1;
            if (stacked)
            {
                foreach (int s in indices)
                {
                    if (chart.Spans[s][c] is { Value: > 0 })
                        lastPositive = s;
                    else if (chart.Spans[s][c] is { Value: < 0 })
                        lastNegative = s;
                }
            }

            for (int i = 0; i < indices.Count; i++)
            {
                int s = indices[i];
                if (chart.Spans[s][c] is not { } span || span.Value == 0)
                    continue;

                double center = chart.Category(c) + (stacked ? 0 : (i + .5) * slot - group / 2);
                double start = chart.ValueCoordinate(span.Start * progress, s);
                double end = chart.ValueCoordinate(span.End * progress, s);
                double origin = Math.Min(start, end);
                double length = Math.Abs(end - start);
                var rect = IsHorizontal ? new Rect(origin, center - width / 2, length, width)
                    : new Rect(center - width / 2, origin, width, length);
                double radius = Math.Min(cornerRadius, Math.Min(width / 2, length / 2));
                bool roundEnd = !stacked || s == (span.Value > 0 ? lastPositive : lastNegative);
                bool roundBase = roundBaseAtZero && span.Start == 0;
                DrawBar(context, chart.Brushes[s], rect, roundEnd ? radius : 0, roundBase ? radius : 0, span.Value > 0);
            }
        }
    }

    private void DrawBar(DrawingContext context, IBrush brush, Rect rect, double end, double start, bool positive)
    {
        bool nearIsEnd = IsHorizontal ? !positive : positive;
        var nearRadius = nearIsEnd ? end : start;
        var farRadius = nearIsEnd ? start : end;
        var near = new Vector(nearRadius, nearRadius);
        var far = new Vector(farRadius, farRadius);
        context.DrawRectangle(brush, null, new RoundedRect(rect, near,
            IsHorizontal ? far : near, far, IsHorizontal ? near : far));
    }

    private protected void DrawDashedCursor(DrawingContext context, ChartLayout chart, int index)
    {
        if (index < 0 || ActiveData is null || CursorBrush is not { } brush)
            return;
        double x = chart.Category(index);
        context.DrawLine(new Pen(brush, 1, DashStyle.Dash), new Point(x, chart.Plot.Top), new Point(x, chart.Plot.Bottom));
    }

    private protected void DrawSeriesPoints(DrawingContext context, ChartLayout chart, int index, bool ignoreBars = false)
    {
        if (index < 0 || ActiveData is null)
            return;
        var outline = new Pen(ResolveBrush("AlderSurface", Brushes.White), 2);
        for (int s = 0; s < chart.Spans.Length; s++)
        {
            if ((!ignoreBars || Series[s].Kind != AlderChartSeriesKind.Bar) && chart.Spans[s][index] is { } span)
                context.DrawEllipse(chart.Brushes[s], outline, new Point(chart.Category(index), chart.ValueCoordinate(span.End, s)), 4, 4);
        }
    }
}
