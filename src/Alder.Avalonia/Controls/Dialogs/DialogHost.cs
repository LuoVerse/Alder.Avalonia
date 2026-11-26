using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Wraps the area that owns modal dialogs, drawers and message boxes.</summary>
public sealed class DialogHost : ContentControl
{
    /// <summary>Identifies the <see cref="HostId"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> HostIdProperty =
        AvaloniaProperty.Register<DialogHost, string?>(nameof(HostId));

    private readonly List<DialogLayer> layers = [];
    private readonly BlurEffect backdropBlur = new() { Radius = 12 };
    private readonly BlurEffect sheetBlur = new() { Radius = 4 };
    private ContentPresenter? page;
    private Grid? overlays;

    /// <summary>The identifier used to select this overlay host within its top level.</summary>
    public string? HostId { get => GetValue(HostIdProperty); set => SetValue(HostIdProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        AbortLayers();
        base.OnApplyTemplate(e);
        page = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        overlays = e.NameScope.Find<Grid>("PART_Overlays");
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AbortLayers();
        base.OnDetachedFromVisualTree(e);
    }

    internal static DialogHost Resolve(Control? owner, string? hostId)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (owner?.FindAncestorOfType<DialogHost>(includeSelf: true) is { } nearest &&
            (hostId is null || nearest.HostId == hostId)) return nearest;

        Visual? root = owner is null ? Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime => Dialog.FindActiveWindow(),
            ISingleViewApplicationLifetime singleView => singleView.MainView,
            _ => null
        } : TopLevel.GetTopLevel(owner);

        if (root is not null)
        {
            if (hostId is null && root is AlderWindow { DefaultDialogHost: { } windowHost } &&
                windowHost.IsAttachedToVisualTree()) return windowHost;

            var matches = root.GetSelfAndVisualDescendants().OfType<DialogHost>()
                .Where(host => host.HostId == hostId).Take(2).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1)
                throw new InvalidOperationException("Multiple dialog hosts match. Specify an owner inside a host or a unique HostId.");
        }

        throw new InvalidOperationException("No matching DialogHost. Wrap the owner content in a DialogHost.");
    }

    internal Task<DialogResult> Show(object? content, OverlayOptions options, Control? owner)
    {
        if (!this.IsAttachedToVisualTree())
            throw new InvalidOperationException("The DialogHost must be attached to a window or view.");
        ApplyTemplate();
        if (overlays is null)
            throw new InvalidOperationException("The DialogHost template must provide PART_Overlays.");

        var previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        bool nestedSheet = options is SheetOptions && layers.Count > 0 && layers[^1].IsSheet;
        var layer = new DialogLayer(content, options, options.FlowDirection ?? owner?.FlowDirection ?? FlowDirection,
            previousFocus, Finish, nestedSheet);
        layers.Add(layer);
        overlays.Children.Add(layer);
        UpdateLayers();
        return layer.Result;
    }

    private void Finish(DialogLayer layer, DialogResult result)
    {
        bool wasTop = layers.Count > 0 && layers[^1] == layer;
        if (!layers.Remove(layer)) return;
        foreach (var item in layers)
            item.ReplacePreviousFocus(layer);
        overlays?.Children.Remove(layer);
        layer.Complete(result);
        UpdateLayers();
        if (wasTop && this.IsAttachedToVisualTree() && layer.PreviousFocus?.Focus() != true && layers.Count > 0)
            layers[^1].FocusInitial();
    }

    private void AbortLayers()
    {
        if (layers.Count == 0) return;
        var pending = layers.ToArray();
        layers.Clear();
        overlays?.Children.Clear();
        foreach (var layer in pending)
            layer.Complete(DialogResult.None);
        UpdateLayers();
    }

    private void UpdateLayers()
    {
        BlurEffect? blur = null;
        bool hasNestedSheet = false;
        for (int index = layers.Count - 1; index >= 0; index--)
        {
            var layer = layers[index];
            layer.IsActive = index == layers.Count - 1;
            layer.IsHitTestVisible = layer.IsActive;
            layer.Effect = blur;
            if (layer.IsSheet) layer.SetSheetCovered(hasNestedSheet);
            hasNestedSheet |= layer.IsNestedSheet;
            if (layer.Backdrop == DialogBackdrop.Blur && (blur is null || !layer.IsSheet))
                blur = layer.IsSheet ? sheetBlur : backdropBlur;
        }
        if (page is not null) page.Effect = blur;
    }

    /// <inheritdoc />
    protected override void OnGettingFocus(FocusChangingEventArgs e)
    {
        base.OnGettingFocus(e);
        if (layers.Count == 0 || e.NewFocusedElement is not Visual next) return;
        if (!layers[^1].IsVisualAncestorOf(next)) e.TryCancel();
    }
}
