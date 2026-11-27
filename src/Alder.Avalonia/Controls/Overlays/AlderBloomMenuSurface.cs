using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderBloomMenuSurface : Panel
{
    // The overlay host clips to its layout bounds, so reserve space for the shadow.
    internal static readonly Thickness ShadowGutter = new(16, 28);
    private Size viewportSize;
    private Size expandedSize;
    private Size arrangedSize;
    private bool open;
    private bool presented;
    private bool settledExpanded;
    private bool waitingForPlacement;
    private IDisposable? completion;
    private Scene? scene;

    private AlderBloomMenu? Owner => TemplatedParent as AlderBloomMenu;

    internal bool HasActiveMotion => waitingForPlacement || completion is not null;

    internal void SetViewport(Size size)
    {
        if (size == viewportSize) return;
        viewportSize = size;
        InvalidateMeasure();
    }

    internal void Reset()
    {
        CancelCompletion();
        open = presented = settledExpanded = false;
        scene = null;
        Opacity = 0;
        IsHitTestVisible = false;
    }

    internal void Transition(bool isOpening)
    {
        open = isOpening;
        IsHitTestVisible = open;
        CancelCompletion();
        if (!presented && !open)
        {
            Owner?.FinishClose();
            return;
        }
        if (!TryStartMotion())
        {
            waitingForPlacement = true;
            LayoutUpdated += OnPlacementReady;
        }
    }

    internal void FinishMotion()
    {
        if (!HasActiveMotion) return;
        CancelCompletion();
        if (open)
        {
            CommitExpanded();
            Present();
        }
        else Owner?.FinishClose();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Reset();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (Owner is not { } owner || Children is not [AlderBloomMenuChrome chrome, Decorator bodyClip, Control caption])
            return base.MeasureOverride(available);

        var popupSize = available.Constrain(viewportSize.Width > 0 && viewportSize.Height > 0 ? viewportSize : available);
        var room = popupSize.Deflate(ShadowGutter);
        double width = Math.Min(room.Width, owner.ExpandedWidth);
        if (viewportSize.Width > 0) width = Math.Min(width, viewportSize.Width * .86);
        double height = viewportSize.Height > 0 ? Math.Min(room.Height, viewportSize.Height * .9) : room.Height;
        bodyClip.Measure(new Size(width, height).Deflate(owner.BorderThickness));
        expandedSize = new Size(width, Math.Min(height, bodyClip.DesiredSize.Inflate(owner.BorderThickness).Height));
        chrome.Measure(expandedSize);
        caption.Measure(owner.TriggerSize);
        return new Size(Math.Max(expandedSize.Width, owner.TriggerSize.Width),
            Math.Max(expandedSize.Height, owner.TriggerSize.Height)).Inflate(ShadowGutter).Constrain(popupSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Owner is not { } owner || Children is not [AlderBloomMenuChrome chrome, Decorator bodyClip, Control caption])
            return base.ArrangeOverride(finalSize);

        var expanded = new Rect(finalSize).CenterRect(new Rect(expandedSize.Constrain(finalSize.Deflate(ShadowGutter))));
        chrome.Arrange(expanded);
        bodyClip.Arrange(expanded.Deflate(owner.BorderThickness));
        caption.Arrange(owner.TriggerCaptionBoundsIn(this, expanded.CenterRect(new Rect(caption.DesiredSize))));

        // Only a genuine viewport or content resize renegotiates the layout.
        // The animated visuals are independent of these layout bounds.
        if (presented && arrangedSize != expanded.Size && open)
        {
            CancelCompletion();
            CommitExpanded();
        }
        arrangedSize = expanded.Size;
        return finalSize;
    }

    private void OnPlacementReady(object? sender, EventArgs e)
    {
        if (!waitingForPlacement || !TryStartMotion()) return;
        LayoutUpdated -= OnPlacementReady;
        waitingForPlacement = false;
    }

    private bool TryStartMotion()
    {
        if (Owner is not { } owner || !IsArrangeValid || arrangedSize.Width <= 0) return false;
        // An overlay popup is first arranged before its positioner writes its Canvas offset.
        if (this.FindAncestorOfType<OverlayPopupHost>() is { } host && double.IsNaN(Canvas.GetTop(host))) return false;
        if (Children is not [AlderBloomMenuChrome chrome, Decorator { Child: Border { Child: Grid body } } bodyClip, Control caption] ||
            body.Children is not [Control header, Decorator { Child: ScrollViewer scroll } irisClip])
            return false;

        // Re-read the caption's exact trigger position after the overlay has been positioned.
        var fallback = new Rect(Bounds.Size).CenterRect(new Rect(caption.DesiredSize));
        caption.Arrange(owner.TriggerCaptionBoundsIn(this, fallback));

        bool firstFrame = !presented;
        bool reduce = AlderMotion.GetReduced(owner);
        var triggerSize = owner.TriggerSize;
        var maximumSize = new Size(Math.Max(chrome.Bounds.Width, triggerSize.Width),
            Math.Max(chrome.Bounds.Height, triggerSize.Height)).Inflate(ShadowGutter);
        var chromeVisual = chrome.GetChromeVisual(maximumSize);
        if (scene is null)
        {
            if (chromeVisual is null ||
                ElementComposition.GetElementVisual(caption) is not { } captionVisual ||
                ElementComposition.GetElementVisual(header) is not { } headerVisual ||
                ClipLayer.Create(bodyClip, bodyClip.Child!) is not { } bodyLayer ||
                ClipLayer.Create(irisClip, scroll) is not { } irisLayer)
            {
                if (open)
                {
                    CommitExpanded();
                    Present();
                }
                else owner.FinishClose();
                return true;
            }
            scene = new Scene(chromeVisual, captionVisual, headerVisual, bodyLayer, irisLayer);
        }

        var anchor = owner.TriggerCenterIn(this, chrome.Bounds.Center);
        var collapsed = new Rect(new Point(anchor.X - triggerSize.Width / 2, anchor.Y - triggerSize.Height / 2), triggerSize);
        var morph = reduce ? BloomMenuMotion.ReducedMorph : BloomMenuMotion.Morph;
        var chromeOffset = collapsed.Position - chrome.Bounds.Position;
        // A small inset leaves room for the shell's full corner radii while the
        // rounded content clip is scaled. The inset goes to zero at full size.
        var collapsedBody = collapsed.Deflate(owner.BorderThickness + new Thickness(6))
            .Translate(new Vector(-bodyClip.Bounds.X, -bodyClip.Bounds.Y));
        var fullBody = new Rect(bodyClip.Bounds.Size);

        if (firstFrame)
        {
            var fullIris = new Rect(irisClip.Bounds.Size);
            if (!reduce)
            {
                var startIris = fullIris.Deflate(new Thickness(fullIris.Width * .26, fullIris.Height * .28));
                scene.Iris.Animate(fullIris, BloomMenuMotion.Iris, startIris);
            }
            RevealItems(owner, reduce);
        }

        // Explicit initial keyframes are needed because the newly attached elements
        // synchronize their layout sizes and opacity in the same compositor commit.
        chrome.ShowAnimatedChrome(true);
        var inset = new Vector(ShadowGutter.Left, ShadowGutter.Top);
        if (firstFrame) SetFrame(scene.Chrome, collapsed.Size.Inflate(ShadowGutter), chromeOffset - inset);
        morph.Resize(scene.Chrome, (open ? chrome.Bounds.Size : collapsed.Size).Inflate(ShadowGutter),
            firstFrame ? collapsed.Size.Inflate(ShadowGutter) : null);
        morph.Translate(scene.Chrome, (open ? default : chromeOffset) - inset, firstFrame ? chromeOffset - inset : null);
        scene.Body.Animate(open ? fullBody : collapsedBody, morph, firstFrame ? collapsedBody : null);
        var contentMotion = open ? BloomMenuMotion.Body : BloomMenuMotion.ExitContent;
        contentMotion.Fade(scene.Body.Content, open ? 1 : 0, from: firstFrame ? 0 : null);
        // The panel title is already at its resting position by the top edge.
        // Let the grid enter first so that the title never appears half clipped.
        var headingDelay = open && owner.ItemCount > 0 ? TimeSpan.FromMilliseconds(reduce ? 95 : 175) : default;
        contentMotion.Fade(scene.Header, open ? 1 : 0, headingDelay, firstFrame ? 0 : null);
        caption.Opacity = 1;
        BloomMenuMotion.Caption.Fade(scene.Caption, open ? 0 : 1,
            TimeSpan.FromMilliseconds(open ? 48 : reduce ? 22 : 64), firstFrame ? 1 : settledExpanded ? 0 : null);
        settledExpanded = false;
        Present();

        var duration = open && reduce ? TimeSpan.FromMilliseconds(240) : morph.Duration;
        completion = DispatcherTimer.RunOnce(FinishMotion, duration, DispatcherPriority.Background);
        return true;
    }

    private void RevealItems(AlderBloomMenu owner, bool reduce)
    {
        const int columns = AlderBloomMenuGrid.ColumnCount;
        int rows = (owner.ItemCount + columns - 1) / columns;
        for (int i = 0; i < owner.ItemCount; i++)
        {
            if (owner.ContainerFromIndex(i) is not AlderBloomMenuItem item) continue;
            double dx = i % columns - (columns - 1) / 2d;
            double dy = i / columns - (rows - 1) / 2d;
            var delay = reduce ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Math.Min(114, 18 + Math.Sqrt(dx * dx + dy * dy) * 34));
            item.Bloom(delay, reduce);
        }
    }

    private void Present()
    {
        presented = true;
        Opacity = 1;
        Owner?.HideTrigger();
    }

    private void CommitExpanded()
    {
        settledExpanded = true;
        if (Children is [AlderBloomMenuChrome chrome, _, Control caption])
        {
            caption.Opacity = 0;
            if (scene is { } visuals)
            {
                chrome.ShowAnimatedChrome(false);
                SetFrame(visuals.Chrome, chrome.Bounds.Size.Inflate(ShadowGutter), new Vector(-ShadowGutter.Left, -ShadowGutter.Top));
                visuals.Body.Reset();
                visuals.Iris.Reset();
                SetOpacity(visuals.Body.Content, 1);
                SetOpacity(visuals.Header, 1);
                SetOpacity(visuals.Caption, 0);
            }
        }
        if (Owner is { } owner)
            for (int i = 0; i < owner.ItemCount; i++)
                (owner.ContainerFromIndex(i) as AlderBloomMenuItem)?.FinishBloom();
    }

    private void CancelCompletion()
    {
        if (waitingForPlacement) LayoutUpdated -= OnPlacementReady;
        waitingForPlacement = false;
        completion?.Dispose();
        completion = null;
    }

    private static void SetFrame(CompositionVisual visual, Size size, Vector translation)
    {
        visual.StopAnimation(nameof(CompositionVisual.Size));
        visual.StopAnimation(nameof(CompositionVisual.Translation));
        visual.Size = new Vector(size.Width, size.Height);
        visual.Translation = new Vector3D(translation.X, translation.Y, 0);
    }

    private static void SetOpacity(CompositionVisual visual, float opacity)
    {
        visual.StopAnimation(nameof(CompositionVisual.Opacity));
        visual.Opacity = opacity;
    }

    private sealed record Scene(CompositionVisual Chrome, CompositionVisual Caption, CompositionVisual Header,
        ClipLayer Body, ClipLayer Iris);

    // Use independent composition transforms because Avalonia re-exports element Size from layout.
    // The inverse transform keeps the live glyphs and scroll viewport at their original positions.
    private sealed record ClipLayer(Control Host, CompositionVisual Clip, CompositionVisual Content)
    {
        private static readonly Vector3D UnitScale = new(1, 1, 1);

        internal static ClipLayer? Create(Control host, Control content) =>
            ElementComposition.GetElementVisual(host) is { } clip && ElementComposition.GetElementVisual(content) is { } child
                ? new ClipLayer(host, clip, child) : null;

        internal void Reset()
        {
            ResetTransform(Clip);
            ResetTransform(Content);
        }

        internal void Animate(Rect rect, BloomMenuMotion motion, Rect? from = null)
        {
            var initial = from is { } start ? (Vector)start.Position : (Vector?)null;
            motion.Scale(Clip, ScaleFor(rect), from is { } value ? ScaleFor(value) : null);
            motion.Translate(Clip, (Vector)rect.Position, initial);
            Compensate(nameof(CompositionVisual.Scale), "Vector3(1, 1, 1) / clip.Scale");
            Compensate(nameof(CompositionVisual.Translation), "-clip.Translation / clip.Scale");
        }

        private Vector3D ScaleFor(Rect rect) => new(
            Math.Max(.001, rect.Width / Math.Max(1, Host.Bounds.Width)),
            Math.Max(.001, rect.Height / Math.Max(1, Host.Bounds.Height)), 1);

        private void Compensate(string property, string expression)
        {
            var animation = Content.Compositor.CreateExpressionAnimation(expression);
            animation.SetReferenceParameter("clip", Clip);
            Content.StartAnimation(property, animation);
        }

        private static void ResetTransform(CompositionVisual visual)
        {
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.StopAnimation(nameof(CompositionVisual.Translation));
            visual.Scale = UnitScale;
            visual.Translation = default;
        }
    }

}

internal sealed class AlderBloomMenuGrid : UniformGrid
{
    internal const int ColumnCount = 3;

    public AlderBloomMenuGrid() => Columns = ColumnCount;

    protected override Size ArrangeOverride(Size finalSize)
    {
        int lastRow = (Children.Count - 1) / ColumnCount;
        for (int i = 0; i < Children.Count; i++)
            if (Children[i] is AlderBloomMenuItem item)
                item.SeparatorThickness = new Thickness(0, 0, i % ColumnCount == ColumnCount - 1 ? 0 : 1,
                    i / ColumnCount == lastRow ? 0 : 1);
        return base.ArrangeOverride(finalSize);
    }
}
