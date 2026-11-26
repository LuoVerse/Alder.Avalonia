namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class SidebarViewModel : GalleryViewModel
{
    public NavigationPreview Usage { get; } = NavigationPreviewCatalog.Create("SidebarDefault");
    public NavigationPreview Complex { get; } = NavigationPreviewCatalog.Create("SidebarComplex");
    public NavigationPreview Account { get; } = NavigationPreviewCatalog.Create("SidebarAccount");
    public NavigationPreview Inset { get; } = NavigationPreviewCatalog.Create("SidebarInset");
    public NavigationPreview Floating { get; } = NavigationPreviewCatalog.Create("SidebarFloating");
    public NavigationPreview Right { get; } = NavigationPreviewCatalog.Create("SidebarRight");
    public NavigationPreview Collapsible { get; } = NavigationPreviewCatalog.Create("SidebarCollapsible");
    public NavigationPreview IconOnly { get; } = NavigationPreviewCatalog.Create("SidebarIconOnly");
    public NavigationPreview Groups { get; } = NavigationPreviewCatalog.Create("SidebarGroups");
    public NavigationPreview CollapsibleGroups { get; } = NavigationPreviewCatalog.Create("SidebarDocumentation");
    public NavigationPreview Avatar { get; } = NavigationPreviewCatalog.Create("SidebarAvatar");
    public NavigationPreview Pages { get; } = NavigationPreviewCatalog.Create("SidebarPages");
    public NavigationPreview MeetingNotes { get; } = NavigationPreviewCatalog.Create("SidebarMeetingNotes");
    public NavigationPreview AgentHub { get; } = NavigationPreviewCatalog.Create("SidebarAgentHub");
    public NavigationPreview AgentWorkspace { get; } = NavigationPreviewCatalog.Create("SidebarAgentWorkspace");
    public NavigationPreview ReducedMotion { get; } = NavigationPreviewCatalog.Create("SidebarReducedMotion");
}
