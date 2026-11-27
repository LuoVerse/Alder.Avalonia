using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>The motion pattern for an AI activity indicator.</summary>
public enum AiLoaderVariant
{
    /// <summary>A rotating open ring.</summary>
    Ring,
    /// <summary>A radial spinner.</summary>
    Classic,
    /// <summary>Three sequentially pulsing dots.</summary>
    Dots,
    /// <summary>An expanding pulse.</summary>
    Pulse,
    /// <summary>A moving scan line.</summary>
    Scan,
    /// <summary>A cycling grid.</summary>
    Grid,
    /// <summary>Four alternating tiles.</summary>
    Shuffle,
    /// <summary>An audio waveform.</summary>
    Wave,
    /// <summary>A pulsing four-point star.</summary>
    Sparkle,
    /// <summary>Intersecting orbits.</summary>
    Atom,
    /// <summary>A smoothly changing outline.</summary>
    Morph,
    /// <summary>Two winding strands.</summary>
    Helix,
    /// <summary>A twisting loop.</summary>
    Twist,
    /// <summary>A figure-eight path.</summary>
    Infinity
}

/// <summary>Draws a compact animated status indicator on one visual.</summary>
public partial class AlderLoader : TemplatedControl
{
    /// <summary>The pattern drawn by the indicator.</summary>
    [GeneratedStyledProperty] public partial AiLoaderVariant Variant { get; set; }

    private readonly Action<TimeSpan> frame;
    private bool pending;
    private bool inViewport = true;
    private double elapsedSeconds;
    private LoaderGlyph? glyph;
    private LoaderMotionLayer? motion;
    private Visual[]? observedAncestors;

    /// <summary>The nominal indicator height. Infinity is one and a half times as wide.</summary>
    [GeneratedStyledProperty(DefaultValue = SpinnerSize.Medium)] public partial SpinnerSize Size { get; set; }

    /// <summary>The indicator color. Default inherits the surrounding foreground.</summary>
    [GeneratedStyledProperty(DefaultValue = FeedbackColor.Accent)] public partial FeedbackColor Color { get; set; }

    /// <summary>Optional status text or custom label content beside the indicator.</summary>
    [GeneratedStyledProperty] public partial object? Label { get; set; }

    private double IndicatorHeight => Size switch { SpinnerSize.Small => 16, SpinnerSize.Medium => 20, _ => 24 };

    static AlderLoader()
    {
        AffectsRender<AlderLoader>(SizeProperty, LabelProperty);
    }

    /// <summary>Creates an indicator that requests frames while visible.</summary>
    public AlderLoader()
    {
        frame = Advance;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        EffectiveViewportChanged += (_, e) =>
        {
            bool visible = new Rect(Bounds.Size).Intersects(e.EffectiveViewport);
            if (visible == inViewport) return;
            inViewport = visible;
            RefreshMotion();
            if (inViewport) InvalidateVisual();
        };
    }

    internal bool CanAnimate =>
        inViewport && Opacity > 0 && IsEffectivelyVisible && this.IsAttachedToVisualTree() && !AlderMotion.GetReduced(this);

    private void Advance(TimeSpan elapsed)
    {
        pending = false;
        if (!CanAnimate || motion?.IsComposed == true) return;
        elapsedSeconds = elapsed.TotalSeconds;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        motion?.Suspend();
        base.OnApplyTemplate(e);
        motion = e.NameScope.Find<LoaderMotionLayer>("PART_LoaderMotion");
        motion?.Attach(this);
        ObserveAncestors();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        inViewport = true;
        ObserveAncestors();
        RefreshMotion();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // A closed top level may never deliver its outstanding animation frame.
        pending = false;
        motion?.Suspend();
        ClearAncestors();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty) glyph = null;
        if (change.Property == LabelProperty && ControlAutomationPeer.FromElement(this) is LoaderAutomationPeer peer)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, change.OldValue as string, peer.GetName());
        if (change.Property == ForegroundProperty || change.Property == VariantProperty ||
            change.Property == SizeProperty || change.Property == LabelProperty ||
            change.Property == IsVisibleProperty || change.Property == AlderMotion.ReducedProperty ||
            (change.Property == OpacityProperty && (Opacity <= 0 || change.GetOldValue<double>() <= 0)))
        {
            RefreshMotion();
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (motion?.IsComposed == true || Foreground is not { } ink) return;
        bool reduced = AlderMotion.GetReduced(this);
        if (this is AlderChatLoader chat)
        {
            // The chat ring is rendered and rotated by the spinner template.
            if (Variant == AiLoaderVariant.Ring) return;
            if (Variant is AiLoaderVariant.Dots or AiLoaderVariant.Pulse)
            {
                chat.DrawTypingIndicator(context, ink, elapsedSeconds, reduced);
                RequestNextFrame(reduced);
                return;
            }
        }
        var box = GetGlyphBox(Bounds.Size);
        if (box.Height <= 0) return;
        var center = box.Center;
        using (context.PushTransform(Matrix.CreateScale(box.Height / 24, box.Height / 24) * Matrix.CreateTranslation(center.X, center.Y)))
            (glyph ??= new LoaderGlyph(ink)).Draw(context, Variant, elapsedSeconds, reduced);
        RequestNextFrame(reduced);
    }

    internal Rect GetGlyphBox(Size bounds)
    {
        double aspect = Variant == AiLoaderVariant.Infinity ? 1.5 : 1;
        double height = Math.Min(bounds.Width / aspect, bounds.Height);
        if (Label is not null) height = Math.Min(IndicatorHeight, height);
        var center = Label is null ? new Rect(bounds).Center : new Point(height * aspect / 2, bounds.Height / 2);
        return new Rect(center.X - height / 2, center.Y - height / 2, height, height);
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new LoaderAutomationPeer(this);

    private sealed class LoaderAutomationPeer(AlderLoader owner) : ControlAutomationPeer(owner)
    {
        protected override string GetNameCore()
        {
            if (AutomationProperties.GetName(owner) is { Length: > 0 } name) return name;
            if (owner.Label is string { Length: > 0 } label) return label;
            if (owner.Label is TextBlock { Text.Length: > 0 } text) return text.Text!;
            return owner.TryFindResource("AlderText.AiLoad", out var fallback) && fallback is string localized ? localized : "Loading";
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.StatusBar;
    }

    private void RequestNextFrame(bool reduced)
    {
        if (!pending && inViewport && Opacity > 0 && IsEffectivelyVisible && !reduced && TopLevel.GetTopLevel(this) is { } top)
        {
            pending = true;
            top.RequestAnimationFrame(frame);
        }
    }

    private void RefreshMotion()
    {
        if (motion is null) return;
        if (!CanAnimate || !LoaderMotionLayer.Supports(this)) motion.Suspend();
        motion.InvalidateArrange();
    }

    private void ObserveAncestors()
    {
        ClearAncestors();
        if (motion is null || !this.IsAttachedToVisualTree()) return;
        observedAncestors = this.GetVisualAncestors().ToArray();
        foreach (var ancestor in observedAncestors) ancestor.PropertyChanged += AncestorChanged;
    }

    private void ClearAncestors()
    {
        if (observedAncestors is null) return;
        foreach (var ancestor in observedAncestors) ancestor.PropertyChanged -= AncestorChanged;
        observedAncestors = null;
    }

    private void AncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsVisibleProperty) return;
        if (!change.GetNewValue<bool>()) motion?.Suspend();
        motion?.InvalidateArrange();
        InvalidateVisual();
    }
}

/// <summary>A chat typing indicator with dots, pulse and spinner variants.</summary>
public sealed partial class AlderChatLoader : AlderLoader
{
    private static readonly SplineEasing TypingEase = LoaderGlyph.Ease;

    static AlderChatLoader()
    {
        VariantProperty.OverrideDefaultValue<AlderChatLoader>(AiLoaderVariant.Dots);
        AffectsMeasure<AlderChatLoader>(VariantProperty, SizeProperty);
        AffectsRender<AlderChatLoader>(SizeProperty);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        return Variant switch
        {
            AiLoaderVariant.Dots => new Size(3 * DotDiameter + 8, DotDiameter),
            AiLoaderVariant.Pulse => new Size(PulseDiameter, PulseDiameter),
            AiLoaderVariant.Ring => desired,
            _ => new Size(20, 20)
        };
    }

    private double DotDiameter => Size switch { SpinnerSize.Small => 5, SpinnerSize.Medium => 6, _ => 8 };
    private double PulseDiameter => Size switch { SpinnerSize.Small => 6, SpinnerSize.Medium => 8, _ => 10 };

    internal void DrawTypingIndicator(DrawingContext context, IBrush ink, double seconds, bool reduced)
    {
        bool dots = Variant == AiLoaderVariant.Dots;
        var (center, diameter, spacing) = GetTypingBox(Bounds.Size);
        if (diameter <= 0) return;
        for (int i = 0; i < (dots ? 3 : 1); i++)
        {
            double strength = dots ? DotStrength(seconds, i) : PulseStrength(seconds);
            double opacity = reduced ? .7 : dots ? .35 + .65 * strength : .45 + .55 * strength;
            double scale = reduced ? 1 : dots ? .85 + .15 * strength : .92 + .08 * strength;
            double radius = diameter * scale / 2;
            double x = dots ? (i - 1) * spacing : 0;
            using var fade = context.PushOpacity(opacity);
            context.DrawEllipse(ink, null, center + new Vector(x, 0), radius, radius);
        }
    }

    internal (Point Center, double Diameter, double Spacing) GetTypingBox(Size bounds)
    {
        bool dots = Variant == AiLoaderVariant.Dots;
        double diameter = dots ? DotDiameter : PulseDiameter;
        double width = dots ? diameter * 3 + 8 : diameter;
        double fit = Math.Min(1, Math.Min(bounds.Width / width, bounds.Height / diameter));
        return (new Rect(bounds).Center, diameter * fit, (diameter + 4) * fit);
    }

    private static double DotStrength(double seconds, int index)
    {
        double progress = (seconds / 1.2 + (2 - index) * .1) % 1;
        return progress <= .4 ? TypingEase.Ease(progress / .4) :
            progress < .8 ? 1 - TypingEase.Ease((progress - .4) / .4) : 0;
    }

    private static double PulseStrength(double seconds)
    {
        double progress = (seconds / 1.4) % 1;
        return TypingEase.Ease(1 - Math.Abs(2 * progress - 1));
    }
}
