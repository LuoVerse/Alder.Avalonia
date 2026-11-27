using Alder.Avalonia.Gallery.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Gallery.Views;

public partial class SalashWindowExample : UserControl
{
    public SalashWindowExample()
    {
        InitializeComponent();
        OpenButton.IsEnabled = !OperatingSystem.IsBrowser();
        SampleSalash.DeferSources(() => SampleSalash.SetSourceFiles([Read("SalashPreviewWindow.axaml")],
            [Read("SalashPreviewWindow.axaml.cs"), Read("SalashWindowViewModel.cs")]));

        static (string Name, string Text) Read(string file) => (file, GalleryExampleSources.FullFile(file));
    }

    private void OpenPreview(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
            new SalashPreviewWindow().Show(owner);
    }
}
