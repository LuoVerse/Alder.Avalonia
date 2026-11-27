using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Identifies the response recorded when a dialog or related overlay closes.</summary>
public enum DialogResult
{
    /// <summary>Indicates dismissal without an explicit response.</summary>
    None,
    /// <summary>Indicates confirmation with the OK action.</summary>
    OK,
    /// <summary>Indicates the Cancel action.</summary>
    Cancel,
    /// <summary>Indicates an affirmative response.</summary>
    Yes,
    /// <summary>Indicates a negative response.</summary>
    No
}
/// <summary>Selects the standard response buttons to show in the dialog footer.</summary>
public enum DialogButtons
{
    /// <summary>Omits the standard response buttons.</summary>
    None,
    /// <summary>Shows an OK button.</summary>
    OK,
    /// <summary>Shows OK and Cancel buttons.</summary>
    OKCancel,
    /// <summary>Shows Yes and No buttons.</summary>
    YesNo,
    /// <summary>Shows Yes, No, and Cancel buttons.</summary>
    YesNoCancel
}
/// <summary>Controls how the layer behind an overlay obscures its host.</summary>
public enum DialogBackdrop
{
    /// <summary>Uses the standard dimming layer.</summary>
    Opaque,
    /// <summary>Blurs the host behind the overlay.</summary>
    Blur,
    /// <summary>Leaves the host visible through the backdrop.</summary>
    Transparent
}
/// <summary>Specifies where scrolling is applied when dialog content exceeds the available space.</summary>
public enum DialogScrollMode
{
    /// <summary>Scrolls inside the dialog body.</summary>
    Inside,
    /// <summary>Scrolls the dialog surface within its host.</summary>
    Outside
}
/// <summary>Specifies the overlay origin or the edge used for a drawer or sheet.</summary>
public enum DialogPlacement
{
    /// <summary>Lets the presentation choose its default position.</summary>
    Auto,
    /// <summary>Centers the surface in the host.</summary>
    Center,
    /// <summary>Aligns the surface to the host's top edge.</summary>
    Top,
    /// <summary>Aligns the surface to the host's bottom edge.</summary>
    Bottom
}
/// <summary>Selects a named width or a host-filling dialog layout.</summary>
public enum DialogSize
{
    /// <summary>Uses the default maximum width of 320 device-independent pixels.</summary>
    ExtraSmall,
    /// <summary>Uses the default maximum width of 384 device-independent pixels.</summary>
    Small,
    /// <summary>Uses the default maximum width of 448 device-independent pixels.</summary>
    Medium,
    /// <summary>Uses the default maximum width of 512 device-independent pixels.</summary>
    Large,
    /// <summary>Expands the dialog toward the available host size.</summary>
    Cover,
    /// <summary>Fills the available host area.</summary>
    Full
}
/// <summary>Selects the semantic treatment used by a message box.</summary>
public enum MessageBoxStatus
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

/// <summary>Overrides the compositor animation for a hosted or native dialog.</summary>
public sealed record DialogAnimation
{
    /// <summary>The duration of the opening animation.</summary>
    public TimeSpan EnterDuration { get; init; } = TimeSpan.FromMilliseconds(250);
    /// <summary>The duration of the closing animation.</summary>
    public TimeSpan ExitDuration { get; init; } = TimeSpan.FromMilliseconds(100);
    /// <summary>The scale of the overlay at the start of the opening animation.</summary>
    public double EnterScale { get; init; } = 1.05;
    /// <summary>The scale of the overlay at the end of the closing animation.</summary>
    public double ExitScale { get; init; } = .95;
    /// <summary>The translation at the start of the opening animation.</summary>
    public Vector EnterOffset { get; init; }
    /// <summary>The translation at the end of the closing animation.</summary>
    public Vector ExitOffset { get; init; }
    internal TimeSpan? BackdropEnterDuration { get; init; }
}

/// <summary>Shared content, actions and dismissal settings for transient overlays.</summary>
public abstract record OverlayOptions
{
    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get; init; }
    /// <summary>The optional geometry displayed with the content.</summary>
    public object? Icon { get; init; }
    /// <summary>The data template used to present the overlay content.</summary>
    public IDataTemplate? ContentTemplate { get; init; }
    /// <summary>The predefined response buttons available in the footer.</summary>
    public DialogButtons Buttons { get; init; }
    /// <summary>Optional content for the overlay footer.</summary>
    public object? Footer { get; init; }
    /// <summary>An optional override for the content's layout and reading direction.</summary>
    public FlowDirection? FlowDirection { get; init; }
    /// <summary>An optional label that replaces the standard primary action text.</summary>
    public string? PrimaryText { get; init; }
    /// <summary>An optional label that replaces the standard cancel action text.</summary>
    public string? CancelText { get; init; }
    /// <summary>Whether the primary action uses destructive emphasis.</summary>
    public bool IsDestructive { get; init; }
    /// <summary>The treatment applied to the host behind the overlay.</summary>
    public DialogBackdrop Backdrop { get; init; }
    /// <summary>Overrides the tint drawn above the optional background blur.</summary>
    public IBrush? BackdropBrush { get; init; }
    /// <summary>Custom opening and closing motion. Null uses the presentation's default.</summary>
    public DialogAnimation? Animation { get; init; }
    /// <summary>Whether to display the overlay's close affordance.</summary>
    public bool ShowCloseButton { get; init; } = true;
    /// <summary>Whether interaction outside the surface can dismiss it.</summary>
    public bool CanLightDismiss { get; init; } = true;
    /// <summary>Whether pressing Escape can dismiss the overlay.</summary>
    public bool CanEscapeDismiss { get; init; } = true;
}

/// <summary>Configures the placement, size, scrolling, and shared presentation of a dialog.</summary>
public record DialogOptions : OverlayOptions
{
    /// <summary>The placement of the dialog surface within its host.</summary>
    public DialogPlacement Placement { get; init; } = DialogPlacement.Auto;
    /// <summary>The named width used for the dialog surface. The default is medium.</summary>
    public DialogSize Size { get; init; } = DialogSize.Medium;
    /// <summary>Where scrolling is applied when content exceeds the available space.</summary>
    public DialogScrollMode ScrollMode { get; init; }

    /// <summary>When set, overrides the maximum width for the selected size.</summary>
    public double? MaxWidth { get; init; }

    /// <summary>Lets custom content supply its own surface, padding and scrolling.</summary>
    public bool IsContentFullBleed { get; init; }

    internal double? TopOffsetRatio { get; init; }

    internal double MaxPanelWidth => MaxWidth is > 0 and < double.PositiveInfinity ? MaxWidth.Value : Size switch
    {
        DialogSize.ExtraSmall => 320,
        DialogSize.Small => 384,
        DialogSize.Large => 512,
        DialogSize.Cover or DialogSize.Full => double.PositiveInfinity,
        _ => 448
    };
}

/// <summary>Extends dialog options with the semantic status shown for a message.</summary>
public sealed record MessageBoxOptions : DialogOptions
{
    /// <summary>Initializes a small message box with an OK button and explicit dismissal.</summary>
    public MessageBoxOptions()
    {
        Buttons = DialogButtons.OK;
        ShowCloseButton = false;
        CanLightDismiss = false;
        CanEscapeDismiss = false;
        Size = DialogSize.Small;
    }

    /// <summary>The semantic color and default indicator for the message.</summary>
    public MessageBoxStatus Status { get; init; }
}

/// <summary>Configures edge placement, the handle, and the shared presentation of a drawer.</summary>
public record DrawerOptions : OverlayOptions
{
    /// <summary>The edge where the drawer opens. The default is the bottom edge.</summary>
    public DrawerPlacement Placement { get; init; } = DrawerPlacement.Bottom;
    /// <summary>Whether the drawer shows its handle.</summary>
    public bool ShowHandle { get; init; } = true;

    /// <summary>Optional width for side drawers or height for top and bottom drawers, in DIPs.</summary>
    public double? Extent { get; init; }
}
