using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class ButtonGroupExample : UserControl
{
    public ButtonGroupExample() => InitializeComponent();

    private void OnMergeMenuOpening(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout menu)
            menu.Popup.ShouldUseOverlayLayer = true;
    }
}
