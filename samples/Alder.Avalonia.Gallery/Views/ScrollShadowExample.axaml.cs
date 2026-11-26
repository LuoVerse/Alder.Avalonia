using Avalonia.Controls;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class ScrollShadowExample : UserControl
{
    public ScrollShadowExample() => InitializeComponent();

    private void VerticalShadowChanged(object? sender, ScrollShadowVisibility visibility)
    {
        if (DataContext is ScrollShadowViewModel model) model.VerticalState = visibility;
    }

    private void HorizontalShadowChanged(object? sender, ScrollShadowVisibility visibility)
    {
        if (DataContext is ScrollShadowViewModel model) model.HorizontalState = visibility;
    }
}
