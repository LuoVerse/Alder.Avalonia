using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Alder.Avalonia.Gallery.Controls;

/// <summary>Starts a local stream on first exposure and cancels it when the Gallery releases the preview.</summary>
public sealed class AiLivePreview : Border
{
    private bool started;
    private AiExampleViewModel? model;
    public AiLivePreview() => EffectiveViewportChanged += OnViewportChanged;

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        if (started || !IsEffectivelyVisible || !new Rect(Bounds.Size).Intersects(e.EffectiveViewport) ||
            DataContext is not AiExampleViewModel current) return;
        started = true;
        model = current;
        _ = current.PlayPreviewAsync(Alder.Avalonia.Controls.AlderMotion.GetReduced(this));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        model?.CancelPreview();
        model = null;
        started = false;
        base.OnDetachedFromVisualTree(e);
    }
}
