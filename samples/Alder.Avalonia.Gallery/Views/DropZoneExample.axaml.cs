using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Gallery.Views;

public partial class DropZoneExample : UserControl
{
    public DropZoneExample() => InitializeComponent();

    private void OnFileListLoaded(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not DropZoneViewModel model || sender is not AlderDropZone zone) return;
        zone.Loaded -= OnFileListLoaded;
        model.InitializePreview(zone);
    }
}
