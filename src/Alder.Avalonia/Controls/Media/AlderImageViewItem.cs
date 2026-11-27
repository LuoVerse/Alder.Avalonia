using Avalonia;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Defines an image and its display metadata for an <see cref="AlderImageView"/>.</summary>
public sealed partial class AlderImageViewItem : AvaloniaObject
{
    /// <summary>The image shown in the viewport.</summary>
    [GeneratedStyledProperty]
    public partial IImage? Source { get; set; }

    /// <summary>An optional smaller image for the thumbnail strip. Defaults to <see cref="Source"/>.</summary>
    [GeneratedStyledProperty]
    public partial IImage? Thumbnail { get; set; }

    /// <summary>An optional title used in the header and thumbnail navigation.</summary>
    [GeneratedStyledProperty]
    public partial string? Title { get; set; }

    /// <summary>An optional file name displayed alongside the image dimensions.</summary>
    [GeneratedStyledProperty]
    public partial string? FileName { get; set; }

    /// <summary>An optional accessible description of the image.</summary>
    [GeneratedStyledProperty]
    public partial string? Description { get; set; }
}
