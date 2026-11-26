using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Gallery.Views;

public partial class DropZoneExample : UserControl
{
    public DropZoneExample() => InitializeComponent();

    private async void OnFileListLoaded(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not DropZoneViewModel model || sender is not AlderDropZone zone) return;
        zone.Loaded -= OnFileListLoaded;
        await model.LoadExampleFilesAsync(zone);
        zone.FilesChanged += OnProgressFilesChanged;
    }

    private async void OnProgressFilesChanged(object? sender, DropZoneFilesChangedEventArgs args)
    {
        if (DataContext is DropZoneViewModel model && sender is AlderDropZone zone)
            await model.AnimateFilesAsync(zone, args.Added);
    }

    private async void OnRetryRequested(object? sender, AlderDropZoneFile file)
    {
        if (DataContext is DropZoneViewModel model && sender is AlderDropZone zone)
            await model.AnimateFilesAsync(zone, [file]);
    }
}
