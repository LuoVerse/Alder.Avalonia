using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

/// <summary>A desktop transition window that can show a replacement window before closing.</summary>
/// <remarks>
/// The inherited AlderWindow template provides local message box and toast hosts. When this is the desktop
/// application's main window, a successful handoff assigns the replacement as its new main window.
/// </remarks>
public abstract class AlderSalashWindow : AlderWindow
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(200);
    private static readonly CubicEaseOut FadeEase = new();
    private bool transitioning;
    private bool committed;
    private bool closed;
    private TaskCompletionSource? fadeCompleted;
    private IDialogContext? dialogContext;
    private EventHandler<DialogResult>? requestCloseHandler;
    private int dialogContextVersion;
    private DialogResult? requestedResult;

    /// <summary>Raised when the close check or creation of the replacement window fails.</summary>
    public event EventHandler<Exception>? TransitionFailed;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(AlderSalashWindow);

    /// <summary>The current close request from the window's <see cref="IDialogContext"/>. A window close uses None.</summary>
    protected DialogResult CloseResult => requestedResult ?? DialogResult.None;

    /// <summary>Asynchronously determines whether the window can begin its handoff.</summary>
    protected virtual Task<bool> CanClose() => Task.FromResult(true);

    /// <summary>Creates the next window on the UI thread. Return null to close without opening another window.</summary>
    protected abstract Task<Window?> CreateNextWindow();

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        SetDialogContext(DataContext as IDialogContext);
        base.OnOpened(e);
    }

    /// <inheritdoc />
    protected sealed override void OnClosing(WindowClosingEventArgs e)
    {
        if (committed) return;
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown or WindowCloseReason.OwnerWindowClosing)
        {
            base.OnClosing(e);
            return;
        }
        if (transitioning)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
        if (e.Cancel) return;
        e.Cancel = true;
        transitioning = true;
        // Complete the current Closing event before attempting a second native close.
        Dispatcher.UIThread.Post(() => _ = HandoffAsync());
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        closed = true;
        SetDialogContext(null);
        fadeCompleted?.TrySetResult();
        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty && !closed)
            SetDialogContext(IsVisible ? DataContext as IDialogContext : null);
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
            fadeCompleted?.TrySetResult();
    }

    private void SetDialogContext(IDialogContext? context)
    {
        if (ReferenceEquals(dialogContext, context)) return;
        if (dialogContext is not null) dialogContext.RequestClose -= requestCloseHandler;

        dialogContext = context;
        requestCloseHandler = null;
        var version = ++dialogContextVersion;
        if (context is null) return;

        requestCloseHandler = (_, result) => Dispatcher.UIThread.Post(() =>
        {
            if (version != dialogContextVersion || closed || !IsVisible || transitioning) return;
            requestedResult = result;
            try { Close(); }
            finally { if (!transitioning) requestedResult = null; }
        });
        context.RequestClose += requestCloseHandler;
    }

    private async Task HandoffAsync()
    {
        try
        {
            if (closed) return;
            var canClose = await CanClose();
            if (!canClose || closed) return;
            var next = await CreateNextWindow();
            if (closed) return;
            if (ReferenceEquals(next, this))
                throw new InvalidOperationException("CreateNextWindow must return a different window.");

            bool restoreActivationAfterModalClose = false;
            if (next is not null)
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var previous = desktop?.MainWindow;
                var replaceMain = desktop is not null && (previous is null || ReferenceEquals(previous, this));
                if (replaceMain) desktop!.MainWindow = next;
                try
                {
                    if (!next.IsVisible)
                    {
                        restoreActivationAfterModalClose = IsDialog && next.ShowActivated;
                        next.Show();
                    }
                }
                catch
                {
                    if (replaceMain && ReferenceEquals(desktop!.MainWindow, next))
                        desktop.MainWindow = previous;
                    throw;
                }

                await FadeOutAsync();
            }

            if (closed) return;
            committed = true;
            if (requestedResult is { } result) Close(result);
            else Close();
            // Avalonia reactivates the modal owner on close. Restore the new window's focus in that case.
            if (restoreActivationAfterModalClose && next is { IsVisible: true }) next.Activate();
        }
        catch (Exception error)
        {
            if (!closed) TransitionFailed?.Invoke(this, error);
        }
        finally
        {
            requestedResult = null;
            transitioning = false;
        }
    }

    private async Task FadeOutAsync()
    {
        if (!CanFadeOut || ElementComposition.GetElementVisual(this) is not { } visual) return;

        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = FadeDuration;
        animation.InsertKeyFrame(0, 1);
        animation.InsertKeyFrame(1, 0, FadeEase);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), animation);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fadeCompleted = completion;
        using var timer = DispatcherTimer.RunOnce(() => completion.TrySetResult(), FadeDuration);
        await completion.Task;
        fadeCompleted = null;
    }

    private bool CanFadeOut => !closed && IsVisible && !AlderMotion.GetReduced(this);
}
