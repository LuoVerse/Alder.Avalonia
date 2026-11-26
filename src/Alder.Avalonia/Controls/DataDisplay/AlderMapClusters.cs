using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderMap
{
    private const double ClusterCellSize = 46;
    private const double ClusterZoomMargin = .08;
    private static readonly TimeSpan ClusterFadeDuration = TimeSpan.FromMilliseconds(140);

    private readonly record struct ClusterKey(int FirstMarker, int Count);
    private sealed record MarkerCluster(Point Center, AlderMapMarker[] Markers);
    private sealed record ClusterLevel(int Zoom, Dictionary<ClusterKey, MarkerCluster> Groups);

    private IReadOnlyList<AlderMapMarker>? clusterSnapshot;
    private ClusterLevel? clusterLevel;
    private ClusterLevel? outgoingClusters;
    private TimeSpan? clusterFrameTime;
    private double clusterProgress;
    private Action<TimeSpan>? advanceClusters;
    private bool clusterFrameQueued;

    private void ResetClusters()
    {
        clusterSnapshot = null;
        clusterLevel = null;
        outgoingClusters = null;
        clusterFrameTime = null;
    }

    private void DrawClusters(DrawingContext context, Rect viewport, ViewportProjection projection, IEnumerable<AlderMapMarker> markers)
    {
        var source = markers as IReadOnlyList<AlderMapMarker> ?? markers.ToArray();
        if (clusterSnapshot is not { } snapshot || !SameMarkers(source, snapshot))
        {
            // Snapshot references so changes to mutable lists also show after a redraw.
            clusterSnapshot = snapshot = source.ToArray();
            clusterLevel = null;
            outgoingClusters = null;
        }

        int level = ClusterZoomLevel();
        var root = TopLevel.GetTopLevel(this);
        if (clusterLevel is null || AlderMotion.GetReduced(this) || root is null)
        {
            if (clusterLevel?.Zoom != level) clusterLevel = CreateClusterLevel(snapshot, level);
            outgoingClusters = null;
        }
        else if (clusterLevel.Zoom != level)
        {
            if (outgoingClusters is null)
            {
                var next = CreateClusterLevel(snapshot, level);
                var previous = clusterLevel;
                clusterLevel = next;
                if (previous.Groups.Count != next.Groups.Count || !previous.Groups.Keys.All(next.Groups.ContainsKey))
                {
                    outgoingClusters = previous;
                    clusterProgress = 0;
                    clusterFrameTime = null;
                }
            }
            else if (outgoingClusters.Zoom == level)
            {
                // Reverse from the current opacity instead of restarting the fade on a fast direction change.
                (clusterLevel, outgoingClusters) = (outgoingClusters, clusterLevel);
                clusterProgress = 1 - clusterProgress;
            }
        }

        var remaining = outgoingClusters;
        double opacity = remaining is null ? 1 : clusterProgress * clusterProgress * (3 - 2 * clusterProgress);
        var visible = viewport.Inflate(ClusterCellSize);
        if (remaining is not null)
        {
            foreach (var (key, cluster) in remaining.Groups)
                if (!clusterLevel.Groups.ContainsKey(key)) Draw(cluster, 1 - opacity, opacity < .5);

            if (root is not null && !clusterFrameQueued)
            {
                clusterFrameQueued = true;
                root.RequestAnimationFrame(advanceClusters ??= AdvanceClusters);
            }
        }

        foreach (var (key, cluster) in clusterLevel.Groups)
        {
            // A group with the same members is drawn once, at full opacity.
            double alpha = remaining is not null && !remaining.Groups.ContainsKey(key) ? opacity : 1;
            Draw(cluster, alpha, alpha >= .5);
        }

        void Draw(MarkerCluster cluster, double alpha, bool hitTarget)
        {
            var position = projection.ProjectWorldMarker(cluster.Center);
            if (!visible.Contains(position) || alpha <= 0) return;
            if (alpha == 1) DrawMarker(context, position, cluster.Markers, hitTarget);
            else
            {
                using var fade = context.PushOpacity(alpha);
                DrawMarker(context, position, cluster.Markers, hitTarget);
            }
        }
    }

    private int ClusterZoomLevel()
    {
        if (clusterLevel is not { Zoom: var current } || Zoom == 22) return (int)Zoom;
        // Fractional zoom only changes projection. A small overlap avoids flapping at level boundaries.
        if (Zoom >= current + 1 + ClusterZoomMargin) return (int)(Zoom - ClusterZoomMargin);
        if (Zoom < current - ClusterZoomMargin) return (int)(Zoom + ClusterZoomMargin);
        return current;
    }

    private static bool SameMarkers(IReadOnlyList<AlderMapMarker> a, IReadOnlyList<AlderMapMarker> b)
    {
        if (a.Count != b.Count) return false;
        for (int index = 0; index < a.Count; index++)
            if (!ReferenceEquals(a[index], b[index])) return false;
        return true;
    }

    private static ClusterLevel CreateClusterLevel(IReadOnlyList<AlderMapMarker> markers, int level)
    {
        // Group all coordinates before clipping to the viewport to keep edge clusters stable during panning.
        double scale = Math.ScaleB(256, level) / ClusterCellSize;
        var buckets = new Dictionary<(int X, int Y), List<(int Index, AlderMapMarker Marker, Point Position)>>();
        for (int index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            if (!marker.Location.IsFinite) continue;
            var position = Project(marker.Location);
            var cell = ((int)(position.X * scale), (int)(position.Y * scale));
            if (!buckets.TryGetValue(cell, out var bucket)) buckets[cell] = bucket = [];
            bucket.Add((index, marker, position));
        }

        var groups = new Dictionary<ClusterKey, MarkerCluster>(buckets.Count);
        foreach (var bucket in buckets.Values)
        {
            var center = new Point(bucket.Average(entry => entry.Position.X), bucket.Average(entry => entry.Position.Y));
            // World-aligned cells nest across levels, so first index and size identify unchanged groups.
            groups.Add(new(bucket[0].Index, bucket.Count), new(center, bucket.Select(entry => entry.Marker).ToArray()));
        }
        return new(level, groups);
    }

    private void AdvanceClusters(TimeSpan time)
    {
        clusterFrameQueued = false;
        if (outgoingClusters is null) return;
        if (clusterFrameTime is { } last)
            clusterProgress = Math.Min(1, clusterProgress + Math.Max(0, (time - last) / ClusterFadeDuration));
        clusterFrameTime = time;
        if (clusterProgress >= 1) outgoingClusters = null;
        InvalidateVisual();
    }
}
