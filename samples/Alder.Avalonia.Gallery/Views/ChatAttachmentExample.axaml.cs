using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Alder.Avalonia.Gallery.Views;

public partial class ChatAttachmentExample : UserControl
{
    public ChatAttachmentExample() => InitializeComponent();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is ChatAttachmentViewModel model) model.ClearFiles();
    }
}
