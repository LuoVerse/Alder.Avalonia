using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment of an Alder toggle button.</summary>
public enum ToggleButtonVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses a visually quiet treatment at rest.</summary>
    Ghost
}

/// <summary>Extends the native toggle button with Alder variants, sizing, and icon presentation.</summary>
public class AlderToggleButton : ToggleButton
{
    /// <summary>Identifies the animated feedback settings.</summary>
    public static readonly StyledProperty<AlderPressableFeedback?> FeedbackProperty =
        AlderButton.FeedbackProperty.AddOwner<AlderToggleButton>();

    /// <summary>Optional motion settings for this control's press and hover feedback.</summary>
    public AlderPressableFeedback? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<ToggleButtonVariant> VariantProperty =
        AvaloniaProperty.Register<AlderToggleButton, ToggleButtonVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AlderButton.SizeProperty.AddOwner<AlderToggleButton>();
    /// <summary>Identifies the <see cref="IsIconOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AlderButton.IsIconOnlyProperty.AddOwner<AlderToggleButton>();
    /// <summary>Identifies the <see cref="FullWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FullWidthProperty =
        AlderButton.FullWidthProperty.AddOwner<AlderToggleButton>();
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AlderButton.IconProperty.AddOwner<AlderToggleButton>();

    /// <summary>The visual treatment applied to the control.</summary>
    public ToggleButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Whether the template uses its icon-only spacing and sizing.</summary>
    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    public bool FullWidth { get => GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }
    /// <summary>The optional geometry displayed with the content.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
            PseudoClasses.Set(":ghost", Variant == ToggleButtonVariant.Ghost);
        AlderPressableFeedback.OnOwnerPropertyChanged(this, change);
    }
}
