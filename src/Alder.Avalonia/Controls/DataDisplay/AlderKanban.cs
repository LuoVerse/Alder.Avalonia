using System.Windows.Input;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A requested move. TargetIndex is the insertion index after removing the source card.</summary>
public sealed record KanbanMoveRequest(object Item, object SourceColumn, object TargetColumn, int SourceIndex, int TargetIndex);

/// <summary>A horizontally scrolling task board. The host commits requested card moves through a command.</summary>
public sealed class AlderKanban : ItemsControl
{
    /// <summary>Identifies the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderKanban, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="ReorderCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> ReorderCommandProperty =
        AvaloniaProperty.Register<AlderKanban, ICommand?>(nameof(ReorderCommand));

    /// <summary>Gets or sets the size.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Gets or sets the reorder command.</summary>
    public ICommand? ReorderCommand { get => GetValue(ReorderCommandProperty); set => SetValue(ReorderCommandProperty, value); }

    private sealed class MoveSession(AlderKanbanColumn column, object item, AlderKanbanCard card, IPointer? pointer, Point origin, Point grabOffset)
    {
        public AlderKanbanColumn Column { get; } = column;
        public AlderKanbanCard Card { get; } = card;
        public object Item { get; } = item;
        public IPointer? Pointer { get; } = pointer;
        public Point Origin { get; } = origin;
        public Point GrabOffset { get; } = grabOffset;
        public TopLevel? KeyRoot { get; set; }
        public Point Position { get; set; } = origin;
        public bool Moving { get; set; } = pointer is null;
        public AlderKanbanColumn? Target { get; set; }
        public int Destination { get; set; }
        public AlderKanbanCard? Indicator { get; set; }
        public FloatingDragPreview? Preview { get; set; }
        public double Height { get; } = card.Bounds.Height;
    }

    private MoveSession? move;
    private ScrollViewer? scroll;
    private DispatcherTimer? edgeTimer;
    private static readonly TimeSpan SettleDuration = TimeSpan.FromMilliseconds(160);
    private static readonly CubicEaseOut SettleEasing = new();

    /// <summary>Initializes a board.</summary>
    public AlderKanban()
    {
        KeyboardNavigation.SetIsTabStop(this, false);
        AddHandler(PointerPressedEvent, StartPointer, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, MovePointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, ReleasePointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(RequestBringIntoViewEvent, KeepPointerDragInPlace);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private void KeepPointerDragInPlace(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (move?.Pointer is not null && ReferenceEquals(e.TargetObject, this)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderKanbanColumn>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderKanbanColumn();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CancelMove();
        base.OnApplyTemplate(e);
        scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelMove();
        base.OnDetachedFromVisualTree(e);
    }

    private void StartPointer(object? sender, PointerPressedEventArgs e)
    {
        if (ReorderCommand is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var source = e.Source as Visual;
        if (e.Pointer.Type != PointerType.Mouse && source is not AlderKanbanHandle &&
            source?.FindAncestorOfType<AlderKanbanHandle>() is null) return;

        for (var visual = source; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual is AlderKanbanCard card)
            {
                BeginMove(card, e.Pointer, e.GetPosition(this), e.GetPosition(card));
                return;
            }
            if (visual is InputElement { Focusable: true } and not AlderKanbanHandle) return;
        }
    }

    private bool BeginMove(AlderKanbanCard card, IPointer? pointer, Point origin, Point grabOffset = default)
    {
        if (ReorderCommand is null || !card.IsEffectivelyEnabled || !card.CanDrag ||
            card.FindAncestorOfType<AlderKanbanColumn>() is not { } column ||
            column.IndexFromContainer(card) is not (>= 0 and var index) || column.ItemsView[index] is not { } item)
            return false;

        CancelMove();
        move = new MoveSession(column, item, card, pointer, origin, grabOffset);
        if (pointer is null)
        {
            card.Classes.Set("kanban-drag-source", true);
            SetTarget(move, column, index);
        }
        return true;
    }

    private void MovePointer(object? sender, PointerEventArgs e)
    {
        if (move is not { Pointer: { } pointer } session || e.Pointer != pointer) return;
        session.Position = e.GetPosition(this);
        if (!session.Moving)
        {
            var difference = session.Position - session.Origin;
            if (Math.Abs(difference.X) < 5 && Math.Abs(difference.Y) < 5) return;
            session.Moving = true;
            session.Preview = MakePreview(session);
            session.Card.Classes.Set("kanban-drag-source", true);
            // Listen above the board so Escape works without focusing and scrolling the board into view.
            session.KeyRoot = TopLevel.GetTopLevel(this);
            session.KeyRoot?.AddHandler(KeyDownEvent, CancelPointerOnEscape, RoutingStrategies.Tunnel);
            pointer.Capture(this);
            e.PreventGestureRecognition();
        }

        session.Preview?.Move(this, session.Position);
        FindPointerTarget(session);
        UpdateEdgeScrolling(session);
        e.Handled = true;
    }

    private void ReleasePointer(object? sender, PointerReleasedEventArgs e)
    {
        if (move is not { } session || session.Pointer != e.Pointer ||
            e.Pointer.Type == PointerType.Mouse && e.InitialPressMouseButton != MouseButton.Left) return;
        if (!session.Moving)
        {
            CancelMove();
            return;
        }
        session.Position = e.GetPosition(this);
        session.Preview?.Move(this, session.Position);
        FindPointerTarget(session);
        FinishMove(session, keyboard: false);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Source, this) && move?.Pointer == e.Pointer) CancelMove();
    }

    private void CancelPointerOnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || move?.Pointer is null) return;
        CancelMove();
        e.Handled = true;
    }

    internal bool ToggleKeyboardMove(AlderKanbanCard card)
    {
        if (move is { Pointer: null } session)
        {
            FinishMove(session, keyboard: true);
            return true;
        }
        return BeginMove(card, null, default);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (move is not { } session)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.Key == Key.Escape)
        {
            CancelMove();
            e.Handled = true;
        }
        else if (session.Pointer is null && session.Target is { } target &&
                 e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            if (e.Key is Key.Left or Key.Right)
            {
                var columns = this.GetRealizedContainers().OfType<AlderKanbanColumn>().Where(column => column.IsEffectivelyEnabled).ToArray();
                var step = (e.Key == Key.Right) == (FlowDirection == FlowDirection.LeftToRight) ? 1 : -1;
                var index = Array.IndexOf(columns, target) + step;
                if (index >= 0 && index < columns.Length) target = columns[index];
            }
            var insertion = session.Destination + (e.Key == Key.Up ? -1 : e.Key == Key.Down ? 1 : 0);
            SetTarget(session, target, insertion);
            if (target.ItemCount > 0) target.ScrollIntoView(Math.Min(session.Destination, target.ItemCount - 1));
            target.BringIntoView();
            e.Handled = true;
        }
        else base.OnKeyDown(e);
    }

    private void FindPointerTarget(MoveSession session)
    {
        if (!new Rect(Bounds.Size).Contains(session.Position))
        {
            SetTarget(session, null, 0);
            return;
        }
        foreach (var column in GetRealizedContainers().OfType<AlderKanbanColumn>())
        {
            if (!column.IsEffectivelyEnabled || BoundsInBoard(column) is not { } bounds || !bounds.Contains(session.Position)) continue;
            if (column == session.Target && column.CardPanel is { } panel &&
                panel.DropSlotBounds is { Height: > 0 } slot &&
                this.TranslatePoint(session.Position, panel) is { } local && slot.Contains(local)) return;
            int raw = column.ItemCount;
            if (column.CardPanel is { } cardPanel && this.TranslatePoint(session.Position, cardPanel) is { } cardPoint &&
                column.CardsScroll is { } cardsScroll && BoundsInBoard(cardsScroll) is { } cardsBounds && cardsBounds.Contains(session.Position))
                raw = cardPanel.GetInsertionIndex(cardPoint.Y);
            else
            {
                foreach (var row in column.GetRealizedContainers().OfType<AlderKanbanCard>())
                {
                    if (BoundsInBoard(row) is not { } rowBounds ||
                        (column.CardsScroll is { } viewport && BoundsInBoard(viewport) is { } clipped && !clipped.Intersects(rowBounds))) continue;
                    if (session.Position.Y < rowBounds.Center.Y)
                    {
                        raw = column.IndexFromContainer(row);
                        break;
                    }
                }
            }
            if (column == session.Column && column.ItemsView.IndexOf(session.Item) < raw) raw--;
            SetTarget(session, column, raw);
            return;
        }
        SetTarget(session, null, 0);
    }

    private Rect? BoundsInBoard(Control control) =>
        control.TranslatePoint(default, this) is { } position ? new Rect(position, control.Bounds.Size) : null;

    private void SetTarget(MoveSession session, AlderKanbanColumn? destination, int index)
    {
        int maximum = Math.Max(0, (destination?.ItemCount ?? 0) - (destination == session.Column ? 1 : 0));
        index = Math.Clamp(index, 0, maximum);
        if (destination == session.Target && index == session.Destination) return;

        if (destination != session.Target) ClearIndicator(session);
        else ClearCardIndicator(session);
        session.Target = destination;
        session.Destination = index;
        if (destination is null) return;

        int source = session.Column.ItemsView.IndexOf(session.Item);
        var sameColumn = destination == session.Column;
        var changed = !sameColumn || index != source;
        var raw = sameColumn && index > source ? index + 1 : index;
        if (destination.CardPanel is { } panel)
        {
            var showSlot = changed || session.Pointer is not null;
            panel.SetDropSlot(showSlot ? raw : -1, showSlot ? session.Height : 0);
            destination.Classes.Set("kanban-has-slot", showSlot);
        }
        else if (changed)
        {
            destination.Classes.Set("kanban-drop-target", true);
            var before = raw < destination.ItemCount;
            session.Indicator = destination.ContainerFromIndex(before ? raw : destination.ItemCount - 1) as AlderKanbanCard;
            session.Indicator?.Classes.Set(before ? "kanban-drop-before" : "kanban-drop-after", true);
        }
        else destination.Classes.Set("kanban-drop-target", false);
        if (session.Pointer is null)
        {
            AutomationProperties.SetHelpText(this, $"{destination.Header} · {index + 1}");
        }
    }

    private static void ClearIndicator(MoveSession session)
    {
        session.Target?.Classes.Set("kanban-drop-target", false);
        session.Target?.Classes.Set("kanban-has-slot", false);
        session.Target?.CardPanel?.SetDropSlot(-1, 0);
        ClearCardIndicator(session);
    }

    private static void ClearCardIndicator(MoveSession session)
    {
        session.Indicator?.Classes.Set("kanban-drop-before", false);
        session.Indicator?.Classes.Set("kanban-drop-after", false);
        session.Indicator = null;
    }

    private FloatingDragPreview? MakePreview(MoveSession session)
    {
        if (OverlayLayer.GetOverlayLayer(this) is not { } overlay || TopLevel.GetTopLevel(this) is not { } window ||
            BoundsInBoard(session.Card) is not { Width: > 0, Height: > 0 } bounds) return null;
        var theme = this.TryFindResource("AlderKanbanDragPreviewTheme", out var value) ? value as ControlTheme : null;
        return new FloatingDragPreview(overlay, session.Card, new Rect(bounds.Size), session.GrabOffset,
            window.RenderScaling, theme, "KanbanDragPreview", inset: 0);
    }

    private void UpdateEdgeScrolling(MoveSession session)
    {
        if (ScrollDelta(session) == default)
        {
            edgeTimer?.Stop();
            return;
        }
        if (edgeTimer is null)
        {
            edgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            edgeTimer.Tick += ScrollAtEdge;
        }
        edgeTimer.Start();
    }

    private Vector ScrollDelta(MoveSession session)
    {
        if (!new Rect(Bounds.Size).Contains(session.Position)) return default;
        var x = session.Position.X < 24 ? -15 : session.Position.X > Bounds.Width - 24 ? 15 : 0;
        var y = session.Target?.CardsScroll is { } column && BoundsInBoard(column) is { } bounds
            ? session.Position.Y < bounds.Top + 20 && session.Position.Y > bounds.Top ? -12 :
              session.Position.Y > bounds.Bottom - 20 && session.Position.Y < bounds.Bottom ? 12 : 0 : 0;
        return new Vector(x, y);
    }

    private void ScrollAtEdge(object? sender, EventArgs e)
    {
        if (move is not { } session)
        {
            edgeTimer?.Stop();
            return;
        }
        var delta = ScrollDelta(session);
        if (delta == default)
        {
            edgeTimer?.Stop();
            return;
        }
        var moved = TryScroll(scroll, new Vector(delta.X, 0));
        moved |= TryScroll(session.Target?.CardsScroll, new Vector(0, delta.Y));
        if (!moved) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (move == session) FindPointerTarget(session);
        }, DispatcherPriority.Loaded);
    }

    private static bool TryScroll(ScrollViewer? viewer, Vector delta)
    {
        if (viewer is null || delta == default) return false;
        var start = viewer.Offset;
        viewer.Offset += delta;
        return viewer.Offset != start;
    }

    private void FinishMove(MoveSession session, bool keyboard)
    {
        var destination = session.Target;
        var index = session.Destination;
        var sourceIndex = session.Column.ItemsView.IndexOf(session.Item);
        var dropOrigin = !keyboard && !AlderMotion.GetReduced(this) ? session.Preview?.OriginIn(this) : null;
        CancelMove();
        if (sourceIndex < 0 || destination is null || (session.Column == destination && sourceIndex == index) ||
            ReorderCommand is not { } command) return;
        var request = new KanbanMoveRequest(session.Item, session.Column.Key ?? session.Column,
            destination.Key ?? destination, sourceIndex, index);
        if (!command.CanExecute(request)) return;
        session.Column.CardPanel?.CompleteDrop();
        if (destination != session.Column) destination.CardPanel?.CompleteDrop();
        command.Execute(request);
        if (!keyboard && dropOrigin is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (move is not null) return;
            var newIndex = destination.ItemsView.IndexOf(session.Item);
            if (newIndex < 0) return;
            if (keyboard && destination.ContainerFromIndex(newIndex) is null) destination.ScrollIntoView(newIndex);
            if (destination.ContainerFromIndex(newIndex) is not { } row) return;
            if (keyboard)
                row.FindDescendantOfType<AlderKanbanHandle>()?.Focus(NavigationMethod.Tab);
            else if (dropOrigin is { } origin && BoundsInBoard(row) is { } bounds)
                OverlayMotion.Shift(row, origin - bounds.Position, SettleDuration, SettleEasing);
        }, DispatcherPriority.Loaded);
    }

    private void CancelMove()
    {
        var session = move;
        if (session is null) return;
        move = null;
        edgeTimer?.Stop();
        session.KeyRoot?.RemoveHandler(KeyDownEvent, CancelPointerOnEscape);
        session.Card.Classes.Set("kanban-drag-source", false);
        ClearIndicator(session);
        session.Preview?.Dispose();
        if (session.Pointer is { } pointer && pointer.Captured == this) pointer.Capture(null);
    }
}

/// <summary>A bounded, optionally scrollable column in a Kanban board.</summary>
public sealed class AlderKanbanColumn : HeaderedItemsControl
{
    /// <summary>Identifies the <see cref="Key"/> property.</summary>
    public static readonly StyledProperty<object?> KeyProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, object?>(nameof(Key));
    /// <summary>Identifies the <see cref="IndicatorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, IBrush?>(nameof(IndicatorBrush));
    /// <summary>Identifies the <see cref="HeaderAction"/> property.</summary>
    public static readonly StyledProperty<object?> HeaderActionProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, object?>(nameof(HeaderAction));
    /// <summary>Identifies the <see cref="EmptyContent"/> property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, object?>(nameof(EmptyContent));
    /// <summary>Identifies the <see cref="AddCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, ICommand?>(nameof(AddCommand));
    /// <summary>Identifies the <see cref="AddLabel"/> property.</summary>
    public static readonly StyledProperty<string?> AddLabelProperty =
        AvaloniaProperty.Register<AlderKanbanColumn, string?>(nameof(AddLabel));

    /// <summary>Gets or sets the key.</summary>
    public object? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    /// <summary>Gets or sets the indicator brush.</summary>
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    /// <summary>Gets or sets the header action.</summary>
    public object? HeaderAction { get => GetValue(HeaderActionProperty); set => SetValue(HeaderActionProperty, value); }
    /// <summary>Gets or sets the empty content.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    /// <summary>Gets or sets the add command.</summary>
    public ICommand? AddCommand { get => GetValue(AddCommandProperty); set => SetValue(AddCommandProperty, value); }
    /// <summary>Gets or sets the add label.</summary>
    public string? AddLabel { get => GetValue(AddLabelProperty); set => SetValue(AddLabelProperty, value); }

    internal ScrollViewer? CardsScroll { get; private set; }
    private ItemsPresenter? cardsPresenter;
    internal KanbanCardPanel? CardPanel => cardsPresenter?.Panel as KanbanCardPanel;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        CardsScroll = e.NameScope.Find<ScrollViewer>("PART_CardsScroll");
        cardsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderKanbanCard>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderKanbanCard();
}

/// <summary>A task card with independently templated body and header, embedded actions and an accessible drag handle.</summary>
public sealed class AlderKanbanCard : HeaderedContentControl
{
    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<AlderKanbanCard, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="Footer"/> property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderKanbanCard, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="ImageSource"/> property.</summary>
    public static readonly StyledProperty<IImage?> ImageSourceProperty =
        AvaloniaProperty.Register<AlderKanbanCard, IImage?>(nameof(ImageSource));
    /// <summary>Identifies the <see cref="CanDrag"/> property.</summary>
    public static readonly StyledProperty<bool> CanDragProperty =
        AvaloniaProperty.Register<AlderKanbanCard, bool>(nameof(CanDrag), true);

    /// <summary>Gets or sets the actions.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>Gets or sets the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>Gets or sets the image source.</summary>
    public IImage? ImageSource { get => GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    /// <summary>Gets or sets the can drag.</summary>
    public bool CanDrag { get => GetValue(CanDragProperty); set => SetValue(CanDragProperty, value); }
}

internal sealed class AlderKanbanHandle : Button
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && this.FindAncestorOfType<AlderKanbanCard>() is { } card &&
            this.FindAncestorOfType<AlderKanban>()?.ToggleKeyboardMove(card) == true)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
