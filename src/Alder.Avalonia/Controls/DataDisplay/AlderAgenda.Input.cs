using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderAgenda
{
    private const double DragThreshold = 4;
    private const double ResizeHotZone = 8;
    private AgendaGesture? gesture;
    private Rectangle? preview;
    private (Border Control, DateOnly Date)? dropCell;
    private AlderAgendaEdit? pendingKeyboardCreate;

    private void RegisterInputHandlers()
    {
        AddHandler(PointerPressedEvent, OnAgendaPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnAgendaMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnAgendaReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnAgendaWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnAgendaKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnAgendaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (gesture is not null || swipe is not null || e.Source is not Visual source) return;
        var card = source.FindAncestorOfType<AlderAgendaEventButton>(includeSelf: true);
        Point origin = e.GetPosition(this);
        bool isTrack = timeGrid is { } grid && (grid == source || grid.IsVisualAncestorOf(source));

        if (e.Pointer.Type == PointerType.Touch)
        {
            BeginTouchPan(e, source);
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (PanIsSettling && IsInDayStrip(source))
        {
            SnapPan(panDays, 0, immediate: true);
            e.Handled = true;
            return;
        }
        if (card is { } selectedCard)
        {
            if (EditCommand is null || selectedCard.Item.IsReadOnly) return;
            var at = DateAt(origin, clamp: true) ?? card.Day;
            DateTime anchor = isTrack ? TimeAt(origin, card.Day) ?? selectedCard.Item.Start : at.ToDateTime(TimeOnly.MinValue);
            var operation = selectedCard.Presentation == EventPresentation.Timed &&
                e.GetPosition(selectedCard).Y >= selectedCard.Bounds.Height - ResizeHotZone
                ? AlderAgendaEditKind.Resize : AlderAgendaEditKind.Move;
            gesture = new AgendaGesture(e.Pointer, origin, anchor, operation, selectedCard);
            selectedCard.Focus(NavigationMethod.Pointer);
        }
        else if (isTrack && TimeAt(origin) is { } start)
        {
            if (EditCommand is null)
            {
                Select(null);
                return;
            }
            gesture = new AgendaGesture(e.Pointer, origin, start, AlderAgendaEditKind.Create, null);
            timeScroll?.Focus(NavigationMethod.Pointer);
        }
        else return;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnAgendaMoved(object? sender, PointerEventArgs e)
    {
        if (gesture is { } active && e.Pointer == active.Pointer)
        {
            var point = e.GetPosition(this);
            Vector distance = point - active.Origin;
            if (!active.HasMoved && distance.Length < DragThreshold) return;
            active.HasMoved = true;
            if (active.Operation == AlderAgendaEditKind.Resize) active.Card?.Classes.Set("resizing", true);
            var edit = GetEdit(active, point);
            ShowPreview(edit, point);
            e.Handled = true;
        }
        else MoveTouchPan(e);
    }

    private void OnAgendaReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetPosition(this);
        if (gesture is { } active && e.Pointer == active.Pointer)
        {
            var edit = active.HasMoved || active.Operation == AlderAgendaEditKind.Create ? GetEdit(active, point) : null;
            CancelGesture();
            if (edit is { } request)
            {
                if (request.Event is null) Select(null);
                else
                    SetCurrentValue(SelectedEventIdProperty, request.Event.Id);
                ApplyEdit(request);
            }
            else if (!active.HasMoved && active.Card is { } card) Select(card.Item);
            e.Handled = true;
        }
        else EndTouchPan(e);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (gesture?.Pointer == e.Pointer || swipe?.Pointer == e.Pointer) CancelGesture();
    }

    private void CancelGesture(bool resetViewport = true)
    {
        var pointer = gesture?.Pointer ?? swipe?.Pointer;
        gesture?.Card?.Classes.Set("resizing", false);
        gesture = null;
        swipe = null;
        if (pointer?.Captured == this) pointer.Capture(null);
        ClearPreview();
        StopPanMotion();
        if (resetViewport)
        {
            SetPanOffset(0);
            UpdateAllDayRows();
        }
        else panDays = 0;
    }

    private AlderAgendaEdit? GetEdit(AgendaGesture active, Point point)
    {
        var item = active.Card?.Item;
        if (active.Operation == AlderAgendaEditKind.Create)
        {
            var date = active.Anchor.Date;
            var target = TimeAt(point, DateOnly.FromDateTime(date), clamp: true) ?? active.Anchor;
            var start = Snap(active.HasMoved && target < active.Anchor ? target : active.Anchor, Math.Floor);
            var first = ShiftTime(date, StartMinute);
            var last = ShiftTime(date, EndMinute);
            var latestStart = ShiftTime(last, -SnapMinutes);
            start = start < first ? first : start > latestStart ? latestStart : start;
            var end = active.HasMoved ? Snap(target > active.Anchor ? target : active.Anchor, Math.Ceiling)
                : ShiftTime(start, DefaultEventDuration);
            if (active.HasMoved && end > last) end = last;
            return new AlderAgendaEdit(active.Operation, null, start, end > start ? end : ShiftTime(start, SnapMinutes));
        }

        if (item is null) return null;
        if (active.Operation == AlderAgendaEditKind.Resize)
        {
            if (TimeAt(point, active.Card!.Day, clamp: true) is not { } time) return null;
            var end = Snap(ShiftTime(item.End, (time - active.Anchor).TotalMinutes), Math.Round);
            return new AlderAgendaEdit(active.Operation, item, item.Start, end > item.Start ? end : ShiftTime(item.Start, SnapMinutes));
        }

        DateTime beginning;
        if (active.Card!.Presentation == EventPresentation.Timed)
        {
            if (TimeAt(point, clamp: true) is not { } target) return null;
            beginning = Snap(ShiftTime(target, (item.Start - active.Anchor).TotalMinutes), Math.Round);
            var day = DateAt(point, clamp: true)!.Value.ToDateTime(TimeOnly.MinValue);
            var min = ShiftTime(day, StartMinute);
            var max = ShiftTime(day, EndMinute - SnapMinutes);
            beginning = beginning < min ? min : beginning > max ? max : beginning;
        }
        else
        {
            if (DateAt(point, clamp: View != AlderCalendarView.Month) is not { } target) return null;
            beginning = ShiftTime(item.Start, (target.DayNumber - DateOnly.FromDateTime(active.Anchor).DayNumber) * 1440d);
        }
        return new AlderAgendaEdit(active.Operation, item, beginning, ShiftTime(beginning, (item.End - item.Start).TotalMinutes));
    }

    private DateOnly? DateAt(Point point, bool clamp = false)
    {
        if (View == AlderCalendarView.Month)
        {
            foreach (var cell in monthCells)
                if (this.TranslatePoint(point, cell.Control) is { } position && new Rect(cell.Control.Bounds.Size).Contains(position))
                    return cell.Date;
            return null;
        }
        if (timeGrid is not { } grid || ScheduleDayWidth <= 0 || this.TranslatePoint(point, grid) is not { } relative)
            return null;
        int column = (int)Math.Floor((relative.X - TimeGutter) / ScheduleDayWidth);
        if (clamp) column = Math.Clamp(column, 0, visibleDays.Length - 1);
        return (uint)column < (uint)visibleDays.Length ? visibleDays[column] : null;
    }

    private DateTime? TimeAt(Point point, DateOnly? fixedDay = null, bool clamp = false)
    {
        if (timeline is not { } track || this.TranslatePoint(point, track) is not { } relative ||
            (fixedDay ?? DateAt(point, clamp)) is not { } date)
            return null;
        if (!clamp && (relative.Y < 0 || relative.Y >= track.Height)) return null;
        double minute = Math.Clamp(StartMinute + relative.Y / PixelsPerMinute, StartMinute, EndMinute);
        return ShiftTime(date.ToDateTime(TimeOnly.MinValue), minute);
    }

    private DateTime Snap(DateTime time, Func<double, double> round) =>
        ShiftTime(time.Date, round(time.TimeOfDay.TotalMinutes / SnapMinutes) * SnapMinutes);

    private void ShowPreview(AlderAgendaEdit? edit, Point point)
    {
        ClearPreview();
        if (edit is null) return;
        if (View == AlderCalendarView.Month)
        {
            if (DateAt(point) is not { } date) return;
            var target = monthCells.FirstOrDefault(cell => cell.Date == date);
            if (target.Control is null) return;
            dropCell = target;
            target.Control.Background = Brush("AlderAgendaDropFill");
            return;
        }
        if (timeline is not { } track || edit.Event is { IsAllDay: true }) return;
        int column = Array.IndexOf(visibleDays, DateOnly.FromDateTime(edit.Start));
        if (column < 0) return;
        double start = Math.Max(StartMinute, edit.Start.TimeOfDay.TotalMinutes);
        double end = Math.Min(EndMinute, edit.End.Date > edit.Start.Date ? 1440 : edit.End.TimeOfDay.TotalMinutes);
        if (end <= start) return;
        preview = new Rectangle
        {
            Fill = Brush("AlderAgendaDropFill"), Stroke = Brush("AlderAccent"),
            StrokeDashArray = [4, 3], StrokeThickness = 1,
            RadiusX = 4, RadiusY = 4, Margin = new Thickness(2, 0),
            IsHitTestVisible = false, ZIndex = 5
        };
        track.Place(preview, daysBefore + column, 1, (start - StartMinute) * PixelsPerMinute, (end - start) * PixelsPerMinute);
    }

    private void ClearPreview()
    {
        if (preview is not null) timeline?.Remove(preview);
        preview = null;
        if (dropCell is { } cell) cell.Control.Background = MonthBackground(cell.Date);
        dropCell = null;
    }

    private void OnAgendaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (gesture is not null || swipe is not null || PanIsSettling) CancelGesture();
            else if (SelectedEventId is not null) Select(null);
            else return;
            e.Handled = true;
            return;
        }
        if (EditCommand is null || e.Source is not Visual source) return;
        var card = source.FindAncestorOfType<AlderAgendaEventButton>(includeSelf: true);
        if (e.Key is Key.Delete or Key.Back)
        {
            if (card is not null && card.Item.Id != SelectedEventId) return;
            if (events.FirstOrDefault(item => item.Id == SelectedEventId) is not { IsReadOnly: false } selection)
                return;
            if (ApplyEdit(new AlderAgendaEdit(AlderAgendaEditKind.Delete, selection, selection.Start, selection.End)))
            {
                Select(null);
                FocusBody();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Enter && source == timeScroll)
        {
            double viewOffset = timeScroll.Offset.Y;
            var day = visibleDays.Contains(Date) ? Date : visibleDays[0];
            var start = ShiftTime(day.ToDateTime(TimeOnly.MinValue),
                Math.Min(EndMinute - SnapMinutes, StartMinute + Math.Ceiling(viewOffset / SlotHeight) * SlotDuration));
            e.Handled = ApplyEdit(new AlderAgendaEdit(AlderAgendaEditKind.Create, null, start, ShiftTime(start, DefaultEventDuration)),
                followCreation: true);
            return;
        }

        if (card is not { Item: { IsReadOnly: false } item } ||
            e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down) ||
            e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;
        int sign = e.Key is Key.Down or Key.Right ? 1 : -1;
        bool horizontal = e.Key is Key.Left or Key.Right;
        if (horizontal && FlowDirection == FlowDirection.RightToLeft) sign = -sign;
        bool resize = !horizontal && card.Presentation == EventPresentation.Timed && e.KeyModifiers == KeyModifiers.Shift;
        var startTime = item.Start;
        var endTime = item.End;
        if (resize)
            endTime = ShiftTime(endTime, sign * SnapMinutes);
        else
        {
            int minutes = horizontal ? 1440 : View == AlderCalendarView.Month ? 10080
                : card.Presentation == EventPresentation.Ribbon ? 1440 : SnapMinutes;
            startTime = ShiftTime(startTime, sign * minutes);
            endTime = ShiftTime(endTime, sign * minutes);
        }
        if (endTime <= startTime || !visibleDays.Contains(DateOnly.FromDateTime(startTime)) ||
            card.Presentation == EventPresentation.Timed &&
            (startTime.TimeOfDay.TotalMinutes < StartMinute || startTime.TimeOfDay.TotalMinutes >= EndMinute ||
             resize && (endTime - startTime.Date).TotalMinutes > EndMinute))
        {
            e.Handled = true;
            return;
        }

        SetCurrentValue(SelectedEventIdProperty, item.Id);
        e.Handled = ApplyEdit(new AlderAgendaEdit(resize ? AlderAgendaEditKind.Resize : AlderAgendaEditKind.Move, item, startTime, endTime));
    }

    private bool ApplyEdit(AlderAgendaEdit edit, bool followCreation = false)
    {
        if (EditCommand is not { } command || !command.CanExecute(edit)) return false;
        if (edit.Kind is AlderAgendaEditKind.Move or AlderAgendaEditKind.Resize)
            restoreFocusId = edit.Event?.Id;
        else if (edit.Kind == AlderAgendaEditKind.Create)
            pendingKeyboardCreate = followCreation ? edit : null;
        command.Execute(edit);
        return true;
    }

    private void FocusBody() => Dispatcher.UIThread.Post(() =>
    {
        if (timeScroll is not null) timeScroll.Focus();
        else today?.Focus();
    }, DispatcherPriority.Loaded);

    private sealed record AgendaGesture(IPointer Pointer, Point Origin, DateTime Anchor,
        AlderAgendaEditKind Operation, AlderAgendaEventButton? Card)
    {
        public bool HasMoved { get; set; }
    }

}
