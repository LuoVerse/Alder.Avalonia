using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Gallery.Views;

public partial class AppLayoutExample : UserControl
{
    private readonly LocalizedNavigationBinding binding = new();
    private readonly LocalizedNavigationBinding inline = new();

    public AppLayoutExample()
    {
        InitializeComponent();
        BindingExample.DataContext = binding.Model;
        InlineExample.DataContext = inline.Model;
        AddHandler(AlderSidebarItem.InvokedEvent, OnNavigationItemInvoked);
    }

    private void OnNavigationItemInvoked(object? sender, RoutedEventArgs args)
    {
        if (args.Source is not AlderSidebarItem { SelectOnInvoke: true } item ||
            item.FindAncestorOfType<AlderAppLayout>() is not { } layout) return;
        layout.GetTemplateDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(control => control.Name == "PART_ContentScroll")?.ScrollToHome();
    }

    private void OnEpisodes(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: NavigationPreview model }) return;
        if (model.SearchItems().FirstOrDefault(item => item.Title == Alder.Avalonia.Gallery.Localization.GalleryLocalization.Current.Text("NavDemoEpisodes")) is { } entry)
            model.SelectedItem = entry;
    }

    private void OnJumpToSection(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: LayoutText section } button ||
            button.FindAncestorOfType<AlderAppLayout>() is not { } layout) return;
        var target = layout.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Classes.Contains("layout-scroll-section") && panel.DataContext == section);
        target?.BringIntoView();
    }
}
