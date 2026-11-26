using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual diameter of a spinner.</summary>
public enum SpinnerSize
{
    /// <summary>Uses the small size.</summary>
    Small,
    /// <summary>Uses the medium size.</summary>
    Medium,
    /// <summary>Uses the large size.</summary>
    Large,
    /// <summary>Uses the extra-large size.</summary>
    ExtraLarge
}

/// <summary>Displays an indeterminate loading indicator with a configurable size and spin period.</summary>
public sealed class AlderSpinner : TemplatedControl
{
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<FeedbackColor> ColorProperty =
        AvaloniaProperty.Register<AlderSpinner, FeedbackColor>(nameof(Color), FeedbackColor.Accent);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<SpinnerSize> SizeProperty = AvaloniaProperty.Register<AlderSpinner, SpinnerSize>(nameof(Size), SpinnerSize.Medium);
    /// <summary>Identifies the <see cref="SpinDuration"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> SpinDurationProperty = ButtonSpinner.SpinDurationProperty.AddOwner<AlderSpinner>();

    /// <summary>Accent is the initial color. Select <see cref="FeedbackColor.Default"/> to inherit the foreground.</summary>
    public FeedbackColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public SpinnerSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The duration of one full spinner rotation.</summary>
    public TimeSpan SpinDuration { get => GetValue(SpinDurationProperty); set => SetValue(SpinDurationProperty, value); }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SpinnerPeer(this);

    private sealed class SpinnerPeer(AlderSpinner owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
    }
}
