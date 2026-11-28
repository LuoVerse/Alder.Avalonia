using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Segment editing shared by numeric dates and localized clock times.</summary>
public abstract partial class AlderTemporalField : AlderTemporalInput
{
    /// <summary>Identifies the date or time component edited by a segment.</summary>
    protected enum Part
    {
        /// <summary>Edits the year component.</summary>
        Year,
        /// <summary>Edits the month component.</summary>
        Month,
        /// <summary>Edits the day component.</summary>
        Day,
        /// <summary>Edits the hour component.</summary>
        Hour,
        /// <summary>Edits the minute component.</summary>
        Minute,
        /// <summary>Edits the second component.</summary>
        Second,
        /// <summary>Edits the AM or PM period of a 12-hour value.</summary>
        Period
    }
    /// <summary>The date or time used when initializing empty editing segments.</summary>
    protected virtual DateTime ReferenceValue => DateTime.Today;
    /// <summary>The culture-dependent pattern from which the segments are built.</summary>
    protected abstract string InputPattern { get; }

    private protected virtual bool IsDateEditor => false;
    private protected bool UsesTwelveHourClock => HasPatternPart('h');
    private protected bool HasPatternPart(char part) => PatternParts().Matches(InputPattern)
        .Any(match => UnescapeToken(match.Value)[0] == part);
    /// <summary>Reads the current committed date or time from the derived editor.</summary>
    protected abstract DateTime? ReadValue();
    /// <summary>Attempts to interpret a text value using the derived editor's input format.</summary>
    protected abstract bool TryParseValue(string text, out DateTime value);
    /// <summary>Checks whether the parsed date or time satisfies the editor's restrictions.</summary>
    protected abstract bool IsAllowed(DateTime value);
    /// <summary>Writes the committed date or time, or clears it when the value is null.</summary>
    protected abstract void WriteValue(DateTime? value);

    private readonly List<Segment> segments = [];
    private readonly List<(AlderTextBlock Label, string Token)> dateLabels = [];
    private StackPanel? panel;
    private Segment? editing;
    private bool writingValue;
    private int typedDigits;
    private int pendingNumber;

    /// <inheritdoc />
    protected override Control? FocusTarget => segments.Count == 0 ? null : segments[0].Button;

    /// <summary>Initializes a new instance of <see cref="AlderTemporalField"/>.</summary>
    protected AlderTemporalField() => ResourcesChanged += (_, _) => UpdateSegments();

    [GeneratedRegex(@"'[^']*'|""[^""]*""|\\.|%[yMdhHmstg/]|y+|M+|d+|[Hh]+|m+|s+|t+|g+|.", RegexOptions.CultureInvariant)]
    private static partial Regex PatternParts();

    private static string UnescapeToken(string token) => token[0] == '%' && token.Length == 2 ? token[1..] : token;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        panel = e.NameScope.Find<StackPanel>("PART_Segments");
        RebuildSegments();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CultureProperty || change.Property == DisplayFormatProperty) RebuildSegments();
    }

    /// <summary>Recreates the editable segments for the current input pattern.</summary>
    protected void RebuildSegments()
    {
        if (panel is null) return;
        var focusedPart = segments.Find(x => x.Button.IsFocused)?.Part;
        ResetTyping(null);
        panel.Children.Clear();
        segments.Clear();
        dateLabels.Clear();
        bool custom = HasDisplayFormat;
        var format = InputCulture.DateTimeFormat;
        foreach (Match match in PatternParts().Matches(InputPattern))
        {
            string token = custom ? UnescapeToken(match.Value) : match.Value;
            if (custom && IsDateEditor && (token[0] == 'g' || token[0] == 'd' && token.Length >= 3))
            {
                var label = new AlderTextBlock { VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
                dateLabels.Add((label, token));
                panel.Children.Add(label);
                continue;
            }
            var part = token[0] switch
            {
                'y' => Part.Year,
                'M' => Part.Month,
                'd' => Part.Day,
                'H' or 'h' => Part.Hour,
                'm' => Part.Minute,
                's' => Part.Second,
                't' => Part.Period,
                _ => (Part?)null
            };
            if (part is null)
            {
                var literal = token[0] switch
                {
                    '/' => format.DateSeparator,
                    ':' => format.TimeSeparator,
                    '\'' or '"' => token[1..^1],
                    '\\' => token[1..],
                    'g' => format.GetEraName(format.Calendar.GetEra(ReadValue() ?? ReferenceValue)),
                    _ => token
                };
                var text = new AlderTextBlock { Text = literal, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
                text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
                panel.Children.Add(text);
                continue;
            }
            var button = new AlderButton { Cursor = new Cursor(StandardCursorType.Ibeam) };
            button[!ThemeProperty] = new DynamicResourceExtension("AlderTemporalSegmentTheme");
            int digits = part == Part.Year && (!custom || token.Length > 2) ? 4 : 2;
            var segment = new Segment(part.Value, digits, token[0] == 'h', button, custom ? token : null);
            button.Click += (_, _) => button.Focus(NavigationMethod.Pointer);
            button.GotFocus += (_, _) => ResetTyping(segment);
            button.LostFocus += (_, _) => FinishTyping(segment);
            button.AddHandler(KeyDownEvent, (_, e) => HandleSegmentKey(segment, e), RoutingStrategies.Tunnel);
            button.TextInput += (_, e) =>
            {
                if (IsReadOnly || string.IsNullOrEmpty(e.Text)) return;
                if (TryParsePastedValue(e.Text) || Type(segment, e.Text)) e.Handled = true;
            };
            segments.Add(segment);
            panel.Children.Add(button);
        }
        SyncSegments();
        if (focusedPart is { } partToRestore) segments.Find(x => x.Part == partToRestore)?.Button.Focus();
    }

    /// <summary>Refreshes the segment display from the editor's current committed value.</summary>
    protected void SyncSegments()
    {
        if (writingValue || panel is null) return;
        ResetTyping(null);
        var value = ReadValue();
        foreach (var segment in segments) segment.Value = value is { } date ? Extract(date, segment) : null;
        UpdateInputError(value is { } current && !IsAllowed(current));
        UpdateSegments();
    }

    private int Extract(DateTime value, Segment segment)
    {
        var calendar = InputCulture.Calendar;
        value = ClampToCalendar(value);
        return segment.Part switch
        {
            Part.Year => calendar.GetYear(value),
            Part.Month => calendar.GetMonth(value),
            Part.Day => calendar.GetDayOfMonth(value),
            Part.Hour => segment.TwelveHour ? (value.Hour + 11) % 12 + 1 : value.Hour,
            Part.Minute => value.Minute,
            Part.Second => value.Second,
            _ => value.Hour / 12
        };
    }

    private DateTime ClampToCalendar(DateTime value)
    {
        var calendar = InputCulture.Calendar;
        return value < calendar.MinSupportedDateTime ? calendar.MinSupportedDateTime :
            value > calendar.MaxSupportedDateTime ? calendar.MaxSupportedDateTime : value;
    }

    private void UpdateSegments()
    {
        var format = InputCulture.DateTimeFormat;
        var current = ReadValue();
        foreach (var (label, token) in dateLabels)
        {
            var value = ClampToCalendar(current ?? ReferenceValue);
            label.Text = token[0] == 'g' ? format.GetEraName(format.Calendar.GetEra(value))
                : current is null ? "––"
                : token.Length == 3 ? format.GetAbbreviatedDayName(value.DayOfWeek) : format.GetDayName(value.DayOfWeek);
        }
        foreach (var segment in segments)
        {
            string text;
            if (segment.Part == Part.Period)
            {
                text = segment.Value switch { 0 => format.AMDesignator, 1 => format.PMDesignator, _ => "––" };
                if (segment.Token == "t" && segment.Value is not null) text = StringInfo.GetNextTextElement(text);
            }
            else if (segment == editing && typedDigits > 0)
                text = pendingNumber.ToString("D" + typedDigits, InputCulture);
            else if (segment.Value is { } number)
                text = FormatNumber(segment, number, format);
            else
                text = segment.Part switch
                {
                    Part.Year => LocalText("YearPlaceholder", "yyyy"),
                    Part.Month => LocalText("MonthPlaceholder", "mm"),
                    Part.Day => LocalText("DayPlaceholder", "dd"),
                    Part.Hour => LocalText("HourPlaceholder", "hh"),
                    Part.Minute => LocalText("MinutePlaceholder", "mm"),
                    _ => LocalText("SecondPlaceholder", "ss")
                };
            segment.Button.Content = text;
            segment.Button.Classes.Set("placeholder", segment.Value is null);
            segment.Button.Classes.Set("invalid", HasInvalidInput || IsInvalid);
            var part = segment.Part.ToString();
            var name = LocalText(segment.Part == Part.Second ? "SecondUnit" : part, part);
            AutomationProperties.SetName(segment.Button, $"{name}, {text}");
        }
    }

    private string FormatNumber(Segment segment, int value, DateTimeFormatInfo format)
    {
        if (segment.Token is not { } token) return value.ToString("D" + segment.Digits, InputCulture);
        if (segment.Part == Part.Month && token.Length >= 3)
            return token.Length == 3 ? format.GetAbbreviatedMonthName(value) : format.GetMonthName(value);
        if (segment.Part == Part.Year && token.Length <= 2) value %= 100;
        int width = segment.Part == Part.Year ? token.Length : Math.Min(token.Length, 2);
        return value.ToString("D" + width, InputCulture);
    }

    private string LocalText(string key, string fallback) =>
        this.TryFindResource("AlderText." + key, out var value) && value is string text ? text : fallback;

    private void HandleSegmentKey(Segment segment, KeyEventArgs e)
    {
        var key = e.Key;
        if (key == Key.V && e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta && !IsReadOnly)
        {
            e.Handled = true;
            Paste();
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (key is Key.Left or Key.Right or Key.Home or Key.End)
        {
            var direction = (key == Key.Left ? -1 : 1) * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1);
            int index = key == Key.Home ? 0 : key == Key.End ? segments.Count - 1 : segments.IndexOf(segment) + direction;
            FocusSegment(index);
        }
        else if (!IsReadOnly && key is Key.Up or Key.Down)
        {
            FinishTyping(segment);
            var reference = ReadValue() ?? ReferenceValue;
            var (min, max) = Limits(segment);
            int next = (segment.Value ?? Extract(reference, segment)) + (key == Key.Up ? 1 : -1);
            segment.Value = next > max ? min : next < min ? max : next;
            Evaluate(clampDay: segment.Part is Part.Year or Part.Month);
        }
        else if (!IsReadOnly && key is Key.Delete or Key.Back)
        {
            ResetTyping(segment);
            segment.Value = null;
            Evaluate();
        }
        else if (key == Key.Enter && typedDigits > 0)
            FinishTyping(segment);
        else if (key == Key.Escape && HasInvalidInput)
            SyncSegments();
        else return;
        e.Handled = true;
    }

    private async void Paste()
    {
        var root = TopLevel.GetTopLevel(this);
        if (root?.Clipboard is not { } clipboard) return;
        string? text;
        try { text = await clipboard.TryGetTextAsync(); }
        catch (Exception error) when (error is InvalidOperationException or ExternalException) { return; }
        if (text is not null && IsKeyboardFocusWithin && !IsReadOnly && IsEffectivelyEnabled && root == TopLevel.GetTopLevel(this))
            TryParsePastedValue(text, fromClipboard: true);
    }

    private bool TryParsePastedValue(string text, bool fromClipboard = false)
    {
        bool wholeText = HasParseFormat && (fromClipboard || text.Length > 4) ||
                         text.Length >= 3 && text.Any(c => !char.IsDigit(c));
        if (!wholeText || !TryParseValue(text, out var date) || !IsAllowed(date))
            return false;
        SetEditedValue(date);
        SyncSegments();
        return true;
    }

    private bool Type(Segment segment, string text)
    {
        if (segment.Part == Part.Period)
        {
            var format = InputCulture.DateTimeFormat;
            if (text.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                InputCulture.CompareInfo.IsPrefix(format.AMDesignator, text, CompareOptions.IgnoreCase))
                segment.Value = 0;
            else if (text.Equals("p", StringComparison.OrdinalIgnoreCase) ||
                     InputCulture.CompareInfo.IsPrefix(format.PMDesignator, text, CompareOptions.IgnoreCase))
                segment.Value = 1;
            else return false;
            Evaluate();
            FocusSegment(segments.IndexOf(segment) + 1);
            return true;
        }
        if (text.Any(c => !char.IsDigit(c))) return false;
        if (editing != segment) ResetTyping(segment);

        var (min, max) = segment.Part == Part.Year && segment.Digits == 2 ? (0, 99) : Limits(segment);
        foreach (char c in text)
        {
            int digit = CharUnicodeInfo.GetDecimalDigitValue(c);
            int candidate = pendingNumber * 10 + digit;
            if (typedDigits >= segment.Digits || candidate > max)
            {
                ResetTyping(segment);
                candidate = digit;
            }
            pendingNumber = candidate;
            typedDigits++;
            bool complete = typedDigits >= segment.Digits || pendingNumber * 10 > max;
            segment.Value = candidate >= min && candidate <= max ? candidate : null;
            if (complete)
            {
                FinishTyping(segment);
                FocusSegment(segments.IndexOf(segment) + 1);
                break;
            }
            UpdateSegments();
        }
        return true;
    }

    private (int Min, int Max) Limits(Segment segment)
    {
        var calendar = InputCulture.Calendar;
        var reference = ClampToCalendar(ReadValue() ?? ReferenceValue);
        var era = calendar.GetEra(reference);
        return segment.Part switch
        {
            Part.Year => (1, calendar.GetYear(calendar.MaxSupportedDateTime)),
            Part.Month => (1, calendar.GetMonthsInYear(calendar.GetYear(reference), era) == 13 || calendar is HebrewCalendar ? 13 : 12),
            Part.Day => (1, 31),
            Part.Hour => segment.TwelveHour ? (1, 12) : (0, 23),
            Part.Minute or Part.Second => (0, 59),
            _ => (0, 1)
        };
    }

    private void ResetTyping(Segment? segment)
    {
        editing = segment;
        typedDigits = pendingNumber = 0;
    }

    private void FinishTyping(Segment segment)
    {
        if (editing != segment) return;
        if (segment.Part == Part.Year && segment.Digits == 2 && typedDigits > 0 && segment.Value is { } year)
            segment.Value = InputCulture.Calendar.ToFourDigitYear(year);
        ResetTyping(null);
        Evaluate();
    }

    private void FocusSegment(int index)
    {
        if (index >= 0 && index < segments.Count) segments[index].Button.Focus(NavigationMethod.Directional);
    }

    private int? PartValue(Part part) => segments.Find(x => x.Part == part)?.Value;

    private void Evaluate(bool clampDay = false)
    {
        bool allEmpty = segments.All(x => x.Value is null);
        if (allEmpty && typedDigits == 0) SetEditedValue(null);
        if (segments.Any(x => x.Value is null) || editing?.Part == Part.Year && typedDigits > 0)
        {
            UpdateInputError(false);
            UpdateSegments();
            return;
        }

        var committed = ReadValue();
        var reference = ClampToCalendar(committed ?? ReferenceValue);
        int hour = PartValue(Part.Hour) ?? (HasDisplayFormat && !IsDateEditor ? reference.Hour : 0);
        if (PartValue(Part.Period) is { } period) hour = hour % 12 + 12 * period;
        else if (HasDisplayFormat && segments.Exists(x => x.Part == Part.Hour && x.TwelveHour))
            hour = hour % 12 + 12 * (reference.Hour / 12);
        bool valid = false;
        try
        {
            DateTime value;
            if (PartValue(Part.Year) is not null || HasDisplayFormat && IsDateEditor)
            {
                var calendar = InputCulture.Calendar;
                int year = PartValue(Part.Year) ?? calendar.GetYear(reference);
                int month = PartValue(Part.Month) ?? (HasDisplayFormat ? calendar.GetMonth(reference) : 1);
                var era = calendar.GetEra(reference);
                int day = PartValue(Part.Day) ?? (HasDisplayFormat ? calendar.GetDayOfMonth(reference) : 1);
                if (clampDay || HasDisplayFormat && PartValue(Part.Day) is null)
                {
                    day = Math.Min(day, calendar.GetDaysInMonth(year, month, era));
                    if (segments.Find(x => x.Part == Part.Day) is { } daySegment) daySegment.Value = day;
                }
                value = calendar.ToDateTime(year, month, day, hour, PartValue(Part.Minute) ?? 0, PartValue(Part.Second) ?? 0, 0, era);
            }
            else
                value = reference.Date.AddHours(hour)
                    .AddMinutes(PartValue(Part.Minute) ?? (HasDisplayFormat ? reference.Minute : 0))
                    .AddSeconds(PartValue(Part.Second) ?? committed?.Second ?? 0);
            valid = IsAllowed(value);
            if (valid) SetEditedValue(value);
        }
        catch (ArgumentOutOfRangeException) { }
        UpdateInputError(!valid);
        UpdateSegments();
    }

    private void SetEditedValue(DateTime? value)
    {
        writingValue = true;
        try { WriteValue(value); }
        finally { writingValue = false; }
    }

    private sealed class Segment(Part part, int digits, bool twelveHour, Button button, string? token)
    {
        public Part Part { get; } = part;
        public int Digits { get; } = digits;
        public bool TwelveHour { get; } = twelveHour;
        public Button Button { get; } = button;
        public string? Token { get; } = token;
        public int? Value { get; set; }
    }
}
