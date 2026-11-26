using Avalonia.Controls;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class ProjectEditorView : UserControl
{
    public ProjectEditorView()
    {
        InitializeComponent();
        if (Design.IsDesignMode) DataContext = new ProjectEditorViewModel("Alder");
    }
}
