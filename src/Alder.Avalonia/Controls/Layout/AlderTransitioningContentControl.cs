using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Controls;

/// <summary>Displays content in a fixed frame and animates replacements using the selected transition.</summary>
/// <remarks>
/// The first content is displayed immediately. Set <see cref="TransitioningContentControl.IsTransitionReversed"/>
/// before replacing content to reverse directional transitions. The inherited <see cref="AlderMotion"/>
/// preference substitutes a short crossfade for the built-in animated modes.
/// </remarks>
public sealed class AlderTransitioningContentControl : TransitioningContentControl
{
    /// <summary>Identifies the <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<AlderContentTransition> TransitionProperty =
        AvaloniaProperty.Register<AlderTransitioningContentControl, AlderContentTransition>(
            nameof(Transition), AlderContentTransition.CrossFade);

    /// <summary>Identifies the <see cref="Duration"/> property.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<AlderTransitioningContentControl, TimeSpan?>(nameof(Duration));

    /// <summary>Gets or sets the built-in transition used for subsequent content changes.</summary>
    public AlderContentTransition Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets the duration override. Null uses the selected transition's default.
    /// Zero or negative values switch immediately.
    /// </summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }
}

/// <summary>Specifies how an <see cref="AlderTransitioningContentControl"/> replaces its content.</summary>
public enum AlderContentTransition
{
    /// <summary>Crossfades the two content layers. The default duration is 180 ms.</summary>
    CrossFade,
    /// <summary>Pushes the two layers horizontally. The default duration is 260 ms.</summary>
    Slide,
    /// <summary>Moves the content vertically while fading. The default duration is 220 ms.</summary>
    Rise,
    /// <summary>Scales the content around its center while fading. The default duration is 240 ms.</summary>
    Scale,
    /// <summary>Fades between layers while changing their focus. The default duration is 260 ms.</summary>
    Blur,
    /// <summary>Reveals the incoming layer along a moving edge shared with the outgoing layer. The default duration is 300 ms.</summary>
    Reveal,
    /// <summary>Replaces content immediately.</summary>
    None
}
