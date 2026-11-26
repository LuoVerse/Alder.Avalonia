using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual intent of a notification and its default indicator.</summary>
public enum ToastVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the accent color.</summary>
    Accent,
    /// <summary>Uses the success treatment.</summary>
    Success,
    /// <summary>Uses the warning treatment.</summary>
    Warning,
    /// <summary>Uses the danger treatment.</summary>
    Danger,
    /// <summary>Uses the visual treatment for an ongoing operation.</summary>
    Loading
}
/// <summary>Places a notification stack at a vertical edge and optional logical corner.</summary>
public enum ToastPlacement
{
    /// <summary>Centers the stack along the bottom edge.</summary>
    Bottom,
    /// <summary>Aligns the stack to the bottom edge at the logical start.</summary>
    BottomStart,
    /// <summary>Aligns the stack to the bottom edge at the logical end.</summary>
    BottomEnd,
    /// <summary>Centers the stack along the top edge.</summary>
    Top,
    /// <summary>Aligns the stack to the top edge at the logical start.</summary>
    TopStart,
    /// <summary>Aligns the stack to the top edge at the logical end.</summary>
    TopEnd
}
/// <summary>Describes what ended the lifetime of a toast notification.</summary>
public enum ToastCloseReason
{
    /// <summary>Closed through a dismissal action or the handle.</summary>
    Dismissed,
    /// <summary>Closed when the configured duration elapsed.</summary>
    Expired,
    /// <summary>Closed after the notification action was invoked.</summary>
    Action,
    /// <summary>Closed when its host was detached or unregistered.</summary>
    HostRemoved,
    /// <summary>Removed from the tail when the stack exceeded its visible limit.</summary>
    Evicted
}

/// <summary>Configures the content, lifetime, placement, and optional action of a toast notification.</summary>
/// <param name="Title">The main text for the notification.</param>
public sealed record ToastOptions(string Title)
{
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get; init; }
    /// <summary>Replaces the default icon and message area, with the action and close buttons kept alongside it.</summary>
    public object? Content { get; init; }
    /// <summary>The data template used to present the content.</summary>
    public IDataTemplate? ContentTemplate { get; init; }
    /// <summary>The visual treatment and default indicator applied to the notification.</summary>
    public ToastVariant Variant { get; init; }
    /// <summary>The per-toast placement override. Null uses the host's placement.</summary>
    public ToastPlacement? Placement { get; init; }
    /// <summary>Zero or Timeout.InfiniteTimeSpan keeps the toast open until dismissed.</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(4);
    /// <summary>An optional replacement for the notification's default indicator.</summary>
    public Geometry? Indicator { get; init; }
    /// <summary>Whether to show the indicator.</summary>
    public bool ShowIndicator { get; init; } = true;
    /// <summary>Whether to show the close button.</summary>
    public bool ShowCloseButton { get; init; } = true;
    /// <summary>The label for the optional notification action.</summary>
    public string? ActionText { get; init; }
    /// <summary>The command executed when the toast action is invoked.</summary>
    public ICommand? Action { get; init; }
    /// <summary>The argument passed to the toast action command.</summary>
    public object? ActionParameter { get; init; }
}

/// <summary>Provides access to the completion and dismissal of a single notification.</summary>
public sealed class ToastHandle
{
    private readonly TaskCompletionSource<ToastCloseReason> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Action? Dismiss { get; set; }
    internal Func<ToastOptions, bool>? Update { get; set; }
    /// <summary>A task that completes with the reason the toast closed.</summary>
    public Task<ToastCloseReason> Completion => completion.Task;

    /// <summary>Dismisses the notification if it is still active. Call this on the UI thread.</summary>
    public void Close()
    {
        Dispatcher.UIThread.VerifyAccess();
        Dismiss?.Invoke();
    }

    /// <summary>Replaces the content in the same stack position and restarts its duration.</summary>
    public bool TryUpdate(ToastOptions options)
    {
        Dispatcher.UIThread.VerifyAccess();
        Toast.Validate(options);
        return Update?.Invoke(options) ?? false;
    }

    internal void Complete(ToastCloseReason reason)
    {
        Dismiss = null;
        Update = null;
        completion.TrySetResult(reason);
    }
}

/// <summary>Displays notifications through a registered toast host.</summary>
public static class Toast
{
    /// <summary>Registers a named or default host in the top level's overlay layer.</summary>
    public static ToastHost Register(TopLevel owner, string? hostId = null) => ToastHost.Register(owner, hostId);

    internal static void Validate(ToastOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Duration < TimeSpan.Zero && options.Duration != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options), "Toast duration cannot be negative.");
    }

    /// <summary>Displays a notification through the resolved host and returns a handle to its lifetime.</summary>
    /// <param name="options">The notification to display.</param>
    /// <param name="owner">An optional control whose top level supplies the host.</param>
    /// <param name="hostId">The name of the host, or null for the default host.</param>
    public static ToastHandle Show(ToastOptions options, Control? owner = null, string? hostId = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        var root = owner is not null
            ? TopLevel.GetTopLevel(owner)
            : Dialog.FindActiveWindow() ?? TopLevel.GetTopLevel(
                (Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)?.MainView);
        var host = root is null ? null : ToastHost.Find(root, hostId);
        if (host is null && hostId is null && root is AlderWindow window)
            host = window.EnsureDefaultToastHost();
        if (host is null)
            throw new InvalidOperationException("No ToastHost is registered for this window and host ID.");
        return host.Show(options);
    }
}
