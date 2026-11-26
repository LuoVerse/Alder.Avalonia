using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;

namespace Alder.Avalonia.Controls;

/// <summary>Rotates the pending icon on the compositor.</summary>
public sealed class ButtonSpinner : PathIcon
{
    /// <summary>Identifies the <see cref="SpinDuration"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> SpinDurationProperty =
        AvaloniaProperty.Register<ButtonSpinner, TimeSpan>(nameof(SpinDuration), FeedbackMotion.DefaultSpinDuration,
            validate: value => value > TimeSpan.Zero);

    /// <summary>The duration of one full rotation.</summary>
    public TimeSpan SpinDuration { get => GetValue(SpinDurationProperty); set => SetValue(SpinDurationProperty, value); }

    private CompositionVisual? visual;
    private bool spinning;
    private bool withinViewport = true;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(PathIcon);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        withinViewport = true;
        EffectiveViewportChanged += ViewportChanged;
        visual = ElementComposition.GetElementVisual(this);
        PositionCenter();
        UpdateSpin();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= ViewportChanged;
        StopSpin();
        visual = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) PositionCenter();
        else if (change.Property == IsVisibleProperty || change.Property == AlderMotion.ReducedProperty) UpdateSpin();
        else if (change.Property == SpinDurationProperty)
        {
            StopSpin();
            UpdateSpin();
        }
    }

    private void ViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        withinViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
        UpdateSpin();
    }

    private void PositionCenter()
    {
        if (visual is not null)
            visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
    }

    private void UpdateSpin()
    {
        if (visual is null) return;
        if (!IsEffectivelyVisible || !withinViewport || AlderMotion.GetReduced(this))
        {
            StopSpin();
            return;
        }
        if (spinning) return;

        FeedbackMotion.Spin(visual, SpinDuration);
        spinning = true;
    }

    private void StopSpin()
    {
        if (!spinning) return;
        FeedbackMotion.StopSpin(visual!);
        spinning = false;
    }
}
