using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Arranges a linear fill, or translates a fixed segment on the compositor.</summary>
internal sealed class FeedbackSweep : Decorator
{
    private static readonly SplineEasing SweepEase = new(.65, 0, .35, 1);
    private static readonly LinearEasing ShimmerEase = new();
    private static readonly ConditionalWeakTable<Control, ShimmerTimeline> SharedTimelines = new();
    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<FeedbackSweep, double>(nameof(Fraction));
    public static readonly StyledProperty<bool> IsIndeterminateProperty = AvaloniaProperty.Register<FeedbackSweep, bool>(nameof(IsIndeterminate));
    public static readonly StyledProperty<bool> IsShimmerProperty = AvaloniaProperty.Register<FeedbackSweep, bool>(nameof(IsShimmer));
    public static readonly StyledProperty<Control?> ShimmerScopeProperty = AlderSkeleton.ShimmerScopeProperty.AddOwner<FeedbackSweep>();
    public static readonly StyledProperty<TimeSpan> DurationProperty = AvaloniaProperty.Register<FeedbackSweep, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(1500));

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public bool IsShimmer { get => GetValue(IsShimmerProperty); set => SetValue(IsShimmerProperty, value); }
    public Control? ShimmerScope { get => GetValue(ShimmerScopeProperty); set => SetValue(ShimmerScopeProperty, value); }
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    private Control? shimmerScope;
    private CompositionVisual? moving;
    private double width;
    private bool withinViewport = true;
    private (double Start, double End, TimeSpan Duration)? current;

    static FeedbackSweep() => AffectsArrange<FeedbackSweep>(FractionProperty, IsIndeterminateProperty, IsShimmerProperty);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        withinViewport = true;
        EffectiveViewportChanged += OnViewport;
        UpdateShimmerScope();
        InvalidateArrange();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= OnViewport;
        LayoutUpdated -= OnSharedLayout;
        shimmerScope = null;
        PseudoClasses.Set(":shared-shimmer", false);
        Stop();
        moving = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        width = finalSize.Width;
        double scopeWidth = Math.Max(width, shimmerScope?.Bounds.Width ?? 0);
        double fill = IsShimmer ? scopeWidth : IsIndeterminate ? width * .4 : width * Math.Clamp(Fraction, 0, 1);
        Child?.Arrange(new Rect(0, 0, fill, finalSize.Height));
        UpdateMotion(scopeWidth);
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShimmerScopeProperty || change.Property == IsShimmerProperty)
            UpdateShimmerScope();
        else if (change.Property == DurationProperty || change.Property == AlderMotion.ReducedProperty)
            InvalidateArrange();
        else if (change.Property == IsVisibleProperty)
        {
            if (IsVisible) InvalidateArrange();
            else Stop();
        }
    }

    private void UpdateShimmerScope()
    {
        var scope = IsShimmer && this.IsAttachedToVisualTree() && ShimmerScope is { } ancestor &&
            ancestor.IsVisualAncestorOf(this) ? ancestor : null;
        if (ReferenceEquals(shimmerScope, scope)) return;
        LayoutUpdated -= OnSharedLayout;
        Stop();
        shimmerScope = scope;
        PseudoClasses.Set(":shared-shimmer", scope is not null);
        if (scope is not null) LayoutUpdated += OnSharedLayout;
        InvalidateArrange();
    }

    private void OnSharedLayout(object? sender, EventArgs e)
    {
        if (Child is { } child && shimmerScope is { } scope && child.Bounds.Width != Math.Max(width, scope.Bounds.Width))
            InvalidateArrange();
        UpdateMotion(Math.Max(width, shimmerScope?.Bounds.Width ?? 0));
    }

    private void OnViewport(object? sender, EffectiveViewportChangedEventArgs e)
    {
        bool visible = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
        if (visible == withinViewport) return;
        withinViewport = visible;
        if (visible) InvalidateArrange();
        else Stop();
    }

    private void UpdateMotion(double scopeWidth)
    {
        var visual = Child is { } child ? ElementComposition.GetElementVisual(child) : null;
        if (visual != moving)
        {
            Stop();
            moving = visual;
        }
        if (moving is null || !IsIndeterminate)
        {
            Stop();
            return;
        }
        if (!this.IsAttachedToVisualTree() || !IsEffectivelyVisible || !withinViewport || AlderMotion.GetReduced(this) || width <= 0)
        {
            Stop();
            return;
        }

        double localOffset = shimmerScope is { } scope ? this.TranslatePoint(default, scope)?.X ?? 0 : 0;
        double start = (IsShimmer ? -scopeWidth : -width * .4) - localOffset;
        double end = (IsShimmer ? scopeWidth : width * 1.4) - localOffset;
        if (current == (start, end, Duration)) return;
        var sweep = moving.Compositor.CreateVector3DKeyFrameAnimation();
        sweep.Duration = Duration;
        sweep.IterationBehavior = AnimationIterationBehavior.Forever;
        sweep.StopBehavior = AnimationStopBehavior.SetToInitialValue;
        if (shimmerScope is { } group)
        {
            // A stripe that scrolls into view or is resized rejoins the group's existing phase.
            var timeline = SharedTimelines.GetValue(group, static _ => new ShimmerTimeline());
            sweep.DelayTime = -TimeSpan.FromTicks(Stopwatch.GetElapsedTime(timeline.StartedAt).Ticks % Duration.Ticks);
        }
        sweep.InsertKeyFrame(0, new Vector3D(start, 0, 0));
        sweep.InsertKeyFrame(1, new Vector3D(end, 0, 0), IsShimmer ? ShimmerEase : SweepEase);
        // Keep the client value distinct from the resting position so Stop can send a reset.
        moving.Translation = new Vector3D(start, 0, 0);
        moving.StartAnimation(nameof(CompositionVisual.Translation), sweep);
        current = (start, end, Duration);
    }

    private void Stop()
    {
        if (moving is not { } visual) return;
        if (current.HasValue) visual.StopAnimation(nameof(CompositionVisual.Translation));
        current = null;
        visual.Translation = default;
    }

    private sealed class ShimmerTimeline
    {
        public long StartedAt { get; } = Stopwatch.GetTimestamp();
    }
}
