using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Opens a modal overlay or a separate owned window and returns its dismissal result.</summary>
public static class Dialog
{
    /// <summary>Shows an overlay in the selected host and waits until it closes.</summary>
    public static Task<DialogResult> ShowModalAsync(
        object? content, DialogOptions? options = null, Control? owner = null, string? hostId = null)
        => DialogHost.Resolve(owner, hostId).Show(content, options ?? new DialogOptions(), owner);

    /// <summary>Creates a view for the supplied model and shows it in a dialog host.</summary>
    public static Task<DialogResult> ShowModalAsync<TView>(
        object viewModel, DialogOptions? options = null, Control? owner = null, string? hostId = null)
        where TView : UserControl, new()
    {
        var host = DialogHost.Resolve(owner, hostId);
        return host.Show(CreateView<TView>(viewModel), options ?? new DialogOptions(), owner);
    }

    /// <summary>Opens a separate native window owned by <paramref name="owner"/>.</summary>
    public static Task<DialogResult> ShowWindowAsync(Window owner, object? content, DialogOptions? options = null)
        => DialogWindow.ShowAsync(owner, content, options ?? new DialogOptions());

    /// <summary>Creates a view for the supplied model and opens it in a native window.</summary>
    /// <remarks>With no owner, the active application window is used.</remarks>
    public static Task<DialogResult> ShowWindowAsync<TView>(
        object viewModel, DialogOptions? options = null, Window? owner = null)
        where TView : UserControl, new()
    {
        Dispatcher.UIThread.VerifyAccess();
        owner ??= FindActiveWindow() ?? throw new InvalidOperationException("No owner window is available.");
        return ShowWindowAsync(owner, CreateView<TView>(viewModel), options);
    }

    internal static Window? FindActiveWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow
            : null;

    internal static TView CreateView<TView>(object viewModel) where TView : UserControl, new()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new TView { DataContext = viewModel };
    }

    /// <summary>Closes the dialog, drawer or message box enclosing a custom content or footer control.</summary>
    public static bool Close(Control source, DialogResult result = DialogResult.Cancel)
    {
        ArgumentNullException.ThrowIfNull(source);
        Dispatcher.UIThread.VerifyAccess();
        var surface = source.FindAncestorOfType<DialogSurface>(includeSelf: true);
        return surface?.RequestClose(result) ?? false;
    }
}

/// <summary>Shows content in an edge-aligned overlay and returns the result when it closes.</summary>
public static class Drawer
{
    /// <summary>Shows a drawer in the resolved host and completes when the drawer closes.</summary>
    /// <param name="content">The content presented in the overlay.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">An optional control used to resolve the overlay host.</param>
    /// <param name="hostId">The host identifier, or null for the default host.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowAsync(
        object? content, DrawerOptions? options = null, Control? owner = null, string? hostId = null)
        => DialogHost.Resolve(owner, hostId).Show(content, options ?? new DrawerOptions(), owner);

    /// <summary>Creates a view for the supplied model and shows a drawer in the resolved host and completes when the drawer closes.</summary>
    /// <typeparam name="TView">The user control created for the model.</typeparam>
    /// <param name="viewModel">The data context assigned to the new view.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">An optional control used to resolve the overlay host.</param>
    /// <param name="hostId">The host identifier, or null for the default host.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowAsync<TView>(
        object viewModel, DrawerOptions? options = null, Control? owner = null, string? hostId = null)
        where TView : UserControl, new()
        => ShowAsync(Dialog.CreateView<TView>(viewModel), options, owner, hostId);
}

/// <summary>Shows a message and optional response buttons in an overlay or owned window.</summary>
public static class MessageBox
{
    /// <summary>Shows a message box in the resolved overlay host and completes when it closes.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">An optional control used to resolve the overlay host.</param>
    /// <param name="hostId">The host identifier, or null for the default host.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowAsync(
        string message, MessageBoxOptions? options = null, Control? owner = null, string? hostId = null)
        => DialogHost.Resolve(owner, hostId).Show(message, options ?? new MessageBoxOptions(), owner);

    /// <summary>Opens an owned native message box and completes when it closes.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">The owning window.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowWindowAsync(Window owner, string message, MessageBoxOptions? options = null)
        => DialogWindow.ShowAsync(owner, message, options ?? new MessageBoxOptions());
}
