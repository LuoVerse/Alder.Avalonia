using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

internal sealed class AlderContentPageTransition : IPageTransition
{
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);
        if (to is not ContentPresenter { TemplatedParent: AlderTransitioningContentControl owner } incoming)
            return Task.CompletedTask;

        var outgoing = from as ContentPresenter;
        var mode = owner.Transition;
        var duration = owner.Duration ?? TimeSpan.FromMilliseconds(mode switch
        {
            AlderContentTransition.Slide or AlderContentTransition.Blur => 260,
            AlderContentTransition.Rise => 220,
            AlderContentTransition.Scale => 240,
            AlderContentTransition.Reveal => 300,
            _ => 180
        });

        if (mode == AlderContentTransition.None || duration <= TimeSpan.Zero ||
            (outgoing?.Content is null && incoming.Content is null) ||
            TopLevel.GetTopLevel(incoming) is not { } top)
        {
            if (outgoing is not null)
                outgoing.IsVisible = false;
            return Task.CompletedTask;
        }

        var reduced = AlderMotion.GetReduced(owner);
        if (reduced)
        {
            mode = AlderContentTransition.CrossFade;
            duration = TimeSpan.FromMilliseconds(Math.Min(duration.TotalMilliseconds, 120));
        }

        return new Playback(owner, top, outgoing, incoming, mode, duration, forward, reduced, cancellationToken).Start();
    }

    private sealed class Playback(
        AlderTransitioningContentControl owner, TopLevel top, ContentPresenter? outgoing, ContentPresenter incoming,
        AlderContentTransition mode, TimeSpan duration, bool forward, bool reduced, CancellationToken token)
    {
        private readonly TaskCompletionSource completion = new();
        private readonly Layer? from = outgoing is null ? null : new Layer(outgoing);
        private readonly Layer to = new(incoming);
        private readonly bool entering = incoming.Content is not null;
        private readonly bool exiting = outgoing?.Content is not null;
        private readonly int direction = forward ? 1 : -1;
        private CancellationTokenRegistration registration;
        private IDisposable? outgoingHitTest;
        private IDisposable? outgoingNavigation;
        private IDisposable? outgoingAccessibility;
        private long started;

        public Task Start()
        {
            if (outgoing is not null)
            {
                outgoingHitTest = outgoing.SetValue(InputElement.IsHitTestVisibleProperty, false, BindingPriority.Animation);
                outgoingNavigation = outgoing.SetValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.None, BindingPriority.Animation);
                outgoingAccessibility = outgoing.SetValue(AutomationProperties.AccessibilityViewProperty, AccessibilityView.Raw, BindingPriority.Animation);
                if (outgoing.IsKeyboardFocusWithin && !owner.Focus())
                    top.FocusManager?.Focus(null);
                outgoing.SetCurrentValue(Visual.ZIndexProperty, 0);
            }

            incoming.SetCurrentValue(Visual.ZIndexProperty, 1);
            incoming.DetachedFromVisualTree += OnDetached;
            started = Stopwatch.GetTimestamp();
            Render(0);
            registration = token.Register(Cancel);
            top.RequestAnimationFrame(OnFrame);
            return completion.Task;
        }

        private void OnFrame(TimeSpan _)
        {
            if (completion.Task.IsCompleted)
                return;

            var progress = Stopwatch.GetElapsedTime(started).TotalMilliseconds / duration.TotalMilliseconds;
            if (progress >= 1 || (!reduced && AlderMotion.GetReduced(owner)))
            {
                Complete(cancelled: false);
                return;
            }

            Render(progress);
            top.RequestAnimationFrame(OnFrame);
        }

        private void Render(double progress)
        {
            var smooth = progress * progress * (3 - 2 * progress);
            var enter = EaseOut(progress);
            var exit = EaseOut(Math.Min(1, progress / .75));
            switch (mode)
            {
                case AlderContentTransition.Slide:
                    var width = incoming.Bounds.Width;
                    from?.Transform(x: -direction * width * smooth);
                    to.Transform(x: direction * width * (1 - smooth));
                    break;
                case AlderContentTransition.Rise:
                    from?.Transform(y: -direction * 10 * exit);
                    from?.Fade(1 - exit);
                    to.Transform(y: direction * 24 * (1 - enter));
                    to.Fade(enter);
                    break;
                case AlderContentTransition.Scale:
                    from?.Transform(scale: 1 - .03 * exit);
                    from?.Fade(1 - exit);
                    to.Transform(scale: .94 + .06 * enter);
                    to.Fade(enter);
                    break;
                case AlderContentTransition.Blur:
                    from?.Blur(4 * exit);
                    from?.Fade(1 - exit);
                    to.Blur(6 * (1 - enter));
                    to.Fade(enter);
                    break;
                case AlderContentTransition.Reveal:
                    // Snap the shared edge so both clips meet at the same physical pixel.
                    var scale = top.RenderScaling;
                    var edge = Math.Round(incoming.Bounds.Width * (forward ? smooth : 1 - smooth) * scale) / scale;
                    if (exiting)
                        from?.ClipAt(edge, leftSide: !forward);
                    if (entering)
                        to.ClipAt(edge, leftSide: forward);
                    break;
                default:
                    from?.Fade(1 - smooth);
                    to.Fade(smooth);
                    break;
            }
        }

        private static double EaseOut(double value)
        {
            var remaining = 1 - value;
            return 1 - remaining * remaining * remaining;
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs args) => Complete(cancelled: token.IsCancellationRequested);

        private void Cancel()
        {
            if (Dispatcher.UIThread.CheckAccess())
                Complete(cancelled: true);
            else
                Dispatcher.UIThread.Post(() => Complete(cancelled: true));
        }

        private void Complete(bool cancelled)
        {
            if (completion.Task.IsCompleted)
                return;

            incoming.DetachedFromVisualTree -= OnDetached;
            if (!cancelled && outgoing is not null)
                outgoing.IsVisible = false;
            from?.Restore();
            to.Restore();
            outgoingHitTest?.Dispose();
            outgoingNavigation?.Dispose();
            outgoingAccessibility?.Dispose();
            registration.Dispose();

            if (cancelled)
                completion.TrySetCanceled(token);
            else
                completion.TrySetResult();
        }
    }

    private sealed class Layer(ContentPresenter presenter)
    {
        private readonly double opacity = presenter.Opacity;
        private readonly int zIndex = presenter.ZIndex;
        private ITransform? baseTransform;
        private IEffect? baseEffect;
        private Geometry? baseClip;
        private MatrixTransform? transform;
        private BlurEffect? blur;
        private bool revealing;

        public void Fade(double value) => presenter.SetCurrentValue(Visual.OpacityProperty, opacity * value);

        public void Transform(double x = 0, double y = 0, double scale = 1)
        {
            if (transform is null)
            {
                baseTransform = presenter.RenderTransform;
                transform = new MatrixTransform();
                presenter.SetCurrentValue(Visual.RenderTransformProperty, transform);
            }

            transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y);
        }

        public void Blur(double radius)
        {
            if (blur is null)
            {
                baseEffect = presenter.Effect;
                blur = new BlurEffect();
                presenter.SetCurrentValue(Visual.EffectProperty, blur);
            }

            blur.Radius = radius;
        }

        public void ClipAt(double edge, bool leftSide)
        {
            if (!revealing)
            {
                baseClip = presenter.Clip;
                revealing = true;
            }

            var bounds = presenter.Bounds.Size;
            edge = Math.Clamp(edge, 0, bounds.Width);
            var rect = leftSide ? new Rect(0, 0, edge, bounds.Height) : new Rect(edge, 0, bounds.Width - edge, bounds.Height);
            // Replace the geometry so the compositor receives a new clip with each frame.
            presenter.SetCurrentValue(Visual.ClipProperty, new RectangleGeometry(rect));
        }

        public void Restore()
        {
            presenter.SetCurrentValue(Visual.ZIndexProperty, zIndex);
            presenter.SetCurrentValue(Visual.OpacityProperty, opacity);
            if (transform is not null)
                presenter.SetCurrentValue(Visual.RenderTransformProperty, baseTransform);
            if (blur is not null)
                presenter.SetCurrentValue(Visual.EffectProperty, baseEffect);
            if (revealing)
                presenter.SetCurrentValue(Visual.ClipProperty, baseClip);
        }
    }
}
