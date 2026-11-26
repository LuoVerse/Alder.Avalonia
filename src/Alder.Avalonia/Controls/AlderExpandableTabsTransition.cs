using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

// Keep each presenter on its own template until it has finished leaving.
internal sealed class AlderExpandableTabsContent : TransitioningContentControl
{
    private sealed record ExpandableTabContent(object Value, IDataTemplate Template);

    private bool arranged;
    private bool hasPresentedContent;
    private TopLevel? firstPanelRoot;
    private bool firstPanelReady;

    public AlderExpandableTabsContent() =>
        DataTemplates.Add(new FuncDataTemplate<ExpandableTabContent>((panel, _) => new ContentPresenter
        {
            Content = panel?.Value,
            ContentTemplate = panel?.Template,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top
        }));

    protected override Size MeasureOverride(Size availableSize)
    {
        if (TemplatedParent is AlderExpandableTabs tabs)
        {
            var value = tabs.SelectedContent;
            if (value is not null && ShouldDeferFirstPanel(tabs))
                return base.MeasureOverride(availableSize);
            if (value is not null)
                hasPresentedContent = true;

            var template = tabs.SelectedContentTemplate;
            if (value is null || template is null)
                Content = value;
            else if (Content is not ExpandableTabContent previous ||
                     !ReferenceEquals(previous.Value, value) || previous.Template != template)
                Content = new ExpandableTabContent(value, template);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        arranged = true;
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        firstPanelRoot = null;
        firstPanelReady = arranged = false;
        base.OnDetachedFromVisualTree(e);
    }

    private bool ShouldDeferFirstPanel(AlderExpandableTabs tabs)
    {
        // Only start the shell before measuring its content when its open size
        // has been reserved. Intrinsically sized panels still measure in one pass.
        if (hasPresentedContent || !arranged || firstPanelReady || AlderMotion.GetReduced(tabs) ||
            tabs.ExpandedMinWidth <= 0 || tabs.ExpandedMinHeight <= AlderExpandableTabsShell.BarHeight ||
            TopLevel.GetTopLevel(this) is not { } top)
            return false;

        if (firstPanelRoot is null)
        {
            firstPanelRoot = top;
            top.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(() =>
            {
                if (firstPanelRoot != top) return;
                firstPanelRoot = null;
                if (!this.IsAttachedToVisualTree() || tabs.SelectedContent is null) return;
                firstPanelReady = true;
                InvalidateMeasure();
            }, DispatcherPriority.Background));
        }
        return true;
    }
}

internal sealed class AlderExpandableTabsTransition : IPageTransition
{
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (to is not ContentPresenter incoming || TopLevel.GetTopLevel(incoming) is not { } top ||
            cancellationToken.IsCancellationRequested) return Task.CompletedTask;

        var outgoing = from as ContentPresenter;
        if (incoming.Content is null && outgoing?.Content is null) return Task.CompletedTask;
        var motion = new ContentMotion(outgoing, incoming, top, cancellationToken);
        return motion.Start();
    }

    private sealed class ContentMotion(ContentPresenter? from, ContentPresenter to, TopLevel top, CancellationToken token)
    {
        private readonly TaskCompletionSource completion = new();
        private readonly bool entering = to.Content is not null;
        private readonly bool exiting = from?.Content is not null;
        private readonly LayerAppearance previous = ReadAppearance(from);
        private ExpandableTabsFrameClock clock;
        private long start;
        private CancellationTokenRegistration registration;

        public Task Start()
        {
            clock.Start();
            start = clock.Current;
            if (from is not null)
            {
                from.ZIndex = 0;
                from.IsHitTestVisible = false;
            }
            to.ZIndex = 1;
            to.IsHitTestVisible = entering;
            if (entering)
                Paint(to, Entrance(0, AlderMotion.GetReduced(to)));
            registration = token.Register(Cancel);
            top.RequestAnimationFrame(OnFrame);
            return completion.Task;
        }

        private void OnFrame(TimeSpan _)
        {
            if (completion.Task.IsCompleted) return;
            if (!to.IsAttachedToVisualTree())
            {
                Stop();
                return;
            }

            bool reduced = AlderMotion.GetReduced(to);
            double elapsed = Stopwatch.GetElapsedTime(start, clock.Advance()).TotalMilliseconds;
            double exit = ExpandableTabsMotion.FadeOut(Math.Min(elapsed / 80, 1));
            double enter = reduced
                ? ExpandableTabsMotion.FadeOut(Math.Min(elapsed / 150, 1))
                : ExpandableTabsMotion.Enter(Math.Min(elapsed / 460, 1));
            if (exiting && from is not null)
                Paint(from, reduced
                    ? new(previous.Opacity * (1 - exit), 0, 1, 0)
                    : new(previous.Opacity * (1 - exit),
                        ExpandableTabsMotion.Mix(previous.Y, -6, exit),
                        ExpandableTabsMotion.Mix(previous.Scale, .98, exit),
                        ExpandableTabsMotion.Mix(previous.Blur, 4, exit)));
            if (entering)
                Paint(to, Entrance(enter, reduced));

            if (entering ? enter >= 1 : exit >= 1)
                Stop();
            else
                top.RequestAnimationFrame(OnFrame);
        }

        private void Stop()
        {
            if (from is not null)
            {
                Reset(from);
                from.IsVisible = false;
            }
            Reset(to);
            registration.Dispose();
            completion.TrySetResult();
        }

        private void Cancel() => completion.TrySetCanceled(token);

        private LayerAppearance Entrance(double progress, bool reduced)
        {
            if (reduced) return new(progress, 0, 1, 0);
            double remaining = 1 - progress;
            // On open the growing viewport reveals the panel. Paint it directly
            // so we don't need a separate compositing pass for its first draw.
            return exiting
                ? new(progress, -8 * remaining, 1 - .02 * remaining, 4 * remaining)
                : new(1, -8 * remaining, 1, 0);
        }

        private static LayerAppearance ReadAppearance(ContentPresenter? layer)
        {
            var matrix = (layer?.RenderTransform as MatrixTransform)?.Matrix ?? Matrix.Identity;
            return new(layer?.Opacity ?? 1, matrix.M32, matrix.M11, (layer?.Effect as BlurEffect)?.Radius ?? 0);
        }

        private static void Paint(Control layer, LayerAppearance appearance)
        {
            layer.Opacity = appearance.Opacity;
            if (appearance.Y == 0 && appearance.Scale == 1)
                layer.RenderTransform = null;
            else
            {
                var transform = layer.RenderTransform as MatrixTransform ?? new MatrixTransform();
                transform.Matrix = Matrix.CreateScale(appearance.Scale, appearance.Scale) * Matrix.CreateTranslation(0, appearance.Y);
                layer.RenderTransform = transform;
            }
            if (appearance.Opacity > .02 && appearance.Blur > .12)
            {
                var blur = layer.Effect as BlurEffect ?? new BlurEffect();
                blur.Radius = appearance.Blur;
                layer.Effect = blur;
            }
            else
                layer.Effect = null;
        }

        private static void Reset(Control layer)
        {
            Paint(layer, new(1, 0, 1, 0));
            layer.IsHitTestVisible = true;
        }
    }

    private readonly record struct LayerAppearance(double Opacity, double Y, double Scale, double Blur);
}
