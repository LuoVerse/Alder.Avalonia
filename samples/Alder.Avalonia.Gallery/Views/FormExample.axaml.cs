using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Gallery.Views;

public partial class FormExample : UserControl
{
    public FormExample() => InitializeComponent();

    private void ResetForm(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FormDemoViewModel { IsSubmitting: false } model)
            return;

        model.Reset();
        ProjectForm.ResetValidation();
    }
}
