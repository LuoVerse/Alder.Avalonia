using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies when indentation guides are drawn in a file tree.</summary>
public enum FileTreeGuideLines
{
    /// <summary>Always shows the guides.</summary>
    Always,
    /// <summary>Shows the guides on pointer hover.</summary>
    Hover,
    /// <summary>Hides the guides.</summary>
    Hidden
}
/// <summary>Indicates where a node should be moved relative to its target.</summary>
public enum FileTreeDropPosition
{
    /// <summary>Places the node before the target.</summary>
    Before,
    /// <summary>Places the node inside the target folder.</summary>
    Into,
    /// <summary>Places the node after the target.</summary>
    After
}

/// <summary>A request to update the caller's file collection. The tree never modifies files.</summary>
public sealed record AlderFileTreeMove(object Item, object Target, FileTreeDropPosition Position)
{
    private readonly IReadOnlyList<object>? items;

    /// <summary>The items in tree order when moving a selection. A single move contains only <see cref="Item"/>.</summary>
    public IReadOnlyList<object> Items { get => items ?? [Item]; init => items = value; }
}

/// <summary>Provides a keyed, observable node model with optional icons and child nodes.</summary>
/// <param name="key">The stable identifier for the node.</param>
/// <param name="title">The initial display label.</param>
public sealed class AlderFileTreeNode(string key, string title) : INotifyPropertyChanged
{
    /// <summary>The stable identifier of this node.</summary>
    public string Key { get; } = key;
    /// <summary>The primary text displayed for the content.</summary>
    public string Title { get; } = title;
    /// <summary>Whether the node represents a container for other nodes.</summary>
    public bool IsFolder { get; init; }
    /// <summary>The optional geometry displayed with the content.</summary>
    public object? Icon { get; init; }
    /// <summary>The optional icon shown when this node is expanded.</summary>
    public Geometry? ExpandedIcon { get; init; }
    /// <summary>Optional supporting information rendered beside the node title.</summary>
    public object? Badge { get; init; }
    /// <summary>The observable collection of child nodes.</summary>
    public AvaloniaList<AlderFileTreeNode> Children { get; } = [];

    /// <summary>Whether the node's children are expanded.</summary>
    public bool IsExpanded
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
        }
    }

    /// <summary>Raised when a node property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A selectable tree with folder indicators, optional indentation guides and command-driven moves.</summary>
public sealed partial class AlderFileTree : TreeView, ICustomKeyboardNavigation
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderFileTree, AlderSize>(nameof(Size), AlderSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="GuideLines"/> Avalonia property.</summary>
    public static readonly StyledProperty<FileTreeGuideLines> GuideLinesProperty =
        AvaloniaProperty.Register<AlderFileTree, FileTreeGuideLines>(nameof(GuideLines));
    /// <summary>Identifies the <see cref="ShowIcons"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowIconsProperty =
        AvaloniaProperty.Register<AlderFileTree, bool>(nameof(ShowIcons), inherits: true);
    /// <summary>Identifies the <see cref="ShowSelectionCheckboxes"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSelectionCheckboxesProperty =
        AvaloniaProperty.Register<AlderFileTree, bool>(nameof(ShowSelectionCheckboxes), inherits: true);
    /// <summary>Identifies the <see cref="IndicatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorIconProperty =
        AvaloniaProperty.Register<AlderFileTree, Geometry?>(nameof(IndicatorIcon), inherits: true);
    /// <summary>Identifies the <see cref="MoveCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> MoveCommandProperty =
        AvaloniaProperty.Register<AlderFileTree, ICommand?>(nameof(MoveCommand), inherits: true);
    /// <summary>Identifies the <see cref="MoveSelectionTogether"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> MoveSelectionTogetherProperty =
        AvaloniaProperty.Register<AlderFileTree, bool>(nameof(MoveSelectionTogether));
    /// <summary>Identifies the internal presentation state inherited by file-tree rows.</summary>
    public static readonly StyledProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.Register<AlderFileTree, bool>("ShowGuides", true, inherits: true);

    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>When indentation guides are drawn for nested rows.</summary>
    public FileTreeGuideLines GuideLines { get => GetValue(GuideLinesProperty); set => SetValue(GuideLinesProperty, value); }
    /// <summary>Whether node icons are visible.</summary>
    public bool ShowIcons { get => GetValue(ShowIconsProperty); set => SetValue(ShowIconsProperty, value); }
    /// <summary>Whether rows expose a selection checkbox.</summary>
    public bool ShowSelectionCheckboxes { get => GetValue(ShowSelectionCheckboxesProperty); set => SetValue(ShowSelectionCheckboxesProperty, value); }
    /// <summary>The geometry for the node's expansion indicator.</summary>
    public Geometry? IndicatorIcon { get => GetValue(IndicatorIconProperty); set => SetValue(IndicatorIconProperty, value); }
    /// <summary>The command that receives a node move request.</summary>
    public ICommand? MoveCommand { get => GetValue(MoveCommandProperty); set => SetValue(MoveCommandProperty, value); }
    /// <summary>Pass all selected nodes to <see cref="MoveCommand"/> when a selected node is dragged.</summary>
    public bool MoveSelectionTogether { get => GetValue(MoveSelectionTogetherProperty); set => SetValue(MoveSelectionTogetherProperty, value); }

    private sealed record FileTreeDrag(AlderFileTreeItem Source, AlderFileTreeItem[] Rows, IReadOnlyList<object> Items);
    private sealed record PointerDrag(IPointer Pointer, TopLevel Root, AlderFileTreeItem Row, Point Origin)
    {
        public FileTreeDrag? Selection { get; set; }
    }

    private PointerDrag? pointerDrag;
    private AlderFileTreeItem? dropItem;
    private FileTreeDropPosition? dropPosition;
    private readonly ConditionalWeakTable<object, ExpandedBranch> expandedBranches = new();
    private sealed record ExpandedBranch(IEnumerable Children);

    /// <summary>Initializes a new instance of <see cref="AlderFileTree"/>.</summary>
    public AlderFileTree()
    {
        AddHandler(PointerPressedEvent, CaptureDragPress, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, UpdatePointerDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, CompletePointerDrag, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        // A generated row also owns its child presenter and its expansion and selection state.
        recycleKey = null;
        return item is not AlderFileTreeItem;
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderFileTreeItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        var row = (AlderFileTreeItem)container;
        row.SetItem(item);
        if (item is not null && expandedBranches.TryGetValue(item, out _))
            row.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        ReleaseRow((AlderFileTreeItem)container);
        base.ClearContainerForItemOverride(container);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GuideLinesProperty || change.Property == IsPointerOverProperty)
            SetCurrentValue(ShowGuidesProperty, GuideLines == FileTreeGuideLines.Always ||
                GuideLines == FileTreeGuideLines.Hover && IsPointerOver);
        if (change.Property == SelectedItemProperty || change.Property == AutoScrollToSelectedItemProperty)
            QueueSelectionScroll();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        QueueSelectionScroll();
    }

    private void QueueSelectionScroll()
    {
        if (!AutoScrollToSelectedItem || SelectedItem is not { } item ||
            !this.IsAttachedToVisualTree() || TreeContainerFromItem(item) is not null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (AutoScrollToSelectedItem && Equals(SelectedItem, item) && this.IsAttachedToVisualTree() &&
                TreeContainerFromItem(item) is null)
                RealizeItem(item);
        }, DispatcherPriority.Loaded);
    }

    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        if (direction is not (NavigationDirection.Next or NavigationDirection.Previous))
            return (false, null);
        if (element is Visual visual && this.IsVisualAncestorOf(visual))
            return (true, null);
        var row = (SelectedItem is { } item ? RealizeItem(item) : null) ?? RealizeRow(this, 0);
        while (row is not null && (!row.Focusable || !row.IsEffectivelyEnabled))
            row = GetAdjacentRow(row, Key.Down);
        return (row is not null, row);
    }

    private AlderFileTreeItem? RealizeItem(object item)
    {
        if (TreeContainerFromItem(item) is AlderFileTreeItem { IsEffectivelyEnabled: true } realized)
            return realized;
        if (ItemsView.IndexOf(item) is >= 0 and var rootIndex)
            return RealizeRow(this, rootIndex);
        var path = new Stack<int>();
        if (!FindExpandedPath(ItemsView, this, item, path)) return null;
        ItemsControl owner = this;
        foreach (var index in path)
        {
            if (RealizeRow(owner, index) is not { } row) return null;
            owner = row;
        }
        return owner as AlderFileTreeItem;
    }

    private bool FindExpandedPath(IEnumerable items, ItemsControl? owner, object target, Stack<int> path)
    {
        int index = 0;
        foreach (var item in items)
        {
            if (item is not null)
            {
                var row = owner?.ContainerFromIndex(index) as AlderFileTreeItem ?? item as AlderFileTreeItem;
                if (Equals(item, target) || GetExpandedChildren(item, row) is { } children &&
                    FindExpandedPath(children, row, target, path))
                {
                    path.Push(index);
                    return true;
                }
            }
            index++;
        }
        return false;
    }

    internal void ToggleSelection(AlderFileTreeItem row) =>
        UpdateSelectionFromContainer(row, select: true, rangeModifier: false, toggleModifier: true, rightButton: false);

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Directional && e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
            e.Source is Visual source && FindRow(source) is { } row && SelectRange(row))
            e.Handled = true;
        else
            base.OnGotFocus(e);
    }

    /// <inheritdoc />
    public override bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
    {
        if (!eventArgs.Handled && container is AlderFileTreeItem row &&
            ItemSelectionEventTriggers.HasRangeSelectionModifier(row, eventArgs) &&
            (eventArgs is PointerEventArgs pointer && !pointer.Properties.IsRightButtonPressed && ShouldTriggerSelection(row, pointer) ||
             eventArgs is KeyEventArgs keyboard && ShouldTriggerSelection(row, keyboard)) && SelectRange(row))
        {
            if (eventArgs is PointerEventArgs) row.PerformFeedback(FeedbackAction.Click);
            eventArgs.Handled = true;
            return true;
        }
        return base.UpdateSelectionFromEvent(container, eventArgs);
    }

    private bool SelectRange(AlderFileTreeItem row)
    {
        if (!SelectionMode.HasFlag(SelectionMode.Multiple) || SelectedItem is not { } anchor || row.Item is not { } target)
            return false;
        var items = GetExpandedItems(ItemsView, this).Select(entry => entry.Item).ToList();
        int start = items.IndexOf(anchor), end = items.IndexOf(target);
        if (start < 0 || end < 0) return false;
        var desired = items.GetRange(Math.Min(start, end), Math.Abs(start - end) + 1);
        if (start > end) desired.Reverse();
        SynchronizeSelection(desired);
        return true;
    }

    private void SynchronizeSelection(IReadOnlyList<object> desired)
    {
        var wanted = desired.ToHashSet();
        for (int i = SelectedItems.Count - 1; i >= 0; i--)
            if (!wanted.Contains(SelectedItems[i]!)) SelectedItems.RemoveAt(i);
        var selected = SelectedItems.Cast<object>().ToHashSet();
        var added = desired.Where(selected.Add).ToArray();
        if (SelectedItems is AvaloniaList<object> list)
            list.AddRange(added);
        else
            foreach (var item in added) SelectedItems.Add(item);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && SelectionMode.HasFlag(SelectionMode.Multiple) &&
            Application.Current?.PlatformSettings?.HotkeyConfiguration.SelectAll?.Any(gesture => gesture.Matches(e)) == true)
        {
            SynchronizeSelection(GetExpandedItems(ItemsView, this).Select(entry => entry.Item).ToArray());
            e.Handled = true;
            return;
        }
        if (!e.Handled && e.Key is Key.Up or Key.Down or Key.Right &&
            e.Source is Visual source && FindRow(source) is { } row)
        {
            var current = row;
            while (GetAdjacentRow(current, e.Key) is { } target)
            {
                if (target.Focus(NavigationMethod.Directional, e.KeyModifiers))
                {
                    target.RowSurface?.BringIntoView();
                    break;
                }
                current = target;
            }
            e.Handled = true;
            return;
        }
        if (ItemCount > 0 || e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right))
            base.OnKeyDown(e);
    }

    private static AlderFileTreeItem? GetAdjacentRow(AlderFileTreeItem row, Key key)
    {
        if (key != Key.Up && row.IsExpanded && row.ItemCount > 0)
            return RealizeRow(row, 0);
        while (row.Parent is ItemsControl owner)
        {
            int index = owner.IndexFromContainer(row);
            if (key == Key.Up)
            {
                if (index <= 0)
                    return owner as AlderFileTreeItem;
                var previous = RealizeRow(owner, index - 1);
                while (previous is { IsExpanded: true, ItemCount: > 0 } &&
                       RealizeRow(previous, previous.ItemCount - 1) is { } lastChild)
                    previous = lastChild;
                return previous;
            }
            if (index >= 0 && index + 1 < owner.ItemCount)
                return RealizeRow(owner, index + 1);
            if (owner is not AlderFileTreeItem parent) break;
            row = parent;
        }
        return null;
    }

    private static AlderFileTreeItem? RealizeRow(ItemsControl owner, int index)
    {
        if ((uint)index >= (uint)owner.ItemCount) return null;
        if (owner.ContainerFromIndex(index) is AlderFileTreeItem row) return row;
        owner.ScrollIntoView(index);
        return owner.ContainerFromIndex(index) as AlderFileTreeItem;
    }

    internal AlderFileTreeItem? RealizeLastVisibleRow()
    {
        var row = RealizeRow(this, ItemCount - 1);
        while (row is { IsExpanded: true, ItemCount: > 0 })
            row = RealizeRow(row, row.ItemCount - 1);
        return row;
    }

    internal void ReleaseRow(AlderFileTreeItem row)
    {
        if (pointerDrag is { } session &&
            (row == session.Row || row == dropItem || row.IsVisualAncestorOf(session.Row) ||
             session.Selection?.Rows.Contains(row) == true))
            EndPointerDrag();
        RememberExpansion(row);
        row.SetItem(null);
    }

    internal void RememberExpansion(AlderFileTreeItem row)
    {
        // AlderFileTreeNode and explicit containers already store expansion on the item.
        if (row.Item is { } item && item is not (AlderFileTreeNode or AlderFileTreeItem))
        {
            if (row.IsExpanded && row.ItemsSource is { } children)
                expandedBranches.AddOrUpdate(item, new ExpandedBranch(children));
            else
                expandedBranches.Remove(item);
        }
    }

    private IEnumerable<(object Item, AlderFileTreeItem? Row)> GetExpandedItems(IEnumerable items, ItemsControl? owner,
        HashSet<object>? skipChildren = null)
    {
        int index = 0;
        foreach (var item in items)
        {
            var row = owner?.ContainerFromIndex(index) as AlderFileTreeItem ?? item as AlderFileTreeItem;
            index++;
            if (item is null) continue;
            if (row is not { IsSection: true })
            {
                yield return (item, row);
                if (skipChildren?.Contains(item) == true) continue;
            }
            if (GetExpandedChildren(item, row) is { } children)
                foreach (var child in GetExpandedItems(children, row, skipChildren)) yield return child;
        }
    }

    private IEnumerable? GetExpandedChildren(object item, AlderFileTreeItem? row)
    {
        if (row is not null) return row.IsExpanded ? row.ItemsView : null;
        if (item is AlderFileTreeNode node) return node.IsExpanded ? node.Children : null;
        return expandedBranches.TryGetValue(item, out var expanded) ? expanded.Children : null;
    }

    internal bool MoveByKeyboard(AlderFileTreeItem row, int direction)
    {
        var parent = row.FindAncestorOfType<AlderFileTreeItem>() as ItemsControl ?? this;
        int index = parent.IndexFromContainer(row) + direction;
        if (RealizeRow(parent, index) is not { } target) return false;
        var position = direction < 0 ? FileTreeDropPosition.Before : FileTreeDropPosition.After;
        if (MoveCommand is not { } command || GetMove(row, target, position) is not { } move || !command.CanExecute(move))
            return false;
        command.Execute(move);
        Dispatcher.UIThread.Post(() => RealizeItem(move.Item)?.Focus(NavigationMethod.Directional), DispatcherPriority.Loaded);
        return true;
    }

    private void CaptureDragPress(object? sender, PointerPressedEventArgs e)
    {
        if (pointerDrag is not null || MoveCommand is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.Source is not Visual visual || FindRow(visual) is not { Item: not null } row || !row.IsDragHandle(visual) ||
            TopLevel.GetTopLevel(this) is not { } root) return;

        pointerDrag = new(e.Pointer, root, row, e.GetPosition(root));
        root.AddHandler(KeyDownEvent, CancelDragOnEscape, RoutingStrategies.Tunnel);
        e.Pointer.Capture(this);
        e.PreventGestureRecognition();
        e.Handled = true;
    }

    private void UpdatePointerDrag(object? sender, PointerEventArgs e)
    {
        if (pointerDrag is not { } session || session.Pointer != e.Pointer) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !session.Row.IsAttachedToVisualTree() || MoveCommand is null)
        {
            EndPointerDrag();
            return;
        }

        var point = e.GetPosition(session.Root);
        if (session.Selection is null)
        {
            var delta = point - session.Origin;
            if (delta.X * delta.X + delta.Y * delta.Y < 25) return;

            session.Selection = CreateDrag(session.Row);
            foreach (var row in session.Selection.Rows) row.Classes.Add("dragging");
            session.Row.Classes.Add("drag-grip-visible");
            BeginDragPreview(session);
        }

        dragPreview?.Move(session.Root, point);
        var drop = FindDrop(session, point);
        SetDrop(drop?.Target, drop?.Move.Position);
        e.Handled = true;
    }

    private void CompletePointerDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (pointerDrag is not { } session || session.Pointer != e.Pointer ||
            e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) return;

        var drop = FindDrop(session, e.GetPosition(session.Root));
        EndPointerDrag();
        if (drop is { } accepted && MoveCommand is { } command) command.Execute(accepted.Move);
        e.Handled = true;
    }

    private void CancelDragOnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || pointerDrag is null) return;
        EndPointerDrag();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Source, this) && pointerDrag?.Pointer == e.Pointer) EndPointerDrag();
    }

    private void EndPointerDrag()
    {
        if (pointerDrag is not { } session) return;
        pointerDrag = null;
        session.Root.RemoveHandler(KeyDownEvent, CancelDragOnEscape);
        session.Row.Classes.Remove("drag-grip-visible");
        if (session.Selection is { } drag)
        {
            foreach (var item in drag.Rows) item.Classes.Remove("dragging");
        }
        SetDrop(null, null);
        dragPreview?.Dispose();
        dragPreview = null;
        if (session.Pointer.Captured == this) session.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndPointerDrag();
        base.OnDetachedFromVisualTree(e);
    }

    private FileTreeDrag CreateDrag(AlderFileTreeItem source)
    {
        if (!MoveSelectionTogether || !source.IsSelected || SelectedItems?.Count is not > 1)
            return new(source, [source], [source.Item!]);

        var selected = SelectedItems.Cast<object>().ToHashSet();
        var entries = GetExpandedItems(ItemsView, this, selected)
            .Where(entry => selected.Contains(entry.Item) && entry.Row is not { IsEffectivelyEnabled: false }).ToArray();
        var rows = entries.Select(entry => entry.Row).OfType<AlderFileTreeItem>().ToArray();
        if (rows.Length == 0) return new(source, [source], [source.Item!]);
        return new(rows.Contains(source) ? source : rows[0], rows, entries.Select(entry => entry.Item).ToArray());
    }

    private (AlderFileTreeItem Target, AlderFileTreeMove Move)? FindDrop(PointerDrag session, Point point)
    {
        if (session.Selection is not { } drag || !session.Row.IsAttachedToVisualTree() ||
            session.Root.InputHitTest(point) is not Visual visual || FindRow(visual) is not { RowSurface: { } surface } target ||
            session.Root.TranslatePoint(point, surface) is not { } hit ||
            !new Rect(surface.Bounds.Size).Contains(hit) || MoveCommand is not { } command) return null;

        double y = hit.Y;
        double height = surface.Bounds.Height;
        var position = y < height * .25 ? FileTreeDropPosition.Before :
            y > height * .75 || !target.IsBranch && y >= height / 2 ? FileTreeDropPosition.After :
            target.IsBranch ? FileTreeDropPosition.Into : FileTreeDropPosition.Before;
        if (GetMove(drag.Source, target, position) is not { } move ||
            drag.Items.Count > 1 && target.GetVisualAncestors().Prepend(target).TakeWhile(ancestor => ancestor != this)
                .OfType<AlderFileTreeItem>().Any(row => row.Item is { } item && drag.Items.Contains(item))) return null;
        if (drag.Items.Count > 1) move = move with { Items = drag.Items };
        return command.CanExecute(move) ? (target, move) : null;
    }

    private static AlderFileTreeMove? GetMove(AlderFileTreeItem source, AlderFileTreeItem target, FileTreeDropPosition position) =>
        source != target && !source.IsVisualAncestorOf(target) && !target.IsSection && target.IsEffectivelyEnabled &&
        source.Item is { } item && target.Item is { } destination ? new(item, destination, position) : null;

    private AlderFileTreeItem? FindRow(Visual visual) =>
        visual.FindAncestorOfType<AlderFileTreeItem>(includeSelf: true) is { } row && row.FindAncestorOfType<AlderFileTree>() == this ? row : null;

    private void SetDrop(AlderFileTreeItem? target, FileTreeDropPosition? position)
    {
        if (dropItem == target && dropPosition == position) return;
        if (dropItem != target) dropItem?.SetDrop(null);
        dropItem = target;
        dropPosition = position;
        target?.SetDrop(position);
    }
}

/// <summary>Presents a node with indentation, expansion, selection, and optional drag affordances.</summary>
public sealed class AlderFileTreeItem : TreeViewItem
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AlderFileTree.SizeProperty.AddOwner<AlderFileTreeItem>();
    /// <summary>Identifies the <see cref="ShowIcons"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowIconsProperty = AlderFileTree.ShowIconsProperty.AddOwner<AlderFileTreeItem>();
    /// <summary>Identifies the <see cref="ShowSelectionCheckboxes"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSelectionCheckboxesProperty = AlderFileTree.ShowSelectionCheckboxesProperty.AddOwner<AlderFileTreeItem>();
    /// <summary>Identifies the <see cref="IndicatorIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IndicatorIconProperty = AlderFileTree.IndicatorIconProperty.AddOwner<AlderFileTreeItem>();
    /// <summary>Identifies the <see cref="MoveCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> MoveCommandProperty = AlderFileTree.MoveCommandProperty.AddOwner<AlderFileTreeItem>();
    /// <summary>Identifies the <see cref="ShowGuides"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowGuidesProperty = AlderFileTree.ShowGuidesProperty.AddOwner<AlderFileTreeItem>();

    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size => GetValue(SizeProperty);
    /// <summary>Whether to show the icons.</summary>
    public bool ShowIcons => GetValue(ShowIconsProperty);
    /// <summary>Whether to show the selection checkboxes.</summary>
    public bool ShowSelectionCheckboxes => GetValue(ShowSelectionCheckboxesProperty);
    /// <summary>The geometry used for the indicator icon.</summary>
    public Geometry? IndicatorIcon => GetValue(IndicatorIconProperty);
    /// <summary>The command associated with the move action.</summary>
    public ICommand? MoveCommand => GetValue(MoveCommandProperty);
    /// <summary>Whether the row currently draws its indentation guides.</summary>
    public bool ShowGuides => GetValue(ShowGuidesProperty);

    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AlderFileTreeItem, object?>(nameof(Icon));
    /// <summary>Identifies the <see cref="ExpandedIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> ExpandedIconProperty =
        AvaloniaProperty.Register<AlderFileTreeItem, Geometry?>(nameof(ExpandedIcon));
    /// <summary>Identifies the <see cref="Badge"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<AlderFileTreeItem, object?>(nameof(Badge));
    /// <summary>Identifies the <see cref="IsFolder"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsFolderProperty =
        AvaloniaProperty.Register<AlderFileTreeItem, bool>(nameof(IsFolder));
    /// <summary>Identifies the <see cref="IsSection"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSectionProperty =
        AvaloniaProperty.Register<AlderFileTreeItem, bool>(nameof(IsSection));
    /// <summary>Identifies the <see cref="DisplayIcon"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderFileTreeItem, object?> DisplayIconProperty =
        AvaloniaProperty.RegisterDirect<AlderFileTreeItem, object?>(nameof(DisplayIcon), x => x.DisplayIcon);
    /// <summary>Identifies the <see cref="RowIndent"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderFileTreeItem, Thickness> RowIndentProperty =
        AvaloniaProperty.RegisterDirect<AlderFileTreeItem, Thickness>(nameof(RowIndent), x => x.RowIndent);
    /// <summary>Identifies the <see cref="GuideIndent"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderFileTreeItem, Thickness> GuideIndentProperty =
        AvaloniaProperty.RegisterDirect<AlderFileTreeItem, Thickness>(nameof(GuideIndent), x => x.GuideIndent);

    /// <summary>An optional geometry or icon control displayed with the content.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>The geometry used for the expanded icon.</summary>
    public Geometry? ExpandedIcon { get => GetValue(ExpandedIconProperty); set => SetValue(ExpandedIconProperty, value); }
    /// <summary>The supporting content displayed beside the row's title.</summary>
    public object? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    /// <summary>Whether the row represents a folder.</summary>
    public bool IsFolder { get => GetValue(IsFolderProperty); set => SetValue(IsFolderProperty, value); }
    /// <summary>Whether the row is treated as a section heading.</summary>
    public bool IsSection { get => GetValue(IsSectionProperty); set => SetValue(IsSectionProperty, value); }
    /// <summary>The icon selected for the row's current expansion state.</summary>
    public object? DisplayIcon { get => field; private set => SetAndRaise(DisplayIconProperty, ref field, value); }
    /// <summary>The indentation applied to the row content.</summary>
    public Thickness RowIndent { get => field; private set => SetAndRaise(RowIndentProperty, ref field, value); }
    /// <summary>The indentation used to align the row's tree guides.</summary>
    public Thickness GuideIndent { get => field; private set => SetAndRaise(GuideIndentProperty, ref field, value); }

    internal object? Item { get; private set; }
    internal Border? RowSurface { get; private set; }
    internal Border? GuideSurface { get; private set; }
    internal bool IsBranch => ItemCount > 0 || IsFolder;
    private Control? dragHandle;
    private AlderCheckBox? check;
    private IDisposable? expansionBinding;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateIndent();
    }

    private void UpdateIndent()
    {
        var depth = this.GetVisualAncestors().TakeWhile(ancestor => ancestor is not AlderFileTree)
            .OfType<AlderFileTreeItem>().Count(parent => !parent.IsSection);
        double step = Size switch { AlderSize.Small => 10, AlderSize.Large => 16, _ => 12 };
        double position = depth * step;
        RowIndent = new Thickness(position, 0, 0, 0);
        GuideIndent = new Thickness(position + 18, 0, 0, 0);
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not AlderFileTreeItem;
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderFileTreeItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ((AlderFileTreeItem)container).SetItem(item);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        var row = (AlderFileTreeItem)container;
        if (this.FindAncestorOfType<AlderFileTree>() is { } tree)
            tree.ReleaseRow(row);
        else
            row.SetItem(null);
        base.ClearContainerForItemOverride(container);
    }

    internal void SetItem(object? item)
    {
        if (ReferenceEquals(Item, item)) return;
        if (expansionBinding is not null)
        {
            expansionBinding.Dispose();
            expansionBinding = null;
            ClearValue(IconProperty);
            ClearValue(ExpandedIconProperty);
            ClearValue(BadgeProperty);
            ClearValue(IsFolderProperty);
        }
        Item = item;
        if (item is not AlderFileTreeNode node) return;
        SetCurrentValue(IconProperty, node.Icon);
        SetCurrentValue(ExpandedIconProperty, node.ExpandedIcon);
        SetCurrentValue(BadgeProperty, node.Badge);
        SetCurrentValue(IsFolderProperty, node.IsFolder);
        expansionBinding = Bind(IsExpandedProperty, CompiledBinding.Create((AlderFileTreeNode n) => n.IsExpanded,
            source: node, mode: BindingMode.TwoWay));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (check is not null) check.Click -= OnCheck;
        base.OnApplyTemplate(e);
        RowSurface = e.NameScope.Find<Border>("PART_Row");
        GuideSurface = e.NameScope.Find<Border>("Guide");
        dragHandle = e.NameScope.Find<Control>("PART_DragHandle");
        check = e.NameScope.Find<AlderCheckBox>("PART_Selection");
        if (check is not null) check.Click += OnCheck;
    }

    /// <inheritdoc />
    protected override void OnRequestBringIntoView(RequestBringIntoViewEventArgs e)
    {
        base.OnRequestBringIntoView(e);
        if (e.TargetObject == this && RowSurface is { } row && row.TranslatePoint(default, this) is { } origin)
            e.TargetRect = new Rect(origin, row.Bounds.Size);
    }

    private void OnCheck(object? sender, RoutedEventArgs e) => this.FindAncestorOfType<AlderFileTree>()?.ToggleSelection(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && IsSection && !IsExpanded || change.Property == IsSectionProperty && IsSection)
            SetCurrentValue(IsExpandedProperty, true);
        if (change.Property == ItemCountProperty || change.Property == IsFolderProperty)
            PseudoClasses.Set(":branch", IsBranch);
        if (change.Property == IconProperty || change.Property == ExpandedIconProperty || change.Property == IsExpandedProperty)
            DisplayIcon = IsExpanded ? ExpandedIcon ?? Icon : Icon;
        if (change.Property == IsExpandedProperty)
            this.FindAncestorOfType<AlderFileTree>()?.RememberExpansion(this);
        if (change.Property == SizeProperty && this.IsAttachedToVisualTree()) UpdateIndent();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && IsFocused && e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Up or Key.Down &&
            this.FindAncestorOfType<AlderFileTree>()?.MoveByKeyboard(this, e.Key == Key.Up ? -1 : 1) == true)
            e.Handled = true;
        if (!e.Handled) base.OnKeyDown(e);
    }

    internal bool IsDragHandle(Visual source) => dragHandle is { } handle &&
        (source == handle || handle.IsVisualAncestorOf(source));

    internal void SetDrop(FileTreeDropPosition? position)
    {
        PseudoClasses.Set(":drop-before", position == FileTreeDropPosition.Before);
        PseudoClasses.Set(":drop-after", position == FileTreeDropPosition.After);
        PseudoClasses.Set(":drop-into", position == FileTreeDropPosition.Into);
    }
}
