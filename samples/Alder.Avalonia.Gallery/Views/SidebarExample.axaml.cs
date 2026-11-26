using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class SidebarExample : UserControl
{
    private readonly LocalizedNavigationBinding binding = new();
    private readonly LocalizedNavigationBinding inline = new();

    public SidebarExample()
    {
        InitializeComponent();
        BindingExample.DataContext = binding.Model;
        InlineExample.DataContext = inline.Model;
    }
}
