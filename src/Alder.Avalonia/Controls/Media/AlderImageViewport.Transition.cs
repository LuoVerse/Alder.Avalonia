using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

internal sealed partial class AlderImageViewport
{
    private static readonly TimeSpan SwitchDuration = TimeSpan.FromMilliseconds(200);
    private static readonly Vector3D UnitScale = new(1, 1, 1);
    private IDisposable? _switchTimer;
    private AlderImageViewTransition _activeTransition;

    private void StartImageTransition(AlderImageViewTransition transition, int direction)
    {
        var incoming = ElementComposition.GetElementVisual(_currentLayer)!;
        var outgoing = ElementComposition.GetElementVisual(_outgoingLayer)!;
        _activeTransition = transition;

        switch (transition)
        {
            case AlderImageViewTransition.Slide:
                var distance = Math.Clamp(Bounds.Width * 0.12, 20, 64) * direction;
                AnimateVector(incoming, nameof(CompositionVisual.Translation), SwitchDuration, default, new Vector3D(distance, 0, 0));
                AnimateVector(outgoing, nameof(CompositionVisual.Translation), SwitchDuration, new Vector3D(-distance * 0.45, 0, 0), default(Vector3D));
                break;
            case AlderImageViewTransition.Zoom:
                incoming.CenterPoint = new Vector3D(Center.X, Center.Y, 0);
                AnimateVector(incoming, nameof(CompositionVisual.Scale), SwitchDuration, UnitScale, new Vector3D(0.9, 0.9, 1));
                break;
            case AlderImageViewTransition.Lift:
                var lift = Math.Clamp(Bounds.Height * 0.08, 12, 32);
                AnimateVector(incoming, nameof(CompositionVisual.Translation), SwitchDuration, default, new Vector3D(0, lift, 0));
                break;
        }

        // The old image remains fully visible during the start of the reveal.
        AnimateOpacity(incoming, from: 0, to: 1);
        AnimateOpacity(outgoing, from: 1, to: 0, hold: 0.35f);
        _switchTimer = DispatcherTimer.RunOnce(FinishImageTransition, SwitchDuration, DispatcherPriority.Background);
    }

    public void FinishImageTransition()
    {
        if (_switchTimer is null) return;
        _switchTimer.Dispose();
        _switchTimer = null;
        ResetLayer(_currentLayer, _activeTransition, incoming: true);
        ResetLayer(_outgoingLayer, _activeTransition, incoming: false);
        _outgoingLayer.Picture.Source = null;
    }

    private static void ResetLayer(ImageLayer layer, AlderImageViewTransition transition, bool incoming)
    {
        if (ElementComposition.GetElementVisual(layer) is not { } visual) return;
        visual.StopAnimation(nameof(CompositionVisual.Opacity));
        // Force final values into the same commit even when the client already
        // reports those values and the compositor is still between keyframes.
        visual.Opacity = 0;
        visual.Opacity = 1;
        if (transition == AlderImageViewTransition.Slide || (incoming && transition == AlderImageViewTransition.Lift))
        {
            visual.StopAnimation(nameof(CompositionVisual.Translation));
            visual.Translation = new Vector3D(1, 0, 0);
            visual.Translation = default;
        }
        if (incoming && transition == AlderImageViewTransition.Zoom)
        {
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.Scale = new Vector3D(0.9, 0.9, 1);
            visual.Scale = UnitScale;
            visual.CenterPoint = default;
        }
    }

    private static void AnimateOpacity(CompositionVisual visual, float from, float to, float hold = 0)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = SwitchDuration;
        animation.InsertKeyFrame(0, from);
        if (hold > 0) animation.InsertKeyFrame(hold, from);
        animation.InsertKeyFrame(1, to, EaseOut);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), animation);
    }

    private sealed class ImageLayer : Canvas
    {
        public Image Picture { get; } = new() { Stretch = Stretch.Fill };

        public ImageLayer()
        {
            IsHitTestVisible = false;
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
            AutomationProperties.SetAccessibilityView(Picture, AccessibilityView.Raw);
            Children.Add(Picture);
        }
    }
}
