using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;

namespace Alder.Avalonia.Controls;

/// <summary>Animates the template surface on the compositor while the button keeps its input bounds.</summary>
public sealed class ButtonFeedback : Border
{
    /// <summary>Identifies the <see cref="PressScale"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> PressScaleProperty =
        AvaloniaProperty.Register<ButtonFeedback, double>(nameof(PressScale), 1);
    /// <summary>Identifies the <see cref="ActiveScale"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ActiveScaleProperty =
        AvaloniaProperty.Register<ButtonFeedback, double>(nameof(ActiveScale), 1);
    /// <summary>The scale used while the button is held down.</summary>
    public double PressScale { get => GetValue(PressScaleProperty); set => SetValue(PressScaleProperty, value); }
    /// <summary>The current scale calculated from the button's interaction state.</summary>
    public double ActiveScale { get => GetValue(ActiveScaleProperty); set => SetValue(ActiveScaleProperty, value); }

    /// <summary>Initialize the compositor animation when the surface first changes scale.</summary>
    public bool DeferMotion { get; init; }

    private CompositionVisual? visual;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!DeferMotion || PressScale != 1 || ActiveScale != 1)
            AttachMotion();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        visual = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (visual is null)
        {
            if (DeferMotion && (change.Property == PressScaleProperty || change.Property == ActiveScaleProperty)
                && (PressScale != 1 || ActiveScale != 1) && VisualRoot is not null)
                AttachMotion();
            return;
        }
        if (change.Property == BoundsProperty)
            visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
        else if (change.Property == PressScaleProperty || change.Property == ActiveScaleProperty)
            SetScale();
        else if (change.Property == AlderMotion.ReducedProperty)
            ConfigureMotion();
    }

    private void AttachMotion()
    {
        visual = ElementComposition.GetElementVisual(this);
        ConfigureMotion();
    }

    private void ConfigureMotion()
    {
        if (visual is null) return;
        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
        AlderPressableFeedback.ConfigureSurfaceScale(visual, AlderMotion.GetReduced(this));
        SetScale();
    }

    private void SetScale() =>
        AlderPressableFeedback.UpdateSurfaceScale(visual!, PressScale, ActiveScale, AlderMotion.GetReduced(this));
}

/// <summary>Cross-fades the variant's hover fill without invalidating the UI on each frame.</summary>
public sealed class ButtonTint : Border
{
    internal bool FeedbackFade { get; init; }
    /// <summary>Initialize the compositor animation when the tint first changes opacity.</summary>
    public bool DeferMotion { get; init; }
    private CompositionVisual? visual;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!DeferMotion || Opacity != 0)
            AttachMotion((float)Opacity);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        visual = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (visual is null && DeferMotion && change.Property == OpacityProperty && VisualRoot is not null)
        {
            AttachMotion((float)change.GetOldValue<double>());
            if (visual is not null) visual.Opacity = (float)Opacity;
        }
        if (change.Property == AlderMotion.ReducedProperty) ConfigureMotion();
    }

    private void AttachMotion(float opacity)
    {
        visual = ElementComposition.GetElementVisual(this);
        if (visual is not null)
            AlderPressableFeedback.ConfigureSurfaceTint(visual, opacity, AlderMotion.GetReduced(this), FeedbackFade);
    }

    private void ConfigureMotion()
    {
        if (visual is null) return;
        AlderPressableFeedback.ConfigureSurfaceTint(visual, (float)Opacity, AlderMotion.GetReduced(this), FeedbackFade);
    }
}
