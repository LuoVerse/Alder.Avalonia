using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Describes a tab move using its original and requested indices.</summary>
/// <param name="OldIndex">The original zero-based index of the tab.</param>
/// <param name="NewIndex">The requested zero-based destination index.</param>
public readonly record struct AlderTabReorderRequest(int OldIndex, int NewIndex);

public sealed partial class AlderTabs
{
    /// <summary>Identifies the <see cref="ReorderTabCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> ReorderTabCommandProperty =
        AvaloniaProperty.Register<AlderTabs, ICommand?>(nameof(ReorderTabCommand));

    /// <summary>Identifies the <see cref="CloseTabCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> CloseTabCommandProperty =
        AvaloniaProperty.Register<AlderTabs, ICommand?>(nameof(CloseTabCommand));


    private AlderMorphingTabSurface? morphingSurface;
    private Border? stripFrame;
    private HeaderDrag? headerDrag;
    private HeaderDrag? dropLayout;

    /// <summary>Reorders the source collection using a <see cref="AlderTabReorderRequest"/>.</summary>
    public ICommand? ReorderTabCommand { get => GetValue(ReorderTabCommandProperty); set => SetValue(ReorderTabCommandProperty, value); }

    /// <summary>Closes a tab. The command receives the corresponding item from the source collection.</summary>
    public ICommand? CloseTabCommand { get => GetValue(CloseTabCommandProperty); set => SetValue(CloseTabCommandProperty, value); }

    internal bool UsesMorphing => Variant == AlderTabsVariant.Morphing && TabStripPlacement == Dock.Top;

    private void InitializeMorphingInput()
    {
        AddHandler(PointerPressedEvent, OnHeaderPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnHeaderPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnHeaderPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnHeaderCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble);
        AddHandler(Button.ClickEvent, OnTabCloseClicked);
    }

    private void UpdateMorphingState(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == VariantProperty || change.Property == TabStripPlacementProperty)
        {
            PseudoClasses.Set(":morphing", UsesMorphing);
            if (!UsesMorphing)
            {
                CompleteMorphingContent();
                FinishDrag(false);
                CancelMorphingClose();
                morphingSurface?.Update(0, null, animate: false);
            }
            InvalidateArrange();
        }
        else if (change.Property == CloseTabCommandProperty)
            PseudoClasses.Set(":closeable", CloseTabCommand is not null);
        else if (change.Property == SelectedIndexProperty && closeRequest is { } pending)
            PseudoClasses.Set(":closing-active-tab", IndexFromContainer(pending.Tab) == SelectedIndex);
        else if (change.Property == AlderMotion.ReducedProperty)
        {
            CompleteMorphingContent();
            if (AlderMotion.GetReduced(this))
            {
                if (closeRequest is { } close) CommitTabClose(close);
                ClearCloseLayout();
                EndCloseReflow();
            }
            if (headerDrag is { Active: true } drag) RenderDrag(drag);
            else UpdateMorphingSurface(animate: false);
        }
    }

    private void UpdateMorphingSurface(bool animate)
    {
        if (dropLayout is not null || closeLayout is not null || !UsesMorphing || morphingSurface is not { } surface || stripFrame is not { } frame) return;
        if (frame.TranslatePoint(default, surface) is not { } frameOrigin) return;

        var activeTab = ContainerFromIndex(SelectedIndex) as TabItem;
        Rect? header = null;
        if (activeTab is { IsVisible: true } && activeTab.TranslatePoint(default, surface) is { } position)
        {
            double displacement = headerDrag is { Active: true } drag
                ? drag.TranslationOf(activeTab)
                : CloseTranslationOf(activeTab);
            var visible = new Rect(position + new Vector(displacement, 0), activeTab.Bounds.Size);
            if (scroll?.TranslatePoint(default, surface) is { } viewport)
                visible = visible.Intersect(new Rect(viewport, scroll.Bounds.Size));
            if (visible.Width > 0) header = visible;
        }

        surface.Update(frameOrigin.Y + frame.Bounds.Height, header,
            animate && headerDrag is not { Active: true } && closeReflow is not { WasSelected: false } &&
            !AlderMotion.GetReduced(this));
    }

    private TabItem? HeaderFromSource(object? source)
    {
        if (source is not Visual visual) return null;
        var tab = visual as TabItem ?? visual.FindAncestorOfType<TabItem>();
        return tab is not null && ItemsControlFromItemContainer(tab) == this ? tab : null;
    }

    private TabItem? HeaderFromPointerSource(object? source) =>
        source is Visual visual && visual is not Button && visual.FindAncestorOfType<Button>() is null
            ? HeaderFromSource(visual)
            : null;

    /// <inheritdoc />
    protected override bool ShouldTriggerSelection(Visual selectable, PointerEventArgs eventArgs) =>
        !(headerDrag is { } drag && ReferenceEquals(drag.Tab, selectable) && eventArgs is PointerPressedEventArgs) &&
        base.ShouldTriggerSelection(selectable, eventArgs);

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!UsesMorphing || ReorderTabCommand is null || headerDrag is { Active: true } || dropLayout is not null ||
            closeRequest is not null || closeLayout is not null || closeReflow is not null || ItemCount < 2 ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            HeaderFromPointerSource(e.Source) is not { IsEffectivelyEnabled: true } tab)
            return;

        int index = IndexFromContainer(tab);
        if (index < 0 || tab.TranslatePoint(default, this) is not { } tabOrigin) return;
        headerDrag = new HeaderDrag(e.Pointer, tab, index, e.GetPosition(this).X, tabOrigin.X);
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (headerDrag is not { Settling: false } drag || !ReferenceEquals(e.Pointer, drag.Pointer)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SettleDrag(commit: false);
            return;
        }

        double delta = e.GetPosition(this).X - drag.PointerStart;
        if (!drag.Active)
        {
            if (Math.Abs(delta) < 5) return;
            StartDrag(drag);
        }

        delta = Math.Clamp(delta, drag.MinimumOffset, drag.MaximumOffset);
        drag.Moving.Motion.Jump(delta);
        int targetIndex = drag.OldIndex;
        double left = drag.Moving.Left + delta;
        foreach (var slot in drag.Neighbors)
        {
            // Swap at the neighboring half-slot. Going forward the moving center
            // crosses its leading edge. Going backward its own leading edge crosses the center.
            bool passed = delta > 0
                ? slot.Left > drag.Moving.Left && left + drag.Moving.Width / 2 >= slot.Left - .5
                : slot.Left < drag.Moving.Left && left <= slot.Left + slot.Width / 2 + .5;
            if (passed && Math.Abs(slot.Index - drag.OldIndex) > Math.Abs(targetIndex - drag.OldIndex))
                targetIndex = slot.Index;
        }

        if (targetIndex != drag.TargetIndex)
        {
            drag.TargetIndex = targetIndex;
            UpdateSlotTargets(drag);
        }
        RenderDrag(drag);
        e.Handled = true;
    }

    private void StartDrag(HeaderDrag drag)
    {
        EndCloseReflow();
        drag.Neighbors = GetRealizedContainers().OfType<TabItem>()
            .Where(tab => tab.IsVisible && !ReferenceEquals(tab, drag.Tab))
            .Select(tab => new HeaderSlot(tab, IndexFromContainer(tab),
                tab.TranslatePoint(default, this)?.X ?? drag.Moving.Left))
            .OrderBy(slot => slot.Left)
            .ToArray();

        double viewportLeft = scroll?.TranslatePoint(default, this)?.X ?? 0;
        double viewportRight = viewportLeft + (scroll?.Bounds.Width ?? Bounds.Width);
        double firstLeft = drag.Neighbors.Length == 0 ? drag.Moving.Left : Math.Min(drag.Moving.Left, drag.Neighbors[0].Left);
        double lastRight = drag.Neighbors.Length == 0 ? drag.Moving.Right : Math.Max(drag.Moving.Right, drag.Neighbors[^1].Right);
        drag.MinimumOffset = Math.Min(0, Math.Max(viewportLeft, firstLeft) - drag.Moving.Left);
        drag.MaximumOffset = Math.Max(0, Math.Min(viewportRight, lastRight) - drag.Moving.Right);
        drag.Active = true;
        drag.Tab.SetCurrentValue(ZIndexProperty, drag.OriginalZIndex + 1);
        drag.Tab.Classes.Set("alder-tab-dragging", true);
        drag.Pointer.Capture(this);
        drag.Tab.Focus(NavigationMethod.Pointer);
    }

    private static void UpdateSlotTargets(HeaderDrag drag)
    {
        var destination = drag.Neighbors.FirstOrDefault(slot => slot.Index == drag.TargetIndex);
        int direction = destination is null ? 0 : Math.Sign(destination.Left - drag.Moving.Left);
        double gap = GetGap(drag, direction);
        double offset = -(drag.Moving.Width + gap) * direction;
        long time = Stopwatch.GetTimestamp();
        foreach (var slot in drag.Neighbors)
        {
            bool displaced = drag.OldIndex < drag.TargetIndex
                ? slot.Index > drag.OldIndex && slot.Index <= drag.TargetIndex
                : slot.Index >= drag.TargetIndex && slot.Index < drag.OldIndex;
            slot.Motion.MoveTo(displaced ? offset : 0, time);
        }

        drag.Destination = direction switch
        {
            > 0 => destination!.Right - drag.Moving.Right,
            < 0 => destination!.Left - drag.Moving.Left,
            _ => 0
        };
    }

    private static double GetGap(HeaderDrag drag, int direction)
    {
        if (direction == 0) return 0;
        double gap = double.PositiveInfinity;
        foreach (var slot in drag.Neighbors)
        {
            if (direction > 0 && slot.Left > drag.Moving.Left)
                gap = Math.Min(gap, slot.Left - drag.Moving.Right);
            else if (direction < 0 && slot.Left < drag.Moving.Left)
                gap = Math.Min(gap, drag.Moving.Left - slot.Right);
        }
        return double.IsFinite(gap) ? Math.Max(0, gap) : 0;
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (headerDrag is not { Settling: false } drag || !ReferenceEquals(e.Pointer, drag.Pointer) ||
            e.InitialPressMouseButton != MouseButton.Left) return;
        if (drag.Active)
        {
            e.Handled = true;
            SettleDrag(commit: true);
            return;
        }

        headerDrag = null;
        if (!drag.Tab.IsEffectivelyEnabled || !ReferenceEquals(HeaderFromPointerSource(e.Source), drag.Tab) ||
            !new Rect(drag.Tab.Bounds.Size).Contains(e.GetPosition(drag.Tab))) return;
        int index = IndexFromContainer(drag.Tab);
        if (index < 0) return;

        drag.Tab.Focus(NavigationMethod.Pointer, e.KeyModifiers);
        SetCurrentValue(SelectedIndexProperty, index);
        e.Handled = true;
    }

    private void OnHeaderCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (headerDrag is { Active: true, Settling: false } drag && ReferenceEquals(drag.Pointer, e.Pointer) &&
            drag.Pointer.Captured != this)
            SettleDrag(commit: false);
    }

    private void SettleDrag(bool commit)
    {
        if (headerDrag is not { } drag) return;
        if (!drag.Active)
        {
            headerDrag = null;
            return;
        }

        drag.Settling = true;
        drag.Commit = commit;
        if (!commit)
        {
            drag.TargetIndex = drag.OldIndex;
            UpdateSlotTargets(drag);
        }
        if (drag.Pointer.Captured == this) drag.Pointer.Capture(null);
        drag.Moving.Motion.MoveTo(drag.Destination, Stopwatch.GetTimestamp());
        RenderDrag(drag);
    }

    private void RenderDrag(HeaderDrag drag)
    {
        bool reduce = AlderMotion.GetReduced(this);
        long time = Stopwatch.GetTimestamp();
        bool moving = drag.Moving.Draw(time, reduce);
        foreach (var slot in drag.Neighbors)
            moving |= slot.Draw(time, reduce);

        // The active tab may also be one of the displaced neighbors.
        UpdateMorphingSurface(animate: false);
        if (!moving)
        {
            if (drag.Settling) FinishDrag(drag.Commit);
            return;
        }

        if (!drag.FramePending && TopLevel.GetTopLevel(this) is { } root)
        {
            drag.FramePending = true;
            root.RequestAnimationFrame(drag.Frame ??= _ =>
            {
                drag.FramePending = false;
                if (ReferenceEquals(headerDrag, drag)) RenderDrag(drag);
            });
        }
    }

    private void FinishDrag(bool commit)
    {
        if (!commit) CompleteDropLayout();
        if (headerDrag is not { } drag) return;
        headerDrag = null;
        if (!drag.Active) return;

        if (drag.Pointer.Captured == this) drag.Pointer.Capture(null);
        if (commit && drag.OldIndex != drag.TargetIndex && IndexFromContainer(drag.Tab) == drag.OldIndex)
        {
            // Hold the last drawn contour while the collection notifies the containers.
            // Drop the offsets only after layout has arranged the reordered headers.
            dropLayout = drag;
            LayoutUpdated += OnDropLayoutUpdated;
            if (!TryReorder(drag.OldIndex, drag.TargetIndex, NavigationMethod.Pointer)) CompleteDropLayout();
        }
        else
            ResetHeaderOffsets(drag);
        InvalidateArrange();
    }

    private void OnDropLayoutUpdated(object? sender, EventArgs e)
    {
        CompleteDropLayout();
        UpdateMorphingSurface(animate: false);
    }

    private void CompleteDropLayout()
    {
        if (dropLayout is not { } drag) return;
        LayoutUpdated -= OnDropLayoutUpdated;
        dropLayout = null;
        ResetHeaderOffsets(drag);
    }

    private static void ResetHeaderOffsets(HeaderDrag drag)
    {
        foreach (var slot in drag.Neighbors) slot.Reset();
        drag.Moving.Reset();
        drag.Tab.SetCurrentValue(ZIndexProperty, drag.OriginalZIndex);
        drag.Tab.Classes.Set("alder-tab-dragging", false);
    }

    private bool TryReorder(int oldIndex, int newIndex, NavigationMethod focusMethod)
    {
        if (oldIndex < 0 || oldIndex == newIndex || newIndex < 0 || newIndex >= ItemCount) return false;
        object request = new AlderTabReorderRequest(oldIndex, newIndex);
        if (ReorderTabCommand?.CanExecute(request) != true) return false;
        var selected = SelectedItem;
        var moving = ItemsView[oldIndex];
        bool restoreFocus = ContainerFromIndex(oldIndex)?.IsKeyboardFocusWithin == true;
        ReorderTabCommand.Execute(request);
        if (selected is not null && !Equals(SelectedItem, selected) && ItemsView.IndexOf(selected) >= 0)
            SetCurrentValue(SelectedItemProperty, selected);
        if (restoreFocus) RestoreHeaderFocus(moving, focusMethod);
        return true;
    }

    private void OnTabCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (!UsesMorphing || e.Source is not Button { Name: "PART_TabClose" } button ||
            HeaderFromSource(button) is not { } tab) return;
        var focusMethod = button.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Pointer;
        e.Handled = TryCloseTab(tab, focusMethod);
    }

    private void RestoreHeaderFocus(object? item, NavigationMethod focusMethod)
    {
        if (item is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!this.IsAttachedToVisualTree()) return;
            var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focus is not null && !ReferenceEquals(focus, this) && HeaderFromSource(focus) is null) return;
            if (ContainerFromIndex(ItemsView.IndexOf(item)) is TabItem tab) tab.Focus(focusMethod);
        }, DispatcherPriority.Loaded);
    }

    private bool HandleMorphingKey(KeyEventArgs e)
    {
        if (!UsesMorphing || e.Handled) return false;
        if (e.Key == Key.Escape && headerDrag is not null)
        {
            SettleDrag(commit: false);
            return e.Handled = true;
        }
        if (HeaderFromSource(e.Source) is not { IsEffectivelyEnabled: true } tab) return false;
        if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)
            return e.Handled = TryCloseTab(tab, NavigationMethod.Tab);
        if (e.KeyModifiers != KeyModifiers.Alt || e.Key is not (Key.Left or Key.Right)) return false;
        int step = (e.Key == Key.Right) == (FlowDirection == FlowDirection.LeftToRight) ? 1 : -1;
        int index = IndexFromContainer(tab);
        return e.Handled = TryReorder(index, index + step, NavigationMethod.Tab);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CompleteMorphingContent();
        FinishDrag(false);
        CancelMorphingClose();
        base.OnDetachedFromVisualTree(e);
    }

    private sealed class HeaderSlot(TabItem tab, int index, double left)
    {
        private readonly CompositionVisual? visual = ElementComposition.GetElementVisual(tab);
        private double drawnOffset;

        public TabItem Tab { get; } = tab;
        public int Index { get; } = index;
        public double Left { get; } = left;
        public double Width { get; } = tab.Bounds.Width;
        public double Right => Left + Width;
        public HeaderSpring Motion { get; } = new();

        public bool Draw(long time, bool reduce)
        {
            if (reduce) Motion.Jump(Motion.Target);
            else Motion.Advance(time);
            SetOffset(Motion.Offset);
            return Motion.IsMoving;
        }

        public void Reset() => SetOffset(0);

        private void SetOffset(double offset)
        {
            if (visual is null || drawnOffset == offset) return;
            visual.Translation = new Vector3D(offset, 0, 0);
            drawnOffset = offset;
        }
    }

    private sealed class HeaderDrag(IPointer pointer, TabItem tab, int index, double pointerStart, double left)
    {
        public IPointer Pointer { get; } = pointer;
        public TabItem Tab { get; } = tab;
        public HeaderSlot Moving { get; } = new(tab, index, left);
        public int OldIndex { get; } = index;
        public int OriginalZIndex { get; } = tab.ZIndex;
        public double PointerStart { get; } = pointerStart;
        public HeaderSlot[] Neighbors { get; set; } = [];
        public int TargetIndex { get; set; } = index;
        public double Destination { get; set; }
        public double MinimumOffset { get; set; }
        public double MaximumOffset { get; set; }
        public bool Active { get; set; }
        public bool Settling { get; set; }
        public bool Commit { get; set; }
        public bool FramePending { get; set; }
        public Action<TimeSpan>? Frame { get; set; }

        public double TranslationOf(TabItem item)
        {
            if (ReferenceEquals(Tab, item)) return Moving.Motion.Offset;
            foreach (var slot in Neighbors)
                if (ReferenceEquals(slot.Tab, item)) return slot.Motion.Offset;
            return 0;
        }
    }

    internal sealed class HeaderSpring(double slowRate = -16.834, double fastRate = -83.166, double maxSeconds = .5)
    {
        // Roots of the damped oscillator. Keeping velocity lets a neighbor
        // reverse smoothly when the pointer moves back across a threshold.
        private long started;
        private double amplitudeSlow;
        private double amplitudeFast;
        private double velocity;

        public double Offset { get; private set; }
        public double Target { get; private set; }
        public bool IsMoving { get; private set; }

        public void Jump(double offset)
        {
            Target = Offset = offset;
            velocity = 0;
            IsMoving = false;
        }

        public void MoveTo(double target, long time)
        {
            if (Target == target) return;
            Advance(time);
            Target = target;
            double displacement = Offset - target;
            amplitudeSlow = (velocity - fastRate * displacement) / (slowRate - fastRate);
            amplitudeFast = displacement - amplitudeSlow;
            started = time;
            IsMoving = true;
        }

        public void Advance(long time)
        {
            if (!IsMoving) return;
            double elapsed = Stopwatch.GetElapsedTime(started, time).TotalSeconds;
            double slow = amplitudeSlow * Math.Exp(slowRate * elapsed);
            double fast = amplitudeFast * Math.Exp(fastRate * elapsed);
            Offset = Target + slow + fast;
            velocity = slowRate * slow + fastRate * fast;
            if (elapsed >= maxSeconds || Math.Abs(Offset - Target) < .35 && Math.Abs(velocity) < 8)
                Jump(Target);
        }
    }
}
