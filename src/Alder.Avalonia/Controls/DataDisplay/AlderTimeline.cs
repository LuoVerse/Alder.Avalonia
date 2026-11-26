using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Aligns the timeline rail and the associated entries.</summary>
public enum TimelineAlignment
{
    /// <summary>Places the rail before the entries.</summary>
    Start,
    /// <summary>Places the rail between alternating entries.</summary>
    Center,
    /// <summary>Places the rail after the entries.</summary>
    End
}

/// <summary>A series of events connected by a continuous rail.</summary>
public sealed class AlderTimeline : ItemsControl
{
    /// <summary>Identifies the <see cref="Alignment"/> property.</summary>
    public static readonly StyledProperty<TimelineAlignment> AlignmentProperty =
        AvaloniaProperty.Register<AlderTimeline, TimelineAlignment>(nameof(Alignment));
    /// <summary>Identifies the <see cref="ShowConnectors"/> property.</summary>
    public static readonly StyledProperty<bool> ShowConnectorsProperty =
        AvaloniaProperty.Register<AlderTimeline, bool>(nameof(ShowConnectors), true);
    /// <summary>Identifies the <see cref="Size"/> property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderTimeline, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="CompactBreakpoint"/> property.</summary>
    public static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<AlderTimeline, double>(nameof(CompactBreakpoint),
            validate: width => double.IsFinite(width) && width >= 0);

    private bool useStartAlignment;

    static AlderTimeline() => AffectsMeasure<AlderTimeline>(CompactBreakpointProperty);

    /// <summary>Gets or sets the alignment.</summary>
    public TimelineAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    /// <summary>Gets or sets the show connectors.</summary>
    public bool ShowConnectors { get => GetValue(ShowConnectorsProperty); set => SetValue(ShowConnectorsProperty, value); }
    /// <summary>Gets or sets the size.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Uses start alignment below this width for a centered timeline. Zero disables the breakpoint.</summary>
    public double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var compact = availableSize.Width < CompactBreakpoint;
        if (useStartAlignment != compact)
        {
            useStartAlignment = compact;
            if (Alignment == TimelineAlignment.Center)
                UpdateItems();
        }
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderTimelineItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderTimelineItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        Configure((AlderTimelineItem)container, index);
    }

    /// <inheritdoc />
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        Configure((AlderTimelineItem)container, newIndex);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlignmentProperty || change.Property == ShowConnectorsProperty ||
            change.Property == SizeProperty || change.Property == ItemCountProperty)
            UpdateItems();
    }

    private void UpdateItems()
    {
        foreach (var item in GetRealizedContainers().OfType<AlderTimelineItem>())
            Configure(item, IndexFromContainer(item));
    }

    private void Configure(AlderTimelineItem item, int index)
    {
        var centered = Alignment == TimelineAlignment.Center && !useStartAlignment;
        item.Classes.Set("connect-before", ShowConnectors && index > 0);
        item.Classes.Set("connect-after", ShowConnectors && index >= 0 && index < ItemCount - 1);
        item.Classes.Set("last-entry", index >= 0 && index == ItemCount - 1);
        item.Classes.Set("centered", centered);
        item.SetEntrySide(Alignment == TimelineAlignment.End || centered && index % 2 != 0);
        item.Classes.Set("trailing-rail", Alignment == TimelineAlignment.End);
        item.Classes.Set("compact", Size == AlderSize.Small);
        item.Classes.Set("spacious", Size == AlderSize.Large);
    }
}

/// <summary>A timeline event with independently templated detail and marker.</summary>
public sealed class AlderTimelineItem : HeaderedContentControl
{
    private Grid? rail;
    private bool preferLeadingEntry;

    /// <summary>Identifies the <see cref="Time"/> property.</summary>
    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<AlderTimelineItem, string?>(nameof(Time));
    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AlderTimelineItem, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="Marker"/> property.</summary>
    public static readonly StyledProperty<object?> MarkerProperty =
        AvaloniaProperty.Register<AlderTimelineItem, object?>(nameof(Marker));
    /// <summary>Identifies the <see cref="MarkerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<AlderTimelineItem, IBrush?>(nameof(MarkerBrush));
    /// <summary>Identifies the <see cref="MarkerColor"/> property.</summary>
    public static readonly StyledProperty<AlderStatusColor> MarkerColorProperty =
        AvaloniaProperty.Register<AlderTimelineItem, AlderStatusColor>(nameof(MarkerColor));
    /// <summary>Identifies the <see cref="ShowMarkerFrame"/> property.</summary>
    public static readonly StyledProperty<bool> ShowMarkerFrameProperty =
        AvaloniaProperty.Register<AlderTimelineItem, bool>(nameof(ShowMarkerFrame), true);
    /// <summary>Identifies the <see cref="ShowMarkerDot"/> property.</summary>
    public static readonly StyledProperty<bool> ShowMarkerDotProperty =
        AvaloniaProperty.Register<AlderTimelineItem, bool>(nameof(ShowMarkerDot), true);
    /// <summary>Identifies the <see cref="IsMarkerCentered"/> property.</summary>
    public static readonly StyledProperty<bool> IsMarkerCenteredProperty =
        AvaloniaProperty.Register<AlderTimelineItem, bool>(nameof(IsMarkerCentered));
    /// <summary>Identifies the <see cref="LeadingContent"/> property.</summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<AlderTimelineItem, object?>(nameof(LeadingContent));
    /// <summary>Identifies the <see cref="LeadingContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> LeadingContentTemplateProperty =
        AvaloniaProperty.Register<AlderTimelineItem, IDataTemplate?>(nameof(LeadingContentTemplate));

    /// <summary>Gets or sets the time.</summary>
    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    /// <summary>Gets or sets the description.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>Gets or sets the marker.</summary>
    public object? Marker { get => GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }
    /// <summary>Overrides the foreground brush of the marker.</summary>
    public IBrush? MarkerBrush { get => GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    /// <summary>Sets the semantic foreground, outline and background of the marker.</summary>
    public AlderStatusColor MarkerColor { get => GetValue(MarkerColorProperty); set => SetValue(MarkerColorProperty, value); }
    /// <summary>Whether to draw the circular frame behind the marker.</summary>
    public bool ShowMarkerFrame { get => GetValue(ShowMarkerFrameProperty); set => SetValue(ShowMarkerFrameProperty, value); }
    /// <summary>Whether an empty marker shows a dot.</summary>
    public bool ShowMarkerDot { get => GetValue(ShowMarkerDotProperty); set => SetValue(ShowMarkerDotProperty, value); }
    /// <summary>Centers the marker vertically alongside the entry.</summary>
    public bool IsMarkerCentered { get => GetValue(IsMarkerCenteredProperty); set => SetValue(IsMarkerCenteredProperty, value); }
    /// <summary>Gets or sets the context shown before the rail in a centered timeline.</summary>
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }
    /// <summary>Gets or sets the template for the context shown before the rail.</summary>
    public IDataTemplate? LeadingContentTemplate { get => GetValue(LeadingContentTemplateProperty); set => SetValue(LeadingContentTemplateProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        rail = e.NameScope.Find<Grid>("PART_Rail");
        UpdateMarkerPosition();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMarkerCenteredProperty)
            UpdateMarkerPosition();
        if (change.Property == LeadingContentProperty)
            SetEntrySide(preferLeadingEntry);
    }

    internal void SetEntrySide(bool leading)
    {
        preferLeadingEntry = leading;
        Classes.Set("leading-entry", leading && (!Classes.Contains("centered") || LeadingContent is null));
    }

    private void UpdateMarkerPosition()
    {
        if (rail is { RowDefinitions.Count: 3 })
            rail.RowDefinitions[0].Height = IsMarkerCentered ? GridLength.Star : new GridLength(0);
    }
}

internal sealed class TimelineLayoutPanel : Panel
{
    public static readonly StyledProperty<TimelineAlignment> AlignmentProperty =
        AvaloniaProperty.Register<TimelineLayoutPanel, TimelineAlignment>(nameof(Alignment));
    public static readonly StyledProperty<bool> BeforeRailProperty =
        AvaloniaProperty.Register<TimelineLayoutPanel, bool>(nameof(BeforeRail));

    public TimelineAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    public bool BeforeRail { get => GetValue(BeforeRailProperty); set => SetValue(BeforeRailProperty, value); }

    static TimelineLayoutPanel() => AffectsMeasure<TimelineLayoutPanel>(AlignmentProperty, BeforeRailProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2) return default;
        var rail = Children[0];
        var entry = Children[1];
        rail.Measure(availableSize);
        var remaining = Math.Max(0, availableSize.Width - rail.DesiredSize.Width);
        var multiplier = Alignment == TimelineAlignment.Center ? 2 : 1;
        var entryConstraint = new Size(remaining / multiplier, availableSize.Height);
        entry.Measure(entryConstraint);
        var companion = Companion;
        companion?.Measure(entryConstraint);
        var contentWidth = Math.Max(entry.DesiredSize.Width, companion?.DesiredSize.Width ?? 0);
        var contentHeight = Math.Max(entry.DesiredSize.Height, companion?.DesiredSize.Height ?? 0);
        return new Size(rail.DesiredSize.Width + contentWidth * multiplier, Math.Max(rail.DesiredSize.Height, contentHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2) return finalSize;
        var rail = Children[0];
        var entry = Children[1];
        var railWidth = Math.Min(finalSize.Width, rail.DesiredSize.Width);
        var width = (finalSize.Width - railWidth) / (Alignment == TimelineAlignment.Center ? 2 : 1);
        var railX = Alignment == TimelineAlignment.Start ? 0 : width;
        var companion = Companion;
        var entryX = BeforeRail && companion is null ? 0 : railX + railWidth;
        rail.Arrange(new Rect(railX, 0, railWidth, finalSize.Height));
        entry.Arrange(new Rect(entryX, 0, width, finalSize.Height));
        companion?.Arrange(new Rect(0, 0, width, finalSize.Height));
        return finalSize;
    }

    private Control? Companion =>
        Alignment == TimelineAlignment.Center && Children.Count > 2 && Children[2].IsVisible ? Children[2] : null;
}
