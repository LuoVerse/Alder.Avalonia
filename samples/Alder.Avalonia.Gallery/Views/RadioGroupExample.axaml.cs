using Avalonia.Controls;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class RadioGroupExample : UserControl
{
    public RadioGroupExample()
    {
        InitializeComponent();
        UncontrolledPlans.PropertyChanged += (_, change) =>
        {
            if (change.Property == AlderChoiceGroup.SelectedKeysProperty &&
                UncontrolledPlans.SelectedKeys.FirstOrDefault() is { } plan &&
                DataContext is RadioGroupViewModel model)
                model.RecordSelection(plan);
        };
    }
}
