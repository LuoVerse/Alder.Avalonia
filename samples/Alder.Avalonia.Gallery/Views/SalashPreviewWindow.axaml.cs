using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class SalashPreviewWindow : AlderSalashWindow
{
    public SalashPreviewWindow()
    {
        InitializeComponent();
        DataContext = new SalashWindowViewModel();
        Opened += (_, _) => AccountField.Focus();
    }

    protected override Task<Window?> CreateNextWindow()
    {
        if (CloseResult != DialogResult.OK) return Task.FromResult<Window?>(null);

        var feedback = new TextBlock { FontSize = 26, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center };
        feedback[!TextBlock.TextProperty] = new DynamicResourceExtension("GalleryText.SalashSignedIn");
        var next = new AlderWindow
        {
            Width = 940, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = feedback, Title = "Alder"
        };
        return Task.FromResult<Window?>(next);
    }
}
