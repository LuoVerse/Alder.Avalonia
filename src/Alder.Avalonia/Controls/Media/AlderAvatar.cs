using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment of an avatar and its fallback.</summary>
public enum AvatarVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the lower-contrast treatment.</summary>
    Soft
}
/// <summary>Specifies the semantic color of the avatar fallback.</summary>
public enum AvatarColor
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses the accent color.</summary>
    Accent,
    /// <summary>Uses the success treatment.</summary>
    Success,
    /// <summary>Uses the warning treatment.</summary>
    Warning,
    /// <summary>Uses the danger treatment.</summary>
    Danger
}

/// <summary>A portrait with a fallback that is available before an image is assigned.</summary>
public sealed class AlderAvatar : TemplatedControl
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderAvatar, AlderSize>(nameof(Size), AlderSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<AvatarColor> ColorProperty =
        AvaloniaProperty.Register<AlderAvatar, AvatarColor>(nameof(Color), inherits: true);
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AvatarVariant> VariantProperty =
        AvaloniaProperty.Register<AlderAvatar, AvatarVariant>(nameof(Variant), inherits: true);
    /// <summary>Identifies the <see cref="Source"/> Avalonia property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = Image.SourceProperty.AddOwner<AlderAvatar>();
    /// <summary>Identifies the <see cref="ImageTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ImageTemplateProperty =
        AvaloniaProperty.Register<AlderAvatar, IDataTemplate?>(nameof(ImageTemplate));
    /// <summary>Identifies the <see cref="Fallback"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FallbackProperty = AvaloniaProperty.Register<AlderAvatar, object?>(nameof(Fallback));
    /// <summary>Identifies the <see cref="FallbackTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FallbackTemplateProperty =
        AvaloniaProperty.Register<AlderAvatar, IDataTemplate?>(nameof(FallbackTemplate));
    /// <summary>Identifies the <see cref="Alt"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> AltProperty = AvaloniaProperty.Register<AlderAvatar, string?>(nameof(Alt));
    /// <summary>Identifies the <see cref="FallbackDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> FallbackDelayProperty =
        AvaloniaProperty.Register<AlderAvatar, TimeSpan>(nameof(FallbackDelay), validate: delay => delay >= TimeSpan.Zero);
    /// <summary>Identifies the <see cref="IsFallbackVisible"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderAvatar, bool> IsFallbackVisibleProperty =
        AvaloniaProperty.RegisterDirect<AlderAvatar, bool>(nameof(IsFallbackVisible), avatar => avatar.IsFallbackVisible);
    internal static readonly StyledProperty<Thickness> GroupFallbackInsetProperty =
        AvaloniaProperty.Register<AlderAvatar, Thickness>(nameof(GroupFallbackInset));

    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public AvatarColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public AvatarVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The image source shown when available.</summary>
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    /// <summary>The template used in place of the default image presenter.</summary>
    public IDataTemplate? ImageTemplate { get => GetValue(ImageTemplateProperty); set => SetValue(ImageTemplateProperty, value); }
    /// <summary>The content shown in place of an unavailable image.</summary>
    [Content]
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }
    /// <summary>The data template used to present the fallback content.</summary>
    public IDataTemplate? FallbackTemplate { get => GetValue(FallbackTemplateProperty); set => SetValue(FallbackTemplateProperty, value); }
    /// <summary>The alternative text identifying the avatar for assistive technology.</summary>
    public string? Alt { get => GetValue(AltProperty); set => SetValue(AltProperty, value); }
    /// <summary>The delay before fallback content is shown while an image is unavailable.</summary>
    public TimeSpan FallbackDelay { get => GetValue(FallbackDelayProperty); set => SetValue(FallbackDelayProperty, value); }
    /// <summary>Whether the template currently presents fallback content.</summary>
    public bool IsFallbackVisible { get => field; private set => SetAndRaise(IsFallbackVisibleProperty, ref field, value); } = true;
    internal Thickness GroupFallbackInset
    {
        get => GetValue(GroupFallbackInsetProperty);
        set => SetValue(GroupFallbackInsetProperty, value);
    }

    private IDisposable? fallbackTimer;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == FallbackDelayProperty)
            UpdateFallback();
        else if (change.Property == CornerRadiusProperty &&
                 this.GetVisualParent() is ContentPresenter presenter &&
                 presenter.GetVisualParent() is AvatarGroupSlot slot &&
                 slot.GetVisualParent() is AvatarGroupPanel group)
            group.InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFallback();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        fallbackTimer?.Dispose();
        fallbackTimer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateFallback()
    {
        fallbackTimer?.Dispose();
        fallbackTimer = null;
        IsFallbackVisible = Source is null && FallbackDelay == TimeSpan.Zero;
        if (Source is null && FallbackDelay > TimeSpan.Zero && this.IsAttachedToVisualTree())
            fallbackTimer = DispatcherTimer.RunOnce(() =>
            {
                fallbackTimer = null;
                IsFallbackVisible = true;
            }, FallbackDelay);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AvatarPeer(this);

    private sealed class AvatarPeer(AlderAvatar owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => AutomationProperties.GetName(owner) ?? owner.Alt ?? owner.Fallback as string;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }
}
