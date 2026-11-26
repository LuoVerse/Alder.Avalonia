using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class SidebarBindingExample : UserControl
{
    public SidebarBindingExample()
    {
        InitializeComponent();
        DataContext = new NavigationBindingViewModel();
    }
}
