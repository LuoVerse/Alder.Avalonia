using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies the time span presented by the calendar.</summary>
public enum AlderCalendarView
{
    /// <summary>Displays a month grid.</summary>
    Month,
    /// <summary>Displays a single week.</summary>
    Week,
    /// <summary>Displays a single day.</summary>
    Day
}

/// <summary>The common month, week, and day presentation used by both calendars.</summary>
public abstract partial class AlderCalendarBase : TemplatedControl
{
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty = AlderTemporalInput.CultureProperty.AddOwner<AlderCalendarBase>();
    /// <summary>Identifies the <see cref="DisplayFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DisplayFormatProperty = AlderTemporalInput.DisplayFormatProperty.AddOwner<AlderCalendarBase>();
    /// <summary>Identifies the <see cref="IsReadOnly"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        TextBox.IsReadOnlyProperty.AddOwner<AlderCalendarBase>();
    /// <summary>Identifies the <see cref="IsInvalid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AlderInput.IsInvalidProperty.AddOwner<AlderCalendarBase>();

    /// <summary>The date that anchors the visible period. Null starts from the calendar's today value.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial DateOnly? DisplayDate { get; set; }
    /// <summary>The date that receives calendar keyboard navigation focus.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial DateOnly? FocusedDate { get; set; }
    /// <summary>The date marked as today. Set it to provide an application-specific reference date.</summary>
    [GeneratedStyledProperty]
    public partial DateOnly? Today { get; set; }
    /// <summary>The earliest user-selectable date, inclusive.</summary>
    [GeneratedStyledProperty]
    public partial DateOnly? Minimum { get; set; }
    /// <summary>The latest user-selectable date, inclusive.</summary>
    [GeneratedStyledProperty]
    public partial DateOnly? Maximum { get; set; }
    /// <summary>The culture used to format dates and determine calendar conventions.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    /// <summary>The date format used in calendar headings. Null or whitespace uses the default for the current view.</summary>
    public string? DisplayFormat { get => GetValue(DisplayFormatProperty); set => SetValue(DisplayFormatProperty, value); }
    /// <summary>The first day in the week layout. Null uses the culture's first day.</summary>
    [GeneratedStyledProperty]
    public partial DayOfWeek? FirstDayOfWeek { get; set; }
    /// <summary>The visible month, week, or day layout.</summary>
    [GeneratedStyledProperty]
    public partial AlderCalendarView View { get; set; }
    /// <summary>The number of months in month view, coerced to the range from one to three.</summary>
    [GeneratedStyledProperty(DefaultValue = 1, CoerceMethodName = nameof(CoerceVisibleMonths))]
    public partial int VisibleMonths { get; set; }
    /// <summary>Whether month grids reserve space for six weeks.</summary>
    [GeneratedStyledProperty]
    public partial bool FixedWeeks { get; set; }
    /// <summary>Whether the calendar header offers a year picker.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowYearPicker { get; set; }
    /// <summary>Whether the current selection is displayed without permitting edits.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    /// <summary>Whether the template presents the calendar as invalid.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }
    /// <summary>A predicate that returns true for a date that cannot be selected.</summary>
    [GeneratedStyledProperty]
    public partial Func<DateOnly, bool>? IsDateUnavailable { get; set; }
    /// <summary>A function that supplies an optional indicator brush for each date cell.</summary>
    [GeneratedStyledProperty]
    public partial Func<DateOnly, IBrush?>? DateIndicator { get; set; }
    /// <summary>The geometry for moving to the previous period.</summary>
    [GeneratedStyledProperty]
    public partial Geometry? PreviousIcon { get; set; }
    /// <summary>The geometry for moving to the next period.</summary>
    [GeneratedStyledProperty]
    public partial Geometry? NextIcon { get; set; }

    /// <summary>The selected date used as a fallback while finding the calendar's visible period.</summary>
    protected abstract DateOnly? SelectionDate { get; }
    /// <summary>Applies a user selection to the supplied date.</summary>
    protected abstract void SelectDate(DateOnly date);
    /// <summary>The range to paint across the calendar, including a range still being previewed.</summary>
    protected virtual AlderDateRange? HighlightedRange => null;
    /// <summary>Reports whether the supplied date belongs to the active selection.</summary>
    protected virtual bool IsSelected(DateOnly date) => SelectionDate == date;
    /// <summary>Reports whether the supplied date should be unavailable for interaction.</summary>
    protected virtual bool IsUnavailable(DateOnly date) => IsDateUnavailable?.Invoke(date) == true;
    /// <summary>Updates the selection preview for the date under the pointer, or clears the hovered date when null.</summary>
    protected virtual void HoverDate(DateOnly? date) { }
    /// <summary>Cancels an in-progress selection preview and reports whether one was active.</summary>
    protected virtual bool CancelPreview() => false;

    internal static int CoerceVisibleMonths(AvaloniaObject _, int value) => Math.Clamp(value, 1, 3);

    private readonly List<CalendarDay> days = [];
    private WrapPanel? months;
    private Button? previous;
    private Button? next;
    private Button? heading;
    private TextBlock? headingText;
    private PathIcon? headingIcon;
    private ListBox? yearPicker;
    private YearRow[] yearRows = [];
    private bool choosingYear;
    private bool rebuilding;
    private bool settingFocusDate;

    internal bool IsYearPickerOpen => choosingYear;

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;
    private System.Globalization.Calendar CalendarSystem => EffectiveCulture.Calendar;
    private DateOnly CalendarToday => Today ?? DateOnly.FromDateTime(DateTime.Today);
    private DayOfWeek WeekStart => FirstDayOfWeek ?? EffectiveCulture.DateTimeFormat.FirstDayOfWeek;
    private DateOnly CalendarMinimum => DateOnly.FromDateTime(CalendarSystem.MinSupportedDateTime);
    private DateOnly CalendarMaximum => DateOnly.FromDateTime(CalendarSystem.MaxSupportedDateTime);
    private DateOnly EffectiveMinimum => Minimum is { } min && min > CalendarMinimum ? min : CalendarMinimum;
    private DateOnly EffectiveMaximum => Maximum is { } max && max < CalendarMaximum ? max : CalendarMaximum;
    private DateOnly Anchor => Clamp(DisplayDate ?? FocusedDate ?? SelectionDate ?? CalendarToday);
    private int PageCount => View == AlderCalendarView.Month ? VisibleMonths : 1;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (previous is not null) previous.Click -= GoPrevious;
        if (next is not null) next.Click -= GoNext;
        if (heading is not null) heading.Click -= ToggleYearPicker;
        base.OnApplyTemplate(e);
        months = e.NameScope.Find<WrapPanel>("PART_Months");
        previous = e.NameScope.Find<Button>("PART_Previous");
        next = e.NameScope.Find<Button>("PART_Next");
        heading = e.NameScope.Find<Button>("PART_Heading");
        headingText = e.NameScope.Find<TextBlock>("PART_HeadingText");
        headingIcon = e.NameScope.Find<PathIcon>("PART_HeadingIcon");
        yearPicker = e.NameScope.Find<ListBox>("PART_Years");
        choosingYear = false;
        if (previous is not null) previous.Click += GoPrevious;
        if (next is not null) next.Click += GoNext;
        if (heading is not null) heading.Click += ToggleYearPicker;
        if (yearPicker is not null)
        {
            // Recycled containers can clear their Content while this template is still active.
            yearPicker.ItemTemplate = new FuncDataTemplate<YearRow?>((row, _) => row is null ? null : BuildYearRow(row));
        }
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayDateProperty || change.Property == CultureProperty || change.Property == DisplayFormatProperty ||
            change.Property == FirstDayOfWeekProperty || change.Property == ViewProperty ||
            change.Property == VisibleMonthsProperty || change.Property == FixedWeeksProperty ||
            change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == TodayProperty)
            Rebuild();
        else if (change.Property == FocusedDateProperty && !settingFocusDate && FocusedDate is { } date)
            Reveal(date, IsKeyboardFocusWithin ? NavigationMethod.Directional : null);
        else if (change.Property == IsDateUnavailableProperty || change.Property == DateIndicatorProperty ||
                 change.Property == IsInvalidProperty)
            RefreshDates();
        else if (change.Property == ShowYearPickerProperty)
        {
            if (!ShowYearPicker && choosingYear) SetYearPicker(false, restoreFocus: false);
            else Rebuild();
        }
    }

    /// <summary>Refreshes the calendar after the derived control changes its selection.</summary>
    protected void SelectionChanged()
    {
        if (months is null) return;
        if (DisplayDate is null && SelectionDate is { } date && days.All(x => x.Date != date || x.Outside))
            Rebuild();
        else
            RefreshDates();
    }

    /// <summary>Reevaluate availability and indicators when the delegates' backing data changes.</summary>
    public void RefreshDates()
    {
        var range = HighlightedRange;
        foreach (var day in days)
        {
            bool unavailable = IsUnavailable(day.Date);
            bool selected = !day.Outside && !unavailable && IsInBounds(day.Date) &&
                            (IsSelected(day.Date) || range?.Contains(day.Date) == true);
            bool start = selected && range?.Start == day.Date;
            bool end = selected && range?.End == day.Date;
            bool middle = selected && range is not null && !start && !end;
            var button = day.Button;
            button.IsEnabled = !day.Outside && IsInBounds(day.Date);
            button.Classes.Set("unavailable", unavailable);
            button.Classes.Set("selected", selected && !middle);
            button.Classes.Set("range", selected && range is not null);
            button.Classes.Set("middle", middle);
            button.Classes.Set("start", start);
            button.Classes.Set("end", end);
            button.Classes.Set("left-edge", day.Column == 0 || !SharesTrack(range, day, -1));
            button.Classes.Set("right-edge", day.Column == 6 || !SharesTrack(range, day, 1));
            button.Classes.Set("invalid", IsInvalid && selected);
            button.Indicator = day.Outside ? null : DateIndicator?.Invoke(day.Date);
        }
        UpdateTabStop();
    }

    private bool SharesTrack(AlderDateRange? range, CalendarDay day, int direction)
    {
        if (range is null) return false;
        long number = (long)day.Date.DayNumber + direction;
        if (number < 0 || number > DateOnly.MaxValue.DayNumber) return false;
        var other = DateOnly.FromDayNumber((int)number);
        return range.Value.Contains(other) && IsInBounds(other) && !IsUnavailable(other) &&
               (View != AlderCalendarView.Month || MonthStart(other) == MonthStart(day.Date));
    }

    private void Rebuild()
    {
        if (months is null || rebuilding) return;
        rebuilding = true;
        try
        {
            var anchor = Anchor;
            var focusedDay = days.Find(x => x.Button.IsFocused);
            var focusMethod = focusedDay?.Button.Classes.Contains(":focus-visible") == true
                ? NavigationMethod.Directional : NavigationMethod.Unspecified;
            months.Children.Clear();
            days.Clear();
            var start = View switch
            {
                AlderCalendarView.Month => MonthStart(anchor),
                AlderCalendarView.Week => StartOfWeek(anchor),
                _ => anchor
            };
            var last = start;
            for (int i = 0; i < PageCount; i++)
            {
                var page = View == AlderCalendarView.Month ? MonthStart(MoveMonths(start, i)) : start;
                if (i > 0 && page <= last) break;
                months.Children.Add(BuildPage(page, PageCount > 1));
                last = page;
            }
            UpdateHeading(HeaderText(anchor, start, last));
            UpdateNavigation(start, last);
            RefreshDates();
            if (focusedDay is not null) FindDay(focusedDay.Date)?.Button.Focus(focusMethod);
        }
        finally { rebuilding = false; }
    }

    private Control BuildPage(DateOnly start, bool showMonth)
    {
        var format = EffectiveCulture.DateTimeFormat;
        var cols = new ColumnDefinitions("*,*,*,*,*,*,*");
        var grid = new Grid { ColumnDefinitions = cols, Width = 252, MaxWidth = 252 };
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(27)));
        int firstColumn = View == AlderCalendarView.Day ? WeekOffset(start.DayOfWeek) : 0;
        for (int col = 0; col < 7; col++)
        {
            if (View == AlderCalendarView.Day && col != firstColumn) continue;
            var dayOfWeek = (DayOfWeek)(((int)WeekStart + col) % 7);
            var label = new AlderTextBlock
            {
                Text = format.GetShortestDayName(dayOfWeek),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
            AutomationProperties.SetName(label, format.GetDayName(dayOfWeek));
            Grid.SetColumn(label, col);
            grid.Children.Add(label);
        }
        int offset = View == AlderCalendarView.Day ? 0 : WeekOffset(start.DayOfWeek);
        int cellCount = View == AlderCalendarView.Month
            ? (FixedWeeks ? 42 : (offset + DaysInMonth(start) + 6) / 7 * 7)
            : View == AlderCalendarView.Week ? 7 : 1;
        for (int row = 0; row < (cellCount + 6) / 7; row++) grid.RowDefinitions.Add(new RowDefinition(new GridLength(36)));
        for (int i = 0; i < cellCount; i++)
        {
            long number = (long)start.DayNumber - offset + i;
            if (number < CalendarMinimum.DayNumber || number > CalendarMaximum.DayNumber) continue;
            var date = DateOnly.FromDayNumber((int)number);
            int col = View == AlderCalendarView.Day ? firstColumn : i % 7;
            bool outside = View == AlderCalendarView.Month && MonthStart(date) != start;
            var cell = CreateDay(date, col, outside);
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, 1 + i / 7);
            grid.Children.Add(cell);
        }
        if (!showMonth) return grid;
        var title = new AlderTextBlock { Text = FormatHeading(start, "Y"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
        title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        return new StackPanel { Spacing = 4, Children = { title, grid } };
    }

    private AlderCalendarDayButton CreateDay(DateOnly date, int column, bool outside)
    {
        var button = new AlderCalendarDayButton { Content = DayNumber(date).ToString(EffectiveCulture), Tag = date };
        button[!ThemeProperty] = new DynamicResourceExtension("AlderCalendarDayTheme");
        button.Classes.Set("outside", outside);
        button.Classes.Set("today", date == CalendarToday);
        AutomationProperties.SetName(button, Format(date, "D"));
        button.Click += (_, _) =>
        {
            if (!IsReadOnly && IsInBounds(date) && !IsUnavailable(date)) SelectDate(date);
        };
        button.PointerEntered += (_, _) => { if (!outside && !IsReadOnly) HoverDate(date); };
        button.PointerExited += (_, _) => { if (!outside && !IsReadOnly) HoverDate(null); };
        button.GotFocus += (_, _) =>
        {
            if (rebuilding || outside) return;
            SetFocusedDate(date);
            if (!IsReadOnly) HoverDate(date);
            UpdateTabStop();
        };
        days.Add(new CalendarDay(date, column, outside, button));
        return button;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            if (choosingYear) { SetYearPicker(false, restoreFocus: true); e.Handled = true; }
            else e.Handled = CancelPreview();
            return;
        }
        if (choosingYear && e.Source is Button { Tag: int year })
        {
            int direction = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
            int? nextYear = e.Key switch
            {
                Key.Left => year - direction,
                Key.Right => year + direction,
                Key.Up => year - 3,
                Key.Down => year + 3,
                Key.Home => yearRows[0].Start,
                Key.End => yearRows[^1].Start + yearRows[^1].Count - 1,
                _ => null
            };
            if (nextYear is { } n) { FocusYear(n); e.Handled = true; }
            return;
        }
        if (e.Source is not AlderCalendarDayButton { Tag: DateOnly from }) return;
        int horizontal = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
        var to = e.Key switch
        {
            Key.Left => AddDays(from, -horizontal),
            Key.Right => AddDays(from, horizontal),
            Key.Up => AddDays(from, -7),
            Key.Down => AddDays(from, 7),
            Key.Home => StartOfWeek(from),
            Key.End => AddDays(StartOfWeek(from), 6),
            Key.PageUp => MoveMonths(from, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -12 : -1),
            Key.PageDown => MoveMonths(from, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 12 : 1),
            _ => (DateOnly?)null
        };
        if (to is null) return;
        Reveal(to.Value, NavigationMethod.Directional);
        e.Handled = true;
    }

    internal void FocusDate(NavigationMethod method) => Reveal(FocusedDate ?? SelectionDate ?? Anchor, method);

    private void Reveal(DateOnly date, NavigationMethod? focusMethod)
    {
        if (months is null) return;
        var target = Clamp(date);
        SetFocusedDate(target);
        if (FindDay(target) is null)
            SetCurrentValue(DisplayDateProperty, target);
        var button = FindDay(target)?.Button;
        UpdateTabStop();
        if (focusMethod is { } method) button?.Focus(method);
    }

    private CalendarDay? FindDay(DateOnly date) => days.Find(x => x.Date == date && !x.Outside);

    private void SetFocusedDate(DateOnly date)
    {
        settingFocusDate = true;
        try { SetCurrentValue(FocusedDateProperty, date); }
        finally { settingFocusDate = false; }
    }

    private void UpdateTabStop()
    {
        var active = days.Find(x => x.Button.IsFocused && x.Button.IsEnabled) ??
                     FindDay(FocusedDate ?? SelectionDate ?? CalendarToday);
        if (active?.Button.IsEnabled != true) active = days.Find(x => x.Button.IsEnabled);
        foreach (var day in days) day.Button.IsTabStop = !choosingYear && day == active;
    }

    private void GoPrevious(object? sender, RoutedEventArgs e) => GoPage(-1);
    private void GoNext(object? sender, RoutedEventArgs e) => GoPage(1);

    private void GoPage(int direction)
    {
        var date = View switch
        {
            AlderCalendarView.Day => AddDays(Anchor, direction),
            AlderCalendarView.Week => AddDays(Anchor, 7 * direction),
            _ => MoveMonths(MonthStart(Anchor), PageCount * direction)
        };
        SetFocusedDate(Clamp(date));
        SetCurrentValue(DisplayDateProperty, Clamp(date));
        UpdateTabStop();
    }

    private void UpdateNavigation(DateOnly start, DateOnly last)
    {
        var end = View switch
        {
            AlderCalendarView.Day => start,
            AlderCalendarView.Week => AddDays(start, 6),
            _ => AddDays(last, DaysInMonth(last) - 1)
        };
        if (previous is not null) { previous.IsEnabled = start > EffectiveMinimum; previous.Classes.Set("years-open", choosingYear); }
        if (next is not null) { next.IsEnabled = end < EffectiveMaximum; next.Classes.Set("years-open", choosingYear); }
    }

    private void ToggleYearPicker(object? sender, RoutedEventArgs e)
    {
        if (ShowYearPicker) SetYearPicker(!choosingYear, restoreFocus: true);
    }

    private void SetYearPicker(bool open, bool restoreFocus)
    {
        if (yearPicker is null || months is null) return;
        choosingYear = open;
        months.Opacity = open ? 0 : 1;
        months.IsHitTestVisible = !open;
        yearPicker.IsVisible = open;
        yearPicker.Opacity = open ? 1 : 0;
        if (open)
        {
            var current = Anchor;
            int era = CalendarSystem.GetEra(AsDateTime(current));
            int year = CalendarSystem.GetYear(AsDateTime(current));
            var earliest = AsDateTime(EffectiveMinimum <= CalendarMaximum ? EffectiveMinimum : CalendarMaximum);
            var latest = AsDateTime(EffectiveMaximum >= CalendarMinimum ? EffectiveMaximum : CalendarMinimum);
            int start = CalendarSystem.GetEra(earliest) == era ? CalendarSystem.GetYear(earliest) : 1;
            int end = CalendarSystem.GetEra(latest) == era ? CalendarSystem.GetYear(latest) : year + 100;
            yearRows = Enumerable.Range(0, Math.Max(0, end - start + 3) / 3)
                .Select(i => new YearRow(start + i * 3, Math.Min(3, end - start - i * 3 + 1), year)).ToArray();
            yearPicker.ItemsSource = yearRows;
            yearPicker.Height = Math.Max(104, months.Bounds.Height);
            yearPicker.Focus();
            FocusYear(year);
        }
        else if (restoreFocus) heading?.Focus();
        var date = Anchor;
        var month = MonthStart(date);
        UpdateHeading(HeaderText(date, View == AlderCalendarView.Week ? StartOfWeek(date) : month, MoveMonths(month, PageCount - 1)));
        UpdateNavigation(View == AlderCalendarView.Day ? date : View == AlderCalendarView.Week ? StartOfWeek(date) : month,
            MoveMonths(month, PageCount - 1));
        UpdateTabStop();
    }

    private Control BuildYearRow(YearRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 4, Margin = new Thickness(2) };
        for (int column = 0; column < row.Count; column++)
        {
            int year = row.Start + column;
            var button = new AlderButton { Content = year.ToString(EffectiveCulture), Tag = year, IsTabStop = year == row.Current };
            button[!ThemeProperty] = new DynamicResourceExtension("AlderCalendarYearTheme");
            button.Classes.Set("current", year == row.Current);
            button.Click += (_, _) => SelectYear(year);
            Grid.SetColumn(button, column);
            grid.Children.Add(button);
        }
        return grid;
    }

    private void FocusYear(int year)
    {
        if (yearPicker is null || yearRows.Length == 0) return;
        int position = year - yearRows[0].Start;
        if (position < 0 || position / 3 >= yearRows.Length || position % 3 >= yearRows[position / 3].Count) return;
        yearPicker.ScrollIntoView(yearRows[position / 3]);
        Dispatcher.UIThread.Post(() =>
        {
            if (choosingYear)
                yearPicker.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => Equals(button.Tag, year))?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void SelectYear(int year)
    {
        if (!choosingYear) return;
        var date = AsDateTime(Anchor);
        try
        {
            var calendar = CalendarSystem;
            int era = calendar.GetEra(date);
            int month = Math.Min(calendar.GetMonth(date), calendar.GetMonthsInYear(year, era));
            int day = Math.Min(calendar.GetDayOfMonth(date), calendar.GetDaysInMonth(year, month, era));
            var target = Clamp(DateOnly.FromDateTime(calendar.ToDateTime(year, month, day, 0, 0, 0, 0, era)));
            SetFocusedDate(target);
            SetCurrentValue(DisplayDateProperty, target);
        }
        catch (ArgumentOutOfRangeException) { return; }
        SetYearPicker(false, restoreFocus: true);
    }

    private void UpdateHeading(string title)
    {
        if (heading is null) return;
        AutomationProperties.SetName(heading, title);
        heading.Classes.Set("expanded", choosingYear);
        headingIcon?.Classes.Set("expanded", choosingYear);
        if (headingText is not null) headingText.Text = title;
        else heading.Content = title;
    }

    /// <summary>Reports whether the supplied date is between the inclusive minimum and maximum.</summary>
    protected bool IsInBounds(DateOnly date) => date >= EffectiveMinimum && date <= EffectiveMaximum;

    private DateOnly Clamp(DateOnly date)
    {
        var min = EffectiveMinimum > CalendarMaximum ? CalendarMaximum : EffectiveMinimum;
        var max = EffectiveMaximum < CalendarMinimum ? CalendarMinimum : EffectiveMaximum;
        if (max < min) max = min;
        return date < min ? min : date > max ? max : date;
    }

    private DateOnly MoveMonths(DateOnly date, int count)
    {
        try { return DateOnly.FromDateTime(CalendarSystem.AddMonths(AsDateTime(date), count)); }
        catch (ArgumentException) { return count < 0 ? CalendarMinimum : CalendarMaximum; }
    }

    private DateOnly MonthStart(DateOnly date) => AddDays(date, 1 - DayNumber(date));
    private DateOnly StartOfWeek(DateOnly date) => AddDays(date, -WeekOffset(date.DayOfWeek));
    private int WeekOffset(DayOfWeek day) => (7 + (int)day - (int)WeekStart) % 7;
    private int DayNumber(DateOnly date) => CalendarSystem.GetDayOfMonth(AsDateTime(date));
    private int DaysInMonth(DateOnly date)
    {
        var value = AsDateTime(date);
        return CalendarSystem.GetDaysInMonth(CalendarSystem.GetYear(value), CalendarSystem.GetMonth(value), CalendarSystem.GetEra(value));
    }

    private DateOnly AddDays(DateOnly date, int days) =>
        DateOnly.FromDayNumber(Math.Clamp(date.DayNumber + days, CalendarMinimum.DayNumber, CalendarMaximum.DayNumber));
    private static DateTime AsDateTime(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);
    private string Format(DateOnly date, string format) => AsDateTime(date).ToString(format, EffectiveCulture);
    private string FormatHeading(DateOnly date, string defaultFormat) =>
        AlderTemporalInput.FormatOrDefault(AsDateTime(date), DisplayFormat, defaultFormat, EffectiveCulture);

    private string HeaderText(DateOnly anchor, DateOnly start, DateOnly last) => View switch
    {
        AlderCalendarView.Day => FormatHeading(anchor, "D"),
        AlderCalendarView.Week => $"{FormatHeading(start, "M")} – {FormatHeading(AddDays(start, 6), "M")}",
        _ => last == start ? FormatHeading(start, "Y") : $"{FormatHeading(start, "MMM")} – {FormatHeading(last, "Y")}"
    };

    private sealed record CalendarDay(DateOnly Date, int Column, bool Outside, AlderCalendarDayButton Button);
    private sealed record YearRow(int Start, int Count, int Current);
}

/// <summary>Represents an interactive day cell and hosts its optional date indicator.</summary>
public sealed class AlderCalendarDayButton : Button
{
    /// <summary>Identifies the <see cref="Indicator"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorProperty =
        AvaloniaProperty.Register<AlderCalendarDayButton, IBrush?>(nameof(Indicator));
    /// <summary>The optional brush drawn as an indicator on this date.</summary>
    public IBrush? Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }
}
