using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;

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

    internal double Spacing { get; init; } = 20;

    internal void Add(Control content)
    {
        LogicalChildren.Add(content);
        Items.Add(new Entry(content.Name) { Content = content });
    }

    internal void Add(Func<Control> create)
    {
        Items.Add(new Entry(null, () =>
        {
            var content = create();
            LogicalChildren.Add(content);
            return content;
        }));
    }

    internal void AddSection(string name, Control heading, GalleryExampleSection section)
    {
        LogicalChildren.Add(heading);
        // Keep section-level bindings and resources while using a single virtual panel for the page.
        LogicalChildren.Add(section);
        Items.Add(new Entry(null) { Section = name, Content = heading, BottomSpacing = 12 });
        foreach (var template in section.Examples)
        {
            Items.Add(new Entry(template.Name, () =>
            {
                var card = template.CreateCard();
                section.AttachExample(card);
                return card;
            }) { Section = name });
        }
    }

    internal bool RevealSection(string section, string? name, bool showCode)
    {
        var entries = Items.OfType<Entry>().Where(entry => entry.Section == section);
        if ((entries.FirstOrDefault(entry => entry.Name == name) ?? entries.FirstOrDefault()) is not { } target)
            return false;

        if (showCode && target.Realize() is ExampleCard card) card.ShowAxaml();
        Reveal(target);
        return true;
    }

    internal ExampleCard? FindExample(string? name) => Items.OfType<Entry>()
        .Select(entry => entry.Content).OfType<ExampleCard>().FirstOrDefault(card => card.Name == name);

    internal void Reveal(Control content)
    {
        if (Items.OfType<Entry>().FirstOrDefault(entry => entry.Content == content) is { } entry)
            Reveal(entry);
    }

    internal void RevealLastExample()
    {
        if (ItemCount > 0)
            Reveal((Entry)Items[ItemCount - 1]!);
    }

    private void Reveal(Entry entry)
    {
        int index = Items.IndexOf(entry);
        if (ItemsPanelRoot is VirtualizingStackPanel { FirstRealizedIndex: < 0 })
        {
            // A section below the viewport needs a layout pass before its list can scroll to an item.
            this.BringIntoView(new Rect(0, 0, Bounds.Width, 48));
            UpdateLayout();
        }

        ScrollIntoView(index);
        UpdateLayout();
        if (ContainerFromIndex(index) is null)
            ScrollIntoView(index);
    }

    internal void PreserveExamplePosition(ExampleCard card)
    {
        if (this.FindAncestorOfType<ScrollViewer>() is not { } scroll ||
            card.TranslatePoint(default, scroll) is not { } before) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!this.IsAttachedToVisualTree()) return;
            // When a code tab changes the card height, the virtual panel can estimate a different visible range.
            if (!card.IsAttachedToVisualTree()) Reveal(card);
            if (card.TranslatePoint(default, scroll) is { } after)
                scroll.Offset += new Vector(0, after.Y - before.Y);
        }, DispatcherPriority.Loaded);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new Slot(this);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        var entry = (Entry)item!;
        var slot = (Slot)container;
        slot.Margin = new Thickness(0, 0, 0, index == ItemCount - 1 ? 0 : entry.BottomSpacing ?? Spacing);
        slot.SetContent(entry.Realize());
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        // Recycle the slot and detach its visuals. The cached card keeps its preview and selected tab.
        ((Slot)container).SetContent(null);
    }

    private sealed class Entry(string? name, Func<Control>? create = null)
    {
        internal string? Name { get; } = name;
        internal string? Section { get; init; }
        internal double? BottomSpacing { get; init; }
        internal Control? Content { get; set; }

        internal Control Realize() => Content ??= create!();
    }

    private sealed class Slot(GalleryExampleList list) : Control
    {
        static Slot()
        {
            RequestBringIntoViewEvent.AddClassHandler<Slot>((slot, e) =>
            {
                if (e.TargetObject == slot)
                {
                    // Scroll to the top of a card even when its source code is taller than the viewport.
                    var target = e.TargetRect;
                    e.TargetRect = new Rect(target.X, target.Y, target.Width, Math.Min(target.Height, 48));
                }
            });
        }

        private Control? content;

        internal void SetContent(Control? value)
        {
            VisualChildren.Clear();
            content = value;
            // Logical ownership stays with the list or section to preserve data, resources, and tab state.
            if (value is not null) VisualChildren.Add(value);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (content is null) return default;
            // ScrollIntoView initially measures the destination unconstrained.
            // Keep wrapping and code height consistent with the current column width.
            if (!double.IsFinite(availableSize.Width) && list.Bounds.Width > 0)
                availableSize = new Size(list.Bounds.Width, availableSize.Height);
            content.Measure(availableSize);
            return content.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            content?.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
