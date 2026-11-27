using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Records the fixed pieces of a loader and animates their visual properties.</summary>
internal sealed class LoaderMotionLayer : Panel
{
    private static readonly LinearEasing Linear = new();
    private AlderLoader? owner;
    private IBrush? ink;
    private AiLoaderVariant? variant;
    private Size animationSize;
    private int version;
    private bool scheduled;
    private bool running;

    internal bool IsComposed => running;

    internal void Attach(AlderLoader loader)
    {
        owner = loader;
        InvalidateArrange();
    }

    internal void Suspend()
    {
        if (!running && !scheduled && Children.Count == 0) return;
        running = false;
        scheduled = false;
        version++;
        Opacity = 0;
        foreach (var piece in Children)
        {
            if (ElementComposition.GetElementVisual(piece) is not { } visual) continue;
            visual.StopAnimation(nameof(CompositionVisual.Opacity));
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.StopAnimation(nameof(CompositionVisual.Translation));
            visual.StopAnimation(nameof(CompositionVisual.RotationAngle));
        }
        // Retire the old paint bounds when the owner takes over drawing the
        // glyph. Hidden pieces can leave stale edge pixels in the handoff frame.
        Children.Clear();
        ink = null;
        variant = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Suspend();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (owner is not { } loader || !loader.CanAnimate || loader.Foreground is not { } foreground ||
            !Supports(loader) || TopLevel.GetTopLevel(this) is not { } top ||
            ElementComposition.GetElementVisual(this) is null || finalSize.Width <= 0 || finalSize.Height <= 0)
        {
            Suspend();
            return finalSize;
        }

        bool rebuild = variant != loader.Variant || !ReferenceEquals(foreground, ink);
        if (rebuild)
        {
            Suspend();
            variant = loader.Variant;
            ink = foreground;
            var glyph = new LoaderGlyph(foreground);
            int count = loader.Variant switch { AiLoaderVariant.Dots => 3, AiLoaderVariant.Pulse => 1, _ => 2 };
            for (int index = 0; index < count; index++)
                Children.Add(new Piece(loader.Variant, index, foreground, glyph) { UseLayoutRounding = false });
        }

        var glyphBox = loader.GetGlyphBox(finalSize);
        for (int i = 0; i < Children.Count; i++)
            Children[i].Arrange(Place(loader, finalSize, glyphBox, i));

        var pieceSize = Children[0].Bounds.Size;
        if ((!running && !scheduled) || animationSize != pieceSize)
        {
            animationSize = pieceSize;
            if (!running) Opacity = 0;
            scheduled = true;
            var runVersion = ++version;
            // Join the same clock used by uncomposed loaders, including on reattachment.
            top.RequestAnimationFrame(time =>
            {
                if (runVersion != version) return;
                scheduled = false;
                for (int i = 0; i < Children.Count; i++)
                    AnimatePiece((Piece)Children[i], loader, time);
                if (running) return;
                running = true;
                Opacity = 1;
                loader.InvalidateVisual();
            });
        }
        return finalSize;
    }

    // Custom brushes can use absolute coordinates that depend on the original drawing space.
    internal static bool Supports(AlderLoader loader) => loader.Foreground is ISolidColorBrush &&
        (loader.Variant is AiLoaderVariant.Dots or AiLoaderVariant.Pulse or AiLoaderVariant.Scan ||
         loader.Variant == AiLoaderVariant.Ring && loader is not AlderChatLoader);

    private static Rect Place(AlderLoader loader, Size bounds, Rect glyphBox, int index)
    {
        if (loader.Variant is not (AiLoaderVariant.Dots or AiLoaderVariant.Pulse)) return glyphBox;
        var dot = loader.Variant == AiLoaderVariant.Dots;
        var (center, diameter, spacing) = loader is AlderChatLoader chat
            ? chat.GetTypingBox(bounds)
            : (glyphBox.Center, glyphBox.Width / 24 * (dot ? 6 : 9.6), glyphBox.Width / 24 * 9);
        var x = center.X + (dot ? (index - 1) * spacing : 0);
        return new Rect(x - diameter / 2, center.Y - diameter / 2, diameter, diameter);
    }

    private static void AnimatePiece(Piece piece, AlderLoader owner, TimeSpan now)
    {
        if (ElementComposition.GetElementVisual(piece) is not { } visual) return;
        visual.CenterPoint = new Vector3D(piece.Bounds.Width / 2, piece.Bounds.Height / 2, 0);
        if (piece.Variant == AiLoaderVariant.Ring)
        {
            if (piece.Index == 0) return;
            var rotation = visual.Compositor.CreateScalarKeyFrameAnimation();
            SetLoop(rotation, .8, now);
            rotation.InsertKeyFrame(0, 0);
            rotation.InsertKeyFrame(1, MathF.Tau, Linear);
            visual.StartAnimation(nameof(CompositionVisual.RotationAngle), rotation);
        }
        else if (piece.Variant == AiLoaderVariant.Scan)
        {
            if (piece.Index == 0) return;
            var shift = visual.Compositor.CreateVector3DKeyFrameAnimation();
            SetLoop(shift, 1.8, now);
            double travel = 5.5 * piece.Bounds.Height / 24;
            shift.InsertKeyFrame(0, new Vector3D(0, -travel, 0));
            shift.InsertKeyFrame(.5f, new Vector3D(0, travel, 0), LoaderGlyph.Ease);
            shift.InsertKeyFrame(1, new Vector3D(0, -travel, 0), LoaderGlyph.Ease);
            visual.StartAnimation(nameof(CompositionVisual.Translation), shift);
        }
        else
        {
            bool dots = piece.Variant == AiLoaderVariant.Dots;
            double period = dots ? 1.2 : 1.4;
            double phase = dots ? (owner is AlderChatLoader ? 2 - piece.Index : -piece.Index) * .1 : 0;
            float peak = dots ? .4f : .5f;
            float end = dots ? .8f : 1f;
            float lowOpacity = dots ? .35f : .45f;
            double lowScale = dots ? .85 : .92;

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            SetLoop(opacity, period, now, phase);
            opacity.InsertKeyFrame(0, lowOpacity);
            opacity.InsertKeyFrame(peak, 1, LoaderGlyph.Ease);
            opacity.InsertKeyFrame(end, lowOpacity, LoaderGlyph.Ease);
            if (end < 1) opacity.InsertKeyFrame(1, lowOpacity);
            visual.StartAnimation(nameof(CompositionVisual.Opacity), opacity);

            var scale = visual.Compositor.CreateVector3DKeyFrameAnimation();
            SetLoop(scale, period, now, phase);
            var start = new Vector3D(lowScale, lowScale, 1);
            scale.InsertKeyFrame(0, start);
            scale.InsertKeyFrame(peak, new Vector3D(1, 1, 1), LoaderGlyph.Ease);
            scale.InsertKeyFrame(end, start, LoaderGlyph.Ease);
            if (end < 1) scale.InsertKeyFrame(1, start);
            visual.StartAnimation(nameof(CompositionVisual.Scale), scale);
        }
    }

    private static void SetLoop(KeyFrameAnimation animation, double seconds, TimeSpan now, double offset = 0)
    {
        animation.Duration = TimeSpan.FromSeconds(seconds);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        var phase = now.TotalSeconds / seconds + offset;
        animation.DelayTime = TimeSpan.FromSeconds(-(phase - Math.Floor(phase)) * seconds);
    }

    private sealed class Piece(AiLoaderVariant variant, int index, IBrush brush, LoaderGlyph glyph) : Control
    {
        public AiLoaderVariant Variant { get; } = variant;
        public int Index { get; } = index;

        public override void Render(DrawingContext context)
        {
            var center = new Rect(Bounds.Size).Center;
            if (Variant is AiLoaderVariant.Dots or AiLoaderVariant.Pulse)
            {
                context.DrawEllipse(brush, null, center, center.X, center.Y);
                return;
            }
            double scale = Bounds.Height / 24;
            using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y));
            if (Variant == AiLoaderVariant.Ring)
            {
                if (Index == 0) glyph.DrawRingRail(context);
                else glyph.DrawRingArc(context);
            }
            else if (Index == 0) glyph.DrawScanRail(context);
            else glyph.DrawScanLine(context);
        }
    }
}
