using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderTabs
{
    private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(120);
    private CloseRequest? closeRequest;
    private CloseLayout? closeLayout;
    private CloseReflow? closeReflow;

    private bool TryCloseTab(TabItem tab, NavigationMethod focusMethod)
    {
        if (closeRequest is not null || closeLayout is not null || !tab.IsEffectivelyEnabled ||
            tab is AlderTabItem { CanClose: false }) return false;
        int index = IndexFromContainer(tab);
        if (index < 0 || index >= ItemCount) return false;
        object? item = ItemsView[index];
        if (CloseTabCommand?.CanExecute(item) != true) return false;

        FinishDrag(false);
        var request = new CloseRequest(tab, item, focusMethod);
        closeRequest = request;
        if (AlderMotion.GetReduced(this))
            CommitTabClose(request);
        else
        {
            tab.Classes.Set("alder-tab-closing", true);
            PseudoClasses.Set(":closing-active-tab", tab.IsSelected);
            request.Timer = DispatcherTimer.RunOnce(() => CommitTabClose(request), CloseDuration, DispatcherPriority.Render);
        }
        return true;
    }

    private void CommitTabClose(CloseRequest request)
    {
        if (!ReferenceEquals(closeRequest, request)) return;
        closeRequest = null;
        request.Timer?.Dispose();

        int index = IndexFromContainer(request.Tab);
        bool restoreFocus = request.Tab.IsKeyboardFocusWithin;
        if (index >= 0 && index < ItemCount && Equals(ItemsView[index], request.Item) &&
            request.Tab.IsEffectivelyEnabled && request.Tab is not AlderTabItem { CanClose: false } &&
            CloseTabCommand?.CanExecute(request.Item) == true)
        {
            if (!AlderMotion.GetReduced(this))
            {
                var anchors = new List<CloseAnchor>();
                foreach (var tab in GetRealizedContainers().OfType<TabItem>())
                {
                    int slot = IndexFromContainer(tab);
                    if (slot >= 0 && slot < ItemCount && !ReferenceEquals(tab, request.Tab) &&
                        tab.TranslatePoint(default, this) is { } origin)
                        anchors.Add(new(ItemsView[slot], origin.X + CloseHeaderOffset(tab)));
                }
                closeLayout = new(anchors, request.Tab.IsSelected);
                LayoutUpdated += OnCloseLayoutUpdated;
            }
            EndCloseReflow();
            contentWasClosed = request.Tab.IsSelected && !AlderMotion.GetReduced(this);
            CloseTabCommand.Execute(request.Item);
            if (restoreFocus && SelectedItem is { } selected) RestoreHeaderFocus(selected, request.FocusMethod);
        }

        request.Tab.Classes.Set("alder-tab-closing", false);
        PseudoClasses.Set(":closing-active-tab", false);
        InvalidateArrange();
    }

    private void OnCloseLayoutUpdated(object? sender, EventArgs e)
    {
        if (closeLayout is not { } layout) return;
        ClearCloseLayout();

        var slots = new List<HeaderSlot>();
        foreach (var anchor in layout.Anchors)
        {
            int index = ItemsView.IndexOf(anchor.Item);
            if (ContainerFromIndex(index) is not TabItem tab || tab.TranslatePoint(default, this) is not { } origin) continue;
            double offset = anchor.Left - origin.X;
            if (Math.Abs(offset) < .5) continue;
            var slot = new HeaderSlot(tab, index, origin.X);
            slot.Motion.Jump(offset);
            slot.Motion.MoveTo(0, Stopwatch.GetTimestamp());
            slots.Add(slot);
        }

        var reflow = new CloseReflow(slots, layout.WasSelected);
        closeReflow = reflow;
        RenderCloseReflow(reflow);
        if (layout.WasSelected)
            UpdateMorphingSurface(animate: true);
    }

    private void RenderCloseReflow(CloseReflow reflow)
    {
        long time = Stopwatch.GetTimestamp();
        bool moving = false;
        foreach (var slot in reflow.Slots)
            moving |= slot.Draw(time, AlderMotion.GetReduced(this));

        if (!reflow.WasSelected) UpdateMorphingSurface(animate: false);
        if (moving && TopLevel.GetTopLevel(this) is { } root)
        {
            root.RequestAnimationFrame(reflow.Frame ??= _ =>
            {
                if (ReferenceEquals(closeReflow, reflow)) RenderCloseReflow(reflow);
            });
        }
        else
            closeReflow = null;
    }

    private double CloseTranslationOf(TabItem tab) =>
        closeReflow is { WasSelected: false } ? CloseHeaderOffset(tab) : 0;

    private double CloseHeaderOffset(TabItem tab)
    {
        if (closeReflow is not { } reflow) return 0;
        foreach (var slot in reflow.Slots)
            if (ReferenceEquals(slot.Tab, tab)) return slot.Motion.Offset;
        return 0;
    }

    private void ClearCloseLayout()
    {
        if (closeLayout is null) return;
        LayoutUpdated -= OnCloseLayoutUpdated;
        closeLayout = null;
    }

    private void EndCloseReflow()
    {
        if (closeReflow is not { } reflow) return;
        closeReflow = null;
        foreach (var slot in reflow.Slots) slot.Reset();
    }

    private void CancelMorphingClose()
    {
        if (closeRequest is { } request)
        {
            closeRequest = null;
            request.Timer?.Dispose();
            request.Tab.Classes.Set("alder-tab-closing", false);
            PseudoClasses.Set(":closing-active-tab", false);
        }
        ClearCloseLayout();
        EndCloseReflow();
    }

    private sealed class CloseRequest(TabItem tab, object? item, NavigationMethod focusMethod)
    {
        public TabItem Tab { get; } = tab;
        public object? Item { get; } = item;
        public NavigationMethod FocusMethod { get; } = focusMethod;
        public IDisposable? Timer { get; set; }
    }

    private readonly record struct CloseAnchor(object? Item, double Left);
    private sealed record CloseLayout(List<CloseAnchor> Anchors, bool WasSelected);

    private sealed class CloseReflow(List<HeaderSlot> slots, bool wasSelected)
    {
        public List<HeaderSlot> Slots { get; } = slots;
        public bool WasSelected { get; } = wasSelected;
        public Action<TimeSpan>? Frame { get; set; }
    }
}
