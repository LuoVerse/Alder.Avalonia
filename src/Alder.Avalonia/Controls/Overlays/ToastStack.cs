using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

internal sealed class ToastStack : Panel
{
    private static readonly SplineEasing Ease = new(.32, .72, 0, 1);
    private readonly List<Entry> visible = [];
    private readonly HashSet<Entry> exiting = [];
    private readonly ToastHost host;
    private bool expanded;
    private bool paused;
    private bool compact;
    private Point regionOrigin;

    public ToastStack(ToastHost host, ToastPlacement placement)
    {
        this.host = host;
        IsTop = placement is ToastPlacement.Top or ToastPlacement.TopStart or ToastPlacement.TopEnd;
        ClipToBounds = false;
    }

    public bool IsTop { get; }

    public void SetCompact(bool value)
    {
        if (compact == value) return;
        compact = value;
        foreach (var entry in visible) entry.Card.SetCompact(value);
    }

    public ToastHandle Show(ToastOptions options)
    {
        var entry = new Entry(this, options);
        entry.Handle.Dismiss = () => Close(entry, ToastCloseReason.Dismissed);
        entry.Handle.Update = update => Update(entry, update);
        entry.Card.SetCompact(compact);
        visible.Insert(0, entry);
        Children.Add(entry.Card);
        TrimToLimit();
        InvalidateMeasure();
        return entry.Handle;
    }

    public void TrimToLimit()
    {
        while (visible.Count > host.MaxVisible)
            Close(visible[^1], ToastCloseReason.Evicted);
    }

    public void Clear(ToastCloseReason reason)
    {
        foreach (var entry in visible) Finish(entry, reason);
        foreach (var entry in exiting) Finish(entry, reason);
        visible.Clear();
        exiting.Clear();
        Children.Clear();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var entry in exiting)
            entry.Card.Measure(entry.Bounds!.Value.Size);
        if (visible.Count == 0) return default;
        foreach (var entry in visible)
            entry.Card.Measure(availableSize);
        var height = (expanded || host.IsExpanded
            ? visible.Sum(entry => entry.Card.DesiredSize.Height)
            : visible[0].Card.DesiredSize.Height) + host.Gap * (visible.Count - 1);
        return new Size(availableSize.Width, Math.Min(height, availableSize.Height));
    }

    public void ArrangeRegion(Rect bounds)
    {
        regionOrigin = bounds.Position;
        Arrange(bounds);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // An exiting card keeps its last slot when the stack origin or height changes.
        var toLocal = default(Point) - regionOrigin;
        foreach (var entry in exiting)
            entry.Card.Arrange(entry.Bounds!.Value.Translate(toLocal));

        bool spread = expanded || host.IsExpanded;
        double position = 0;
        for (int index = 0; index < visible.Count; index++)
        {
            var entry = visible[index];
            var card = entry.Card;
            bool entering = entry.Bounds is null;
            double height = card.DesiredSize.Height;
            double y = IsTop ? position : finalSize.Height - height - position;
            // Collapsed cards share the face height so descriptions can't protrude from the stack.
            if (!spread && index > 0)
            {
                height = visible[0].Card.DesiredSize.Height;
                y = IsTop ? position : finalSize.Height - height - position;
            }
            card.ZIndex = visible.Count - index;
            card.IsHitTestVisible = spread || index == 0;
            card.SetExpandedContent(spread || index == 0);
            card.Arrange(new Rect(0, y, finalSize.Width, height));
            double scale = spread ? 1 : Math.Max(.1, 1 - index * host.StackScaleFactor);
            if (scale != entry.Scale || height != entry.Bounds?.Height || entering)
            {
                var duration = scale == entry.Scale || entering ? TimeSpan.Zero : TimeSpan.FromMilliseconds(350);
                OverlayMotion.Scale(card, scale, new Point(finalSize.Width / 2, IsTop ? 0 : height), duration, Ease);
                entry.Scale = scale;
            }
            var origin = regionOrigin + new Vector(0, y);
            if (entry.Bounds is { Position: var previous } && previous != origin && !AlderMotion.GetReduced(card))
                OverlayMotion.ShiftFromCurrent(card, previous - origin, TimeSpan.FromMilliseconds(350), Ease);
            entry.Bounds = new Rect(origin, new Size(finalSize.Width, height));
            position += host.Gap + (spread ? height : 0);
            if (entering)
            {
                Play(entry, entering: true);
                Resume(entry);
            }
        }
        return finalSize;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateInteraction();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateInteraction();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty) UpdateInteraction();
    }

    private void UpdateInteraction()
    {
        bool interacting = IsPointerOver || IsKeyboardFocusWithin;
        if (paused == interacting) return;
        paused = expanded = interacting;
        Background = expanded ? Brushes.Transparent : null;
        foreach (var entry in visible)
        {
            if (interacting) Pause(entry);
            else Resume(entry);
        }
        InvalidateMeasure();
    }

    private bool Update(Entry entry, ToastOptions options)
    {
        if (entry.Closing || entry.Handle.Completion.IsCompleted) return false;
        Pause(entry);
        entry.Options = options;
        entry.Remaining = options.Duration;
        entry.Card.SetContent(options);
        Resume(entry);
        InvalidateMeasure();
        return true;
    }

    private void Close(Entry entry, ToastCloseReason reason)
    {
        int index = visible.IndexOf(entry);
        if (index < 0) return;
        bool front = index == 0;
        entry.Closing = true;
        visible.RemoveAt(index);
        Pause(entry);
        var card = entry.Card;
        if (entry.Bounds is null || AlderMotion.GetReduced(card))
        {
            Children.Remove(card);
            Finish(entry, reason);
        }
        else
        {
            exiting.Add(entry);
            card.ZIndex = front && reason != ToastCloseReason.Evicted ? int.MaxValue : 0;
            var duration = TimeSpan.FromMilliseconds(front ? 250 : 200);
            Play(entry, entering: false, front, duration);
            entry.ExitTimer = DispatcherTimer.RunOnce(() =>
            {
                if (!exiting.Remove(entry)) return;
                Children.Remove(card);
                Finish(entry, reason);
            }, duration, DispatcherPriority.Render);
            card.StopInteraction();
        }
        InvalidateMeasure();
    }

    private void Play(Entry entry, bool entering, bool front = true, TimeSpan? duration = null)
    {
        var card = entry.Card;
        if (AlderMotion.GetReduced(card)) return;
        var size = card.Bounds.Size;
        var offset = !entering && card.SwipeDirection != 0
            ? new Vector3D(card.SwipeDirection * size.Width, 0, 0)
            : new Vector3D(0, (IsTop ? -1 : 1) * (entering || front ? size.Height : 0), 0);
        OverlayMotion.Animate(card, entering, offset, entering || front ? 1 : entry.Scale * .96,
            duration ?? TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(150),
            new Point(size.Width / 2, IsTop ? 0 : size.Height), Ease);
    }

    private void Resume(Entry entry)
    {
        if (paused || entry.Bounds is null || entry.Closing || entry.Timer is not null || entry.Options.Duration <= TimeSpan.Zero)
            return;
        entry.Started = Stopwatch.GetTimestamp();
        entry.Timer = DispatcherTimer.RunOnce(() => Close(entry, ToastCloseReason.Expired),
            entry.Remaining > TimeSpan.Zero ? entry.Remaining : TimeSpan.FromMilliseconds(1));
    }

    private static void Pause(Entry entry)
    {
        if (entry.Timer is null) return;
        entry.Timer.Dispose();
        entry.Timer = null;
        entry.Remaining -= Stopwatch.GetElapsedTime(entry.Started);
    }

    private static void Finish(Entry entry, ToastCloseReason reason)
    {
        Pause(entry);
        entry.ExitTimer?.Dispose();
        entry.Handle.Complete(reason);
    }

    private sealed class Entry
    {
        public Entry(ToastStack stack, ToastOptions options)
        {
            Options = options;
            Remaining = options.Duration;
            Card = new ToastCard(options, reason => stack.Close(this, reason));
        }

        public ToastOptions Options { get; set; }
        public ToastHandle Handle { get; } = new();
        public ToastCard Card { get; }
        public IDisposable? Timer { get; set; }
        public IDisposable? ExitTimer { get; set; }
        public long Started { get; set; }
        public TimeSpan Remaining { get; set; }
        public Rect? Bounds { get; set; }
        public double Scale { get; set; } = 1;
        public bool Closing { get; set; }
    }
}
