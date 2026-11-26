using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A selectable checkbox card with optional icon, custom indicator, and ripple feedback.</summary>
public sealed partial class AlderCheckboxButtonGroupItem : AlderCheckBox
{
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

    /// <summary>The brush used for the inset selection ring.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? SelectionBrush { get; set; }

    /// <summary>Whether activating the card produces a clipped ripple.</summary>
    [GeneratedStyledProperty]
    public partial bool IsRippleEnabled { get; set; }

    internal bool CanActivate => !IsReadOnly && Parent is not AlderCheckboxButtonGroup { IsReadOnly: true };

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
    }
}
