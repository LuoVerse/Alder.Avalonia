using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class AgendaInteractions
{
    private static readonly DateOnly Monday = new(2030, 6, 3);

    [AvaloniaFact]
    public async Task AgendaPacksOverlapsAndSpansAndOpensTheDayFromMonthOverflow()
    {
        var agenda = new AlderAgenda
        {
            Events =
            [
                new AlderAgendaEvent("span", "Project window", At(Monday, 0), At(Monday.AddDays(3), 0)) { IsAllDay = true },
                new AlderAgendaEvent("first", "First", At(Monday, 9), At(Monday, 11)),
                new AlderAgendaEvent("second", "Second", At(Monday, 10), At(Monday, 12)),
                new AlderAgendaEvent("third", "Third", At(Monday, 13), At(Monday, 14))
            ],
            FirstDayOfWeek = DayOfWeek.Monday,
            DisplayDate = Monday,
            AllowsSwipe = true,
            StartHour = 8,
            EndHour = 15
        };
        using var preview = new AgendaWindow(agenda);
        var first = preview.Bounds(EventButton(agenda, "First"));
        var second = preview.Bounds(EventButton(agenda, "Second"));
        Assert.True(first.Right <= second.Left);
        Assert.True(first.Top < second.Top && second.Top < first.Bottom);
        Assert.True(preview.Bounds(EventButton(agenda, "Project window")).Width > 2 * first.Width);

        preview.Click(Part<Button>(agenda, "AllDayToggle"));
        Assert.False(agenda.IsAllDayExpanded);
        Assert.DoesNotContain(agenda.GetVisualDescendants().OfType<Button>(), button => AutomationProperties.GetName(button) == "Project window");
        preview.Click(Part<Button>(agenda, "AllDayToggle"));
        Assert.True(agenda.IsAllDayExpanded);

        var view = Part<AlderSegment>(agenda, "PART_View");
        preview.Click(view.Items.OfType<AlderSegmentItem>().Single(item => Equals(item.Key, AlderCalendarView.Month)));
        Assert.Equal(AlderCalendarView.Month, agenda.View);
        Assert.NotNull(EventButton(agenda, "Project window"));
        Assert.NotNull(EventButton(agenda, "Second"));
        preview.Click(Part<Button>(agenda, "MonthMore"));
        Assert.Equal(AlderCalendarView.Day, agenda.View);
        Assert.Equal(Monday, agenda.DisplayDate);
        preview.Click(Part<Button>(agenda, "PART_Next"));
        Assert.Equal(Monday.AddDays(1), agenda.DisplayDate);
        var track = preview.Bounds(Part<Control>(agenda, "TimeGrid"));
        var wheelPoint = new Point(track.Center.X, track.Top + 28);
        preview.ScrollHorizontally(wheelPoint, -track.Width / 100);
        preview.ScrollHorizontally(wheelPoint, -track.Width / 100);
        Assert.Equal(Monday.AddDays(1), agenda.DisplayDate);
        await preview.WaitUntil(() => agenda.DisplayDate == Monday.AddDays(2));
        Assert.Equal(Monday.AddDays(2), agenda.DisplayDate);
    }

    [AvaloniaFact]
    public void AgendaKeepsExistingControlsAndFocusWhenTheCollectionChanges()
    {
        var original = new AlderAgendaEvent("original", "Visible event", At(Monday, 9), At(Monday, 11));
        var outside = new AlderAgendaEvent("outside", "Outside current week", At(Monday.AddDays(60), 9), At(Monday.AddDays(60), 11));
        var otherDay = new AlderAgendaEvent("other", "Other day", At(Monday.AddDays(1), 9), At(Monday.AddDays(1), 11));
        var events = new ObservableCollection<AlderAgendaEvent> { original, otherDay };
        var agenda = new AlderAgenda
        {
            Events = events, FirstDayOfWeek = DayOfWeek.Monday, DisplayDate = Monday,
            View = AlderCalendarView.Week, StartHour = 8, EndHour = 15
        };
        using var preview = new AgendaWindow(agenda);
        var button = EventButton(agenda, original.Title);
        var unaffected = EventButton(agenda, otherDay.Title);
        var scroll = Part<ScrollViewer>(agenda, "TimeScroll");
        Assert.True(button.Focus());
        double fullWidth = button.Bounds.Width;

        events.Add(outside);
        preview.Pump();
        Assert.Same(button, EventButton(agenda, original.Title));
        Assert.True(button.IsFocused);
        Assert.Same(scroll, Part<ScrollViewer>(agenda, "TimeScroll"));

        var overlap = new AlderAgendaEvent("overlap", "Overlap", At(Monday, 10), At(Monday, 12));
        events.Add(overlap);
        preview.Pump();
        Assert.Same(button, EventButton(agenda, original.Title));
        Assert.Same(unaffected, EventButton(agenda, otherDay.Title));
        Assert.True(button.IsFocused);
        Assert.True(button.Bounds.Width < fullWidth);
        events.Remove(overlap);
        preview.Pump();
        Assert.Equal(fullWidth, button.Bounds.Width);
        Assert.True(button.IsFocused);

        agenda.DisplayDate = Monday.AddDays(60);
        preview.Pump();
        Assert.NotNull(EventButton(agenda, outside.Title));
    }

    [AvaloniaFact]
    public void AgendaDispatchesCreateMoveResizeAndDeleteAndProtectsReadOnlyEvents()
    {
        var events = new ObservableCollection<AlderAgendaEvent>
        {
            new("one", "Editable", At(Monday, 9), At(Monday, 10)),
            new("two", "Read only", At(Monday, 11), At(Monday, 12)) { IsReadOnly = true }
        };
        var changes = new List<AlderAgendaEdit>();
        var agenda = new AlderAgenda
        {
            Events = events, FirstDayOfWeek = DayOfWeek.Monday, DisplayDate = Monday,
            StartHour = 8, EndHour = 15,
            EditCommand = new AgendaCommand(edit =>
            {
                changes.Add(edit);
                if (edit.Kind == AlderAgendaEditKind.Create)
                {
                    events.Add(new AlderAgendaEvent($"created-{changes.Count}", $"Created {changes.Count}", edit.Start, edit.End));
                    return;
                }

                int index = events.IndexOf(edit.Event!);
                if (edit.Kind == AlderAgendaEditKind.Delete) events.RemoveAt(index);
                else events[index] = edit.Event! with { Start = edit.Start, End = edit.End };
            })
        };

        using var preview = new AgendaWindow(agenda);
        var editable = EventButton(agenda, "Editable");
        var card = preview.Bounds(editable);
        var handle = Part<Border>(editable, "ResizeHandle");
        preview.Move(card.Center);
        Assert.Equal(0, handle.Opacity);
        Assert.Null(ToolTip.GetTip(editable));
        preview.Move(new Point(card.Center.X, card.Bottom - 4));
        Assert.True(handle.Opacity > 0);
        preview.Move(card.Center);
        Assert.Equal(0, handle.Opacity);

        preview.Click(EventButton(agenda, "Read only"));
        Assert.Equal("two", agenda.SelectedEventId);
        preview.Press(PhysicalKey.Delete);
        Assert.Empty(changes);

        var track = Part<Grid>(agenda, "TimeGrid");
        double gutter = track.ColumnDefinitions[0].Width.Value;
        double dayWidth = (track.Bounds.Width - gutter) / 7;
        var empty = track.TranslatePoint(new Point(gutter + dayWidth * 3.5, agenda.SlotHeight * 4), preview.Window)!.Value;
        preview.Drag(empty, empty + new Vector(0, agenda.SlotHeight));
        Assert.Single(changes);
        Assert.Equal(AlderAgendaEditKind.Create, changes[^1].Kind);
        Assert.Equal(At(Monday.AddDays(3), 12), changes[^1].Start);
        Assert.Equal(At(Monday.AddDays(3), 13), changes[^1].End);

        card = preview.Bounds(EventButton(agenda, "Editable"));
        preview.Drag(card.Center, card.Center + new Vector(dayWidth, agenda.SlotHeight));
        Assert.Equal(AlderAgendaEditKind.Move, changes[^1].Kind);
        Assert.Equal(At(Monday.AddDays(1), 10), changes[^1].Start);

        card = preview.Bounds(EventButton(agenda, "Editable"));
        preview.Drag(new Point(card.Center.X, card.Bottom - 2), new Point(card.Center.X, card.Bottom - 2 + agenda.SlotHeight / 2));
        Assert.Equal(AlderAgendaEditKind.Resize, changes[^1].Kind);
        Assert.Equal(At(Monday.AddDays(1), 11, 30), changes[^1].End);
        Assert.True(EventButton(agenda, "Editable").Focus());
        preview.Press(PhysicalKey.Delete);
        Assert.Equal(AlderAgendaEditKind.Delete, changes[^1].Kind);
        Assert.DoesNotContain(events, item => item.Id == "one");
        Assert.Contains(events, item => item.Id == "two");
        var scroll = Part<Control>(agenda, "TimeGrid").FindAncestorOfType<ScrollViewer>()!;
        Assert.True(scroll.IsFocused);
        preview.Press(PhysicalKey.Enter);
        Assert.Equal(AlderAgendaEditKind.Create, changes[^1].Kind);
        Assert.Equal(Monday, DateOnly.FromDateTime(changes[^1].Start));
        Assert.True(EventButton(agenda, $"Created {changes.Count}").IsFocused);

        track = Part<Grid>(agenda, "TimeGrid");
        var slot = track.TranslatePoint(new Point(gutter + dayWidth * 5.5, agenda.SlotHeight * 2), preview.Window)!.Value;
        int before = changes.Count;
        preview.Drag(slot, slot);
        Assert.Equal(before + 1, changes.Count);
        Assert.Equal(AlderAgendaEditKind.Create, changes[^1].Kind);
        Assert.Equal(At(Monday.AddDays(5), 10), changes[^1].Start);
        Assert.Equal(TimeSpan.FromMinutes(agenda.DefaultEventDuration), changes[^1].End - changes[^1].Start);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgendaTouchPanFollowsTheFingerAndSettlesOnAWholeDay(bool rightToLeft)
    {
        var agenda = new AlderAgenda
        {
            DisplayDate = Monday, FirstDayOfWeek = DayOfWeek.Monday, AllowsSwipe = true,
            FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            StartHour = 8, EndHour = 15,
            Events =
            [
                new AlderAgendaEvent("next", "Next week", At(Monday.AddDays(7), 9), At(Monday.AddDays(7), 10)),
                new AlderAgendaEvent("one", "All day one", At(Monday.AddDays(7), 0), At(Monday.AddDays(8), 0)) { IsAllDay = true },
                new AlderAgendaEvent("two", "All day two", At(Monday.AddDays(7), 0), At(Monday.AddDays(8), 0)) { IsAllDay = true }
            ]
        };
        using var preview = new AgendaWindow(agenda, reduced: false);
        var week = Part<Decorator>(agenda, "WeekDayStrip");
        var allDay = Part<Decorator>(agenda, "AllDayStrip");
        var timed = Part<Decorator>(agenda, "TimedDayStrip");
        var ruler = Part<Canvas>(agenda, "TimeRuler");
        var bufferedEvent = EventButton(agenda, "Next week");
        Assert.False(bufferedEvent.IsTabStop);
        var track = preview.Bounds(timed);
        var origin = new Point(track.Center.X, track.Top + 130);
        var dayWidth = track.Width / 7;
        double movement = dayWidth * .32 * (rightToLeft ? 1 : -1);
        double rulerX = preview.Bounds(ruler).X;
        double initialStripX = preview.Bounds(week.Child!).X;
        double initialAllDayHeight = allDay.Bounds.Height;

        using var finger = preview.Window.TouchBegin(origin);
        await Task.Delay(40);
        Point dragged = origin + new Vector(movement, 0);
        preview.Window.TouchMove(finger, dragged);
        preview.Pump();
        Assert.Equal(Monday, agenda.DisplayDate);
        Assert.InRange(preview.Bounds(week.Child!).X, initialStripX + movement - 1, initialStripX + movement + 1);
        Assert.InRange(preview.Bounds(allDay.Child!).X, initialStripX + movement - 1, initialStripX + movement + 1);
        Assert.InRange(preview.Bounds(timed.Child!).X, initialStripX + movement - 1, initialStripX + movement + 1);
        Assert.Equal(rulerX, preview.Bounds(ruler).X);
        Assert.True(allDay.Bounds.Height > initialAllDayHeight);

        preview.Window.TouchEnd(finger, dragged);
        Assert.Equal(Monday, agenda.DisplayDate);
        await preview.WaitUntil(() => agenda.DisplayDate > Monday);
        int days = agenda.DisplayDate!.Value.DayNumber - Monday.DayNumber;
        Assert.InRange(days, 1, 4);
        Assert.True(bufferedEvent.IsTabStop);
        var activeStrip = Part<Decorator>(agenda, "WeekDayStrip");
        Assert.Same(week, activeStrip);
        double snappedX = initialStripX + dayWidth * days * (rightToLeft ? 1 : -1);
        Assert.InRange(preview.Bounds(activeStrip.Child!).X, snappedX - 1, snappedX + 1);
    }

    [AvaloniaFact]
    public async Task AgendaKeepsTheGridAndScrollPositionWhenRollingPastTheDateBuffer()
    {
        int enumerations = 0;
        IEnumerable<AlderAgendaEvent> ReadEvents()
        {
            enumerations++;
            for (int day = -8; day < 20; day++)
                for (int i = 0; i < 16; i++)
                {
                    var start = At(Monday.AddDays(day), 9).AddMinutes(i * 30);
                    yield return new AlderAgendaEvent($"{day}-{i}", $"Entry {day}-{i}", start, start.AddMinutes(35));
                }
        }

        var agenda = new AlderAgenda
        {
            DisplayDate = Monday, View = AlderCalendarView.Day, AllowsSwipe = true,
            StartHour = 8, EndHour = 21, Events = ReadEvents()
        };
        using var preview = new AgendaWindow(agenda);
        var strip = Part<Decorator>(agenda, "TimedDayStrip");
        var scroller = Part<ScrollViewer>(agenda, "TimeScroll");
        var ruler = Part<Canvas>(agenda, "TimeRuler");
        var cachedEvent = EventButton(agenda, "Entry 3-0");
        scroller.Offset = new Vector(0, 84);
        preview.Pump();
        double scrollOffset = scroller.Offset.Y;
        int initialEnumerations = enumerations;
        var track = preview.Bounds(strip);
        var wheelPoint = new Point(track.Center.X, preview.Bounds(scroller).Top + 80);
        double wheelForDay = track.Width / 50;

        preview.ScrollHorizontally(wheelPoint, -wheelForDay * 3);
        await preview.WaitUntil(() => agenda.DisplayDate == Monday.AddDays(3));
        Assert.Equal(Monday.AddDays(3), agenda.DisplayDate);
        Assert.Same(cachedEvent, EventButton(agenda, "Entry 3-0"));
        Assert.True(cachedEvent.IsTabStop);

        preview.ScrollHorizontally(wheelPoint, -wheelForDay * 2.25);
        var entering = EventButton(agenda, "Entry 5-0");
        await preview.WaitUntil(() => agenda.DisplayDate == Monday.AddDays(5));
        Assert.Equal(Monday.AddDays(5), agenda.DisplayDate);
        Assert.Same(strip, Part<Decorator>(agenda, "TimedDayStrip"));
        Assert.Same(scroller, Part<ScrollViewer>(agenda, "TimeScroll"));
        Assert.Same(ruler, Part<Canvas>(agenda, "TimeRuler"));
        Assert.Same(entering, EventButton(agenda, "Entry 5-0"));
        Assert.True(entering.IsTabStop);
        Assert.False(cachedEvent.IsTabStop);
        Assert.Equal(scrollOffset, scroller.Offset.Y);
        Assert.Equal(initialEnumerations, enumerations);

        preview.ScrollHorizontally(wheelPoint, wheelForDay * 6);
        await preview.WaitUntil(() => agenda.DisplayDate == Monday.AddDays(-1));
        Assert.Equal(Monday.AddDays(-1), agenda.DisplayDate);
        Assert.Same(scroller, Part<ScrollViewer>(agenda, "TimeScroll"));
        Assert.True(EventButton(agenda, "Entry -1-0").IsTabStop);

        agenda.DisplayDate = Monday;
        preview.Pump();
        Assert.Same(scroller, Part<ScrollViewer>(agenda, "TimeScroll"));
        Assert.True(EventButton(agenda, "Entry 0-0").IsTabStop);
        Assert.Equal(initialEnumerations, enumerations);
    }

    private static DateTime At(DateOnly day, int hour, int minute = 0) => day.ToDateTime(new TimeOnly(hour, minute));

    private static T Part<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().First(item => item.Name == name);

    private static Button EventButton(AlderAgenda agenda, string title) =>
        agenda.GetVisualDescendants().OfType<Button>().First(button =>
            AutomationProperties.GetName(button)?.StartsWith(title, StringComparison.Ordinal) == true);

    private sealed class AgendaWindow : IDisposable
    {
        public AgendaWindow(AlderAgenda agenda, bool reduced = true)
        {
            Window = new AlderTestWindow { Width = 900, Height = 700, Content = agenda };
            AlderMotion.SetReduced(Window, reduced);
            Window.Show();
            Pump();
        }

        public Window Window { get; }
        public Rect Bounds(Control control)
        {
            var start = control.TranslatePoint(default, Window)!.Value;
            var end = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), Window)!.Value;
            return new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), control.Bounds.Width, control.Bounds.Height);
        }
        public void Click(Control control) => Drag(Bounds(control).Center, Bounds(control).Center);
        public void Move(Point point)
        {
            Window.MouseMove(point);
            Pump();
        }

        public void Drag(Point start, Point end)
        {
            Window.MouseMove(start);
            Window.MouseDown(start, MouseButton.Left);
            if (start != end) Window.MouseMove(end);
            Window.MouseUp(end, MouseButton.Left);
            Pump();
        }

        public void Press(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void ScrollHorizontally(Point point, double delta)
        {
            Window.MouseWheel(point, new Vector(delta, 0), RawInputModifiers.None);
            Pump();
        }

        public async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 50 && !condition(); i++)
            {
                await Task.Delay(20);
                Pump();
            }
        }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private sealed class AgendaCommand(Action<AlderAgendaEdit> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is AlderAgendaEdit;
        public void Execute(object? parameter)
        {
            if (parameter is AlderAgendaEdit edit) execute(edit);
        }
    }
}
