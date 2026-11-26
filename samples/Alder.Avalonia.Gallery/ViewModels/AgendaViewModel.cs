using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class AgendaViewModel : GalleryViewModel
{
    private readonly Dictionary<string, string> titleKeys = [];
    private readonly DateOnly initialDate;
    private readonly AlderAgendaEvent[] initialEvents;

    public AgendaViewModel()
    {
        var now = DateTime.Now;
        var day = DateOnly.FromDateTime(now);
        Date = initialDate = day;
        int hour = now.Hour is >= 8 and < 20 ? Math.Clamp(now.Hour - 1, 9, 16) : 9;

        var previous = day.AddDays(-1);
        var next = day.AddDays(1);
        Add("AgendaSupportHandoff", At(previous, hour - 1, 0), At(previous, hour, 30), "#A86134");
        Add("AgendaMorningBrief", At(day, hour - 1, 30), At(day, hour, 0), "#496BC7");
        Add("AgendaResearchSync", At(day, hour - 1, 45), At(day, hour, 45), "#496BC7",
            status: AlderAgendaEventStatus.Unconfirmed);
        Add("AgendaLunchBreak", At(day, hour, 30), At(day, hour + 1, 15), "#9251A9");
        Add("AgendaRoadmap", At(day, hour + 1, 30), At(day, hour + 3, 0), "#496BC7");
        Add("AgendaBuildReview", At(day, hour + 2, 0), At(day, hour + 3, 0), "#258E75");
        Add("AgendaQuickCheck", At(day, hour + 1, 30), At(day, hour + 1, 50), "#3A8294");
        Add("AgendaReleaseNotes", At(day, hour + 2, 20), At(day, hour + 2, 45), "#688931");
        Add("AgendaMentor", At(day, hour + 3, 15), At(day, hour + 3, 45), "#258E75", readOnly: true);

        Add("AgendaVendorReview", At(next, hour, 30), At(next, hour + 1, 30), "#A86134",
            status: AlderAgendaEventStatus.Declined);
        Add("AgendaFieldInterview", At(day.AddDays(2), hour - 1, 30), At(day.AddDays(2), hour + 1, 0), "#258E75");
        Add("AgendaRetrospective", At(day.AddDays(3), hour + 1, 0), At(day.AddDays(3), hour + 2, 0), "#9251A9");
        Add("AgendaProductBrief", At(day.AddDays(-2), hour + 1, 0), At(day.AddDays(-2), hour + 2, 0), "#3A8294");
        Add("AgendaTeamDemo", At(day.AddDays(4), hour, 0), At(day.AddDays(4), hour + 1, 0), "#496BC7");

        Add("AgendaFocusDays", At(day, 0, 0), At(day.AddDays(3), 0, 0), "#258E75", allDay: true);
        Add("AgendaReleaseWindow", At(day, 0, 0), At(day.AddDays(1), 0, 0), "#496BC7", allDay: true);

        initialEvents = Events.ToArray();
        EditCommand = ReactiveCommand.Create<AlderAgendaEdit>(Edit);
        Reset = ReactiveCommand.Create(RestoreInitialState);
        PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.PropertyName)) return;
            for (int i = 0; i < Events.Count; i++)
            {
                var item = Events[i];
                if (titleKeys.TryGetValue(item.Id, out var resourceKey))
                    Events[i] = item with { Title = Text(resourceKey) };
            }
        };
    }

    public ObservableCollection<AlderAgendaEvent> Events { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [];
    public ICommand EditCommand { get; }
    public ICommand Reset { get; }
    public DateOnly? Date { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public AlderCalendarView View { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = AlderCalendarView.Week;
    public string? SelectedEventId { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool IsAllDayExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    private void RestoreInitialState()
    {
        SelectedEventId = null;
        Date = initialDate;
        View = AlderCalendarView.Week;
        IsAllDayExpanded = true;
        titleKeys.Clear();
        foreach (var item in initialEvents) titleKeys.Add(item.Id, item.Id);
        Events = new(initialEvents.Select(item => item with { Title = Text(titleKeys[item.Id]) }));
    }

    private void Add(string titleKey, DateTime start, DateTime end, string color, bool allDay = false,
        bool readOnly = false, AlderAgendaEventStatus status = AlderAgendaEventStatus.Confirmed)
    {
        titleKeys.Add(titleKey, titleKey);
        Events.Add(new AlderAgendaEvent(titleKey, Text(titleKey), start, end)
        {
            Color = Color.Parse(color), IsAllDay = allDay, IsReadOnly = readOnly, Status = status
        });
    }

    private void Edit(AlderAgendaEdit edit)
    {
        if (edit.Kind == AlderAgendaEditKind.Create)
        {
            var id = Guid.NewGuid().ToString();
            titleKeys.Add(id, "AgendaNewEvent");
            Events.Add(new AlderAgendaEvent(id, Text("AgendaNewEvent"), edit.Start, edit.End));
            SelectedEventId = id;
            return;
        }

        if (edit.Event is not { IsReadOnly: false } item) return;
        int index = Events.IndexOf(item);
        if (index < 0) return;
        if (edit.Kind == AlderAgendaEditKind.Delete)
        {
            Events.RemoveAt(index);
            titleKeys.Remove(item.Id);
        }
        else Events[index] = item with { Start = edit.Start, End = edit.End };
    }

    private static DateTime At(DateOnly day, int hour, int minute) => day.ToDateTime(new TimeOnly(hour, minute));
}
