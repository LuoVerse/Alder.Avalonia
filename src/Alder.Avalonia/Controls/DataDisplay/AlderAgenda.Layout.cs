using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderAgenda
{
    private const double TimeGutter = 62;
    private const double RibbonHeight = 25;
    private readonly List<AlderAgendaEventButton> eventButtons = [];
    private readonly List<(Border Control, DateOnly Date)> monthCells = [];
    private readonly List<TextBlock> timeLabels = [];
    private readonly List<AgendaDayStrip> dayStrips = [];
    private readonly Dictionary<DateOnly, (Border Cell, Button Date)> dayHeaders = [];
    private DateOnly[] visibleDays = [];
    private DateOnly[] scheduleDays = [];
    private int daysBefore;
    private Grid? weekColumns;
    private AgendaDayStrip? allDayStrip;
    private Grid? allDayColumns;
    private int[] allDayLanes = [];
    private ILookup<DateOnly, AlderAgendaEvent>? timedEvents;
    private AgendaTimeline? timeline;
    private Grid? timeGrid;
    private ScrollViewer? timeScroll;
    private Border? nowLine;
    private TextBlock? nowLabel;
    private Border? nowBadge;
    private DateTime clockTime;
    private string? restoreFocusId;
    private bool? previousAllDayExpanded;
    private bool bufferRefillQueued;

    private void RenderAgenda()
    {
        if (body is null) return;
        CancelGesture(resetViewport: false);
        double scrollOffset = timeScroll?.Offset.Y ?? 0;
        var focused = eventButtons.FirstOrDefault(button => button.IsFocused);
        string? focusId = restoreFocusId ?? focused?.Item.Id;
        restoreFocusId = null;
        var previousEvents = events;
        events = Events?.Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Id) && item.End >= item.Start)
            .DistinctBy(item => item.Id).ToArray() ?? [];
        if (pendingKeyboardCreate is { } creation)
        {
            var created = events.FirstOrDefault(item => item.Start == creation.Start && item.End == creation.End &&
                !previousEvents.Any(previous => previous.Id == item.Id));
            if (created is not null)
            {
                focusId = created.Id;
                pendingKeyboardCreate = null;
            }
        }
        eventButtons.Clear();
        monthCells.Clear();
        timeLabels.Clear();
        dayStrips.Clear();
        dayHeaders.Clear();
        weekColumns = allDayColumns = null;
        allDayStrip = null;
        allDayLanes = [];
        timedEvents = null;
        timeline = null;
        timeGrid = null;
        timeScroll = null;
        nowLine = nowBadge = null;
        nowLabel = null;
        renderedToday = Today;
        UpdateHeading();

        body.Content = View == AlderCalendarView.Month ? BuildMonth() : BuildSchedule();
        UpdateSelection();
        UpdateClock();

        if (timeScroll is { } scroller)
        {
            double initialOffset = restoreViewport ? scrollOffset : InitialOffset();
            restoreViewport = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (timeScroll == scroller)
                    scroller.Offset = new Vector(0, Math.Clamp(initialOffset, 0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
            }, DispatcherPriority.Loaded);
        }

        if (focusId is not null)
            Dispatcher.UIThread.Post(() =>
            {
                var button = eventButtons.FirstOrDefault(button => button.IsTabStop && button.Item.Id == focusId);
                if (button is { IsEffectivelyVisible: true })
                {
                    button.Focus(NavigationMethod.Tab);
                    button.BringIntoView();
                }
                else timeScroll?.Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Loaded);
    }

    private void UpdateHeading()
    {
        if (heading is not null) heading.Text = Date.ToString("Y", Language);
    }

    private double InitialOffset()
    {
        int minute = renderedToday >= visibleDays[0] && renderedToday <= visibleDays[^1] &&
                     Now.TimeOfDay.TotalMinutes is var time && time >= StartMinute && time < EndMinute
            ? Math.Max(StartMinute, (int)time - 90)
            : Math.Max(StartMinute, Math.Min(8 * 60, EndMinute - 120));
        return Math.Floor((double)(minute - StartMinute) / SlotDuration) * SlotHeight;
    }

    private Control BuildSchedule()
    {
        DateOnly first = ScheduleStart(swipedStart ?? FirstScheduledDay(Date));
        scheduleDays = BufferedDays(first, DayCount);
        daysBefore = first.DayNumber - scheduleDays[0].DayNumber;
        visibleDays = scheduleDays.AsSpan(daysBefore, DayCount).ToArray();
        timedEvents = events.Where(item => !item.IsAllDay).ToLookup(item => DateOnly.FromDateTime(item.Start));

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        layout.Children.Add(BuildWeekHeader());
        Add(layout, BuildAllDay(), 1, 0);

        var track = new AgendaTimeline(scheduleDays.Length)
        {
            Height = (EndMinute - StartMinute) * PixelsPerMinute,
            Background = Brushes.Transparent,
            Lines = Brush("AlderSeparator"),
            WeekendBrush = Brush("AlderAgendaWeekend"),
            Weekends = scheduleDays.Select(IsWeekend).ToArray(),
            SlotHeight = SlotHeight
        };
        timeline = track;
        var grid = Columns(1, TimeGutter);
        grid.Name = "TimeGrid";
        grid.Height = track.Height;
        grid.VerticalAlignment = VerticalAlignment.Top;
        grid.Background = Brushes.Transparent;
        timeGrid = grid;
        var ruler = new Canvas { Name = "TimeRuler", IsHitTestVisible = false };
        grid.Children.Add(ruler);
        Add(grid, DayStrip("TimedDayStrip", track), 0, 1);
        for (int minute = StartMinute; minute <= EndMinute; minute += SlotDuration)
        {
            var time = FormatTime(DateTime.MinValue.AddMinutes(minute));
            double y = (minute - StartMinute) * PixelsPerMinute;
            var label = Text(time, 11, "AlderMuted");
            label.Width = TimeGutter - 9;
            label.TextAlignment = TextAlignment.Right;
            timeLabels.Add(label);
            Canvas.SetTop(label, Math.Clamp(y - 8, 0, Math.Max(0, track.Height - 16)));
            ruler.Children.Add(label);
        }

        for (int col = 0; col < scheduleDays.Length; col++)
            AddTimedEvents(track, scheduleDays[col], col);

        nowLine = new Border { Background = Brush("AlderAgendaNowTrack"), IsHitTestVisible = false, ZIndex = 2 };
        nowLabel = Text("", 10, "AlderAgendaOnNow");
        nowLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nowBadge = new Border
        {
            Background = Brush("AlderAgendaNow"), Padding = new Thickness(4, 1),
            CornerRadius = new CornerRadius(4),
            Child = nowLabel, IsHitTestVisible = false, ZIndex = 2
        };
        Canvas.SetRight(nowBadge, 6);
        ruler.Children.Add(nowBadge);

        timeScroll = new AlderScrollViewer
        {
            Name = "TimeScroll",
            Content = grid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Top,
            Focusable = true
        };
        AutomationProperties.SetName(timeScroll, Caption("AgendaTimeGrid", "Schedule"));
        Add(layout, timeScroll, 2, 0);
        return layout;
    }

    private DateOnly[] BufferedDays(DateOnly first, int count)
    {
        int buffer = AllowsSwipe ? Math.Max(3, count) : 0;
        int start = Math.Max(0, first.DayNumber - buffer);
        int end = Math.Min(DateOnly.MaxValue.DayNumber + 1, first.DayNumber + count + buffer);
        return Enumerable.Range(start, end - start).Select(DateOnly.FromDayNumber).ToArray();
    }

    private DateOnly ShiftBufferCenter(int days)
    {
        int center = scheduleDays[0].DayNumber + days + Math.Max(3, visibleDays.Length);
        return DateOnly.FromDayNumber(Math.Clamp(center, 0, DateOnly.MaxValue.DayNumber - visibleDays.Length + 1));
    }

    private void RefillSchedule(DateOnly around)
    {
        if (timeline is not { } track) return;
        var focused = eventButtons.FirstOrDefault(button => button.IsFocused);
        var previous = scheduleDays;
        scheduleDays = BufferedDays(around, visibleDays.Length);
        daysBefore = visibleDays[0].DayNumber - scheduleDays[0].DayNumber;

        foreach (var strip in dayStrips) strip.SetColumnCount(scheduleDays.Length);
        PopulateWeekHeader();
        PopulateAllDay();

        var first = scheduleDays[0];
        var last = scheduleDays[^1];
        eventButtons.RemoveAll(button => button.Presentation == EventPresentation.Timed && (button.Day < first || button.Day > last));
        track.RebaseColumns(scheduleDays.Select(IsWeekend).ToArray(), previous[0].DayNumber - first.DayNumber);
        for (int column = 0; column < scheduleDays.Length; column++)
        {
            var day = scheduleDays[column];
            if (day < previous[0] || day > previous[^1]) AddTimedEvents(track, day, column, insertBeforeLaterDates: day < previous[0]);
        }
        UpdateSelection();
        UpdateClock();
        if (focused is not null && !eventButtons.Contains(focused))
        {
            var replacement = eventButtons.FirstOrDefault(button => button.IsTabStop && button.Item.Id == focused.Item.Id);
            if (replacement is not null) replacement.Focus(NavigationMethod.Tab);
            else timeScroll?.Focus(NavigationMethod.Tab);
        }
    }

    private void CommitScheduleViewport(DateOnly first, bool resetVertical)
    {
        int next = first.DayNumber - scheduleDays[0].DayNumber;
        if (next < 0 || next > scheduleDays.Length - visibleDays.Length)
        {
            RefillSchedule(first);
            next = first.DayNumber - scheduleDays[0].DayNumber;
        }

        daysBefore = next;
        visibleDays = scheduleDays.AsSpan(daysBefore, visibleDays.Length).ToArray();
        panDays = 0;
        ApplyPanOffset();
        UpdateAllDayRows();
        foreach (var (day, header) in dayHeaders)
            SetDayTabStop(header.Date, IsVisibleDate(day));
        foreach (var button in eventButtons)
        {
            bool visible = button.Presentation == EventPresentation.Ribbon
                ? DateOnly.FromDateTime(button.Item.Start) <= visibleDays[^1] && LastDay(button.Item) >= first
                : IsVisibleDate(button.Day);
            SetDayTabStop(button, visible);
        }

        if (resetVertical && timeScroll is { } scroller)
            scroller.Offset = new Vector(0, Math.Clamp(InitialOffset(), 0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
        WarmScheduleBuffer();
    }

    private void WarmScheduleBuffer()
    {
        if (bufferRefillQueued || !AllowsSwipe || timeline is not { } track) return;
        bool nearStart = -PanMinimum < 2 && scheduleDays[0] > DateOnly.MinValue;
        bool nearEnd = PanMaximum < 2 && scheduleDays[^1] < DateOnly.MaxValue;
        if (!nearStart && !nearEnd) return;

        var first = visibleDays[0];
        bufferRefillQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            bufferRefillQueued = false;
            if (!this.IsAttachedToVisualTree() || timeline != track || visibleDays[0] != first ||
                swipe is not null || gesture is not null || PanIsSettling || panDays != 0) return;
            RefillSchedule(ShiftBufferCenter(nearStart ? -2 : 2));
            ApplyPanOffset();
            UpdateAllDayRows();
        }, DispatcherPriority.Background);
    }

    private bool IsVisibleDate(DateOnly day) => day >= visibleDays[0] && day <= visibleDays[^1];

    private void SetDayTabStop(Button button, bool visible)
    {
        if (button.IsTabStop == visible) return;
        button.IsTabStop = visible;
        if (!visible && button.IsFocused) timeScroll?.Focus(NavigationMethod.Tab);
    }

    private AgendaDayStrip DayStrip(string name, Control content)
    {
        var strip = new AgendaDayStrip(visibleDays.Length, scheduleDays.Length)
        {
            Name = name, Child = content, ClipToBounds = true
        };
        strip.SetOffset(daysBefore);
        dayStrips.Add(strip);
        return strip;
    }

    private bool IsScheduledColumnVisible(int column) => IsScheduledSpanVisible(column, column + 1);

    private bool IsScheduledSpanVisible(int start, int end) =>
        end > daysBefore && start < daysBefore + visibleDays.Length;

    private Grid BuildWeekHeader()
    {
        var header = Columns(1, TimeGutter);
        header.Background = Brush("AlderAgendaHeader");
        weekColumns = Columns(scheduleDays.Length);
        Add(header, DayStrip("WeekDayStrip", weekColumns), 0, 1);
        PopulateWeekHeader();
        return header;
    }

    private void PopulateWeekHeader()
    {
        if (weekColumns is not { } days) return;
        ResizeColumns(days, scheduleDays.Length);
        foreach (var day in dayHeaders.Keys.ToArray())
        {
            if (day >= scheduleDays[0] && day <= scheduleDays[^1]) continue;
            days.Children.Remove(dayHeaders[day].Cell);
            dayHeaders.Remove(day);
        }
        for (int i = 0; i < scheduleDays.Length; i++)
        {
            var day = scheduleDays[i];
            if (dayHeaders.TryGetValue(day, out var existing))
            {
                Grid.SetColumn(existing.Cell, i);
                continue;
            }
            var date = DateButton(day, day.Day.ToString(Language));
            date.IsTabStop = IsScheduledColumnVisible(i);
            var name = Text(Language.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek), 11, "AlderMuted");
            if (day == renderedToday)
            {
                name.FontWeight = FontWeight.SemiBold;
                name.Foreground = Brush("AlderInk");
            }
            var cell = new Border
            {
                Background = IsWeekend(day) ? Brush("AlderAgendaWeekend") : Brushes.Transparent,
                Padding = new Thickness(3, 6),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 3,
                    Children = { name, date }
                }
            };
            dayHeaders.Add(day, (cell, date));
            Grid.SetColumn(cell, i);
            days.Children.Insert(i, cell);
        }
    }

    private Control BuildAllDay()
    {
        var band = Columns(1, TimeGutter);
        allDayColumns = Columns(scheduleDays.Length);
        band.Background = Brush("AlderAgendaHeader");
        allDayStrip = DayStrip("AllDayStrip", allDayColumns);
        Add(band, allDayStrip, 0, 1);

        var toggle = new AlderButton
        {
            Name = "AllDayToggle",
            Theme = FindTheme("AlderAgendaSmallButtonTheme"),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 3,
                Children =
                {
                    AllDayChevron(),
                    Text(Caption("AgendaAllDay", "All day"), 11)
                }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        AutomationProperties.SetName(toggle, IsAllDayExpanded
            ? Caption("AgendaCollapseAllDay", "Collapse all-day events")
            : Caption("AgendaExpandAllDay", "Expand all-day events"));
        toggle.Click += (_, _) =>
        {
            bool hadFocus = toggle.IsFocused;
            SetCurrentValue(IsAllDayExpandedProperty, !IsAllDayExpanded);
            if (hadFocus)
                Dispatcher.UIThread.Post(() => body?.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(button => button.Name == "AllDayToggle")?.Focus(), DispatcherPriority.Loaded);
        };
        Add(band, toggle, 0, 0);
        PopulateAllDay();
        UpdateAllDayRows();
        return new Border { BorderBrush = Brush("AlderSeparator"), BorderThickness = new Thickness(0, 1, 0, 1), Child = band };
    }

    private void PopulateAllDay()
    {
        if (allDayColumns is not { } days) return;
        var spans = PackSpans(events.Where(item => item.IsAllDay), scheduleDays);
        eventButtons.RemoveAll(button => button.Presentation == EventPresentation.Ribbon);
        days.Children.Clear();
        days.RowDefinitions.Clear();
        ResizeColumns(days, scheduleDays.Length);
        int rowCount = IsAllDayExpanded && spans.Count > 0 ? spans.Max(span => span.Row) + 1 : 1;
        for (int row = 0; row < rowCount; row++)
            days.RowDefinitions.Add(new RowDefinition(RibbonHeight, GridUnitType.Pixel));
        for (int i = 0; i < scheduleDays.Length; i++)
        {
            var divider = new Border
            {
                Background = IsWeekend(scheduleDays[i]) ? Brush("AlderAgendaWeekend") : Brushes.Transparent,
                BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = Brush("AlderSeparator"),
                IsHitTestVisible = false
            };
            Grid.SetRowSpan(divider, rowCount);
            Add(days, divider, 0, i);
            if (IsAllDayExpanded) continue;
            int count = spans.Count(span => i >= span.Start && i < span.End);
            if (count == 0) continue;
            var summary = Text(string.Format(Language, Caption("AgendaEventCount", "{0} events"), count), 11, "AlderMuted");
            summary.Margin = new Thickness(6, 0);
            Add(days, summary, 0, i);
        }
        if (IsAllDayExpanded)
        {
            allDayLanes = new int[scheduleDays.Length];
            foreach (var span in spans)
            {
                var button = MakeEvent(span.Event, scheduleDays[span.Start], EventPresentation.Ribbon);
                button.IsTabStop = IsScheduledSpanVisible(span.Start, span.End);
                Add(days, button, span.Row, span.Start, span.End - span.Start);
                for (int column = span.Start; column < span.End; column++)
                    allDayLanes[column] = Math.Max(allDayLanes[column], span.Row + 1);
            }
        }
    }

    private void UpdateAllDayRows(bool canShrink = true)
    {
        if (allDayStrip is not { } strip) return;
        int rowCount = 1;
        if (allDayLanes.Length > 0)
        {
            double left = daysBefore + panDays;
            int start = Math.Max(0, (int)Math.Floor(left + .001));
            int end = Math.Min(allDayLanes.Length, (int)Math.Ceiling(left + visibleDays.Length - .001));
            for (int column = start; column < end; column++)
                rowCount = Math.Max(rowCount, allDayLanes[column]);
        }
        double height = rowCount * RibbonHeight;
        if (double.IsNaN(strip.Height) || height > strip.Height || canShrink && height < strip.Height)
            strip.Height = height;
    }

    private PathIcon AllDayChevron()
    {
        var transform = new RotateTransform(IsAllDayExpanded ? 90 : 0);
        var icon = new AlderPathIcon
        {
            Width = 10, Height = 10,
            Data = this.TryFindResource("AlderIconChevronRight", out var glyph) ? glyph as Geometry : null,
            RenderTransform = transform
        };
        if (previousAllDayExpanded is { } previous && !AlderMotion.GetReduced(this))
        {
            double angle = transform.Angle;
            transform.Angle = previous ? 90 : 0;
            transform.Transitions = [new DoubleTransition
            {
                Property = RotateTransform.AngleProperty, Duration = TimeSpan.FromMilliseconds(150)
            }];
            Dispatcher.UIThread.Post(() => transform.Angle = angle, DispatcherPriority.Loaded);
        }
        previousAllDayExpanded = null;
        return icon;
    }

    private void AddTimedEvents(AgendaTimeline track, DateOnly day, int column, bool insertBeforeLaterDates = false)
    {
        var appointments = timedEvents![day]
            .Select(item => new TimedPlacement(item,
                Math.Max(StartMinute, item.Start.TimeOfDay.TotalMinutes),
                Math.Min(EndMinute, Math.Max(item.Start.TimeOfDay.TotalMinutes + Math.Max(SnapMinutes, 21 / PixelsPerMinute),
                    item.End.Date > item.Start.Date ? 1440 : item.End.TimeOfDay.TotalMinutes))))
            .Where(item => item.Start < item.End)
            .OrderBy(item => item.Start).ThenByDescending(item => item.End).ToArray();

        int groupStart = 0;
        while (groupStart < appointments.Length)
        {
            var lanes = new List<double>();
            int i = groupStart;
            double end = appointments[i].End;
            for (; i < appointments.Length && (i == groupStart || appointments[i].Start < end); i++)
            {
                var appointment = appointments[i];
                int lane = lanes.FindIndex(value => value <= appointment.Start);
                if (lane == -1)
                {
                    lane = lanes.Count;
                    lanes.Add(appointment.End);
                }
                else lanes[lane] = appointment.End;
                end = Math.Max(end, appointment.End);
                appointments[i] = appointment with { Lane = lane };
            }
            for (int index = groupStart; index < i; index++)
            {
                var appointment = appointments[index];
                double height = Math.Max(1, (appointment.End - appointment.Start) * PixelsPerMinute - 2);
                var button = MakeEvent(appointment.Event, day, EventPresentation.Timed, height);
                button.IsTabStop = IsScheduledColumnVisible(column);
                if (lanes.Count > 1 && button.Content is StackPanel content)
                    content.Children[1].IsVisible = false;
                track.Place(button, column + appointment.Lane / (double)lanes.Count,
                    1d / lanes.Count, (appointment.Start - StartMinute) * PixelsPerMinute, height, insertBeforeLaterDates);
            }
            groupStart = i;
        }
    }

    private Control BuildMonth()
    {
        var monthStart = new DateOnly(Date.Year, Date.Month, 1);
        var first = StartOfWeek(monthStart);
        var monthEnd = monthStart.AddDays(DateTime.DaysInMonth(Date.Year, Date.Month) - 1);
        int weekCount = (monthEnd.DayNumber - first.DayNumber) / 7 + 1;
        visibleDays = Enumerable.Range(0, weekCount * 7).Select(i => ShiftDate(first, i)).ToArray();
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var header = Columns(7);
        header.Background = Brush("AlderAgendaHeader");
        for (int i = 0; i < 7; i++)
        {
            var day = visibleDays[i];
            var name = Text(Language.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek), 11, "AlderMuted");
            name.Margin = new Thickness(8, 8);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            Add(header, name, 0, i);
        }
        layout.Children.Add(header);
        var weeks = new Grid();
        for (int i = 0; i < weekCount; i++)
        {
            weeks.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            Add(weeks, BuildMonthWeek(visibleDays.AsSpan(i * 7, 7).ToArray()), i, 0);
        }
        Add(layout, new AlderScrollViewer
        {
            Content = weeks, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        }, 1, 0);
        return layout;
    }

    private Control BuildMonthWeek(DateOnly[] week)
    {
        var cells = Columns(7);
        var ribbon = Columns(7);
        ribbon.Margin = new Thickness(0, 28, 0, 0);
        ribbon.VerticalAlignment = VerticalAlignment.Top;
        var spans = PackSpans(events.Where(item => item.IsAllDay && LastDay(item) > DateOnly.FromDateTime(item.Start)), week);
        int rowCount = spans.Count == 0 ? 0 : spans.Max(span => span.Row) + 1;
        for (int row = 0; row < rowCount; row++)
            ribbon.RowDefinitions.Add(new RowDefinition(RibbonHeight, GridUnitType.Pixel));

        for (int i = 0; i < week.Length; i++)
        {
            var day = week[i];
            var content = new StackPanel { Spacing = 1 };
            var date = DateButton(day, day.Day == 1 ? day.ToString("MMM d", Language) : day.Day.ToString(Language));
            date.HorizontalAlignment = HorizontalAlignment.Right;
            content.Children.Add(date);
            int used = spans.Where(span => span.Start <= i && i < span.End).Select(span => span.Row + 1).DefaultIfEmpty().Max();
            if (used > 0) content.Children.Add(new Border { Height = used * RibbonHeight });
            var items = events.Where(item => DateOnly.FromDateTime(item.Start) == day &&
                (!item.IsAllDay || LastDay(item) == day))
                .OrderByDescending(item => item.IsAllDay).ThenBy(item => item.Start).ToArray();
            foreach (var item in items.Take(MonthEventLimit))
                content.Children.Add(MakeEvent(item, day, EventPresentation.Month));
            int overflow = items.Length - MonthEventLimit;
            if (overflow > 0)
            {
                var more = new AlderButton
                {
                    Name = "MonthMore",
                    Theme = FindTheme("AlderAgendaSmallButtonTheme"),
                    Content = string.Format(Language, Caption("AgendaMore", "+{0} more"), overflow),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                more.Click += (_, _) => OpenDay(day);
                content.Children.Add(more);
            }

            var cell = new Border
            {
                Name = "MonthCell",
                MinHeight = 66,
                Background = MonthBackground(day),
                BorderBrush = Brush("AlderSeparator"),
                BorderThickness = new Thickness(1, 1, 0, 0),
                Padding = new Thickness(4, 2, 4, 3),
                Child = content
            };
            monthCells.Add((cell, day));
            Add(cells, cell, 0, i);
        }

        foreach (var span in spans)
            Add(ribbon, MakeEvent(span.Event, week[span.Start], EventPresentation.Ribbon), span.Row, span.Start, span.End - span.Start);
        return new Panel { Children = { cells, ribbon } };
    }

    private Button DateButton(DateOnly day, string label)
    {
        var button = new AlderButton { Theme = FindTheme("AlderAgendaDayButtonTheme"), Content = label };
        button.Classes.Set("today", day == renderedToday);
        button.Classes.Set("outside", View == AlderCalendarView.Month && day.Month != Date.Month);
        AutomationProperties.SetName(button, day.ToString("D", Language));
        button.Click += (_, _) => OpenDay(day);
        return button;
    }

    private AlderAgendaEventButton MakeEvent(AlderAgendaEvent item, DateOnly day, EventPresentation presentation, double height = 0)
    {
        var title = Text(item.Title, 12);
        title.FontWeight = FontWeight.SemiBold;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var button = new AlderAgendaEventButton(item, day, presentation)
        {
            ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var color = item.Color ?? (Brush("AlderAccent") as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
        button.Accent = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        var surface = (Background as ISolidColorBrush)?.Color ?? Colors.White;
        bool dark = ActualThemeVariant == ThemeVariant.Dark;
        bool muted = item.Status != AlderAgendaEventStatus.Confirmed;
        if (item.Status == AlderAgendaEventStatus.Declined)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
            title.Foreground = Brush("AlderMuted");
        }
        double tint = presentation == EventPresentation.Ribbon ? (dark ? .43 : .27) : (dark ? .29 : .15);
        button.Background = new SolidColorBrush(muted ? surface : Mix(surface, color, tint));
        button.Classes.Set("outlined", muted);
        button.Classes.Set("compact", presentation != EventPresentation.Timed);
        button.Classes.Set("dashed", item.Status == AlderAgendaEventStatus.Unconfirmed);

        string? times = item.IsAllDay ? null : FormatTime(item.Start) + " – " + FormatTime(item.End);
        if (presentation == EventPresentation.Timed && times is not null)
        {
            var time = Text(height >= 37 ? times : FormatTime(item.Start), 10, "AlderMuted");
            time.Classes.Add("agendaEventTime");
            time.TextTrimming = TextTrimming.CharacterEllipsis;
            button.Content = new StackPanel
            {
                Spacing = height >= 37 ? 2 : 6,
                Orientation = height >= 37 ? Orientation.Vertical : Orientation.Horizontal,
                Children = { title, time }
            };
            button.Classes.Set("short", height < 37);
            button.Classes.Set("resizable", !item.IsReadOnly && EditCommand is not null);
        }
        else button.Content = title;

        var accessible = times is null ? item.Title : $"{item.Title}, {times}";
        AutomationProperties.SetName(button, accessible);
        button.Click += (_, _) => Select(button.Item);
        eventButtons.Add(button);
        return button;
    }

    private void Select(AlderAgendaEvent? item) =>
        SetCurrentValue(SelectedEventIdProperty, item?.Id == SelectedEventId ? null : item?.Id);

    private void UpdateSelection()
    {
        foreach (var button in eventButtons)
            button.Classes.Set("selected", SelectedEventId is not null && button.Item.Id == SelectedEventId);
        UpdatePastEvents();
    }

    private void UpdatePastEvents()
    {
        var now = Now;
        foreach (var button in eventButtons)
        {
            var item = button.Item;
            bool past = item.IsAllDay ? LastDay(item) < DateOnly.FromDateTime(now) : item.End < now;
            button.Classes.Set("past", past && SelectedEventId != item.Id);
        }
    }

    private void UpdateClock()
    {
        UpdatePastEvents();
        if (timeline is not { } track || nowLine is null || nowBadge is null || nowLabel is null) return;
        var now = clockTime = Now;
        int index = Array.IndexOf(scheduleDays, DateOnly.FromDateTime(now));
        double y = (now.TimeOfDay.TotalMinutes - StartMinute) * PixelsPerMinute;
        bool show = index >= 0 && y >= 0 && y < track.Height;
        nowLine.IsVisible = show;
        UpdateClockRuler(refresh: true);
        if (!show)
        {
            track.ActiveColumn = -1;
            track.RefreshLines();
            return;
        }
        nowLabel.Text = FormatTime(now);
        Canvas.SetTop(nowBadge, Math.Clamp(y - 9, 0, Math.Max(0, track.Height - 18)));
        track.Place(nowLine, 0, scheduleDays.Length, y, 1);
        track.ActiveColumn = index;
        track.ActiveY = y;
        track.ActiveBrush = Brush("AlderAgendaNow");
        track.RefreshLines();
    }

    private void UpdateClockRuler(bool refresh = false)
    {
        if (nowBadge is null) return;
        var now = clockTime;
        int today = DateOnly.FromDateTime(now).DayNumber - visibleDays[0].DayNumber;
        double y = (now.TimeOfDay.TotalMinutes - StartMinute) * PixelsPerMinute;
        bool show = today + 1 > panDays && today < panDays + visibleDays.Length && y >= 0 && y < timeline?.Height;
        if (!refresh && nowBadge.IsVisible == show) return;
        nowBadge.IsVisible = show;
        for (int i = 0; i < timeLabels.Count; i++)
            timeLabels[i].IsVisible = !show || Math.Abs(y - i * SlotHeight) > 12;
    }

    private IBrush MonthBackground(DateOnly day) =>
        day.Month != Date.Month || IsWeekend(day) ? Brush("AlderAgendaWeekend") : Brushes.Transparent;

    private static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    private static DateOnly LastDay(AlderAgendaEvent item) =>
        DateOnly.FromDateTime(item.End.Date > item.Start.Date ? item.End.AddDays(-1) : item.End);

    private static List<DaySpan> PackSpans(IEnumerable<AlderAgendaEvent> events, DateOnly[] days)
    {
        var scheduled = events.Select(item =>
        {
            int start = Math.Max(0, DateOnly.FromDateTime(item.Start).DayNumber - days[0].DayNumber);
            int end = Math.Min(days.Length, LastDay(item).DayNumber - days[0].DayNumber + 1);
            return new DaySpan(item, start, end, 0);
        }).Where(item => item.Start < item.End)
          .OrderBy(item => item.Event.Start).ThenByDescending(item => item.Event.End);
        var spans = new List<DaySpan>();
        var rowEnds = new List<int>();
        foreach (var item in scheduled)
        {
            int row = rowEnds.FindIndex(end => end <= item.Start);
            if (row < 0)
            {
                row = rowEnds.Count;
                rowEnds.Add(item.End);
            }
            else rowEnds[row] = item.End;
            spans.Add(item with { Row = row });
        }
        return spans;
    }

    private ControlTheme? FindTheme(string key) => this.TryFindResource(key, out var value) ? value as ControlTheme : null;

    private IBrush Brush(string key) => TryBrush(key) ?? Brushes.Transparent;
    private IBrush? TryBrush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    private TextBlock Text(string value, double size, string? brush = null)
    {
        var text = new AlderTextBlock { Text = value, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        if (brush is not null) text.Foreground = Brush(brush);
        return text;
    }

    private static Grid Columns(int count, double gutter = 0)
    {
        var grid = new Grid();
        if (gutter > 0) grid.ColumnDefinitions.Add(new ColumnDefinition(gutter, GridUnitType.Pixel));
        for (int i = 0; i < count; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        return grid;
    }

    private static void ResizeColumns(Grid grid, int count)
    {
        while (grid.ColumnDefinitions.Count > count) grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
        while (grid.ColumnDefinitions.Count < count) grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
    }

    private static void Add(Grid grid, Control child, int row, int col, int span = 1)
    {
        Grid.SetRow(child, row);
        Grid.SetColumn(child, col);
        Grid.SetColumnSpan(child, span);
        grid.Children.Add(child);
    }

    private static Color Mix(Color surface, Color accent, double fraction) => Color.FromRgb(
        (byte)Math.Round(surface.R + (accent.R - surface.R) * fraction),
        (byte)Math.Round(surface.G + (accent.G - surface.G) * fraction),
        (byte)Math.Round(surface.B + (accent.B - surface.B) * fraction));

    private readonly record struct TimedPlacement(AlderAgendaEvent Event, double Start, double End, int Lane = 0);
    private readonly record struct DaySpan(AlderAgendaEvent Event, int Start, int End, int Row);
}

internal enum EventPresentation { Timed, Ribbon, Month }

internal sealed class AlderAgendaEventButton(AlderAgendaEvent item, DateOnly day, EventPresentation presentation) : Button
{
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<AlderAgendaEventButton, IBrush?>(nameof(Accent));

    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public AlderAgendaEvent Item { get; } = item;
    public DateOnly Day { get; } = day;
    public EventPresentation Presentation { get; } = presentation;
}

// The child is wider than the viewport whenever swipe is enabled. Only its transform changes during a pan.
internal sealed class AgendaDayStrip(int visibleCount, int renderedCount) : Decorator
{
    private readonly TranslateTransform translation = new();
    private double offset;

    public double DayWidth => Bounds.Width / visibleCount;

    public void SetOffset(double days)
    {
        offset = days;
        translation.X = -offset * DayWidth;
    }

    public void SetColumnCount(int count)
    {
        if (renderedCount == count) return;
        renderedCount = count;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        Child?.Measure(new Size(width / visibleCount * renderedCount, availableSize.Height));
        return new Size(width, Child?.DesiredSize.Height ?? 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            double dayWidth = finalSize.Width / visibleCount;
            translation.X = -offset * dayWidth;
            child.RenderTransform = translation;
            child.Arrange(new Rect(0, 0, dayWidth * renderedCount, finalSize.Height));
        }
        return finalSize;
    }
}

/// <summary>Arranges events by fractional day columns and minute offsets, and draws the grid without slot controls.</summary>
internal sealed class AgendaTimeline : Panel
{
    private int columns;
    private readonly Control lines;
    private readonly Dictionary<Control, Rect> slots = [];
    public double DayWidth => Bounds.Width / columns;
    public bool[] Weekends { get; set; } = [];
    public IBrush? Lines { get; init; }
    public IBrush? WeekendBrush { get; init; }
    public IBrush? ActiveBrush { get; set; }
    public int ActiveColumn { get; set; } = -1;
    public double ActiveY { get; set; }
    public double SlotHeight { get; init; }

    public AgendaTimeline(int columns)
    {
        this.columns = columns;
        lines = new TimelineLines(this) { IsHitTestVisible = false };
        Children.Add(lines);
    }

    public void RefreshLines() => lines.InvalidateVisual();

    public void RebaseColumns(bool[] weekends, int shift)
    {
        columns = weekends.Length;
        Weekends = weekends;
        foreach (var (child, slot) in slots.ToArray())
        {
            if (child is not AlderAgendaEventButton) continue;
            double x = slot.X + shift;
            if (x < 0 || x >= columns) Remove(child);
            else slots[child] = new Rect(x, slot.Y, slot.Width, slot.Height);
        }
        InvalidateMeasure();
        RefreshLines();
    }

    public void Place(Control control, double column, double span, double y, double height, bool insertBeforeLaterDates = false)
    {
        if (!slots.ContainsKey(control))
        {
            int index = Children.Count;
            if (insertBeforeLaterDates && control is AlderAgendaEventButton entering)
                for (int i = 1; i < Children.Count; i++)
                {
                    if (Children[i] is not AlderAgendaEventButton existing || existing.Day <= entering.Day) continue;
                    index = i;
                    break;
                }
            Children.Insert(index, control);
        }
        slots[control] = new Rect(column, y, span, height);
        InvalidateMeasure();
    }

    public void Remove(Control control)
    {
        if (!slots.Remove(control)) return;
        Children.Remove(control);
        InvalidateMeasure();
    }

    private void DrawLines(DrawingContext context)
    {
        var pen = new Pen(Lines, 1);
        for (int i = 0; i < columns; i++)
        {
            double x = i * DayWidth;
            if (Weekends[i] && WeekendBrush is { } weekend)
                context.FillRectangle(weekend, new Rect(x, 0, DayWidth, Bounds.Height));
            context.DrawLine(pen, new Point(x + .5, 0), new Point(x + .5, Bounds.Height));
        }
        for (double y = SlotHeight; y < Bounds.Height; y += SlotHeight)
            context.DrawLine(pen, new Point(0, y + .5), new Point(Bounds.Width, y + .5));
        if (ActiveColumn >= 0)
        {
            double x = ActiveColumn * DayWidth;
            context.DrawLine(new Pen(ActiveBrush, 2), new Point(x, ActiveY), new Point(x + DayWidth, ActiveY));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        lines.Measure(new Size(width, Height));
        double dayWidth = width / columns;
        foreach (var (child, slot) in slots)
            child.Measure(new Size(dayWidth * slot.Width, slot.Height));
        return new Size(width, Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        lines.Arrange(new Rect(finalSize));
        double dayWidth = finalSize.Width / columns;
        foreach (var (child, slot) in slots)
        {
            child.Arrange(new Rect(dayWidth * slot.X, slot.Y, dayWidth * slot.Width, slot.Height));
        }
        return finalSize;
    }

    private sealed class TimelineLines(AgendaTimeline timeline) : Control
    {
        public override void Render(DrawingContext context) => timeline.DrawLines(context);
    }
}
