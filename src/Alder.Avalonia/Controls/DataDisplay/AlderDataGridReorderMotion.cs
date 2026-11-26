using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderDataGrid
{
    private static readonly TimeSpan ReorderMotionDuration = TimeSpan.FromMilliseconds(180);
    private static readonly CubicEaseOut ReorderMotionEasing = new();
    private readonly HashSet<Control> movingRows = [];
    private PointerDrop? pendingDrop;
    private ReorderMotion? pendingRowMotion;

    private sealed record PointerDrop(object Key, int Source, int Destination);
    private sealed record ReorderMotion(TableView View, Dictionary<object, (int Index, double Top)> PreviousRows);

    private void TrackPointerDrop(DataGridReorderRequest request, int source, int target)
    {
        int insertion = target + (request.Position == DataGridDropPosition.After ? 1 : 0);
        int destination = insertion - (source < insertion ? 1 : 0);
        if (source != destination && !AlderMotion.GetReduced(this))
            pendingDrop = new(request.RowKey, source, destination);
    }

    private protected override void OnVisibleRowsChanging(IReadOnlyList<object> nextRows)
    {
        var drop = pendingDrop;
        pendingDrop = null;
        // The command may update its collection later. Snapshot just before that update replaces the realized rows.
        if (drop is null || NativeView is not { } view || nextRows.Count != view.ItemCount ||
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
        if (positions.Count == 0) return;
        ClearPendingRowMotion();
        pendingRowMotion = new(view, positions);
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
                !motion.PreviousRows.TryGetValue(GetRowKey(item), out var previous) || index == previous.Index ||
                row.TranslatePoint(default, this) is not { } point || !visible.Intersects(new Rect(point, row.Bounds.Size))) continue;

            double offset = previous.Top - point.Y;
            if (Math.Abs(offset) < .5) continue;
            OverlayMotion.Shift(row, new Vector(0, offset), ReorderMotionDuration, ReorderMotionEasing);
            movingRows.Add(row);
        }
    }

    internal void StopRowReorderMotion(Control row)
    {
        if (movingRows.Remove(row)) ResetRowTranslation(row);
    }

    private void CancelRowReorderMotion()
    {
        pendingDrop = null;
        ClearPendingRowMotion();
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
