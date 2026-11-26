using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderDataGrid
{
    private FloatingDragPreview? CreateDragPreview(ReorderSession session)
    {
        if (OverlayLayer.GetOverlayLayer(this) is not { } overlay ||
            TopLevel.GetTopLevel(this) is not { } root ||
            GetBodyViewport() is not { } viewport ||
            FindIndex(session.Source) is not (>= 0 and var index) ||
            NativeView?.ContainerFromIndex(index) is not TableViewRow row ||
            row.TranslatePoint(default, this) is not { } rowOrigin) return null;

        var rowRect = new Rect(rowOrigin, row.Bounds.Size);
        // Snapshot only the columns visible at the start of the drag. The row may be recycled during scrolling.
        var visible = rowRect.Intersect(new Rect(viewport.X, rowRect.Y, viewport.Width, rowRect.Height));
        if (visible.Width <= 0 || visible.Height <= 0) return null;

        var theme = this.TryFindResource("AlderDataGridDragPreviewTheme", out var value) ? value as ControlTheme : null;
        return new FloatingDragPreview(overlay, row, visible.Translate(-(Vector)rowOrigin), session.Origin - visible.Position,
            root.RenderScaling, theme, "DataGridDragPreview");
    }
}
