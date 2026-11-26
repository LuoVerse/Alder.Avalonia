using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Describes an event's confirmation state for its visual presentation.</summary>
public enum AlderAgendaEventStatus
{
    /// <summary>Identifies an event marked as confirmed.</summary>
    Confirmed,
    /// <summary>Identifies an event marked as unconfirmed.</summary>
    Unconfirmed,
    /// <summary>Identifies an event marked as declined.</summary>
    Declined
}
/// <summary>Identifies the operation requested by an agenda interaction.</summary>
public enum AlderAgendaEditKind
{
    /// <summary>Requests a new event.</summary>
    Create,
    /// <summary>Requests a new start time while preserving the event duration.</summary>
    Move,
    /// <summary>Requests a new boundary for an existing event.</summary>
    Resize,
    /// <summary>Requests removal of the event.</summary>
    Delete
}
/// <summary>Controls how the agenda presents hours on its time axis.</summary>
public enum AlderAgendaHourCycle
{
    /// <summary>Formats hours with the active culture's convention.</summary>
    Locale,
    /// <summary>Uses a 12-hour clock and an AM or PM period.</summary>
    TwelveHour,
    /// <summary>Uses a 24-hour clock.</summary>
    TwentyFourHour
}

/// <summary>Start and end are wall-clock times in the agenda's time zone. All-day end dates are exclusive.</summary>
public sealed record AlderAgendaEvent(string Id, string Title, DateTime Start, DateTime End)
{
    /// <summary>Whether the event belongs in the all-day region.</summary>
    public bool IsAllDay { get; init; }
    /// <summary>Whether the agenda prevents this event from being moved, resized, or deleted by the user.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>An optional override for the event's assigned color.</summary>
    public Color? Color { get; init; }
    /// <summary>The confirmation status used to style this event.</summary>
    public AlderAgendaEventStatus Status { get; init; }
}

/// <summary>A requested edit. The consumer applies it to the bound events collection.</summary>
public sealed record AlderAgendaEdit(AlderAgendaEditKind Kind, AlderAgendaEvent? Event, DateTime Start, DateTime End);

/// <summary>A day, week, and month schedule with selectable events and optional command-driven editing.</summary>
public sealed partial class AlderAgenda : TemplatedControl
{
    /// <summary>Identifies the <see cref="Events"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable<AlderAgendaEvent>?> EventsProperty =
        AvaloniaProperty.Register<AlderAgenda, IEnumerable<AlderAgendaEvent>?>(nameof(Events));
    /// <summary>Identifies the <see cref="DisplayDate"/> Avalonia property.</summary>
    public static readonly StyledProperty<DateOnly?> DisplayDateProperty =
        AvaloniaProperty.Register<AlderAgenda, DateOnly?>(nameof(DisplayDate), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="View"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderCalendarView> ViewProperty =
        AvaloniaProperty.Register<AlderAgenda, AlderCalendarView>(nameof(View), AlderCalendarView.Week, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="SelectedEventId"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SelectedEventIdProperty =
        AvaloniaProperty.Register<AlderAgenda, string?>(nameof(SelectedEventId), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsAllDayExpanded"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAllDayExpandedProperty =
        AvaloniaProperty.Register<AlderAgenda, bool>(nameof(IsAllDayExpanded), true, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="EditCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<AlderAgenda, ICommand?>(nameof(EditCommand));
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty = AlderTemporalInput.CultureProperty.AddOwner<AlderAgenda>();
    /// <summary>Identifies the <see cref="TimeZone"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeZoneInfo?> TimeZoneProperty =
        AvaloniaProperty.Register<AlderAgenda, TimeZoneInfo?>(nameof(TimeZone));
    /// <summary>Identifies the <see cref="FirstDayOfWeek"/> Avalonia property.</summary>
    public static readonly StyledProperty<DayOfWeek?> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<AlderAgenda, DayOfWeek?>(nameof(FirstDayOfWeek));
    /// <summary>Identifies the <see cref="HourCycle"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderAgendaHourCycle> HourCycleProperty =
        AvaloniaProperty.Register<AlderAgenda, AlderAgendaHourCycle>(nameof(HourCycle));
    /// <summary>Identifies the <see cref="WeekDays"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> WeekDaysProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(WeekDays), 7, validate: value => value is >= 1 and <= 7);
    /// <summary>Identifies the <see cref="AllowsSwipe"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AllowsSwipeProperty =
        AvaloniaProperty.Register<AlderAgenda, bool>(nameof(AllowsSwipe));
    /// <summary>Identifies the <see cref="StartHour"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> StartHourProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(StartHour), validate: hour => hour is >= 0 and < 24);
    /// <summary>Identifies the <see cref="EndHour"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> EndHourProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(EndHour), 24, validate: hour => hour is > 0 and <= 24);
    /// <summary>Identifies the <see cref="SlotDuration"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> SlotDurationProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(SlotDuration), 60, validate: value => value is >= 5 and <= 360);
    /// <summary>Identifies the <see cref="SlotHeight"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SlotHeightProperty =
        AvaloniaProperty.Register<AlderAgenda, double>(nameof(SlotHeight), 60, validate: value => value > 0 && double.IsFinite(value));
    /// <summary>Identifies the <see cref="SnapMinutes"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> SnapMinutesProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(SnapMinutes), 5, validate: value => value is >= 1 and <= 60);
    /// <summary>Identifies the <see cref="DefaultEventDuration"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> DefaultEventDurationProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(DefaultEventDuration), 60, validate: value => value is >= 1 and <= 1440);
    /// <summary>Identifies the <see cref="MonthEventLimit"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> MonthEventLimitProperty =
        AvaloniaProperty.Register<AlderAgenda, int>(nameof(MonthEventLimit), 2, validate: value => value is >= 0 and <= 10);

    /// <summary>The events to place on the agenda. The caller retains ownership of the collection.</summary>
    public IEnumerable<AlderAgendaEvent>? Events { get => GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
    /// <summary>The date that anchors the visible day, week, or month.</summary>
    public DateOnly? DisplayDate { get => GetValue(DisplayDateProperty); set => SetValue(DisplayDateProperty, value); }
    /// <summary>The agenda layout. The default is week view.</summary>
    public AlderCalendarView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }
    /// <summary>The key of the selected event, or null when there is no selection.</summary>
    public string? SelectedEventId { get => GetValue(SelectedEventIdProperty); set => SetValue(SelectedEventIdProperty, value); }
    /// <summary>Whether additional all-day events in the header are exposed.</summary>
    public bool IsAllDayExpanded { get => GetValue(IsAllDayExpandedProperty); set => SetValue(IsAllDayExpandedProperty, value); }
    /// <summary>The command that receives an edit request to create, move, resize, or delete an event.</summary>
    public ICommand? EditCommand { get => GetValue(EditCommandProperty); set => SetValue(EditCommandProperty, value); }
    /// <summary>The culture used to format or interpret displayed values.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>The time zone used to display event times.</summary>
    public TimeZoneInfo? TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }
    /// <summary>The first day in a week, or null to use the active culture's convention.</summary>
    public DayOfWeek? FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    /// <summary>The clock format used for the time axis.</summary>
    public AlderAgendaHourCycle HourCycle { get => GetValue(HourCycleProperty); set => SetValue(HourCycleProperty, value); }
    /// <summary>The number of days displayed in week view, from one to seven.</summary>
    public int WeekDays { get => GetValue(WeekDaysProperty); set => SetValue(WeekDaysProperty, value); }
    /// <summary>Whether touch swipes can change the visible agenda period.</summary>
    public bool AllowsSwipe { get => GetValue(AllowsSwipeProperty); set => SetValue(AllowsSwipeProperty, value); }
    /// <summary>The first hour shown on the time grid, from zero to 23.</summary>
    public int StartHour { get => GetValue(StartHourProperty); set => SetValue(StartHourProperty, value); }
    /// <summary>The exclusive last hour of the time grid, from one to 24.</summary>
    public int EndHour { get => GetValue(EndHourProperty); set => SetValue(EndHourProperty, value); }
    /// <summary>The number of minutes between time-grid slots.</summary>
    public int SlotDuration { get => GetValue(SlotDurationProperty); set => SetValue(SlotDurationProperty, value); }
    /// <summary>The height of one time-grid slot in device-independent pixels.</summary>
    public double SlotHeight { get => GetValue(SlotHeightProperty); set => SetValue(SlotHeightProperty, value); }
    /// <summary>The time increment used when snapping an interactive edit.</summary>
    public int SnapMinutes { get => GetValue(SnapMinutesProperty); set => SetValue(SnapMinutesProperty, value); }
    /// <summary>The initial duration, in minutes, of a newly requested event.</summary>
    public int DefaultEventDuration { get => GetValue(DefaultEventDurationProperty); set => SetValue(DefaultEventDurationProperty, value); }
    /// <summary>The maximum number of events shown directly in each month-view cell.</summary>
    public int MonthEventLimit { get => GetValue(MonthEventLimitProperty); set => SetValue(MonthEventLimitProperty, value); }

    private CultureInfo Language => Culture ?? CultureInfo.CurrentCulture;
    private DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZone ?? TimeZoneInfo.Local).DateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now);
    private DateOnly Date => DisplayDate ?? Today;
    private DayOfWeek WeekStart => FirstDayOfWeek ?? Language.DateTimeFormat.FirstDayOfWeek;
    private bool Compact => Bounds.Width is > 0 and < 540;
    private int DayCount => View == AlderCalendarView.Day ? 1 : Math.Min(WeekDays, Compact ? 3 : 7);
    private double PixelsPerMinute => SlotHeight / SlotDuration;
    private int StartMinute => StartHour * 60;
    private int EndMinute => Math.Max(StartHour + 1, EndHour) * 60;
    private DateOnly? swipedStart;
    private DateOnly renderedToday;
    private readonly DispatcherTimer clock;
    private INotifyCollectionChanged? observedEvents;
    private IReadOnlyList<AlderAgendaEvent> events = [];

    private ContentControl? body;
    private TextBlock? heading;
    private Button? previous, next, today;
    private bool restoreViewport;

    /// <summary>Initializes a new instance of <see cref="AlderAgenda"/>.</summary>
    public AlderAgenda()
    {
        clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        clock.Tick += (_, _) =>
        {
            if (renderedToday != Today) RenderAgenda();
            else UpdateClock();
        };
        ActualThemeVariantChanged += (_, _) => RenderAgenda();
        RegisterInputHandlers();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (previous is not null) previous.Click -= Previous;
        if (next is not null) next.Click -= Next;
        if (today is not null) today.Click -= GoToday;
        base.OnApplyTemplate(e);
        body = e.NameScope.Find<ContentControl>("PART_Body");
        heading = e.NameScope.Find<TextBlock>("PART_Heading");
        previous = e.NameScope.Find<Button>("PART_Previous");
        next = e.NameScope.Find<Button>("PART_Next");
        today = e.NameScope.Find<Button>("PART_Today");
        if (previous is not null) previous.Click += Previous;
        if (next is not null) next.Click += Next;
        if (today is not null) today.Click += GoToday;
        RenderAgenda();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveEvents();
        RenderAgenda();
        clock.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        clock.Stop();
        CancelGesture(resetViewport: false);
        ObserveEvents(false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (body is null) return;
        if (change.Property == SelectedEventIdProperty)
        {
            UpdateSelection();
            return;
        }
        if (change.Property == ViewProperty || change.Property == DisplayDateProperty || change.Property == EditCommandProperty)
            pendingKeyboardCreate = null;
        if (change.Property == DisplayDateProperty && committingDate is { } current && change.GetNewValue<DateOnly?>() == current)
            return;
        if (change.Property == DisplayDateProperty && AllowsSwipe && timeline is not null && visibleDays.Length == DayCount && restoreViewport)
        {
            swipedStart = null;
            CancelGesture(resetViewport: false);
            CommitScheduleViewport(FirstScheduledDay(Date), resetVertical: false);
            UpdateHeading();
            return;
        }
        if (change.Property == IsAllDayExpandedProperty)
            previousAllDayExpanded = change.GetOldValue<bool>();
        if (change.Property == EventsProperty) ObserveEvents(this.IsAttachedToVisualTree());
        if (change.Property == ViewProperty &&
            (change.GetOldValue<AlderCalendarView>() == AlderCalendarView.Month || View == AlderCalendarView.Month))
            restoreViewport = false;
        if (change.Property == ViewProperty || change.Property == FirstDayOfWeekProperty ||
            change.Property == WeekDaysProperty || change.Property == AllowsSwipeProperty ||
            change.Property == DisplayDateProperty)
            swipedStart = null;
        if (change.Property == BoundsProperty)
        {
            bool wasCompact = change.GetOldValue<Rect>().Width is > 0 and < 540;
            if (wasCompact == Compact) return;
            PseudoClasses.Set(":compact", Compact);
            Dispatcher.UIThread.Post(RenderAgenda, DispatcherPriority.Loaded);
            return;
        }
        if (change.Property == DisplayDateProperty || change.Property == ViewProperty ||
            change.Property == EventsProperty || change.Property == IsAllDayExpandedProperty ||
            change.Property == CultureProperty || change.Property == TimeZoneProperty ||
            change.Property == FirstDayOfWeekProperty || change.Property == HourCycleProperty ||
            change.Property == WeekDaysProperty || change.Property == AllowsSwipeProperty || change.Property == StartHourProperty ||
            change.Property == EndHourProperty || change.Property == SlotDurationProperty ||
            change.Property == SlotHeightProperty || change.Property == SnapMinutesProperty ||
            change.Property == MonthEventLimitProperty || change.Property == EditCommandProperty ||
            change.Property == BackgroundProperty ||
            change.Property == FlowDirectionProperty)
            RenderAgenda();
    }

    private void ObserveEvents(bool attach = true)
    {
        if (observedEvents is not null) observedEvents.CollectionChanged -= EventsChanged;
        observedEvents = attach ? Events as INotifyCollectionChanged : null;
        if (observedEvents is not null) observedEvents.CollectionChanged += EventsChanged;
    }

    private void EventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) RenderAgenda();
        else Dispatcher.UIThread.Post(() => { if (ReferenceEquals(sender, observedEvents)) RenderAgenda(); });
    }

    private void Previous(object? sender, RoutedEventArgs e) => Navigate(-1);
    private void Next(object? sender, RoutedEventArgs e) => Navigate(1);
    private void GoToday(object? sender, RoutedEventArgs e)
    {
        CompletePan();
        if (View != AlderCalendarView.Month && AllowsSwipe)
        {
            var date = Today;
            NavigateSchedule(date, FirstScheduledDay(date), resetVertical: true);
            return;
        }
        swipedStart = null;
        restoreViewport = false;
        if (DisplayDate == Today) RenderAgenda();
        else SetCurrentValue(DisplayDateProperty, Today);
    }

    private void Navigate(int direction)
    {
        CompletePan();
        if (View == AlderCalendarView.Month)
        {
            var date = Date;
            int month = Math.Clamp((date.Year - 1) * 12 + date.Month - 1 + direction, 0, 9999 * 12 - 1);
            SetCurrentValue(DisplayDateProperty, new DateOnly(month / 12 + 1, month % 12 + 1,
                Math.Min(date.Day, DateTime.DaysInMonth(month / 12 + 1, month % 12 + 1))));
        }
        else if (AllowsSwipe && dayStrips.Count != 0)
            NavigateSchedule(ShiftDate(Date, direction * DayCount), ScheduleStart(ShiftDate(visibleDays[0], direction * DayCount)));
        else
            SetCurrentValue(DisplayDateProperty, ShiftDate(Date, direction * DayCount));
    }

    private void OpenDay(DateOnly date)
    {
        restoreViewport = false;
        SetCurrentValue(DisplayDateProperty, date);
        SetCurrentValue(ViewProperty, AlderCalendarView.Day);
        FocusBody();
    }

    private string FormatTime(DateTime value) => HourCycle switch
    {
        AlderAgendaHourCycle.TwelveHour => value.ToString("h:mm tt", Language),
        AlderAgendaHourCycle.TwentyFourHour => value.ToString("HH:mm", Language),
        _ => value.ToString("t", Language)
    };

    private DateOnly StartOfWeek(DateOnly date) => ShiftDate(date, -((7 + (int)date.DayOfWeek - (int)WeekStart) % 7));
    private DateOnly FirstScheduledDay(DateOnly date) => ScheduleStart(View == AlderCalendarView.Day ? date
        : DayCount == 7 ? StartOfWeek(date) : ShiftDate(date, -DayCount / 2));

    private DateOnly ScheduleStart(DateOnly first) =>
        DateOnly.FromDayNumber(Math.Min(first.DayNumber, DateOnly.MaxValue.DayNumber - DayCount + 1));

    private static DateOnly ShiftDate(DateOnly date, int days) =>
        DateOnly.FromDayNumber(Math.Clamp(date.DayNumber + days, 0, DateOnly.MaxValue.DayNumber));

    private static DateTime ShiftTime(DateTime value, double minutes) =>
        new(Math.Clamp(value.Ticks + (long)(minutes * TimeSpan.TicksPerMinute), 0, DateTime.MaxValue.Ticks), value.Kind);

    private string Caption(string key, string fallback) => this.TryFindResource("AlderText." + key, out var value) && value is string text
        ? text : fallback;
}
