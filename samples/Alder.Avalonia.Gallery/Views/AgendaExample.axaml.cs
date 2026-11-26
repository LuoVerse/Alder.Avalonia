using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class AgendaExample : UserControl
{
    public AgendaExample()
    {
        InitializeComponent();
        DataContext = new AgendaViewModel();
        ExampleSource.AttachFiles(SampleAgenda, this);
    }
}
