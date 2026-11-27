using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderDataGrid
{
    private static readonly TimeSpan ReorderMotionDuration = TimeSpan.FromMilliseconds(180);
    private static readonly CubicEaseOut ReorderMotionEasing = new();
    private readonly HashSet<Control> movingRows = [];
    private PointerDrop? pendingDrop;
    private ReorderMotion? pendingRowMotion;
    private FloatingDragPreview? landingPreview;
    private Control? landingRow;
    private IDisposable? landingTimer;

    private sealed record PointerDrop(object Key, int Source, int Destination);
    private sealed record ReorderMotion(TableView View, object Key, Dictionary<object, (int Index, double Top)> PreviousRows);

    private void TrackPointerDrop(ReorderSession session, DataGridReorderRequest request, int source, int target)
    {
        int insertion = target + (request.Position == DataGridDropPosition.After ? 1 : 0);
        int destination = insertion - (source < insertion ? 1 : 0);
        if (source != destination && !AlderMotion.GetReduced(this))
        {
            pendingDrop = new(request.RowKey, source, destination);
            landingPreview = session.Preview;
            session.Preview = null;
        }
    }

    private void ReleaseUncommittedPreview(FloatingDragPreview preview)
    {
        // A binding or command can post the collection update. Wait through that dispatcher turn.
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(landingPreview, preview) && landingRow is null) ClearLandingPreview();
        }, DispatcherPriority.Background);
    }

    private protected override void OnVisibleRowsChanging(IReadOnlyList<object> nextRows)
    {
        var drop = pendingDrop;
        pendingDrop = null;
        // The command may update its collection later. Snapshot just before that update replaces the realized rows.
        if (drop is null || NativeView is not { } view || nextRows.Count != view.ItemCount ||
            (uint)drop.Source >= (uint)nextRows.Count || (uint)drop.Destination >= (uint)nextRows.Count ||
            Equals(GetRowKey(nextRows[drop.Source]), drop.Key) ||
            (ChildrenSelector is null && !Equals(GetRowKey(nextRows[drop.Destination]), drop.Key)) ||
            GetBodyViewport() is not { } body) return;

        var visible = body.Intersect(effectiveViewport);
        var positions = new Dictionary<object, (int Index, double Top)>();
        foreach (var row in view.GetRealizedContainers())
        {
            int index = view.IndexFromContainer(row);
            if (index < 0 || index == drop.Source || index >= view.ItemCount || view.ItemsView[index] is not { } item ||
                row.TranslatePoint(default, this) is not { } point || !visible.Intersects(new Rect(point, row.Bounds.Size))) continue;
            positions[GetRowKey(item)] = (index, point.Y);
        }
        if (positions.Count == 0 && landingPreview is null) return;
        ClearPendingRowMotion();
        pendingRowMotion = new(view, drop.Key, positions);
        LayoutUpdated += OnRowReorderLayout;
    }

    private void OnRowReorderLayout(object? sender, EventArgs e)
    {
        var motion = pendingRowMotion;
        ClearPendingRowMotion();
        if (motion is null || !ReferenceEquals(NativeView, motion.View) || AlderMotion.GetReduced(this) ||
            GetBodyViewport() is not { } body) return;

        var visible = body.Intersect(effectiveViewport);
        var view = motion.View;
        foreach (var row in view.GetRealizedContainers())
        {
            int index = view.IndexFromContainer(row);
            if (index < 0 || index >= view.ItemCount || view.ItemsView[index] is not { } item ||
                row.TranslatePoint(default, this) is not { } point || !visible.Intersects(new Rect(point, row.Bounds.Size))) continue;

            var key = GetRowKey(item);
            if (Equals(key, motion.Key))
            {
                SettleLandingPreview(row, new Point(Math.Max(body.X, point.X), point.Y));
                continue;
            }
            if (!motion.PreviousRows.TryGetValue(key, out var previous) || index == previous.Index) continue;
            double offset = previous.Top - point.Y;
            if (Math.Abs(offset) < .5) continue;
            OverlayMotion.Shift(row, new Vector(0, offset), ReorderMotionDuration, ReorderMotionEasing);
            movingRows.Add(row);
        }
    }

    private void SettleLandingPreview(Control row, Point origin)
    {
        if (landingPreview?.Settle(this, origin, ReorderMotionDuration, ReorderMotionEasing) != true) return;
        landingRow = row;
        row.Classes.Set("grid-drop-settling", true);
        landingTimer = DispatcherTimer.RunOnce(ClearLandingPreview, ReorderMotionDuration, DispatcherPriority.Render);
    }

    private void ClearLandingPreview()
    {
        landingTimer?.Dispose();
        landingTimer = null;
        landingRow?.Classes.Set("grid-drop-settling", false);
        landingRow = null;
        landingPreview?.Dispose();
        landingPreview = null;
    }

    internal void StopRowReorderMotion(Control row)
    {
        if (ReferenceEquals(landingRow, row)) ClearLandingPreview();
        if (movingRows.Remove(row)) ResetRowTranslation(row);
    }

    private void CancelRowReorderMotion()
    {
        pendingDrop = null;
        ClearPendingRowMotion();
        ClearLandingPreview();
        foreach (var row in movingRows) ResetRowTranslation(row);
        movingRows.Clear();
    }

    private void ClearPendingRowMotion()
    {
        if (pendingRowMotion is null) return;
        pendingRowMotion = null;
        LayoutUpdated -= OnRowReorderLayout;
    }

    private static void ResetRowTranslation(Control row)
    {
        if (ElementComposition.GetElementVisual(row) is not { } visual) return;
        visual.StopAnimation(nameof(CompositionVisual.Translation));
        visual.Translation = default;
    }
}
