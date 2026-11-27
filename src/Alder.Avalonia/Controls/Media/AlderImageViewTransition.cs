namespace Alder.Avalonia.Controls;

/// <summary>Controls how <see cref="AlderImageView"/> changes between images.</summary>
public enum AlderImageViewTransition
{
    /// <summary>Crossfades the two images.</summary>
    CrossFade,
    /// <summary>Slides images horizontally in the direction of navigation.</summary>
    Slide,
    /// <summary>Fades in the next image while scaling it up to its fitted size.</summary>
    Zoom,
    /// <summary>Fades in the next image while lifting it into place.</summary>
    Lift,
    /// <summary>Shows the new image immediately.</summary>
    None
}
