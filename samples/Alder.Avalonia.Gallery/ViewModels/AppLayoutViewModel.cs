namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class AppLayoutViewModel : GalleryViewModel
{
    public NavigationPreview Usage { get; } = CreatePreview("LayoutDefault");
    public NavigationPreview InsetDashboard { get; } = CreatePreview("LayoutDashboard");
    public NavigationPreview Docs { get; } = CreatePreview("LayoutDocs");
    public NavigationPreview Collapsible { get; } = CreatePreview("LayoutCollapsible");
    public NavigationPreview OffCanvas { get; } = CreatePreview("LayoutOffcanvas");
    public NavigationPreview Inset { get; } = CreatePreview("LayoutInset");
    public NavigationPreview Floating { get; } = CreatePreview("LayoutFloating");
    public NavigationPreview Scroll { get; } = CreatePreview("LayoutScroll");
    public NavigationPreview Aside { get; } = CreatePreview("LayoutAside");
    public NavigationPreview Breadcrumbs { get; } = CreatePreview("LayoutBreadcrumbs");
    public NavigationPreview Toolbar { get; } = CreatePreview("LayoutToolbar");
    public NavigationPreview Complex { get; } = CreatePreview("LayoutComplex");
    public NavigationPreview ResizableSidebar { get; } = CreatePreview("LayoutResizableSidebar").RestoreWidths("resizable-sidebar");
    public NavigationPreview ResizableAside { get; } = CreatePreview("LayoutResizableAside").RestoreWidths("resizable-aside");

    private static NavigationPreview CreatePreview(string scenario)
    {
        var preview = NavigationPreviewCatalog.Create(scenario);
        preview.IsSearchEnabled = true;
        return preview;
    }
}
