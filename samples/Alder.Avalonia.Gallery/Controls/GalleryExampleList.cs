using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace Alder.Avalonia.Gallery.Controls;

internal sealed class GalleryExampleList : AlderItemsControl
{
    public GalleryExampleList()
    {
        Template = new FuncControlTemplate<GalleryExampleList>((list, scope) =>
        {
            var presenter = new ItemsPresenter { Name = "PART_ItemsPresenter", ItemsPanel = list.ItemsPanel };
            scope.Register(presenter.Name, presenter);
            return presenter;
        });
        ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { CacheLength = 0.25 });
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.SetValue(MarginProperty, new Thickness(0, 0, 0, index == ItemCount - 1 ? 0 : 20),
            BindingPriority.Style);
    }
}
