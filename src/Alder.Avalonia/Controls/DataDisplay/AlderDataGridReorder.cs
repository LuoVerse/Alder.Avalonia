using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Indicates which side of a target row receives the dragged row.</summary>
public enum DataGridDropPosition
{
    /// <summary>Inserts the row before the target.</summary>
    Before,
    /// <summary>Inserts the row after the target.</summary>
    After
}

/// <summary>The application applies this move using stable keys from the current data source.</summary>
public sealed record DataGridReorderRequest(object RowKey, object TargetKey, DataGridDropPosition Position, object? ParentKey);

public sealed partial class AlderDataGrid
{
    /// <summary>Identifies the <see cref="CanReorderRows"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> CanReorderRowsProperty =
        AvaloniaProperty.Register<AlderDataGrid, bool>(nameof(CanReorderRows));
    /// <summary>Identifies the <see cref="AllowReorderWhenSorted"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowReorderWhenSortedProperty =
        AvaloniaProperty.Register<AlderDataGrid, bool>(nameof(AllowReorderWhenSorted));
    /// <summary>Identifies the <see cref="ReorderCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> ReorderCommandProperty =
        AvaloniaProperty.Register<AlderDataGrid, ICommand?>(nameof(ReorderCommand));

    /// <summary>Whether the user can reorder rows with drag handles.</summary>
    public bool CanReorderRows { get => GetValue(CanReorderRowsProperty); set => SetValue(CanReorderRowsProperty, value); }
    /// <summary>Whether reordering is available while a column sort is active.</summary>
    public bool AllowReorderWhenSorted { get => GetValue(AllowReorderWhenSortedProperty); set => SetValue(AllowReorderWhenSortedProperty, value); }
    /// <summary>The command notified of a requested row move.</summary>
    public ICommand? ReorderCommand { get => GetValue(ReorderCommandProperty); set => SetValue(ReorderCommandProperty, value); }

    private sealed class ReorderSession(object source, object? parent, IPointer? pointer, Point origin)
    {
        public object Source { get; } = source;
        public object? Parent { get; } = parent;
        public IPointer? Pointer { get; } = pointer;
        public Point Origin { get; } = origin;
        public Point Position { get; set; } = origin;
        public bool IsMoving { get; set; } = pointer is null;
        public DataGridReorderRequest? Candidate { get; set; }
        public FloatingDragPreview? Preview { get; set; }
    }

    private ReorderSession? reorder;
    private DispatcherTimer? reorderScroll;
    internal bool CanStartReorder => CanReorderRows && ReorderCommand is not null &&
        (SortDescriptor is null || AllowReorderWhenSorted);

    /// <summary>Initializes a new instance of <see cref="AlderDataGrid"/>.</summary>
    public AlderDataGrid()
    {
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, false);
        RowsChanged += (_, _) => UpdateReorderRows();
        AddHandler(PointerPressedEvent, ObserveRowPress, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, CompletePointerReorder, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private protected override AlderTableColumn? CreateLeadingColumn()
    {
        if (!CanReorderRows) return null;
        var column = new AlderTableColumn
        {
            Width = new GridLength(32),
            CanUserResize = false,
            Pin = TableColumnPin.Start,
            CellTemplate = new FuncDataTemplate<object>((_, _) => new AlderDataGridDragHandle(), supportsRecycling: true)
        };
        column.Classes.Add("reorder-handle");
        return column;
    }

    internal bool BeginPointerReorder(object item, IPointer pointer, Point position)
    {
        if (!BeginReorder(item, pointer, position)) return false;
        pointer.Capture(this);
        return true;
    }

    internal bool BeginKeyboardReorder(object item)
    {
        if (!BeginReorder(item, null, default)) return false;
        CancelRowReorderMotion();
        Focus(NavigationMethod.Tab);
        UpdateReorderRows();
        return true;
    }

    private bool BeginReorder(object item, IPointer? pointer, Point position)
    {
        if (!CanStartReorder || IsRowDisabled(item)) return false;
        CancelReorder();
        var key = GetRowKey(item);
        reorder = new(key, GetRowParentKey(key), pointer, position);
        return true;
    }

    private void ObserveRowPress(object? sender, PointerPressedEventArgs e)
    {
        if (!CanStartReorder || e.Pointer.Type != PointerType.Mouse ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || NativeView is not { } view) return;

        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual is TableViewRow row)
            {
                int index = view.IndexFromContainer(row);
                if (index >= 0 && view.ItemsView[index] is { } item)
                    BeginReorder(item, e.Pointer, e.GetPosition(this));
                return;
            }

            // Let embedded editors, actions, and the dedicated grip handle their own input.
            if (visual is Control { Focusable: true }) return;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (reorder is not { Pointer: { } pointer } session || pointer != e.Pointer) return;
        // A desktop backend can deliver the last move without button flags just before release.
        // For a captured drag, release and capture loss are the authoritative end of the gesture.
        if (pointer.Type == PointerType.Mouse && !ReferenceEquals(pointer.Captured, this) &&
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CancelReorder();
            return;
        }

        session.Position = e.GetPosition(this);
        if (!session.IsMoving)
        {
            double dx = session.Position.X - session.Origin.X;
            double dy = session.Position.Y - session.Origin.Y;
            if (dx * dx + dy * dy < 16) return;
            CancelRowReorderMotion();
            session.Preview = CreateDragPreview(session);
            session.IsMoving = true;
            pointer.Capture(this);
            e.PreventGestureRecognition();
            UpdateReorderRows();
        }

        session.Preview?.Move(this, session.Position);
        UpdatePointerTarget(session);
        UpdateReorderScroll(session.Position);
        e.Handled = true;
    }

    private void CompletePointerReorder(object? sender, PointerReleasedEventArgs e)
    {
        if (reorder is not { } session || session.Pointer != e.Pointer ||
            (e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left)) return;

        if (!session.IsMoving)
        {
            CancelReorder();
            return;
        }

        session.Position = e.GetPosition(this);
        session.Preview?.Move(this, session.Position);
        UpdatePointerTarget(session);
        FinishReorder(restoreFocus: false);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Source, this) && reorder?.Pointer == e.Pointer) CancelReorder();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (reorder is not { Pointer: null } session)
        {
            if (e.Key == Key.Escape && reorder is not null)
            {
                CancelReorder();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
                MoveKeyboardTarget(session, e.Key == Key.Up ? -1 : 1);
                break;
            case Key.Enter:
            case Key.Space:
                FinishReorder(restoreFocus: true);
                break;
            case Key.Escape:
                var source = session.Source;
                CancelReorder();
                RestoreHandleFocus(source);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (ReferenceEquals(e.Source, this) && reorder is { Pointer: null })
            CancelReorder();
    }

    private void UpdatePointerTarget(ReorderSession session)
    {
        if (NativeView is not { } view || GetBodyViewport() is not { } viewport ||
            !viewport.Contains(session.Position))
        {
            SetCandidate(session, null);
            return;
        }

        DataGridReorderRequest? request = null;
        foreach (var row in view.GetRealizedContainers().OfType<TableViewRow>())
        {
            int index = view.IndexFromContainer(row);
            var top = row.TranslatePoint(default, this)?.Y;
            if (index < 0 || index >= view.ItemCount || top is null || session.Position.Y < top ||
                session.Position.Y >= top + row.Bounds.Height || view.ItemsView[index] is not { } item ||
                !TryGetTarget(session, item, out var key)) continue;

            var side = session.Position.Y < top + row.Bounds.Height / 2 ? DataGridDropPosition.Before : DataGridDropPosition.After;
            request = new(session.Source, key, side, session.Parent);
            break;
        }
        SetCandidate(session, request);
    }

    private void MoveKeyboardTarget(ReorderSession session, int step)
    {
        if (NativeView is not { } view) return;
        int start = FindIndex(session.Candidate?.TargetKey ?? session.Source);
        int source = FindIndex(session.Source);
        if (start < 0 || source < 0) return;
        for (int i = start + step; i >= 0 && i < view.ItemCount; i += step)
        {
            if (view.ItemsView[i] is not { } item) continue;
            if (i == source)
            {
                SetCandidate(session, null);
                view.ScrollIntoView(i);
                return;
            }
            if (!TryGetTarget(session, item, out var key)) continue;

            SetCandidate(session, new(session.Source, key, i < source ? DataGridDropPosition.Before : DataGridDropPosition.After, session.Parent));
            view.ScrollIntoView(i);
            return;
        }
    }

    private bool TryGetTarget(ReorderSession session, object item, out object key)
    {
        key = GetRowKey(item);
        return !Equals(key, session.Source) && Equals(GetRowParentKey(key), session.Parent) && !IsRowDisabled(item);
    }

    private void SetCandidate(ReorderSession session, DataGridReorderRequest? request)
    {
        if (session.Candidate == request) return;
        session.Candidate = request;
        UpdateReorderRows();
    }

    private void UpdateReorderRows()
    {
        if (NativeView is null) return;
        foreach (var row in NativeView.GetRealizedContainers())
        {
            var index = NativeView.IndexFromContainer(row);
            if (index >= 0 && index < NativeView.ItemCount && NativeView.ItemsView[index] is { } item)
                UpdateReorderRow(row, item);
        }
    }

    internal void UpdateReorderRow(Control row, object item)
    {
        var key = GetRowKey(item);
        var candidate = reorder?.Candidate;
        row.Classes.Set("grid-drag-source", reorder is { IsMoving: true } session && Equals(session.Source, key));
        row.Classes.Set("grid-drop-before", candidate is { Position: DataGridDropPosition.Before } && Equals(candidate.TargetKey, key));
        row.Classes.Set("grid-drop-after", candidate is { Position: DataGridDropPosition.After } && Equals(candidate.TargetKey, key));
    }

    private void UpdateReorderScroll(Point position)
    {
        if (ScrollDirection(position) == 0)
        {
            reorderScroll?.Stop();
            return;
        }
        if (reorderScroll is null)
        {
            reorderScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            reorderScroll.Tick += (_, _) => ScrollDuringReorder();
        }
        reorderScroll.Start();
    }

    private int ScrollDirection(Point position)
    {
        if (GetBodyViewport() is not { } viewport || !viewport.Contains(position)) return 0;
        double edge = Math.Min(28, viewport.Height / 3);
        if (position.Y < viewport.Top + edge) return -1;
        return position.Y > viewport.Bottom - edge ? 1 : 0;
    }

    private Rect? GetBodyViewport()
    {
        if (GridScroll?.Presenter is not ScrollContentPresenter body ||
            body.TranslatePoint(default, this) is not { } origin) return null;
        return new Rect(origin, body.Bounds.Size);
    }

    private void ScrollDuringReorder()
    {
        if (reorder is not { Pointer: not null } session || GridScroll is not { } scroll)
        {
            reorderScroll?.Stop();
            return;
        }

        int direction = ScrollDirection(session.Position);
        if (direction == 0)
        {
            reorderScroll?.Stop();
            return;
        }
        scroll.Offset = new Vector(scroll.Offset.X, Math.Clamp(scroll.Offset.Y + direction * 22, 0, scroll.ScrollBarMaximum.Y));
        UpdatePointerTarget(session);
    }

    private int FindIndex(object key) => GetDisplayedRowIndex(key);

    private void FinishReorder(bool restoreFocus)
    {
        if (reorder is not { } session) return;
        var request = session.Candidate;
        if (request is not null && CanStartReorder && NativeView is { } view &&
            FindIndex(request.RowKey) is >= 0 and var source && source < view.ItemCount && view.ItemsView[source] is { } sourceItem &&
            !IsRowDisabled(sourceItem) && Equals(GetRowParentKey(request.RowKey), session.Parent) &&
            FindIndex(request.TargetKey) is >= 0 and var target && target < view.ItemCount && view.ItemsView[target] is { } targetItem &&
            TryGetTarget(session, targetItem, out _) && ReorderCommand is { } command && command.CanExecute(request))
        {
            if (session.Pointer is not null) TrackPointerDrop(session, request, source, target);
            CancelReorder();
            var preview = landingPreview;
            try { command.Execute(request); }
            finally { if (preview is not null) ReleaseUncommittedPreview(preview); }
        }
        else CancelReorder();
        if (restoreFocus) RestoreHandleFocus(session.Source);
    }

    private void RestoreHandleFocus(object key)
    {
        Dispatcher.UIThread.Post(() =>
        {
            int index = FindIndex(key);
            if (index < 0 || NativeView is not { } view || !this.IsAttachedToVisualTree()) return;
            view.ScrollIntoView(index);
            Dispatcher.UIThread.Post(() =>
            {
                if (FindIndex(key) is >= 0 and var current &&
                    view.ContainerFromIndex(current)?.FindDescendantOfType<AlderDataGridDragHandle>() is { } handle)
                    handle.Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Background);
    }

    private void CancelReorder()
    {
        var session = reorder;
        if (session is null) return;
        reorder = null;
        reorderScroll?.Stop();
        session.Preview?.Dispose();
        if (session.Pointer is { } pointer && ReferenceEquals(pointer.Captured, this))
            pointer.Capture(null);
        UpdateReorderRows();
    }
}

internal sealed class AlderDataGridDragHandle : Button
{
    public AlderDataGridDragHandle() =>
        this[!AutomationProperties.NameProperty] = new DynamicResourceExtension("AlderText.DataGridDragRow");

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (DataContext is { } item && this.FindAncestorOfType<AlderDataGrid>() is { } grid &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && grid.BeginPointerReorder(item, e.Pointer, e.GetPosition(grid)))
        {
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.Key is Key.Enter or Key.Space) && DataContext is { } item &&
            this.FindAncestorOfType<AlderDataGrid>()?.BeginKeyboardReorder(item) == true)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
