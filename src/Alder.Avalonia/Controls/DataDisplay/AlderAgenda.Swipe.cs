using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderAgenda
{
    private const double SwipeThreshold = 9;
    private const double WheelPixels = 50;
    private static readonly TimeSpan WheelIdle = TimeSpan.FromMilliseconds(45);
    private TouchPan? swipe;
    private Action<TimeSpan>? panFrame;
    private bool panFrameRequested;
    private double panDays;
    private long wheelUpdated;
    private double wheelVelocity;
    private long panStarted;
    private double panFrom;
    private double panTo;
    private double panDuration;
    private double panInitialSlope;
    private Action? panCompleted;
    private DateOnly? committingDate;

    private double ScheduleDayWidth => dayStrips.Count == 0 ? 0 : dayStrips[^1].DayWidth;
    private bool PanIsSettling => panStarted != 0 || wheelUpdated != 0;
    private int PanMinimum => -daysBefore;
    private int PanMaximum => scheduleDays.Length - visibleDays.Length - daysBefore;
    private int PanDirection => FlowDirection == FlowDirection.RightToLeft ? 1 : -1;

    private bool IsInDayStrip(Visual source) => dayStrips.Any(strip => strip == source || strip.IsVisualAncestorOf(source));

    private Point TouchPosition(PointerEventArgs e) => e.GetPosition(TopLevel.GetTopLevel(this));

    private void BeginTouchPan(PointerPressedEventArgs e, Visual source)
    {
        if (!AllowsSwipe || ScheduleDayWidth <= 0 || !IsInDayStrip(source)) return;
        StopPanMotion();
        swipe = new TouchPan(e.Pointer, TouchPosition(e), panDays);
    }

    private void MoveTouchPan(PointerEventArgs e)
    {
        if (swipe is not { } touch || touch.Pointer != e.Pointer || ScheduleDayWidth <= 0) return;
        Vector movement = TouchPosition(e) - touch.Origin;
        if (!touch.IsDragging)
        {
            if (Math.Abs(movement.Y) >= SwipeThreshold && Math.Abs(movement.Y) >= Math.Abs(movement.X))
            {
                swipe = null;
                SnapPan(panDays, 0);
                return;
            }
            if (Math.Abs(movement.X) < SwipeThreshold) return;
            touch.IsDragging = true;
            e.Pointer.Capture(this);
        }
        SetPanOffset(touch.Start + PanDirection * movement.X / ScheduleDayWidth);
        touch.Sample(panDays);
        e.Handled = true;
    }

    private void EndTouchPan(PointerReleasedEventArgs e)
    {
        if (swipe is not { } touch || touch.Pointer != e.Pointer) return;
        swipe = null;
        if (!touch.IsDragging)
        {
            if (panDays != 0) SnapPan(panDays, 0);
            return;
        }

        if (ScheduleDayWidth > 0)
            SetPanOffset(touch.Start + PanDirection * (TouchPosition(e).X - touch.Origin.X) / ScheduleDayWidth);
        if (e.Pointer.Captured == this) e.Pointer.Capture(null);
        double velocity = touch.RecentVelocity;
        double projected = panDays + Math.Clamp(velocity * .19, -3, 3);
        SnapPan(projected, velocity);
        e.Handled = true;
    }

    private void OnAgendaWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!AllowsSwipe || ScheduleDayWidth <= 0 || gesture is not null || swipe is not null ||
            Math.Abs(e.Delta.X) <= Math.Abs(e.Delta.Y) || e.Source is not Visual source || !IsInDayStrip(source)) return;
        if (panStarted != 0) StopPanMotion();
        long timestamp = Stopwatch.GetTimestamp();
        double delta = PanDirection * e.Delta.X * WheelPixels / ScheduleDayWidth;
        double elapsed = wheelUpdated == 0 ? 0 : Stopwatch.GetElapsedTime(wheelUpdated, timestamp).TotalSeconds;
        if (elapsed is > .002 and < .07) wheelVelocity = delta / elapsed;
        SetPanOffset(panDays + delta);
        wheelUpdated = Stopwatch.GetTimestamp();
        RequestPanFrame();
        e.Handled = true;
    }

    private void SetPanOffset(double offset)
    {
        if (dayStrips.Count == 0) return;
        double next = ClampPan(offset);
        if (next == panDays) return;
        panDays = next;
        if (next < PanMinimum || next > PanMaximum)
        {
            int shift = next < PanMinimum ? (int)Math.Floor(next - PanMinimum) - 1 : (int)Math.Ceiling(next - PanMaximum) + 1;
            RefillSchedule(ShiftBufferCenter(shift));
        }
        ApplyPanOffset();
        UpdateAllDayRows(canShrink: false);
    }

    private double ClampPan(double offset)
    {
        int first = visibleDays[0].DayNumber;
        int last = visibleDays[^1].DayNumber;
        return Math.Clamp(offset, -first, DateOnly.MaxValue.DayNumber - last);
    }

    private void ApplyPanOffset()
    {
        foreach (var strip in dayStrips) strip.SetOffset(daysBefore + panDays);
        UpdateClockRuler();
    }

    private void SnapPan(double projected, double velocity, bool immediate = false, bool fromWheel = false)
    {
        if (dayStrips.Count == 0) return;
        double target = Math.Round(projected, MidpointRounding.AwayFromZero);
        if (!fromWheel && target == 0 && Math.Abs(velocity) > (ScheduleDayWidth > 0 ? 220 / ScheduleDayWidth : 0))
            target = Math.Sign(velocity);
        int days = (int)ClampPan(target);
        AnimatePan(days, () =>
        {
            if (days == 0) UpdateAllDayRows();
            else SetScheduleDate(ShiftDate(Date, days), ShiftDate(visibleDays[0], days));
        }, immediate, velocity, fromWheel);
    }

    private void NavigateSchedule(DateOnly date, DateOnly first, bool resetVertical = false)
    {
        if (dayStrips.Count == 0)
        {
            if (resetVertical) restoreViewport = false;
            SetCurrentValue(DisplayDateProperty, date);
            return;
        }

        int days = first.DayNumber - visibleDays[0].DayNumber;
        void Arrive() => SetScheduleDate(date, first, resetVertical);

        if (Math.Abs(days - panDays) <= DayCount || days >= PanMinimum && days <= PanMaximum)
            AnimatePan(days, Arrive);
        else
        {
            Arrive();
            if (AlderMotion.GetReduced(this)) return;
            SetPanOffset(-Math.Sign(days));
            AnimatePan(0, null);
        }
    }

    private void SetScheduleDate(DateOnly date, DateOnly first, bool resetVertical = false)
    {
        swipedStart = first == FirstScheduledDay(date) ? null : first;
        CommitScheduleViewport(first, resetVertical);
        committingDate = date;
        try { SetCurrentValue(DisplayDateProperty, date); }
        finally { committingDate = null; }
        UpdateHeading();
    }

    private void AnimatePan(double target, Action? completed, bool immediate = false, double? velocity = null, bool fromWheel = false)
    {
        StopPanMotion();
        double distance = Math.Abs(target - panDays);
        if (immediate || distance < .002 || AlderMotion.GetReduced(this) || TopLevel.GetTopLevel(this) is null)
        {
            SetPanOffset(target);
            completed?.Invoke();
            return;
        }

        panFrom = panDays;
        panTo = target;
        panDuration = fromWheel ? Math.Min(.20, .115 + distance * .075) : Math.Min(.42, .20 + distance * .055);
        panInitialSlope = velocity is { } speed ? Math.Clamp(speed * panDuration / (target - panFrom), 0, 3) : 3;
        panCompleted = completed;
        panStarted = Stopwatch.GetTimestamp();
        RequestPanFrame();
    }

    private void RequestPanFrame()
    {
        if (panFrameRequested || TopLevel.GetTopLevel(this) is not { } root) return;
        panFrameRequested = true;
        root.RequestAnimationFrame(panFrame ??= AdvancePan);
    }

    private void AdvancePan(TimeSpan _)
    {
        panFrameRequested = false;
        if (wheelUpdated != 0)
        {
            if (Stopwatch.GetElapsedTime(wheelUpdated) >= WheelIdle) SnapPan(panDays, wheelVelocity, fromWheel: true);
            else RequestPanFrame();
            return;
        }
        if (panStarted == 0) return;
        double progress = Stopwatch.GetElapsedTime(panStarted).TotalSeconds / panDuration;
        if (progress >= 1 || AlderMotion.GetReduced(this))
        {
            CompletePan();
            return;
        }
        double remaining = 1 - progress;
        double eased = progress * progress * (3 - 2 * progress) + panInitialSlope * progress * remaining * remaining;
        SetPanOffset(panFrom + (panTo - panFrom) * eased);
        RequestPanFrame();
    }

    private void CompletePan()
    {
        if (panStarted == 0) return;
        var completed = panCompleted;
        StopPanMotion();
        SetPanOffset(panTo);
        completed?.Invoke();
    }

    private void StopPanMotion()
    {
        wheelUpdated = panStarted = 0;
        wheelVelocity = 0;
        panCompleted = null;
    }

    private sealed class TouchPan(IPointer pointer, Point origin, double start)
    {
        private long sampledAt = Stopwatch.GetTimestamp();
        private double lastOffset = start;
        private double velocity;

        public IPointer Pointer { get; } = pointer;
        public Point Origin { get; } = origin;
        public double Start { get; } = start;
        public bool IsDragging { get; set; }
        public double RecentVelocity => Stopwatch.GetElapsedTime(sampledAt).TotalMilliseconds < 100 ? velocity : 0;

        public void Sample(double offset)
        {
            double elapsed = Stopwatch.GetElapsedTime(sampledAt).TotalSeconds;
            if (elapsed < .012) return;
            velocity = (offset - lastOffset) / elapsed;
            lastOffset = offset;
            sampledAt = Stopwatch.GetTimestamp();
        }
    }
}
