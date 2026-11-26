using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the center content and sweep shared by Alder circular charts.</summary>
public abstract class AlderCircularChart : AlderPlotChart
{
    /// <summary>Identifies the <see cref="CenterContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> CenterContentProperty =
        AvaloniaProperty.Register<AlderCircularChart, object?>(nameof(CenterContent));
    /// <summary>Identifies the <see cref="CenterContentTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> CenterContentTemplateProperty =
        AvaloniaProperty.Register<AlderCircularChart, IDataTemplate?>(nameof(CenterContentTemplate));
    /// <summary>Identifies the <see cref="StartAngle"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<AlderCircularChart, double>(nameof(StartAngle), 0, validate: double.IsFinite);
    /// <summary>Identifies the <see cref="SweepAngle"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<AlderCircularChart, double>(nameof(SweepAngle), -360,
            validate: value => double.IsFinite(value) && Math.Abs(value) <= 360);

    /// <summary>The content to display in the open center of the plot.</summary>
    public object? CenterContent { get => GetValue(CenterContentProperty); set => SetValue(CenterContentProperty, value); }
    /// <summary>The data template for rendering the plot's center content.</summary>
    public IDataTemplate? CenterContentTemplate { get => GetValue(CenterContentTemplateProperty); set => SetValue(CenterContentTemplateProperty, value); }
    /// <summary>Start angle in degrees, with zero at the right edge. Positive sweeps are clockwise.</summary>
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    /// <summary>The angular extent available to the chart, measured in degrees.</summary>
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }

    private protected override Rect TooltipBounds => new(Bounds.Size);
    private protected Point Center => TooltipBounds.Center;
    private protected double Radius => Math.Max(0, Math.Min(
        Bounds.Width - Padding.Left - Padding.Right, Bounds.Height - Padding.Top - Padding.Bottom) / 2);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartAngleProperty || change.Property == SweepAngleProperty)
            ResetLayout();
    }
}

internal static class ChartPolarGeometry
{
    public static Point At(Point center, double radius, double degrees)
    {
        var radians = degrees * (Math.PI / 180);
        return center + new Vector(Math.Cos(radians) * radius, Math.Sin(radians) * radius);
    }

    public static double Distance(Point first, Point second) =>
        new Vector(first.X - second.X, first.Y - second.Y).Length;

    public static double GetAngle(Point center, Point position) =>
        Math.Atan2(position.Y - center.Y, position.X - center.X) * (180 / Math.PI);

    public static double Offset(double angle, double start, double direction) =>
        ((angle - start) * direction % 360 + 360) % 360;

    public static void DrawArc(DrawingContext context, Point center, double radius, double start, double sweep,
        IBrush brush, double thickness, bool rounded)
    {
        if (radius <= 0 || thickness <= 0 || sweep == 0)
            return;
        var pen = new Pen(brush, thickness, lineCap: rounded ? PenLineCap.Round : PenLineCap.Flat);
        if (Math.Abs(sweep) >= 360)
            context.DrawEllipse(null, pen, center, radius, radius);
        else
            context.DrawGeometry(null, pen, Arc(center, radius, start, sweep, false));
    }

    public static StreamGeometry Arc(Point center, double radius, double start, double sweep, bool sector)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(At(center, radius, start), sector);
        var size = new Size(radius, radius);
        var direction = sweep < 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise;
        path.ArcTo(At(center, radius, start + sweep / 2), size, 0, false, direction);
        path.ArcTo(At(center, radius, start + sweep), size, 0, false, direction);
        if (sector)
            path.LineTo(center);
        path.EndFigure(sector);
        return geometry;
    }
}
