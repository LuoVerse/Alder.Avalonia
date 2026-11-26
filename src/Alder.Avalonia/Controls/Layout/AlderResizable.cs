using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>Arranges resizable content panes and their handles along a horizontal or vertical axis.</summary>
public sealed partial class AlderResizable : Panel
{
    private const double SeparatorLength = 1;
    private const double Epsilon = 0.0001;

    /// <summary>The direction in which panes are laid out and resized.</summary>
    [GeneratedStyledProperty]
    public partial Orientation Orientation { get; set; }

    /// <summary>Raised when the arranged panel sizes or collapse states change.</summary>
    public event EventHandler? LayoutChanged;

    private Track[] arrangedTracks = [];
    private double arrangedSpace;

    static AlderResizable() => AffectsMeasure<AlderResizable>(OrientationProperty);

    internal double Primary(Size size) => Orientation == Orientation.Horizontal ? size.Width : size.Height;
    internal double Primary(Point point) => Orientation == Orientation.Horizontal ? point.X : point.Y;
    private double Cross(Size size) => Orientation == Orientation.Horizontal ? size.Height : size.Width;
    private Size Axes(double length, double cross) => Orientation == Orientation.Horizontal ? new(length, cross) : new(cross, length);
    private Rect Slot(double start, double length, double cross) => Orientation == Orientation.Horizontal
        ? new Rect(start, 0, length, cross) : new Rect(0, start, cross, length);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var tracks = CollectTracks();
        double separators = SeparatorSpace(tracks);
        double length = Primary(availableSize);
        double cross = Cross(availableSize);
        if (double.IsPositiveInfinity(length))
        {
            length = separators;
            foreach (var track in tracks)
            {
                track.Panel.Measure(availableSize);
                var panel = track.Panel;
                var request = panel.Collapsed ? panel.CollapsedSize : panel.RequestedSize;
                var contentSize = request.IsAbsolute || panel.Collapsed
                    ? Resolve(request, 0) : Primary(panel.DesiredSize);
                length += Math.Max(panel.Collapsed ? 0 : Resolve(panel.MinSize, 0), contentSize);
            }
        }

        Fit(tracks, Math.Max(0, length - separators), rememberInitialSpace: false);
        double desiredCross = 0;
        foreach (var track in tracks)
        {
            track.Panel.Measure(Axes(track.Size, cross));
            desiredCross = Math.Max(desiredCross, Cross(track.Panel.DesiredSize));
            if (track.TrailingHandle is not { } handle) continue;
            handle.SetOrientation(Orientation);
            handle.Measure(Axes(handle.HitWidth, cross));
            desiredCross = Math.Max(desiredCross, Cross(handle.DesiredSize));
        }
        return Axes(length, desiredCross);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var tracks = CollectTracks();
        double length = Primary(finalSize);
        double cross = Cross(finalSize);
        double separators = SeparatorSpace(tracks);
        var line = separators > 0 ? Math.Min(SeparatorLength, length / separators) : 0;
        var space = Math.Max(0, length - separators);
        Fit(tracks, space, rememberInitialSpace: true);
        var scale = LayoutHelper.GetLayoutScale(this);

        double position = 0;
        foreach (var track in tracks)
        {
            var start = Snap(position);
            position += track.Size;
            var end = Snap(position);
            track.Panel.Arrange(Slot(start, end - start, cross));
            if (track.TrailingHandle is not { } handle) continue;
            double hitWidth = Math.Min(length, handle.HitWidth);
            handle.Arrange(Slot(end + (line - hitWidth) / 2, hitWidth, cross));
            position += line;
        }

        foreach (var handle in Children.OfType<AlderResizableHandle>())
            if (!tracks.Any(track => track.TrailingHandle == handle)) handle.Arrange(default);

        bool changed = arrangedTracks.Length != tracks.Length || tracks.Where((track, i) =>
            arrangedTracks[i].Panel != track.Panel || Math.Abs(arrangedTracks[i].Size - track.Size) > Epsilon ||
            arrangedTracks[i].Collapsed != track.Collapsed).Any();
        arrangedTracks = tracks;
        arrangedSpace = space;
        if (changed) LayoutChanged?.Invoke(this, EventArgs.Empty);
        return finalSize;

        // Snap cumulative edges with a consistent tie-break so a one-DIP slot doesn't turn into a two-DIP gap.
        double Snap(double value) => UseLayoutRounding ? Math.Round(value * scale, MidpointRounding.AwayFromZero) / scale : value;
    }

    private Track[] CollectTracks()
    {
        var tracks = new List<Track>();
        AlderResizableHandle? handle = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (child is AlderResizablePanel panel)
            {
                if (tracks.Count > 0) tracks[^1].TrailingHandle = handle;
                panel.SetGroup(this);
                tracks.Add(new Track(panel));
                handle = null;
            }
            else if (child is AlderResizableHandle separator) handle = separator;
        }
        return tracks.ToArray();
    }

    private static double SeparatorSpace(Track[] tracks) => tracks.Count(track => track.TrailingHandle is not null) * SeparatorLength;

    private static void Fit(Track[] tracks, double space, bool rememberInitialSpace)
    {
        double requested = 0;
        int autoCount = 0;
        foreach (var track in tracks)
        {
            var panel = track.Panel;
            if (rememberInitialSpace && panel.InitialSpace == 0) panel.InitialSpace = space;
            track.Collapsed = panel.Collapsed;
            (track.Min, track.Max) = GetLimits(panel, space, track.Collapsed);
            var preferred = panel.RequestedSize;
            track.IsAuto = !track.Collapsed && preferred.IsAuto;
            if (track.IsAuto) autoCount++;
            else
            {
                double initial = panel.InitialSpace > 0 ? panel.InitialSpace : space;
                double size = Resolve(preferred, space);
                if (panel.Size is null && initial > 0)
                {
                    if (preferred.IsStar && panel.KeepsPixels) size = Resolve(preferred, initial);
                    if (preferred.IsAbsolute && !panel.KeepsPixels) size = preferred.Value / initial * space;
                }
                track.Size = Math.Clamp(size, track.Min, track.Max);
                requested += track.Size;
            }
        }

        var autoSize = autoCount == 0 ? 0 : Math.Max(0, space - requested) / autoCount;
        foreach (var track in tracks)
            if (track.IsAuto) track.Size = Math.Clamp(autoSize, track.Min, track.Max);

        double minimum = tracks.Sum(track => track.Min);
        if (minimum > space)
        {
            foreach (var track in tracks) track.Size = track.Min / minimum * space;
            return;
        }

        double remaining = space - tracks.Sum(track => track.Size);
        Distribute(tracks.Where(track => track.IsAuto), ref remaining);
        Distribute(tracks.Where(track => !track.Collapsed && !track.Panel.KeepsPixels), ref remaining);
        Distribute(tracks.Where(track => !track.Collapsed), ref remaining);
    }

    private static void Distribute(IEnumerable<Track> tracks, ref double remaining)
    {
        if (Math.Abs(remaining) <= Epsilon) return;
        bool grow = remaining > 0;
        var active = tracks.Where(track => HasCapacity(track)).ToList();
        while (active.Count > 0 && Math.Abs(remaining) > Epsilon)
        {
            double share = remaining / active.Count;
            foreach (var track in active)
            {
                var size = Math.Clamp(track.Size + share, track.Min, track.Max);
                remaining -= size - track.Size;
                track.Size = size;
            }
            active.RemoveAll(track => !HasCapacity(track));
        }

        bool HasCapacity(Track track) => grow ? track.Max - track.Size > Epsilon : track.Size - track.Min > Epsilon;
    }

    private static double Resolve(GridLength size, double space, double auto = 0) =>
        size.IsAuto ? auto : size.IsStar ? size.Value / 100 * space : size.Value;

    private static (double Min, double Max) GetLimits(AlderResizablePanel panel, double space, bool collapsed)
    {
        double min = Resolve(collapsed ? panel.CollapsedSize : panel.MinSize, space);
        double max = collapsed ? min : Math.Max(min, Resolve(panel.MaxSize, space, space));
        return (min, max);
    }

    internal ResizeSession? BeginResize(AlderResizableHandle handle)
    {
        var index = Array.FindIndex(arrangedTracks, track => track.TrailingHandle == handle);
        if (index < 0 || arrangedSpace <= Epsilon) return null;
        return new ResizeSession(this, index);
    }

    internal bool TogglePane(AlderResizableHandle handle)
    {
        var index = Array.FindIndex(arrangedTracks, track => track.TrailingHandle == handle);
        if (index < 0) return false;
        var before = arrangedTracks[index].Panel;
        var after = arrangedTracks[index + 1].Panel;
        var pane = before.IsCollapsible ? before : after;
        if (!pane.IsCollapsible) return false;
        if (pane.IsCollapsed) pane.Expand();
        else pane.Collapse();
        return true;
    }

    internal sealed class ResizeSession
    {
        private readonly AlderResizable group;
        private readonly Track[] origin;
        private readonly int boundary;
        private readonly double space;
        private readonly Orientation orientation;
        private double lastDelta;

        internal ResizeSession(AlderResizable group, int boundary)
        {
            this.group = group;
            this.boundary = boundary;
            space = group.arrangedSpace;
            orientation = group.Orientation;
            origin = group.arrangedTracks;
        }

        internal double KeyboardStep => space / 20;
        internal double Value => Math.Clamp(origin.Take(boundary + 1).Sum(track => track.Size) / space * 100, 0, 100);
        internal double Minimum => Math.Clamp(RangeBoundary(minimum: true), 0, Value);
        internal double Maximum => Math.Clamp(RangeBoundary(minimum: false), Value, 100);

        internal void ResizeTo(double percentage) => Resize((percentage - Value) / 100 * space);

        private double RangeBoundary(bool minimum)
        {
            double left = 0, right = 0;
            for (int i = 0; i < origin.Length; i++)
            {
                var pane = origin[i].Panel;
                var (min, max) = GetLimits(pane, space, collapsed: false);
                if (pane.IsCollapsible)
                {
                    var collapsed = Resolve(pane.CollapsedSize, space);
                    min = Math.Min(min, collapsed);
                    max = Math.Max(max, collapsed);
                }
                if (i <= boundary) left += minimum ? min : max;
                else right += minimum ? max : min;
            }
            return (minimum ? Math.Max(left, space - right) : Math.Min(left, space - right)) / space * 100;
        }

        internal void Resize(double delta)
        {
            if (Math.Abs(delta - lastDelta) <= Epsilon || orientation != group.Orientation ||
                Math.Abs(space - group.arrangedSpace) > Epsilon ||
                !origin.Select(track => track.Panel).SequenceEqual(group.arrangedTracks.Select(track => track.Panel))) return;

            lastDelta = delta;
            var targets = origin.Select(track => (track.Size, track.Collapsed)).ToArray();
            int left = boundary, right = boundary + 1;
            double remaining = delta;
            while (left >= 0 && right < targets.Length && Math.Sign(remaining) == Math.Sign(delta) && Math.Abs(remaining) > Epsilon)
            {
                double previous = targets[left].Size;
                var (leftSize, leftCollapsed, rightCollapsed) = FindNearestPair(
                    origin[left].Panel, origin[right].Panel, targets[left], targets[right], remaining, space);
                double used = leftSize - previous;
                targets[left] = (leftSize, leftCollapsed);
                targets[right] = (Math.Max(0, targets[right].Size - used), rightCollapsed);
                remaining -= used;

                bool growLeft = remaining > 0;
                bool leftHasRoom = HasRoom(origin[left].Panel, targets[left], space, growLeft);
                bool rightHasRoom = HasRoom(origin[right].Panel, targets[right], space, !growLeft);
                if (leftHasRoom && rightHasRoom) break;
                if (!leftHasRoom) left--;
                if (!rightHasRoom) right++;
            }

            for (int i = 0; i < origin.Length; i++)
            {
                var pane = origin[i].Panel;
                var (size, collapsed) = targets[i];
                if (!collapsed)
                {
                    var request = pane.KeepsPixels ? new GridLength(size) : new GridLength(size / space * 100, GridUnitType.Star);
                    pane.SetCurrentValue(AlderResizablePanel.SizeProperty, request);
                }
                if (collapsed != pane.Collapsed) pane.SetCurrentValue(AlderResizablePanel.IsCollapsedProperty, collapsed);
            }
        }

        private static bool HasRoom(AlderResizablePanel pane, (double Size, bool Collapsed) track, double space, bool grow)
        {
            var (min, max) = GetLimits(pane, space, collapsed: false);
            if (pane.IsCollapsible) min = Math.Min(min, Resolve(pane.CollapsedSize, space));
            return grow ? track.Size < max - Epsilon : track.Size > min + Epsilon;
        }

        private static (double Size, bool LeftCollapsed, bool RightCollapsed) FindNearestPair(
            AlderResizablePanel left, AlderResizablePanel right, (double Size, bool Collapsed) before,
            (double Size, bool Collapsed) after, double delta, double space)
        {
            double total = before.Size + after.Size;
            double target = before.Size + delta;
            double bestDistance = Math.Abs(delta);
            var best = (before.Size, before.Collapsed, after.Collapsed);
            // A pane can occupy its expanded interval or the single collapsed size.
            // Pick the nearest feasible divider position across both states.
            for (int a = 0; a <= (left.IsCollapsible ? 1 : 0); a++)
            for (int b = 0; b <= (right.IsCollapsible ? 1 : 0); b++)
            {
                var (lo, hi) = GetLimits(left, space, a != 0);
                var (rightLo, rightHi) = GetLimits(right, space, b != 0);
                lo = Math.Max(lo, total - rightHi);
                hi = Math.Min(hi, total - rightLo);
                if (lo > hi + Epsilon) continue;
                double size = Math.Clamp(target, Math.Max(0, lo), Math.Max(lo, hi));
                var distance = Math.Abs(target - size);
                if (distance >= bestDistance - Epsilon) continue;
                bestDistance = distance;
                best = (size, a != 0, b != 0);
            }
            return best;
        }
    }

    private sealed class Track(AlderResizablePanel panel)
    {
        internal AlderResizablePanel Panel { get; } = panel;
        internal AlderResizableHandle? TrailingHandle { get; set; }
        internal bool IsAuto { get; set; }
        internal bool Collapsed { get; set; }
        internal double Size { get; set; }
        internal double Min { get; set; }
        internal double Max { get; set; }
    }
}
