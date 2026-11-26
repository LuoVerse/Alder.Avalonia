using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderMap
{
    /// <summary>Optional vector roads to draw above the geographic basemap.</summary>
    [GeneratedStyledProperty]
    public partial IReadOnlyList<AlderMapRoadLayer>? RoadLayers { get; set; }

    /// <summary>The road color for layers supplied through <see cref="RoadLayers"/>.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? RoadBrush { get; set; }

    private const double RoadCoordinateScale = 65536;
    private sealed record ProjectedRoadLayer(MapCoordinate Origin, StreamGeometry Geometry, double Width, double MinimumZoom);
    private ProjectedRoadLayer[]? projectedRoads;

    private void DrawRoads(DrawingContext context, in ViewportProjection projection)
    {
        if (RoadLayers is not { Count: > 0 } layers || RoadBrush is not { } brush) return;
        projectedRoads ??= ProjectRoads(layers);
        foreach (var road in projectedRoads)
        {
            if (Zoom < road.MinimumZoom) continue;
            var origin = projection.ProjectMarker(road.Origin);
            var scale = projection.World / RoadCoordinateScale;
            var cosine = projection.Cosine * scale;
            var sine = projection.Sine * scale;
            using var transform = context.PushTransform(new Matrix(cosine, sine, -sine, cosine, origin.X, origin.Y));
            context.DrawGeometry(null, new Pen(brush, road.Width / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), road.Geometry);
        }
    }

    private static ProjectedRoadLayer[] ProjectRoads(IReadOnlyList<AlderMapRoadLayer> layers)
    {
        var result = new List<ProjectedRoadLayer>(layers.Count);
        foreach (var layer in layers)
        {
            if (layer.Lines.Count == 0 || !double.IsFinite(layer.Width) || layer.Width <= 0) continue;
            var origin = layer.Lines.FirstOrDefault(line => line.Count >= 2)?[0];
            if (origin is not { IsFinite: true } first) continue;
            var center = Project(first);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                foreach (var line in layer.Lines)
                {
                    if (line.Count < 2 || line.Any(point => !point.IsFinite)) continue;
                    path.BeginFigure(Local(line[0]), false);
                    for (int i = 1; i < line.Count; ++i) path.LineTo(Local(line[i]));
                    path.EndFigure(false);
                }
            }
            result.Add(new(first, geometry, layer.Width, layer.MinimumZoom));

            Point Local(MapCoordinate location)
            {
                var projected = Project(location);
                return new Point(WrapDelta(projected.X - center.X) * RoadCoordinateScale,
                    (projected.Y - center.Y) * RoadCoordinateScale);
            }
        }
        return result.ToArray();
    }
}
