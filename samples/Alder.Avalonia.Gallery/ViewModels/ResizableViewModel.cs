using System.Windows.Input;
using Avalonia.Controls;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class ResizableViewModel : GalleryViewModel
{
    public ResizableViewModel() => ToggleSidebar = ReactiveCommand.Create(() => IsSidebarCollapsed = !IsSidebarCollapsed);

    public GridLength? UsageSidebarSize
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsSidebarCollapsed
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SidebarAction));
        }
    }

    public string SidebarAction => Text(IsSidebarCollapsed ? "ResizableExpandSidebar" : "ResizableCollapseSidebar");

    public ICommand ToggleSidebar { get; }
}
