using System.Windows.Input;
using Alder.Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class EmptyStateViewModel : GalleryViewModel
{
    public EmptyStateViewModel() =>
        Report = ReactiveCommand.Create<string>(key => Toast.Show(new ToastOptions(Text(key))));

    public ICommand Report { get; }
}
