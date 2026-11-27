using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderImageView
{
    private OverlayLayer? overlayLayer;
    private ThemeVariantScope? overlayScope;
    private IInputElement? previousFocus;
    private double inlineMinimumHeight;

    private bool OpenImmersive(NavigationMethod method)
    {
        if (IsImmersive) return true;
        if (!AllowImmersivePreview || shownSource is null || topLevel is null ||
            !this.IsAttachedToVisualTree() ||
            inlineHost is null || surface is null || viewport is null ||
            OverlayLayer.GetOverlayLayer(this) is not { } layer) return false;

        var before = BoundsInTopLevel();
        previousFocus = topLevel.FocusManager?.GetFocusedElement() ?? open;
        overlayLayer = layer;
        inlineMinimumHeight = inlineHost.MinHeight;
        inlineHost.SetCurrentValue(MinHeightProperty, Math.Max(inlineMinimumHeight, inlineHost.Bounds.Height));
        inlineHost.Child = null;

        // Keep the same viewport and tools so zoom, pan, selection, and input live in one place.
        overlayScope = new ThemeVariantScope { Name = "AlderImageViewOverlay", Child = surface };
        KeyboardNavigation.SetTabNavigation(overlayScope, KeyboardNavigationMode.Cycle);
        SyncOverlayAppearance();
        UpdateOverlaySize();
        layer.PropertyChanged += OnOverlaySizeChanged;
        topLevel.PropertyChanged += OnOverlaySizeChanged;
        layer.Children.Add(overlayScope);
        isImmersive = true;
        RefreshLayout();
        topLevel.UpdateLayout();
        AnimateFrom(before);
        if (close?.Focus(method) != true) viewport.Focus(method);
        RaisePropertyChanged(IsImmersiveProperty, false, true);
        return IsImmersive;
    }

    private void CloseImmersive(bool restoreFocus, NavigationMethod method = NavigationMethod.Unspecified)
    {
        if (overlayLayer is not { } layer || overlayScope is not { } scope) return;
        var before = restoreFocus ? BoundsInTopLevel() : null;
        layer.PropertyChanged -= OnOverlaySizeChanged;
        if (topLevel is not null) topLevel.PropertyChanged -= OnOverlaySizeChanged;

        scope.Child = null;
        layer.Children.Remove(scope);
        overlayLayer = null;
        overlayScope = null;
        isImmersive = false;
        if (inlineHost is not null)
        {
            inlineHost.Child = surface;
            inlineHost.SetCurrentValue(MinHeightProperty, inlineMinimumHeight);
        }

        RefreshLayout();
        if (restoreFocus)
        {
            topLevel?.UpdateLayout();
            AnimateFrom(before);
            if (previousFocus is not Visual visual || !visual.IsAttachedToVisualTree() ||
                previousFocus.Focus(method) != true)
                open?.Focus(method);
        }
        previousFocus = null;
        RaisePropertyChanged(IsImmersiveProperty, true, false);
    }

    private void OnOverlaySizeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == TopLevel.ClientSizeProperty)
            UpdateOverlaySize();
    }

    private void UpdateOverlaySize()
    {
        if (overlayScope is null || overlayLayer is null || topLevel is null) return;
        var size = overlayLayer.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) size = topLevel.ClientSize;
        overlayScope.Width = size.Width;
        overlayScope.Height = size.Height;
    }

    private void SyncOverlayAppearance()
    {
        if (overlayScope is null) return;
        overlayScope.RequestedThemeVariant = ActualThemeVariant;
        overlayScope.FlowDirection = FlowDirection;
        AlderMotion.SetReduced(overlayScope, AlderMotion.GetReduced(this));
    }

    private Rect? BoundsInTopLevel()
    {
        if (viewport is null || topLevel is null ||
            viewport.TranslatePoint(viewport.ImageBounds.Position, topLevel) is not { } origin) return null;
        return new Rect(origin, viewport.ImageBounds.Size);
    }

    private void AnimateFrom(Rect? before)
    {
        if (before is not { } bounds || viewport is null || topLevel is null ||
            topLevel.TranslatePoint(bounds.Position, viewport) is not { } local) return;
        viewport.AnimateLayoutFrom(new Rect(local, bounds.Size));
    }
}
