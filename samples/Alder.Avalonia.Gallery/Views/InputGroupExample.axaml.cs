using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class InputGroupExample : UserControl
{
    public InputGroupExample() => InitializeComponent();

    private async void CopyWebsite(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InputGroupViewModel model || string.IsNullOrEmpty(model.Website)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            model.ReportCopy(false);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(model.Website);
            model.ReportCopy(true);
        }
        catch (Exception error) when (error is InvalidOperationException or ExternalException)
        {
            model.ReportCopy(false);
        }
    }
}
