using Alder.Avalonia.Controls;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Gallery.Controls;

internal sealed class GalleryPageScrollViewer : AlderScrollViewer
{
    public GalleryPageScrollViewer() : base(resolveVerticalThumbEnd: true) => Classes.Add("galleryPageScroll");

    protected override Type StyleKeyOverride => typeof(AlderScrollViewer);

    protected override void ScrollToVerticalThumbEnd()
    {
        // Measure the final example before using the extent, which can still be estimated.
        if (Content is GallerySectionStack sections) sections.LoadRemainingSections();
        UpdateLayout();
        this.GetVisualDescendants().OfType<GalleryExampleList>().LastOrDefault()?.RevealLastExample();
        base.ScrollToVerticalThumbEnd();
    }
}
