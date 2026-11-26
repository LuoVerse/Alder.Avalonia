using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alder.Avalonia.Gallery.Views;

public partial class PressableFeedbackExample : UserControl
{
    public PressableFeedbackExample()
    {
        InitializeComponent();
        DataContext = new PressableFeedbackViewModel();

        foreach (var card in Examples.Children.OfType<ExampleCard>())
            ExampleSource.AttachFiles(card, this, showOnlyThisCard: true);
    }

    private void ResetProgressSamples(object? sender, RoutedEventArgs e)
    {
        this.FindControl<AlderButton>("DeleteAccountSample")?.Feedback?.Reset();
        this.FindControl<AlderButton>("ApplySettingsSample")?.Feedback?.Reset();
    }
}
