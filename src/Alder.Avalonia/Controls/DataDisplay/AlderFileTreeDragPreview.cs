using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderFileTree
{
    private FileTreeDragPreview? dragPreview;

    private void BeginDragPreview(PointerDrag session)
    {
        if (OverlayLayer.GetOverlayLayer(this) is not { } layer ||
            session.Row.RowSurface is not { Child: { } content } row ||
            session.Root.TranslatePoint(session.Origin, row) is not { } grab ||
            content.TranslatePoint(default, row) is not { } contentOrigin ||
            row.Bounds.Width <= 0 || row.Bounds.Height <= 0) return;

        var theme = this.TryFindResource("AlderFileTreeDragPreviewTheme", out var value) ? value as ControlTheme : null;
        dragPreview = new FileTreeDragPreview(layer, row, content, contentOrigin, grab, session.Root.RenderScaling, theme);
    }

    private sealed class FileTreeDragPreview : IDisposable
    {
        private readonly OverlayLayer layer;
        private readonly Border surface;
        private readonly Image image;
        private readonly RenderTargetBitmap snapshot;
        private readonly TranslateTransform translation = new();
        private readonly Point grabOffset;

        public FileTreeDragPreview(OverlayLayer layer, Border row, Control content, Point contentOrigin,
            Point grabOffset, double scaling, ControlTheme? theme)
        {
            this.layer = layer;
            this.grabOffset = grabOffset;
            var size = row.Bounds.Size;
            var pixels = new PixelSize((int)Math.Ceiling(size.Width * scaling), (int)Math.Ceiling(size.Height * scaling));
            snapshot = new RenderTargetBitmap(pixels);

            // Capture the templated contents once, without inheriting the dimmed source row's opacity.
            using (var context = snapshot.CreateDrawingContext())
            using (context.PushTransform(Matrix.CreateScale(pixels.Width / size.Width, pixels.Height / size.Height)))
            {
                DrawGuides(context, row);
                context.FillRectangle(new VisualBrush(content), new Rect(contentOrigin, content.Bounds.Size));
            }

            image = new AlderImage { Source = snapshot, Stretch = Stretch.Fill, IsHitTestVisible = false };
            surface = new Border
            {
                Name = "FileTreeDragPreview",
                Width = size.Width,
                Height = size.Height,
                CornerRadius = row.CornerRadius,
                Child = image,
                Theme = theme,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = translation,
                IsHitTestVisible = false,
                ClipToBounds = false,
                ZIndex = int.MaxValue
            };
            AutomationProperties.SetAccessibilityView(surface, AccessibilityView.Raw);
            layer.Children.Add(surface);
        }

        private static void DrawGuides(DrawingContext context, Border row)
        {
            var rowBounds = new Rect(row.Bounds.Size);
            foreach (var parent in row.GetVisualAncestors().TakeWhile(ancestor => ancestor is not AlderFileTree)
                         .OfType<AlderFileTreeItem>())
            {
                if (parent.GuideSurface is not { IsEffectivelyVisible: true, Background: { } brush } guide ||
                    guide.TranslatePoint(default, row) is not { } origin) continue;

                var visible = new Rect(origin, guide.Bounds.Size).Intersect(rowBounds);
                if (visible.Width > 0 && visible.Height > 0) context.FillRectangle(brush, visible);
            }
        }

        public void Move(Visual root, Point pointer)
        {
            if (root.TranslatePoint(pointer, layer) is not { } position) return;
            translation.X = position.X - grabOffset.X;
            translation.Y = position.Y - grabOffset.Y;
        }

        public void Dispose()
        {
            layer.Children.Remove(surface);
            image.Source = null;
            snapshot.Dispose();
        }
    }
}
