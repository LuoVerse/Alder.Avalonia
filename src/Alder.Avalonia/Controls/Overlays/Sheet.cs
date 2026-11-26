using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Extends drawer options with snap points and sheet-specific dismissal and drag behavior.</summary>
public sealed record SheetOptions : DrawerOptions
{
    /// <summary>Adds an eight-DIP inset on each edge and rounds every corner.</summary>
    public bool IsDetached { get; init; }

    /// <summary>Optional maximum width of a top or bottom sheet.</summary>
    public double? MaxWidth { get; init; }

    /// <summary>Partial stops from .05 to 1 as fractions, or above 1 as DIPs.</summary>
    public IReadOnlyList<double>? SnapPoints { get; init; }
    /// <summary>Preferred opening extent, using the same units as the stops. Defaults to the smallest stop.</summary>
    public double? InitialSnapPoint { get; init; }
    /// <summary>Whether the backdrop fades as the sheet is dragged past its last snap point.</summary>
    public bool FadeBackdropAfterLastSnap { get; init; }
    /// <summary>Whether dragging the sheet requires starting from its handle.</summary>
    public bool DragFromHandleOnly { get; init; }
}

/// <summary>A resizable edge sheet hosted in the application window.</summary>
public static class Sheet
{
    /// <summary>Shows an edge sheet in the resolved host and completes when the sheet closes.</summary>
    /// <param name="content">The content presented in the overlay.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">An optional control used to resolve the overlay host.</param>
    /// <param name="hostId">The host identifier, or null for the default host.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowAsync(
        object? content, SheetOptions? options = null, Control? owner = null, string? hostId = null)
        => DialogHost.Resolve(owner, hostId).Show(content, options ?? new SheetOptions(), owner);

    /// <summary>Creates a view for the supplied model and shows an edge sheet in the resolved host and completes when the sheet closes.</summary>
    /// <typeparam name="TView">The user control created for the model.</typeparam>
    /// <param name="viewModel">The data context assigned to the new view.</param>
    /// <param name="options">Optional settings for presentation and dismissal.</param>
    /// <param name="owner">An optional control used to resolve the overlay host.</param>
    /// <param name="hostId">The host identifier, or null for the default host.</param>
    /// <returns>A task that completes with the dismissal result.</returns>
    public static Task<DialogResult> ShowAsync<TView>(
        object viewModel, SheetOptions? options = null, Control? owner = null, string? hostId = null)
        where TView : UserControl, new()
        => ShowAsync(Dialog.CreateView<TView>(viewModel), options, owner, hostId);
}
