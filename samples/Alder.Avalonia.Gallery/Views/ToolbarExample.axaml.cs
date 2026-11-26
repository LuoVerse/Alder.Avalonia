using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class ToolbarExample : UserControl
{
    public ToolbarExample() => InitializeComponent();

    private async void CopyText(object? sender, RoutedEventArgs e) => await CopySample(cut: false);
    private async void CutText(object? sender, RoutedEventArgs e) => await CopySample(cut: true);

    private async Task CopySample(bool cut)
    {
        if (DataContext is not ToolbarViewModel model) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            model.ClipboardUnavailable();
            return;
        }

        var sample = model.Sample;
        try
        {
            await clipboard.SetTextAsync(sample);
            model.Copied(cut, sample);
        }
        catch (Exception error) when (error is InvalidOperationException or ExternalException)
        {
            model.ClipboardUnavailable();
        }
    }
}
