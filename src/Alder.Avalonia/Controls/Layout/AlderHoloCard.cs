using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vector = Avalonia.Vector;

namespace Alder.Avalonia.Controls;

/// <summary>A layered card with pointer-driven perspective, content parallax and holographic glare.</summary>
public sealed class AlderHoloCard : ContentControl
{
    /// <summary>Identifies the <see cref="ImageSource"/> Avalonia property.</summary>
    public static readonly StyledProperty<IImage?> ImageSourceProperty = AvaloniaProperty.Register<AlderHoloCard, IImage?>(nameof(ImageSource));
    /// <summary>Identifies the <see cref="ImageStretch"/> Avalonia property.</summary>
    public static readonly StyledProperty<Stretch> ImageStretchProperty =
        AvaloniaProperty.Register<AlderHoloCard, Stretch>(nameof(ImageStretch), Stretch.UniformToFill);
    /// <summary>Identifies the <see cref="Pattern"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> PatternProperty = AvaloniaProperty.Register<AlderHoloCard, IBrush?>(nameof(Pattern));
    /// <summary>Identifies the <see cref="ShinePattern"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> ShinePatternProperty = AvaloniaProperty.Register<AlderHoloCard, IBrush?>(nameof(ShinePattern));
    /// <summary>Identifies the <see cref="InsetBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> InsetBrushProperty = AvaloniaProperty.Register<AlderHoloCard, IBrush?>(nameof(InsetBrush));
    /// <summary>Identifies the <see cref="ShowInset"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowInsetProperty = AvaloniaProperty.Register<AlderHoloCard, bool>(nameof(ShowInset), true);
    /// <summary>Identifies the <see cref="IsInteractive"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty = AvaloniaProperty.Register<AlderHoloCard, bool>(nameof(IsInteractive), true);
    /// <summary>Identifies the <see cref="ActivateOnHover"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ActivateOnHoverProperty = AvaloniaProperty.Register<AlderHoloCard, bool>(nameof(ActivateOnHover));
    /// <summary>Identifies the <see cref="TiltOnTouch"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> TiltOnTouchProperty = AvaloniaProperty.Register<AlderHoloCard, bool>(nameof(TiltOnTouch), true);
    /// <summary>Identifies the <see cref="MaxTilt"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaxTiltProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(MaxTilt), 14.29,
        validate: value => value is >= 0 and <= 45);
    /// <summary>Identifies the <see cref="HoverScale"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> HoverScaleProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(HoverScale), 1.05,
        validate: value => value is >= 1 and <= 2);
    /// <summary>Identifies the <see cref="Perspective"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> PerspectiveProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(Perspective), 600,
        validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="ParallaxStrength"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ParallaxStrengthProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(ParallaxStrength), 10,
        validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="TextureOpacity"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> TextureOpacityProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(TextureOpacity), .1,
        validate: value => value is >= 0 and <= 1);
    /// <summary>Identifies the <see cref="ShineOpacity"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ShineOpacityProperty = AvaloniaProperty.Register<AlderHoloCard, double>(nameof(ShineOpacity), 1,
        validate: value => value is >= 0 and <= 1);
    /// <summary>Identifies the <see cref="IsActive"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderHoloCard, bool> IsActiveProperty =
        AvaloniaProperty.RegisterDirect<AlderHoloCard, bool>(nameof(IsActive), card => card.IsActive);

    /// <summary>The image shown beneath the card's texture and shine layers.</summary>
    public IImage? ImageSource { get => GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    /// <summary>How the image scales to fit the card.</summary>
    public Stretch ImageStretch { get => GetValue(ImageStretchProperty); set => SetValue(ImageStretchProperty, value); }
    /// <summary>The texture brush drawn over the image.</summary>
    public IBrush? Pattern { get => GetValue(PatternProperty); set => SetValue(PatternProperty, value); }
    /// <summary>The brush used for the moving shine pattern.</summary>
    public IBrush? ShinePattern { get => GetValue(ShinePatternProperty); set => SetValue(ShinePatternProperty, value); }
    /// <summary>The brush used to paint the inset detail.</summary>
    public IBrush? InsetBrush { get => GetValue(InsetBrushProperty); set => SetValue(InsetBrushProperty, value); }
    /// <summary>Whether to show the inset.</summary>
    public bool ShowInset { get => GetValue(ShowInsetProperty); set => SetValue(ShowInsetProperty, value); }
    /// <summary>Whether the holo card is interactive.</summary>
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }
    /// <summary>Leaves touch gestures to the parent when cards are displayed in a collection.</summary>
    public bool ActivateOnHover { get => GetValue(ActivateOnHoverProperty); set => SetValue(ActivateOnHoverProperty, value); }
    /// <summary>Whether touch input can drive the tilt effect.</summary>
    public bool TiltOnTouch { get => GetValue(TiltOnTouchProperty); set => SetValue(TiltOnTouchProperty, value); }
    /// <summary>The maximum pointer-driven tilt in degrees.</summary>
    public double MaxTilt { get => GetValue(MaxTiltProperty); set => SetValue(MaxTiltProperty, value); }
    /// <summary>The scale reached when the interactive card is hovered.</summary>
    public double HoverScale { get => GetValue(HoverScaleProperty); set => SetValue(HoverScaleProperty, value); }
    /// <summary>The depth used when projecting the card's tilt.</summary>
    public double Perspective { get => GetValue(PerspectiveProperty); set => SetValue(PerspectiveProperty, value); }
    /// <summary>The amount of relative movement of the visual layers.</summary>
    public double ParallaxStrength { get => GetValue(ParallaxStrengthProperty); set => SetValue(ParallaxStrengthProperty, value); }
    /// <summary>The opacity of the texture layer.</summary>
    public double TextureOpacity { get => GetValue(TextureOpacityProperty); set => SetValue(TextureOpacityProperty, value); }
    /// <summary>The maximum opacity of the shine layer.</summary>
    public double ShineOpacity { get => GetValue(ShineOpacityProperty); set => SetValue(ShineOpacityProperty, value); }
    /// <summary>Whether the holo card is active.</summary>
    public bool IsActive { get => field; private set => SetAndRaise(IsActiveProperty, ref field, value); }

    private Border? rotator;
    private Border? shadow;
    private Border? inset;
    private ContentPresenter? face;
    private HoloCardEffects? effects;
    private readonly MatrixTransform tilt = new();
    private readonly TranslateTransform parallax = new();
    private Vector3 target;
    private Vector3 current;
    private Vector3 velocity;
    private TimeSpan? lastFrame;
    private bool framePending;
    private IPointer? contact;
    private readonly Action<TimeSpan> animate;

    /// <summary>Initializes a new instance of <see cref="AlderHoloCard"/>.</summary>
    public AlderHoloCard()
    {
        animate = Advance;
        AddHandler(PointerPressedEvent, OnContactStarted, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnContactEnded, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnContactEnded, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Reset();
        base.OnApplyTemplate(e);
        rotator = e.NameScope.Find<Border>("PART_Rotator");
        shadow = e.NameScope.Find<Border>("PART_Shadow");
        inset = e.NameScope.Find<Border>("PART_Inset");
        face = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        effects = e.NameScope.Find<HoloCardEffects>("PART_Effects");
        UpdateInset();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse) Track(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse || e.Pointer == contact) Track(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse || e.Pointer == contact) ReturnToRest();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == IsInteractiveProperty || change.Property == IsEffectivelyEnabledProperty ||
             change.Property == AlderMotion.ReducedProperty) && !CanTrack) Reset();
        if ((change.Property == ActivateOnHoverProperty || change.Property == TiltOnTouchProperty) &&
            contact is not null && (ActivateOnHover || !TiltOnTouch)) ReturnToRest();
        if (change.Property == CornerRadiusProperty) UpdateInset();
        if (IsActive && (change.Property == MaxTiltProperty || change.Property == HoverScaleProperty ||
            change.Property == PerspectiveProperty || change.Property == ParallaxStrengthProperty || change.Property == BoundsProperty))
            RenderMotion();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Reset();
        base.OnDetachedFromVisualTree(e);
    }

    private bool CanTrack => IsInteractive && IsEffectivelyEnabled && !AlderMotion.GetReduced(this);

    private void OnContactStarted(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse || !CanTrack || ActivateOnHover || !TiltOnTouch) return;
        contact = e.Pointer;
        Track(e.GetPosition(this));
    }

    private void OnContactEnded(object? sender, PointerEventArgs e)
    {
        if (e.Pointer == contact) ReturnToRest();
    }

    private void Track(Point pointer)
    {
        if (!CanTrack || rotator is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (!new Rect(Bounds.Size).Contains(pointer))
        {
            ReturnToRest();
            return;
        }
        var next = new Vector3((float)(2 * pointer.X / Bounds.Width - 1), (float)(2 * pointer.Y / Bounds.Height - 1), 1);
        if (next == target) return;
        target = next;
        IsActive = true;
        QueueFrame();
    }

    private void ReturnToRest()
    {
        contact = null;
        target = default;
        if (IsActive) QueueFrame();
    }

    private void QueueFrame()
    {
        if (framePending || TopLevel.GetTopLevel(this) is not { } top) return;
        framePending = true;
        top.RequestAnimationFrame(animate);
    }

    private void Advance(TimeSpan now)
    {
        framePending = false;
        if (!IsActive || !CanTrack) return;
        float dt = lastFrame is { } last ? (float)Math.Clamp((now - last).TotalSeconds, .001, .05) : 1f / 60;
        lastFrame = now;

        // Solve the overdamped springs for tracking (k=180, c=28) and release (k=120, c=22).
        // Both use unit mass. Keeping velocity lets a change of direction interrupt either spring.
        const float slowRate = -10;
        float fastRate = target.Z == 0 ? -12 : -18;
        var displacement = current - target;
        var slow = (velocity - fastRate * displacement) / (slowRate - fastRate);
        var fast = (displacement - slow) * MathF.Exp(fastRate * dt);
        slow *= MathF.Exp(slowRate * dt);
        current = target + slow + fast;
        velocity = slowRate * slow + fastRate * fast;
        if (Vector3.DistanceSquared(current, target) < .000004f && velocity.LengthSquared() < .0025f)
        {
            current = target;
            velocity = default;
            lastFrame = null;
            if (target.Z == 0) Reset();
            else RenderMotion();
            return;
        }
        RenderMotion();
        QueueFrame();
    }

    private void RenderMotion()
    {
        var radians = Math.PI / 180 * MaxTilt;
        var (sinX, cosX) = Math.SinCos(-current.Y * radians);
        var (sinY, cosY) = Math.SinCos(current.X * radians);
        double intensity = Math.Clamp((double)current.Z, 0, 1);
        double scale = 1 + (HoverScale - 1) * intensity;
        double depth = Math.Max(Perspective, Math.Max(Bounds.Width, Bounds.Height));
        // Rotate the card in 3D, then project its plane with w = 1 - z / depth.
        tilt.Matrix = new Matrix(cosY * scale, sinY * sinX * scale, -sinY * cosX * scale / depth,
            0, cosX * scale, sinX * scale / depth, 0, 0, 1);
        if (rotator is not null) rotator.RenderTransform = tilt;

        double drift = ParallaxStrength * MaxTilt / 18;
        parallax.X = -current.X * drift;
        parallax.Y = -current.Y * drift;
        if (face is not null) face.RenderTransform = parallax;
        if (shadow is not null)
        {
            double remaining = 1 - intensity;
            shadow.Opacity = 1 - remaining * remaining * remaining;
        }
        effects?.SetGlare(new Point((current.X + 1) / 2, (current.Y + 1) / 2), intensity);
    }

    private void UpdateInset()
    {
        if (inset is null) return;
        var radius = CornerRadius;
        inset.CornerRadius = new CornerRadius(
            Math.Max(0, radius.TopLeft - 8), Math.Max(0, radius.TopRight - 8),
            Math.Max(0, radius.BottomRight - 8), Math.Max(0, radius.BottomLeft - 8));
    }

    private void Reset()
    {
        contact = null;
        target = current = velocity = default;
        lastFrame = null;
        IsActive = false;
        if (rotator is not null) rotator.RenderTransform = null;
        if (shadow is not null) shadow.Opacity = 0;
        if (face is not null) face.RenderTransform = null;
        effects?.SetGlare(default, 0);
    }
}
