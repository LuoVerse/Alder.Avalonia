using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class AlderUserMenuChevron : Decorator
{
    private static readonly ConditionalWeakTable<Compositor, ImplicitAnimationCollection> Rotations = new();
    private const string RotationProperty = nameof(CompositionVisual.RotationAngle);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AlderUserMenuChevron, bool>(nameof(IsOpen));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateRotation(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UpdateRotation(animate: false);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            UpdateRotation(animate: true);
        else if (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this))
            UpdateRotation(animate: false);
        else if (change.Property == BoundsProperty && ElementComposition.GetElementVisual(this) is { } visual)
            UpdateCenter(visual);
    }

    private void UpdateRotation(bool animate)
    {
        if (ElementComposition.GetElementVisual(this) is not { } visual)
        {
            RenderTransform = IsOpen ? new RotateTransform(180) : null;
            return;
        }

        if (RenderTransform is not null) RenderTransform = null;
        UpdateCenter(visual);
        var angle = IsOpen ? MathF.PI : 0;
        if (animate && IsEffectivelyVisible && !AlderMotion.GetReduced(this))
        {
            visual.ImplicitAnimations ??= Rotations.GetValue(visual.Compositor, CreateRotation);
        }
        else if (visual.ImplicitAnimations is not null)
        {
            visual.ImplicitAnimations = null;
            visual.StopAnimation(RotationProperty);
            // The client already has the target during an implicit animation.
            // Force that value into the commit when reduced motion interrupts it.
            if (visual.RotationAngle == angle) visual.RotationAngle = IsOpen ? 0 : MathF.PI;
        }
        visual.RotationAngle = angle;
    }

    private void UpdateCenter(CompositionVisual visual) =>
        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);

    private static ImplicitAnimationCollection CreateRotation(Compositor compositor)
    {
        var rotation = compositor.CreateScalarKeyFrameAnimation();
        rotation.Target = RotationProperty;
        rotation.Duration = TimeSpan.FromMilliseconds(150);
        rotation.InsertExpressionKeyFrame(1, "this.FinalValue", new SineEaseInOut());
        var animations = compositor.CreateImplicitAnimationCollection();
        animations[RotationProperty] = rotation;
        return animations;
    }
}
