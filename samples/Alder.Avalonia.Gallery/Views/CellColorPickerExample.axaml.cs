using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class CellColorPickerExample : UserControl
{
    public CellColorPickerExample()
    {
        InitializeComponent();
        DataContext = new CellColorPickerViewModel();
        foreach (var card in Examples.Children.OfType<ExampleCard>())
            ExampleSource.AttachFiles(card, this, showOnlyThisCard: true);
    }
}
