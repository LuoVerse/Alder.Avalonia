using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the layout of a radio option.</summary>
public enum RadioButtonGroupItemVariant
{
    /// <summary>A bordered option card with a trailing indicator.</summary>
    Card,
    /// <summary>A compact option row with a leading indicator.</summary>
    Inline
}

/// <summary>A selectable radio card with optional icon, custom indicator, and ripple feedback.</summary>
public sealed partial class AlderRadioButtonGroupItem : RadioButton
{
    /// <summary>Identifies the animated feedback settings.</summary>
    public static readonly StyledProperty<AlderPressableFeedback?> FeedbackProperty =
        AlderButton.FeedbackProperty.AddOwner<AlderRadioButtonGroupItem>();

    /// <summary>Optional motion settings for this control's press and hover feedback.</summary>
    public AlderPressableFeedback? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>The layout used to present this option.</summary>
    [GeneratedStyledProperty]
    public partial RadioButtonGroupItemVariant Variant { get; set; }

    /// <summary>The optional description displayed below the radio card label.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }

    /// <summary>The optional content displayed before the card label.</summary>
    [GeneratedStyledProperty]
    public partial object? Icon { get; set; }

    /// <summary>Optional content shown below the card description.</summary>
    [GeneratedStyledProperty]
    public partial object? Details { get; set; }

    /// <summary>The custom content shown at the top end of the card while selected.</summary>
    [GeneratedStyledProperty]
    public partial object? Indicator { get; set; }

    /// <summary>Whether to display the default or custom selection indicator.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowIndicator { get; set; }

    /// <summary>Aligns the selection indicator along the trailing edge of the card.</summary>
    [GeneratedStyledProperty(DefaultValue = VerticalAlignment.Top)]
    public partial VerticalAlignment IndicatorVerticalAlignment { get; set; }

    /// <summary>The brush used for the inset selection ring.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? SelectionBrush { get; set; }

    /// <summary>Whether activating the card produces a clipped ripple.</summary>
    [GeneratedStyledProperty]
    public partial bool IsRippleEnabled { get; set; }

    internal bool CanActivate => Parent is not AlderRadioButtonGroup { IsReadOnly: true };

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (CanActivate) base.OnClick();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRippleEnabledProperty) AlderPressableFeedback.RefreshDefaultRipple(this);
        AlderPressableFeedback.OnOwnerPropertyChanged(this, change);
    }
}
