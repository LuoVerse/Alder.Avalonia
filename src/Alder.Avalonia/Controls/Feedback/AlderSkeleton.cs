using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Alder.Avalonia.Controls;

/// <summary>Selects how a loading placeholder animates.</summary>
public enum SkeletonAnimation
{
    /// <summary>Moves a highlight across the placeholder.</summary>
    Shimmer,
    /// <summary>Animates the placeholder's opacity.</summary>
    Pulse,
    /// <summary>Displays a static placeholder.</summary>
    None
}

/// <summary>A decorative loading placeholder sized by its surrounding layout.</summary>
public sealed class AlderSkeleton : TemplatedControl
{
    /// <summary>Identifies the <see cref="Animation"/> Avalonia property.</summary>
    public static readonly StyledProperty<SkeletonAnimation> AnimationProperty =
        AvaloniaProperty.Register<AlderSkeleton, SkeletonAnimation>(nameof(Animation));
    /// <summary>Identifies the <see cref="ShimmerDuration"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> ShimmerDurationProperty =
        AvaloniaProperty.Register<AlderSkeleton, TimeSpan>(nameof(ShimmerDuration), TimeSpan.FromSeconds(2),
            validate: value => value > TimeSpan.Zero);
    /// <summary>Identifies the <see cref="ShimmerScope"/> property.</summary>
    public static readonly StyledProperty<Control?> ShimmerScopeProperty =
        AvaloniaProperty.Register<AlderSkeleton, Control?>(nameof(ShimmerScope));

    /// <summary>The motion style used while the placeholder is active.</summary>
    public SkeletonAnimation Animation { get => GetValue(AnimationProperty); set => SetValue(AnimationProperty, value); }
    /// <summary>The duration of one shimmer cycle.</summary>
    public TimeSpan ShimmerDuration { get => GetValue(ShimmerDurationProperty); set => SetValue(ShimmerDurationProperty, value); }
    /// <summary>The shared ancestor whose width and animation phase align participating placeholders.</summary>
    public Control? ShimmerScope { get => GetValue(ShimmerScopeProperty); set => SetValue(ShimmerScopeProperty, value); }
}
