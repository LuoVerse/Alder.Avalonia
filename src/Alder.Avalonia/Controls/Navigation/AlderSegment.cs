using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the visual emphasis of a segmented selector.</summary>
public enum AlderSegmentVariant
{
    /// <summary>Uses the default treatment.</summary>
    Default,
    /// <summary>Uses a visually quiet treatment at rest.</summary>
    Ghost
}
/// <summary>Selects the component dimensions of the segmented selector.</summary>
public enum AlderSegmentSize
{
    /// <summary>Uses the small size.</summary>
    Small,
    /// <summary>Uses the medium size.</summary>
    Medium,
    /// <summary>Uses the large size.</summary>
    Large
}

/// <summary>A single-selection group with a sliding selection indicator.</summary>
public sealed class AlderSegment : ListBox
{
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly AttachedProperty<AlderSegmentVariant> VariantProperty =
        AvaloniaProperty.RegisterAttached<AlderSegment, StyledElement, AlderSegmentVariant>(nameof(Variant), inherits: true);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly AttachedProperty<AlderSegmentSize> SizeProperty =
        AvaloniaProperty.RegisterAttached<AlderSegment, StyledElement, AlderSegmentSize>(nameof(Size), AlderSegmentSize.Medium, inherits: true);
    /// <summary>Identifies the <see cref="SelectedKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SelectedKeyProperty =
        AvaloniaProperty.Register<AlderSegment, object?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ShowSeparators"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowSeparatorsProperty =
        AvaloniaProperty.Register<AlderSegment, bool>(nameof(ShowSeparators));
    /// <summary>Identifies the <see cref="ExpandSelectedItem"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ExpandSelectedItemProperty =
        AvaloniaProperty.Register<AlderSegment, bool>(nameof(ExpandSelectedItem), inherits: true);

    /// <summary>The visual treatment applied to the control.</summary>
    public AlderSegmentVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSegmentSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The key of the currently selected segment.</summary>
    public object? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }
    /// <summary>Whether to draw dividers between neighboring segments.</summary>
    public bool ShowSeparators { get => GetValue(ShowSeparatorsProperty); set => SetValue(ShowSeparatorsProperty, value); }
    /// <summary>Whether the selected segment reveals its expanded content.</summary>
    public bool ExpandSelectedItem { get => GetValue(ExpandSelectedItemProperty); set => SetValue(ExpandSelectedItemProperty, value); }

    /// <summary>Gets the visual treatment applied to the control.</summary>
    public static AlderSegmentVariant GetVariant(StyledElement control) => control.GetValue(VariantProperty);
    /// <summary>Sets the visual treatment applied to the control.</summary>
    public static void SetVariant(StyledElement control, AlderSegmentVariant value) => control.SetValue(VariantProperty, value);
    /// <summary>Gets the size preset used for the control.</summary>
    public static AlderSegmentSize GetSize(StyledElement control) => control.GetValue(SizeProperty);
    /// <summary>Sets the size preset used for the control.</summary>
    public static void SetSize(StyledElement control, AlderSegmentSize value) => control.SetValue(SizeProperty, value);

    private bool selectingByKey;
    private AlderSegmentTrack? track;

    static AlderSegment() => SelectionModeProperty.OverrideMetadata<AlderSegment>(
        new StyledPropertyMetadata<SelectionMode>(SelectionMode.Single, coerce: (_, _) => SelectionMode.Single));

    /// <summary>Initializes a new instance of <see cref="AlderSegment"/>.</summary>
    public AlderSegment() => ItemsView.CollectionChanged += (_, _) =>
    {
        if (SelectedKey is not null) SelectByKey();
        track?.InvalidateArrange();
    };

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderSegmentItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderSegmentItem();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        track = e.NameScope.Find<AlderSegmentTrack>("PART_Track");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ItemsSourceProperty && !selectingByKey)
        {
            // Reconcile after the native selection model has processed the replacement.
            var key = SelectedKey;
            selectingByKey = true;
            try
            {
                base.OnPropertyChanged(change);
                if (key is not null) SelectByKeyCore(key);
            }
            finally { selectingByKey = false; }
            track?.InvalidateArrange();
            return;
        }

        base.OnPropertyChanged(change);
        if (change.Property == SelectedKeyProperty && !selectingByKey)
            SelectByKey();
        else if (change.Property == SelectedItemProperty && !selectingByKey)
        {
            selectingByKey = true;
            try { SetCurrentValue(SelectedKeyProperty, KeyOf(SelectedItem)); }
            finally { selectingByKey = false; }
        }

        if (change.Property == SelectedIndexProperty || change.Property == ShowSeparatorsProperty ||
            change.Property == AlderMotion.ReducedProperty || change.Property == FlowDirectionProperty)
            track?.InvalidateArrange();
    }

    private void SelectByKey()
    {
        if (selectingByKey) return;
        selectingByKey = true;
        try { SelectByKeyCore(SelectedKey); }
        finally { selectingByKey = false; }
    }

    private void SelectByKeyCore(object? key)
    {
        if (key is not null)
            for (int index = 0; index < ItemsView.Count; index++)
                if (Equals(KeyOf(ItemsView[index]), key))
                {
                    SetCurrentValue(SelectedIndexProperty, index);
                    return;
                }
        SetCurrentValue(SelectedIndexProperty, -1);
    }

    private static object? KeyOf(object? item) => item is AlderSegmentItem segment ? segment.Key ?? segment : item;

    internal void RefreshSeparatorLayout() => track?.InvalidateArrange();
}

/// <summary>Represents one selectable segment with a key and optional icon.</summary>
public sealed class AlderSegmentItem : ListBoxItem
{
    /// <summary>Identifies the inherited setting that expands the selected segment.</summary>
    public static readonly StyledProperty<bool> ExpandSelectedItemProperty =
        AlderSegment.ExpandSelectedItemProperty.AddOwner<AlderSegmentItem>();

    /// <summary>Whether the selected segment reveals its expanded content.</summary>
    public bool ExpandSelectedItem { get => GetValue(ExpandSelectedItemProperty); set => SetValue(ExpandSelectedItemProperty, value); }

    /// <summary>Identifies the <see cref="Key"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> KeyProperty =
        AvaloniaProperty.Register<AlderSegmentItem, object?>(nameof(Key));
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AlderSegmentItem, Geometry?>(nameof(Icon));
    /// <summary>Identifies the <see cref="ShowSeparator"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool?> ShowSeparatorProperty =
        AvaloniaProperty.Register<AlderSegmentItem, bool?>(nameof(ShowSeparator));

    /// <summary>The stable value represented by this segment.</summary>
    public object? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    /// <summary>The optional geometry displayed with the content.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Overrides the group's separator setting for the leading edge of this item.</summary>
    public bool? ShowSeparator { get => GetValue(ShowSeparatorProperty); set => SetValue(ShowSeparatorProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowSeparatorProperty)
            this.FindAncestorOfType<AlderSegment>()?.RefreshSeparatorLayout();
        if (change.Property == IconProperty)
            PseudoClasses.Set(":has-icon", Icon is not null);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
            PseudoClasses.Set(":has-content", Content is not null || ContentTemplate is not null);
    }

    internal void SetSeparatorVisible(bool visible) => PseudoClasses.Set(":separator", visible);
}

internal sealed class AlderSegmentTrack : Panel
{
    private static readonly TimeSpan IndicatorDuration = TimeSpan.FromMilliseconds(270);
    private static readonly SplineEasing IndicatorEasing = new(.22, .82, .27, 1);
    private AlderSegmentItem? lastItem;
    private Rect lastBounds;
    private FlowDirection lastFlow;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2) return base.MeasureOverride(availableSize);
        Children[0].Measure(default);
        Children[1].Measure(availableSize);
        return Children[1].DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (TemplatedParent is not AlderSegment owner || Children.Count != 2) return base.ArrangeOverride(finalSize);

        var indicator = Children[0];
        Children[1].Arrange(new Rect(finalSize));
        var selection = owner.SelectedIndex;
        for (int index = 0; index < owner.ItemCount; index++)
        {
            if (owner.ContainerFromIndex(index) is AlderSegmentItem item)
                item.SetSeparatorVisible((item.ShowSeparator ?? owner.ShowSeparators) &&
                    index > 0 && index != selection && index != selection + 1);
        }

        var selected = owner.ContainerFromIndex(selection) as AlderSegmentItem;
        var bounds = selected?.TranslatePoint(default, this) is { } position
            ? new Rect(position, selected.Bounds.Size) : default;
        indicator.IsVisible = bounds.Width > 0 && bounds.Height > 0;
        indicator.Arrange(bounds);
        bool reduced = AlderMotion.GetReduced(owner);
        if (!reduced && lastItem != selected && lastFlow == FlowDirection &&
            lastBounds.Width > 0 && bounds.Width > 0 && ElementComposition.GetElementVisual(indicator) is { } visual)
            OverlayMotion.MoveBetween(visual, lastBounds, bounds, IndicatorDuration, IndicatorEasing);
        else if (reduced || lastFlow != FlowDirection || lastItem != selected || bounds != lastBounds)
            OverlayMotion.Reset(indicator);

        lastItem = selected;
        lastBounds = bounds;
        lastFlow = FlowDirection;
        return finalSize;
    }
}
