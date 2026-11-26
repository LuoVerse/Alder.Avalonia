using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace Alder.Avalonia.Gallery.Views;

public partial class NavbarExample : UserControl
{
    private TopLevel? root;

    public NavbarExample() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        root = TopLevel.GetTopLevel(this);
        root?.AddHandler(KeyDownEvent, FocusDocsSearch, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        root?.RemoveHandler(KeyDownEvent, FocusDocsSearch);
        root = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void FocusDocsSearch(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (!e.Handled && e.Key == Key.K && e.KeyModifiers == modifier && DocsSearch.IsEffectivelyVisible)
            e.Handled = DocsSearch.Focus();
    }

    private void PrepareDropdown(object? sender, EventArgs e)
    {
        if (sender is not AlderMenuFlyout flyout) return;
        flyout.Popup.ShouldUseOverlayLayer = true;
        flyout.Popup.WindowManagerAddShadowHint = false;
    }

    private async void OpenSearch(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control trigger) return;

        var localization = GalleryLocalization.Current;
        var sections = new[] { "NavbarDocs", "NavDemoFeatures", "NavbarIntegrations", "NavbarPricing" };
        var palette = new AlderCommand
        {
            Placeholder = localization.Text("NavDemoSearchDocs"),
            Groups = [new AlderCommandGroup { Items = sections.Select(key => new AlderCommandItem
                { Label = localization.Text(key), Command = (DataContext as NavbarViewModel)?.SearchDocs,
                    CommandParameter = localization.Text(key) }).ToArray() }]
        };
        await palette.ShowAsync(trigger);
    }
}
