using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Platform;

namespace Alder.Avalonia.Gallery.Views;

public partial class GalleryWindow : AlderWindow
{
    public GalleryWindow()
    {
        InitializeComponent();
        DataContext = GalleryContent.DataContext;
        Opened += (_, _) => MacWindowColorSpace.Apply(this);
    }
}
