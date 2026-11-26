using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual emphasis and intent of an Alder button.</summary>
public enum ButtonVariant
{
    /// <summary>Uses the highest-emphasis button treatment.</summary>
    Primary,
    /// <summary>Uses the supporting button treatment.</summary>
    Secondary,
    /// <summary>Uses the lower-emphasis button treatment.</summary>
    Tertiary,
    /// <summary>Draws a border around the button.</summary>
    Outline,
    /// <summary>Keeps the resting button surface visually unobtrusive.</summary>
    Ghost,
    /// <summary>Uses a high-emphasis destructive treatment.</summary>
    Danger,
    /// <summary>Uses a quieter destructive treatment.</summary>
    DangerSoft
}
/// <summary>Provides the shared compact, standard, and spacious control sizes.</summary>
public enum AlderSize
{
    /// <summary>Uses the small size.</summary>
    Small,
    /// <summary>Uses the medium size.</summary>
    Medium,
    /// <summary>Uses the large size.</summary>
    Large
}

/// <summary>Extends the native button with Alder variants, optional icon content, and pending and ripple feedback.</summary>
public partial class AlderButton : Button
{
    /// <summary>Identifies the animated feedback settings.</summary>
    public static readonly StyledProperty<AlderPressableFeedback?> FeedbackProperty =
        AvaloniaProperty.Register<AlderButton, AlderPressableFeedback?>(nameof(Feedback));

    /// <summary>Optional motion settings for this control's press and hover feedback.</summary>
    public AlderPressableFeedback? Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>The visual treatment applied to the control.</summary>
    [GeneratedStyledProperty(DefaultValue = ButtonVariant.Primary)]
    public partial ButtonVariant Variant { get; set; }
    /// <summary>The size preset used for the control.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }
    /// <summary>Whether the button shows pending feedback and suppresses another click.</summary>
    [GeneratedStyledProperty]
    public partial bool IsPending { get; set; }
    /// <summary>Whether the template uses the icon-only sizing and spacing.</summary>
    [GeneratedStyledProperty]
    public partial bool IsIconOnly { get; set; }
    /// <summary>Whether the control stretches to fill the available width.</summary>
    [GeneratedStyledProperty]
    public partial bool FullWidth { get; set; }
    /// <summary>The optional geometry displayed with the content.</summary>
    [GeneratedStyledProperty]
    public partial Geometry? Icon { get; set; }
    /// <summary>Whether pointer and keyboard activation can start a ripple when reduced motion is off.</summary>
    [GeneratedStyledProperty]
    public partial bool IsRippleEnabled { get; set; }

    private Popup? menuPopup;

    internal void InvokeConfirmedClick() => OnClick();

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsEffectivelyEnabled) PrepareFlyout();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (IsFocused) PrepareFlyout();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) PrepareFlyout();
        base.OnPointerPressed(e);
    }

    internal virtual void PrepareFlyout()
    {
        if (Flyout is AlderMenuFlyout menu) menu.PreparePresenter(this);
        else if (Flyout is AlderPopover popover) popover.PreparePresenter(this);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FlyoutProperty) ObserveMenuPopup(null);
        if (change.Property == IsRippleEnabledProperty) AlderPressableFeedback.RefreshDefaultRipple(this);
        AlderPressableFeedback.OnOwnerPropertyChanged(this, change);
    }

    /// <inheritdoc />
    protected override void OpenFlyout()
    {
        using var rowActionInput = Flyout is MenuFlyout menu && menu is not AlderMenuFlyout
            ? AlderDataGrid.KeepRowActionPointerInput(this, menu)
            : null;
        base.OpenFlyout();
    }

    /// <inheritdoc />
    protected override void OnFlyoutOpened()
    {
        base.OnFlyoutOpened();
        ObserveMenuPopup((Flyout as AlderMenuFlyout)?.Popup);
    }

    /// <inheritdoc />
    protected override void OnFlyoutClosed()
    {
        base.OnFlyoutClosed();
        ObserveMenuPopup(null);
    }

    private void ObserveMenuPopup(Popup? popup)
    {
        menuPopup?.PropertyChanged -= MenuPopupChanged;
        menuPopup = popup;
        menuPopup?.PropertyChanged += MenuPopupChanged;
        PseudoClasses.Set(":flyout-dismissing", popup is { IsHitTestVisible: false });
    }

    private void MenuPopupChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        // Input is disabled when the exit motion starts, before the native Closed event.
        if (change.Property == IsHitTestVisibleProperty && !change.GetNewValue<bool>())
            PseudoClasses.Set(":flyout-dismissing", true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ObserveMenuPopup(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (!IsPending) base.OnClick();
    }
}
