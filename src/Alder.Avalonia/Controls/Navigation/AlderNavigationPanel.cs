using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Virtualizes one navigation level while retaining measured row or subtree heights.</summary>
internal sealed class AlderNavigationPanel : VirtualizingPanel
{
    private const double RowHeight = 38;
    private readonly SortedDictionary<int, Entry> realized = [];
    private readonly Dictionary<int, double> heights = [];
    private readonly Dictionary<int, double> prefixChanges = [];
    private readonly Dictionary<object, Stack<Control>> pool = [];
    private readonly Dictionary<object, Entry> detachedBranches = new(ReferenceEqualityComparer.Instance);
    private Rect? viewport;
    private double width;
    private int reveal = -1;

    public AlderNavigationPanel() => EffectiveViewportChanged += (_, e) =>
    {
        if (viewport == e.EffectiveViewport) return;
        viewport = e.EffectiveViewport;
        InvalidateMeasure();
    };

    private bool IsPopupMenu => ItemsControl is AlderContextMenu or MenuItem;
    private double Spacing => IsPopupMenu ? 2 : 0;
    private double Estimate => !IsPopupMenu && AlderSidebar.GetCompactRows(this) ? 32 : RowHeight;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlderSidebar.CompactRowsProperty || change.Property == AlderSidebar.RailProperty)
        {
            heights.Clear();
            prefixChanges.Clear();
            InvalidateMeasure();
        }
    }

    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        base.OnItemsControlChanged(oldValue);
        realized.Clear();
        heights.Clear();
        prefixChanges.Clear();
        pool.Clear();
        detachedBranches.Clear();
        viewport = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double estimate = Estimate;
        width = !IsPopupMenu && double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var visible = viewport ?? new Rect(0, 0, width, Math.Min(availableSize.Height, 400));
        var branch = ItemsControl as AlderSidebarItem;
        // A collapsed, realized parent keeps its small viewport cache for the next expansion.
        if (branch is { AreChildrenVisible: false })
            return new Size(width, Offset(Items.Count));

        double start = Math.Max(0, visible.Top - estimate);
        double end = visible.Height > 0 && visible.Bottom > 0 ? visible.Bottom + estimate : 0;
        if (end == 0 && branch is { IsSubmenuOpen: true })
        {
            // The first child must be available to the keyboard before the opening clip reaches it.
            if (realized.Count > 0)
                return new Size(width, Offset(Items.Count));
            start = 0;
            end = estimate;
        }
        int first = end > 0 ? IndexAt(start) : Items.Count;
        int last = first;
        double top = Offset(first);

        for (; last < Items.Count && top < end; last++)
        {
            var child = GetOrCreate(last);
            Measure(child, last, availableSize.Width);
            top += HeightOf(last);
        }

        foreach (int index in realized.Keys.ToArray())
        {
            if (index >= first && index < last) continue;
            if (index == reveal || branch?.IsSubmenuAnimating == true || realized[index].Control.IsKeyboardFocusWithin)
                Measure(realized[index].Control, index, availableSize.Width);
            else
                Release(index);
        }

        return new Size(width, Math.Max(0, Offset(Items.Count) - Spacing));
    }

    private void Measure(Control child, int index, double availableWidth)
    {
        child.Measure(new Size(availableWidth, double.PositiveInfinity));
        width = Math.Max(width, child.DesiredSize.Width);
        double height = Math.Max(1, child.DesiredSize.Height);
        if (IsPopupMenu) height = child.IsVisible ? child.DesiredSize.Height + Spacing : 0;
        double change = height - HeightOf(index);
        if (change == 0) return;
        if (height == Estimate) heights.Remove(index);
        else heights[index] = height;
        for (int node = index + 1; node <= Items.Count; node += node & -node)
            prefixChanges[node] = prefixChanges.GetValueOrDefault(node) + change;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (index, entry) in realized)
            entry.Control.Arrange(new Rect(0, Offset(index), finalSize.Width, RowHeightOf(index)));
        return finalSize;
    }

    protected override Control? ScrollIntoView(int index)
    {
        if ((uint)index >= (uint)Items.Count || !this.IsAttachedToVisualTree() || reveal >= 0) return null;
        reveal = index;
        try
        {
            var child = GetOrCreate(index);
            Measure(child, index, Bounds.Width);
            child.Arrange(new Rect(0, Offset(index), Bounds.Width, RowHeightOf(index)));
            child.BringIntoView(RevealBounds(child));
            InvalidateMeasure();
            TopLevel.GetTopLevel(this)?.UpdateLayout();
            // Measuring newly realized rows can move the target during the layout pass.
            if (!IsRevealVisible(child)) child.BringIntoView(RevealBounds(child));
            return child;
        }
        finally { reveal = -1; }
    }

    // A sidebar branch includes its subtree, so scroll only the header into view.
    private Rect RevealBounds(Control child) => new(0, 0, Bounds.Width,
        IsPopupMenu ? child.Bounds.Height : Math.Min(Estimate, child.Bounds.Height));

    private bool IsRevealVisible(Control child) =>
        IsArrangeValid && child.IsMeasureValid && child.IsArrangeValid && viewport is { } visible &&
        visible.Contains(RevealBounds(child).Translate(new Vector(child.Bounds.X, child.Bounds.Y)));

    protected override IEnumerable<Control> GetRealizedContainers() => realized.Values.Select(entry => entry.Control);
    protected override Control? ContainerFromIndex(int index) => realized.GetValueOrDefault(index)?.Control;
    protected override int IndexFromContainer(Control container) =>
        realized.FirstOrDefault(pair => ReferenceEquals(pair.Value.Control, container), new(-1, null!)).Key;

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        int index = from is Control child ? IndexFromContainer(child) : -1;
        int next = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => Items.Count - 1,
            NavigationDirection.Down or NavigationDirection.Next => index + 1,
            NavigationDirection.Up or NavigationDirection.Previous => index - 1,
            _ => -1
        };
        if (IsPopupMenu)
        {
            if (direction is NavigationDirection.Left or NavigationDirection.Right) return from;
            if (direction is not (NavigationDirection.First or NavigationDirection.Last or NavigationDirection.Up or
                NavigationDirection.Down or NavigationDirection.Next or NavigationDirection.Previous)) return null;
            int step = direction is NavigationDirection.Up or NavigationDirection.Previous or NavigationDirection.Last ? -1 : 1;
            return FindFocusable(next, step, wrap);
        }
        if (wrap && Items.Count > 0) next = (next + Items.Count) % Items.Count;
        if (realized.TryGetValue(next, out var entry) && IsRevealVisible(entry.Control)) return entry.Control;
        return ScrollIntoView(next);
    }

    private Control? FindFocusable(int index, int step, bool wrap)
    {
        if (!this.IsAttachedToVisualTree()) return null;
        for (int remaining = Items.Count; remaining > 0; remaining--, index += step)
        {
            if ((uint)index >= (uint)Items.Count)
            {
                if (!wrap) return null;
                index = step > 0 ? 0 : Items.Count - 1;
            }
            if (Items[index] is Separator or Control { IsVisible: false }) continue;
            bool wasRealized = realized.ContainsKey(index);
            var child = GetOrCreate(index);
            if (child is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
            {
                if (wasRealized && viewport is { } visible && visible.Contains(child.Bounds)) return child;
                return ScrollIntoView(index);
            }
            if (!wasRealized) Release(index);
        }
        return null;
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        // Keep a surviving focused or visible row attached when a sibling is inserted or removed.
        var surviving = new List<(int Old, int New, Entry Entry)>();
        foreach (var (old, entry) in realized.ToArray())
        {
            int index = ItemsControl?.ItemsView.IndexOf(entry.Item) ?? -1;
            if (index >= 0 && ReferenceEquals(items[index], entry.Item) && surviving.All(row => row.New != index))
                surviving.Add((old, index, entry));
            else
                Release(old);
        }
        realized.Clear();
        foreach (var (old, index, entry) in surviving) realized.Add(index, entry);
        foreach (var (old, index, entry) in surviving)
            if (old != index) ItemContainerGenerator?.ItemContainerIndexChanged(entry.Control, old, index);
        heights.Clear();
        prefixChanges.Clear();
        detachedBranches.Clear();
        InvalidateMeasure();
    }

    private Control GetOrCreate(int index)
    {
        if (realized.TryGetValue(index, out var existing)) return existing.Control;
        var item = Items[index];
        var generator = ItemContainerGenerator!;
        if (item is not null && detachedBranches.Remove(item, out var branch))
        {
            realized.Add(index, branch);
            InsertInternalChild(realized.Keys.TakeWhile(i => i < index).Count(), branch.Control);
            generator.ItemContainerPrepared(branch.Control, item, index);
            return branch.Control;
        }
        bool generated = generator.NeedsContainer(item, index, out var key);
        var control = generated
            ? key is not null && pool.TryGetValue(key, out var recycled) && recycled.TryPop(out var reuse)
                ? reuse
                : generator.CreateContainer(item, index, key)
            : (Control)item!;
        // ItemsControl owns inline menu items logically even when they are outside the viewport.
        // Preserve that parent so command bindings, shortcuts and radio groups still work offscreen.
        bool keepLogical = IsPopupMenu && !generated && ItemsControl?.ItemsSource is null && control.Parent == ItemsControl;
        realized.Add(index, new Entry(item, control, generated, key, keepLogical));
        generator.PrepareItemContainer(control, item, index);
        int position = realized.Keys.TakeWhile(i => i < index).Count();
        if (keepLogical) Children.Insert(position, control);
        else InsertInternalChild(position, control);
        if (generated && control is AlderSidebarItem row && OwnerMenu() is { } menu)
            menu.RestoreExpansion(row, item);
        generator.ItemContainerPrepared(control, item, index);
        return control;
    }

    private void Release(int index)
    {
        var entry = realized[index];
        if (entry.Control is AlderSidebarItem row)
        {
            if (entry.Generated) OwnerMenu()?.RememberExpansion(row, entry.Item);
            if (row.ItemsPanelRoot is AlderNavigationPanel children) children.ReleaseAll();
        }
        else if (entry.Control is MenuItem menuItem)
        {
            if (menuItem.IsSubMenuOpen) menuItem.Close();
            if (menuItem.ItemsPanelRoot is AlderNavigationPanel submenu) submenu.ReleaseAll();
        }
        realized.Remove(index);
        if (entry.KeepLogical) Children.Remove(entry.Control);
        else RemoveInternalChild(entry.Control);
        if (!entry.Generated) return;

        // Rebinding a TreeViewItem's child collection is treated as removal by TreeView.
        // Keep branch containers tied to their own data item so view recycling cannot deselect its children.
        if (entry.Item is { } item && entry.Control is AlderSidebarItem { ItemCount: > 0 })
        {
            if (detachedBranches.Count >= 16) detachedBranches.Remove(detachedBranches.Keys.First());
            detachedBranches[item] = entry;
            return;
        }

        ItemContainerGenerator!.ClearItemContainer(entry.Control);
        entry.Control.ClearValue(ItemsControl.ItemsSourceProperty);
        entry.Control.ClearValue(DataContextProperty);
        if (entry.Control is AlderSidebarItem)
        {
            entry.Control.ClearValue(TreeViewItem.IsSelectedProperty);
            entry.Control.ClearValue(TreeViewItem.IsExpandedProperty);
        }
        else if (entry.Control is MenuItem)
            entry.Control.ClearValue(MenuItem.IsCheckedProperty);
        if (entry.Key is null) return;
        if (!pool.TryGetValue(entry.Key, out var recycled)) pool.Add(entry.Key, recycled = new());
        if (recycled.Count < 32) recycled.Push(entry.Control);
    }

    private void ReleaseAll()
    {
        foreach (int index in realized.Keys.ToArray()) Release(index);
        detachedBranches.Clear();
        pool.Clear();
        InvalidateMeasure();
    }

    private AlderSidebarMenu? OwnerMenu() => ItemsControl as AlderSidebarMenu ??
        ItemsControl?.FindAncestorOfType<AlderSidebarMenu>();

    private double HeightOf(int index) => heights.TryGetValue(index, out double height) ? height : Estimate;
    private double RowHeightOf(int index) => Math.Max(0, HeightOf(index) - Spacing);

    // Sparse Fenwick sums keep height corrections local and prefix lookups logarithmic.
    // Unmeasured rows continue to use an ordinary row's height, even beside a large expanded subtree.
    private double Offset(int index)
    {
        double offset = index * Estimate;
        for (int node = index; node > 0; node -= node & -node)
            offset += prefixChanges.GetValueOrDefault(node);
        return offset;
    }

    private int IndexAt(double offset)
    {
        int low = 0;
        int high = Items.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (Offset(middle + 1) <= offset) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private sealed record Entry(object? Item, Control Control, bool Generated, object? Key, bool KeepLogical);
}
