using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

internal sealed class AlderMorphingTabContentTransition : IPageTransition
{
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken) =>
        to is ContentPresenter { TemplatedParent: AlderTabs tabs } incoming &&
        TopLevel.GetTopLevel(incoming) is { } root && !cancellationToken.IsCancellationRequested
            ? tabs.TransitionMorphingContent(from as ContentPresenter, incoming, root, cancellationToken)
            : Task.CompletedTask;
}

public sealed partial class AlderTabs
{
    private MorphingContentMotion? contentMotion;
    private bool contentWasClosed;

    internal Task TransitionMorphingContent(ContentPresenter? from, ContentPresenter to, TopLevel root, CancellationToken token)
    {
        if (from is null) return Task.CompletedTask;
        if (from.Content is null || ReferenceEquals(from.Content, to.Content))
        {
            ResetContentLayer(from);
            return Task.CompletedTask;
        }

        if (contentWasClosed) from.Opacity = 0;
        contentMotion = new MorphingContentMotion(from, to, root, AlderMotion.GetReduced(this), token);
        return contentMotion.Start();
    }

    private void PrepareMorphingContentChange()
    {
        if (contentMotion is not { IsRunning: true } motion) return;
        if (SelectedItem is null)
            motion.Complete();
        else if (motion.IncomingIsDominant)
            // Let TabControl promote the more visible presenter before it handles the next selection.
            motion.Complete(keepIncoming: true);
        else
            motion.KeepOutgoingOnCancel = true;
    }

    private void CompleteMorphingContent()
    {
        contentMotion?.Complete();
        contentMotion = null;
    }

    private static void ResetContentLayer(ContentPresenter presenter)
    {
        presenter.Opacity = 1;
        presenter.RenderTransform = null;
        presenter.Effect = null;
        presenter.IsHitTestVisible = true;
    }

    private sealed class MorphingContentMotion(
        ContentPresenter from, ContentPresenter to, TopLevel root, bool reduced, CancellationToken token)
    {
        private static readonly double PressSpringEnd = PressSpring(1);
        private readonly TaskCompletionSource completion = new();
        private readonly ContentLayer outgoing = new(from);
        private readonly ContentLayer incoming = new(to);
        private CancellationTokenRegistration cancellation;
        private long start;

        public bool IsRunning => !completion.Task.IsCompleted;
        public bool IncomingIsDominant => to.Opacity >= from.Opacity;
        public bool KeepOutgoingOnCancel { get; set; }

        public Task Start()
        {
            from.IsHitTestVisible = false;
            from.ZIndex = 0;
            to.ZIndex = 1;
            incoming.Draw(0, reduced ? 0 : 8, reduced ? 0 : 6);
            start = Stopwatch.GetTimestamp();
            cancellation = token.Register(Cancel);
            root.RequestAnimationFrame(OnFrame);
            return completion.Task;
        }

        private void OnFrame(TimeSpan _)
        {
            if (!IsRunning) return;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            double exit = EaseOut(Math.Min(1, elapsed / (reduced ? 80 : 120)));
            double enter = Math.Min(1, elapsed / (reduced ? 120 : 210));
            enter = reduced ? EaseOut(enter) : PressSpring(enter) / PressSpringEnd;

            if (outgoing.Opacity > 0)
                outgoing.Draw(outgoing.Opacity * (1 - exit), reduced ? 0 : Mix(outgoing.Offset, -5, exit),
                    reduced ? 0 : Mix(outgoing.BlurRadius, 5, exit));
            incoming.Draw(enter, reduced ? 0 : 8 * (1 - enter), reduced ? 0 : 6 * (1 - enter));
            if (enter >= 1)
                Complete();
            else
                root.RequestAnimationFrame(OnFrame);
        }

        public void Complete(bool keepIncoming = false)
        {
            if (!IsRunning) return;
            ResetContentLayer(from);
            if (!keepIncoming) ResetContentLayer(to);
            cancellation.Dispose();
            completion.TrySetResult();
        }

        private void Cancel()
        {
            if (!KeepOutgoingOnCancel) ResetContentLayer(from);
            ResetContentLayer(to);
            completion.TrySetCanceled(token);
        }

        private static double PressSpring(double progress)
        {
            double time = progress * 7;
            return 1 - (1 + time) * Math.Exp(-time);
        }

        private static double EaseOut(double progress)
        {
            double remaining = 1 - progress;
            return 1 - remaining * remaining * remaining * remaining * remaining;
        }

        private static double Mix(double start, double end, double progress) => start + (end - start) * progress;
    }

    private sealed class ContentLayer(ContentPresenter presenter)
    {
        private readonly TranslateTransform transform = presenter.RenderTransform as TranslateTransform ?? new();
        private readonly BlurEffect blur = presenter.Effect as BlurEffect ?? new();

        public double Opacity { get; } = presenter.Opacity;
        public double Offset { get; } = (presenter.RenderTransform as TranslateTransform)?.Y ?? 0;
        public double BlurRadius { get; } = (presenter.Effect as BlurEffect)?.Radius ?? 0;

        public void Draw(double opacity, double offset, double radius)
        {
            presenter.Opacity = opacity;
            transform.Y = offset;
            presenter.RenderTransform = offset == 0 ? null : transform;
            blur.Radius = radius;
            presenter.Effect = radius > .1 && opacity > .01 ? blur : null;
        }
    }
}
