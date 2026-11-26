using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class EmptyStateExample : UserControl
{
    public EmptyStateExample()
    {
        InitializeComponent();
        DataContext = new EmptyStateViewModel();
        foreach (var card in Examples.Children.OfType<ExampleCard>())
            ExampleSource.AttachFiles(card, this, showOnlyThisCard: true);
    }
}
