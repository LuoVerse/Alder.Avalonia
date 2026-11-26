using System.Windows.Input;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.Views;

public partial class AppLayoutInlineExample : UserControl
{
    public AppLayoutInlineExample()
    {
        SelectSettingsCommand = ReactiveCommand.Create(() =>
        {
            ProjectsItem.IsExpanded = true;
            NavigationSidebar.SelectedItem = SettingsItem;
        });
        InitializeComponent();
        DataContext = new NavigationBindingViewModel();
    }

    public ICommand SelectSettingsCommand { get; }
}
