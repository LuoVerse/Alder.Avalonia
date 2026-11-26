using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual treatment and indicator behavior of a tab strip.</summary>
public enum AlderTabsVariant
{
    /// <summary>Uses the primary treatment.</summary>
    Primary,
    /// <summary>Uses the secondary treatment.</summary>
    Secondary,
    /// <summary>Animates a shared surface between selected tab headers.</summary>
    Morphing
}
/// <summary>Aligns tabs within the available space on their strip.</summary>
public enum AlderTabAlignment
{
    /// <summary>Aligns headers with the logical start of the strip.</summary>
    Start,
    /// <summary>Centers the headers in the strip.</summary>
    Center,
    /// <summary>Aligns headers with the logical end of the strip.</summary>
    End
}

/// <summary>A TabControl with a scrollable tab strip and a shared selection indicator.</summary>
public sealed partial class AlderTabs : TabControl
{
    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderTabItem();

    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderTabsVariant> VariantProperty =
        AvaloniaProperty.Register<AlderTabs, AlderTabsVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="ShowSeparators"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSeparatorsProperty =
        AvaloniaProperty.Register<AlderTabs, bool>(nameof(ShowSeparators));
    /// <summary>Identifies the <see cref="GetShowSeparator"/> attached property.</summary>
    public static readonly AttachedProperty<bool?> ShowSeparatorProperty =
        AvaloniaProperty.RegisterAttached<AlderTabs, TabItem, bool?>("ShowSeparator");
    /// <summary>Identifies the <see cref="HeaderAlignment"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderTabAlignment> HeaderAlignmentProperty =
        AvaloniaProperty.Register<AlderTabs, AlderTabAlignment>(nameof(HeaderAlignment), AlderTabAlignment.Center);
    /// <summary>Identifies the <see cref="FillTabStrip"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> FillTabStripProperty =
        AvaloniaProperty.Register<AlderTabs, bool>(nameof(FillTabStrip), true);
    /// <summary>Identifies the <see cref="IndicatorBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<AlderTabs, IBrush?>(nameof(IndicatorBrush));

    private ScrollViewer? scroll;
    private AlderTabStrip? strip;
    private Button? previous;
    private Button? next;
    private bool revealSelection = true;
    private LinearGradientBrush? edgeMask;

    /// <summary>The visual treatment applied to the control.</summary>
    public AlderTabsVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>Whether to show the separators.</summary>
    public bool ShowSeparators { get => GetValue(ShowSeparatorsProperty); set => SetValue(ShowSeparatorsProperty, value); }
    /// <summary>The placement of headers within the tab strip.</summary>
    public AlderTabAlignment HeaderAlignment { get => GetValue(HeaderAlignmentProperty); set => SetValue(HeaderAlignmentProperty, value); }
    /// <summary>Whether the headers share the available tab strip width.</summary>
    public bool FillTabStrip { get => GetValue(FillTabStripProperty); set => SetValue(FillTabStripProperty, value); }
    /// <summary>An optional brush for the active tab indicator.</summary>
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }

    /// <summary>Gets the per-tab separator override. Null uses the tab strip's setting.</summary>
    public static bool? GetShowSeparator(TabItem item) => item.GetValue(ShowSeparatorProperty);
    /// <summary>Sets the per-tab separator override. Null uses the tab strip's setting.</summary>
    public static void SetShowSeparator(TabItem item, bool? value) => item.SetValue(ShowSeparatorProperty, value);

    static AlderTabs()
    {
        ShowSeparatorProperty.Changed.AddClassHandler<TabItem>((tab, _) =>
        {
            if (ItemsControlFromItemContainer(tab) is AlderTabs owner) owner.RefreshSeparators();
        });
    }

    /// <summary>Initializes a new instance of <see cref="AlderTabs"/>.</summary>
    public AlderTabs() => InitializeMorphingInput();

    internal bool IsVertical => TabStripPlacement is Dock.Left or Dock.Right;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CompleteMorphingContent();
        FinishDrag(false);
        CancelMorphingClose();
        if (scroll is not null) scroll.ScrollChanged -= OnScrollChanged;
        if (previous is not null) previous.Click -= ScrollPrevious;
        if (next is not null) next.Click -= ScrollNext;
        base.OnApplyTemplate(e);
        scroll = e.NameScope.Find<ScrollViewer>("PART_TabScroller");
        strip = e.NameScope.Find<AlderTabStrip>("PART_TabStrip");
        previous = e.NameScope.Find<Button>("PART_ScrollPrevious");
        next = e.NameScope.Find<Button>("PART_ScrollNext");
        morphingSurface = e.NameScope.Find<AlderMorphingTabSurface>("PART_MorphingSurface");
        stripFrame = e.NameScope.Find<Border>("StripFrame");
        edgeMask = null;
        if (scroll is not null) scroll.ScrollChanged += OnScrollChanged;
        if (previous is not null) previous.Click += ScrollPrevious;
        if (next is not null) next.Click += ScrollNext;
        revealSelection = true;
        RefreshSeparators();
        UpdateOverflow();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedItemProperty)
            PrepareMorphingContentChange();
        base.OnPropertyChanged(change);
        UpdateMorphingState(change);
        if (change.Property == SelectedIndexProperty)
        {
            revealSelection = true;
            strip?.InvalidateArrange();
            InvalidateArrange();
            RefreshSeparators();
        }
        else if (change.Property == TabStripPlacementProperty)
        {
            PseudoClasses.Set(":vertical", IsVertical);
            revealSelection = true;
            InvalidateArrange();
            UpdateOverflow();
        }
        else if (change.Property == VariantProperty || change.Property == ShowSeparatorsProperty)
        {
            RefreshSeparators();
            strip?.InvalidateArrange();
        }
        else if (change.Property == AlderMotion.ReducedProperty)
            strip?.InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        if (headerDrag is { Active: true }) FinishDrag(false);
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateSeparator((TabItem)container, index);
    }

    /// <inheritdoc />
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        if (headerDrag is { Active: true }) FinishDrag(false);
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        UpdateSeparator((TabItem)container, newIndex);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        if (headerDrag is { Active: true }) FinishDrag(false);
        container.Classes.Set("alder-tab-separator", false);
        base.ClearContainerForItemOverride(container);
    }

    private void RefreshSeparators()
    {
        foreach (var container in GetRealizedContainers())
            if (container is TabItem tab) UpdateSeparator(tab, IndexFromContainer(tab));
    }

    private void UpdateSeparator(TabItem tab, int index)
    {
        int selectedIndex = SelectedIndex;
        bool visible = Variant == AlderTabsVariant.Primary && index > 0 &&
                       index != selectedIndex && index != selectedIndex + 1 &&
                       (GetShowSeparator(tab) ?? ShowSeparators);
        tab.Classes.Set("alder-tab-separator", visible);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        contentWasClosed = false;
        if (revealSelection && headerDrag is not { Active: true } && ContainerFromIndex(SelectedIndex) is TabItem tab)
        {
            revealSelection = false;
            RevealHeader(tab);
        }
        UpdateOverflow();
        UpdateMorphingSurface(animate: !AlderMotion.GetReduced(this));
        return size;
    }

    private void RevealHeader(TabItem tab)
    {
        if (scroll is not { } viewer || tab.TranslatePoint(default, viewer) is not { } position) return;
        double first = IsVertical ? position.Y : position.X;
        double last = first + (IsVertical ? tab.Bounds.Height : tab.Bounds.Width);
        double viewport = IsVertical ? viewer.Viewport.Height : viewer.Viewport.Width;
        double delta = first < 0 ? first : Math.Max(0, last - viewport);
        if (viewport > 0 && Math.Abs(delta) > .5)
        {
            var offset = IsVertical ? new Vector(0, delta) : new Vector(delta, 0);
            viewer.SetCurrentValue(ScrollViewer.OffsetProperty, viewer.Offset + offset);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!HandleMorphingKey(e) && !NavigateTabs(e)) base.OnKeyDown(e);
    }

    private bool NavigateTabs(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source) return false;
        var tab = source as TabItem ?? source.FindAncestorOfType<TabItem>();
        if (tab is null || ItemsControlFromItemContainer(tab) != this) return false;

        bool rtl = FlowDirection == FlowDirection.RightToLeft;
        int direction = e.Key switch
        {
            Key.Up when IsVertical => -1,
            Key.Down when IsVertical => 1,
            Key.Left when !IsVertical => rtl ? 1 : -1,
            Key.Right when !IsVertical => rtl ? -1 : 1,
            Key.Home => 1,
            Key.End => -1,
            _ => 0
        };
        if (direction == 0 || ItemCount == 0) return false;

        int candidate = e.Key switch { Key.Home => -1, Key.End => ItemCount, _ => IndexFromContainer(tab) };
        for (int tested = 0; tested < ItemCount; tested++)
        {
            candidate += direction;
            if (candidate == ItemCount) candidate = 0;
            else if (candidate < 0) candidate = ItemCount - 1;
            if (ContainerFromIndex(candidate) is not TabItem { IsEffectivelyEnabled: true, IsVisible: true } target) continue;
            SetCurrentValue(SelectedIndexProperty, candidate);
            target.Focus(NavigationMethod.Directional);
            break;
        }
        e.Handled = true;
        return true;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateOverflow();
        if (headerDrag is { Active: true } && (e.OffsetDelta != default || e.ViewportDelta != default || e.ExtentDelta != default))
            FinishDrag(false);
        if (e.OffsetDelta != default) UpdateMorphingSurface(animate: false);
        if (e.ExtentDelta == default && e.ViewportDelta == default) return;
        revealSelection = true;
        InvalidateArrange();
    }
    private void ScrollPrevious(object? sender, RoutedEventArgs e) => ScrollBy(-1);
    private void ScrollNext(object? sender, RoutedEventArgs e) => ScrollBy(1);

    private void ScrollBy(int direction)
    {
        if (scroll is not { } viewer) return;
        var step = IsVertical ? new Vector(0, viewer.Viewport.Height * .7) : new Vector(viewer.Viewport.Width * .7, 0);
        viewer.SetCurrentValue(ScrollViewer.OffsetProperty, viewer.Offset + step * direction);
    }

    private void UpdateOverflow()
    {
        if (scroll is not { } viewer) return;
        var offset = IsVertical ? viewer.Offset.Y : viewer.Offset.X;
        var maximum = IsVertical ? viewer.ScrollBarMaximum.Y : viewer.ScrollBarMaximum.X;
        bool canScrollPrevious = offset > 1;
        bool canScrollNext = maximum - offset > 1;
        PseudoClasses.Set(":can-scroll-previous", canScrollPrevious);
        PseudoClasses.Set(":can-scroll-next", canScrollNext);

        if (!canScrollPrevious && !canScrollNext)
        {
            if (edgeMask is not null && ReferenceEquals(viewer.OpacityMask, edgeMask)) viewer.ClearValue(OpacityMaskProperty);
            return;
        }

        // Mask the scrolled content itself so transparent strips retain their underlying surface.
        edgeMask ??= new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            GradientStops =
            [
                new(Colors.Transparent, 0), new(Colors.Black, 0),
                new(Colors.Black, 1), new(Colors.Transparent, 1)
            ]
        };
        var end = new RelativePoint(IsVertical ? 0 : 1, IsVertical ? 1 : 0, RelativeUnit.Relative);
        if (edgeMask.EndPoint != end) edgeMask.EndPoint = end;
        double length = IsVertical ? viewer.Viewport.Height : viewer.Viewport.Width;
        double fade = Math.Min(0.5, 32 / Math.Max(1, length));
        var stops = edgeMask.GradientStops;
        stops[1].Offset = canScrollPrevious ? fade : 0;
        stops[2].Offset = canScrollNext ? 1 - fade : 1;
        if (!ReferenceEquals(viewer.OpacityMask, edgeMask)) viewer.SetCurrentValue(OpacityMaskProperty, edgeMask);
    }
}

// Keep natural widths when they overflow, and distribute any spare width equally.
internal sealed class AlderTabHeadersPanel : StackPanel
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = 0;
        double total = 0;
        double largest = 0;
        foreach (var tab in Children)
        {
            if (!tab.IsVisible) continue;
            count++;
            double width = tab.DesiredSize.Width;
            total += width;
            largest = Math.Max(largest, width);
        }
        if (count == 0 || finalSize.Width <= total) return base.ArrangeOverride(finalSize);

        double equalWidth = finalSize.Width / count;
        double extra = (finalSize.Width - total) / count;
        double x = 0;
        foreach (var tab in Children)
        {
            if (!tab.IsVisible) continue;
            double width = equalWidth >= largest ? equalWidth : tab.DesiredSize.Width + extra;
            tab.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }
        return finalSize;
    }
}

// Arranges the indicator and the tab headers in the same scrollable coordinate space.
internal sealed class AlderTabStrip : Panel
{
    private Rect lastIndicator;
    private TabItem? lastTab;
    private AlderTabsVariant lastVariant;
    private bool lastVertical;
    private bool lastReduced;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2) return base.MeasureOverride(availableSize);
        Children[0].Measure(default);
        Children[1].Measure(availableSize);
        return Children[1].DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (TemplatedParent is not AlderTabs owner || Children.Count != 2) return base.ArrangeOverride(finalSize);
        var indicator = Children[0];
        Children[1].Arrange(new Rect(finalSize));
        if (owner.UsesMorphing)
        {
            lastIndicator = default;
            lastTab = null;
            return finalSize;
        }
        var tab = owner.ContainerFromIndex(owner.SelectedIndex) as TabItem;
        var bounds = default(Rect);
        if (tab?.TranslatePoint(default, this) is { } position && tab.IsVisible)
        {
            bounds = new Rect(position, tab.Bounds.Size);
            if (owner.Variant == AlderTabsVariant.Secondary)
            {
                bounds = owner.TabStripPlacement switch
                {
                    Dock.Left => bounds.WithWidth(2),
                    Dock.Right => new Rect(bounds.Right - 2, bounds.Y, 2, bounds.Height),
                    Dock.Bottom => bounds.WithHeight(2),
                    _ => new Rect(bounds.X, bounds.Bottom - 2, bounds.Width, 2)
                };
            }
        }

        bool reduced = AlderMotion.GetReduced(owner);
        bool move = !reduced && tab != lastTab && lastIndicator.Width > 0 && bounds.Width > 0 &&
                    lastVariant == owner.Variant && lastVertical == owner.IsVertical;
        if (bounds != lastIndicator || reduced != lastReduced)
        {
            indicator.Arrange(bounds);
            if (ElementComposition.GetElementVisual(indicator) is { } visual)
            {
                if (move) OverlayMotion.MoveBetween(visual, lastIndicator, bounds);
                else
                {
                    visual.StopAnimation(nameof(CompositionVisual.Translation));
                    visual.StopAnimation(nameof(CompositionVisual.Scale));
                }
            }
        }
        lastIndicator = bounds;
        lastTab = tab;
        lastVariant = owner.Variant;
        lastVertical = owner.IsVertical;
        lastReduced = reduced;
        return finalSize;
    }
}
