using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class AppLayoutBindingExample : UserControl
{
    public AppLayoutBindingExample()
    {
        InitializeComponent();
        DataContext = new NavigationBindingViewModel();
    }
}
