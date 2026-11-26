using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Alder.Avalonia.Gallery.Views;

public partial class CheckBoxExample : UserControl
{
    public CheckBoxExample() => InitializeComponent();

    private void OnExternalLabelTapped(object? sender, TappedEventArgs e) =>
        ExternalCheck.SetCurrentValue(ToggleButton.IsCheckedProperty, ExternalCheck.IsChecked != true);
}
