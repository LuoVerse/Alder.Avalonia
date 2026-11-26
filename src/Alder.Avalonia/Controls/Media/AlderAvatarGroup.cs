using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Selects how overlapping avatars separate neighboring outlines.</summary>
public enum AvatarOverlap
{
    /// <summary>Clips overlapped avatars to create a separation between items.</summary>
    Clip,
    /// <summary>Draws a ring to separate overlapped items.</summary>
    Ring
}

/// <summary>Displays inline avatars or an item source as a stack or a wrapping grid.</summary>
public sealed class AlderAvatarGroup : ItemsControl
{
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty = AlderAvatar.SizeProperty.AddOwner<AlderAvatarGroup>();
    /// <summary>Identifies the <see cref="Color"/> Avalonia property.</summary>
    public static readonly StyledProperty<AvatarColor> ColorProperty = AlderAvatar.ColorProperty.AddOwner<AlderAvatarGroup>();
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AvatarVariant> VariantProperty = AlderAvatar.VariantProperty.AddOwner<AlderAvatarGroup>();
    /// <summary>Identifies the <see cref="Max"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, int>(nameof(Max), int.MaxValue, validate: value => value >= 0);
    /// <summary>Identifies the <see cref="IsGrid"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsGridProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, bool>(nameof(IsGrid));
    /// <summary>Identifies the <see cref="Overlap"/> Avalonia property.</summary>
    public static readonly StyledProperty<AvatarOverlap> OverlapProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, AvatarOverlap>(nameof(Overlap));
    /// <summary>Identifies the <see cref="OverlapAmount"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> OverlapAmountProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, double>(nameof(OverlapAmount), 8, validate: NonnegativeFinite);
    /// <summary>Identifies the <see cref="Spacing"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, double>(nameof(Spacing), 12, validate: NonnegativeFinite);
    /// <summary>Identifies the <see cref="SeamThickness"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SeamThicknessProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, double>(nameof(SeamThickness), 2, validate: NonnegativeFinite);
    /// <summary>Identifies the <see cref="RingBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, IBrush?>(nameof(RingBrush));
    /// <summary>Identifies the <see cref="CountContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> CountContentProperty =
        AvaloniaProperty.Register<AlderAvatarGroup, object?>(nameof(CountContent));
    /// <summary>Identifies the <see cref="OverflowCount"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderAvatarGroup, int> OverflowCountProperty =
        AvaloniaProperty.RegisterDirect<AlderAvatarGroup, int>(nameof(OverflowCount), group => group.OverflowCount);

    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>The color applied to the control.</summary>
    public AvatarColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public AvatarVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The maximum number of visible avatars before showing overflow.</summary>
    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    /// <summary>Whether avatars use the grid arrangement in place of the overlap row.</summary>
    public bool IsGrid { get => GetValue(IsGridProperty); set => SetValue(IsGridProperty, value); }
    /// <summary>The treatment that separates overlapping avatars.</summary>
    public AvatarOverlap Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }
    /// <summary>The amount by which adjacent avatars overlap.</summary>
    public double OverlapAmount { get => GetValue(OverlapAmountProperty); set => SetValue(OverlapAmountProperty, value); }
    /// <summary>The space between items in device-independent pixels.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    /// <summary>The thickness of the separation around overlapping avatars.</summary>
    public double SeamThickness { get => GetValue(SeamThicknessProperty); set => SetValue(SeamThicknessProperty, value); }
    /// <summary>The brush used to color the separating rings.</summary>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    /// <summary>Overrides the automatic count with fallback content or a fully configured count avatar.</summary>
    public object? CountContent { get => GetValue(CountContentProperty); set => SetValue(CountContentProperty, value); }
    /// <summary>The number of avatars represented by the overflow indicator.</summary>
    public int OverflowCount { get => field; private set => SetAndRaise(OverflowCountProperty, ref field, value); }

    private AvatarGroupPanel? panel;

    /// <summary>Initializes a new instance of <see cref="AlderAvatarGroup"/>.</summary>
    public AlderAvatarGroup() => ItemsView.CollectionChanged += (_, _) => UpdateAvatars();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClearSlots();
        base.OnApplyTemplate(e);
        panel = e.NameScope.Find<AvatarGroupPanel>("PART_Avatars");
        UpdateAvatars();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ItemTemplateProperty ||
            change.Property == MaxProperty || change.Property == CountContentProperty)
            UpdateAvatars();
    }

    private void UpdateAvatars()
    {
        int visible = Math.Min(ItemsView.Count, Max);
        OverflowCount = ItemsView.Count - visible;
        if (panel is not { } avatars) return;

        ClearSlots();
        for (int index = 0; index < visible; index++)
            avatars.Children.Add(new AvatarGroupSlot(ItemsView[index], ItemTemplate));
        if (CountContent is not null || OverflowCount > 0)
        {
            var count = CountContent as AlderAvatar ?? new AlderAvatar { Fallback = CountContent ?? $"+{OverflowCount}" };
            avatars.Children.Add(new AvatarGroupSlot(count, null));
        }
    }

    private void ClearSlots()
    {
        if (panel is null) return;
        // Inline controls already have this ItemsControl as a logical parent. The
        // presenters keep that relationship and provide independent clipping.
        foreach (var child in panel.Children.OfType<AvatarGroupSlot>())
            child.ReleaseContent();
        panel.Children.Clear();
    }

    private static bool NonnegativeFinite(double value) => double.IsFinite(value) && value >= 0;
}

internal sealed class AvatarGroupSlot : Decorator
{
    private RoundedRect ringOutline;
    private IPen? ringPen;

    internal AvatarGroupSlot(object? item, IDataTemplate? template)
    {
        Presenter = new ContentPresenter { Content = item, ContentTemplate = template };
        Child = Presenter;
    }

    private ContentPresenter Presenter { get; }
    internal AlderAvatar? Avatar => Presenter.Child as AlderAvatar;

    internal void ReleaseContent()
    {
        Avatar?.ClearValue(AlderAvatar.GroupFallbackInsetProperty);
        Presenter.Content = null;
    }

    internal RoundedRect GetOutline()
    {
        var avatar = Avatar;
        var rect = avatar is null
            ? new Rect(Bounds.Size)
            : avatar.Bounds.Translate((Vector)Presenter.Bounds.Position);
        double limit = Math.Min(rect.Width, rect.Height) / 2;
        var radius = avatar?.CornerRadius ?? new CornerRadius(limit);
        return new RoundedRect(rect, Math.Min(radius.TopLeft, limit), Math.Min(radius.TopRight, limit),
            Math.Min(radius.BottomRight, limit), Math.Min(radius.BottomLeft, limit));
    }

    internal void SetRing(IBrush? brush, double thickness)
    {
        if (brush is null || thickness == 0)
        {
            if (ringPen is null) return;
            ringPen = null;
            InvalidateVisual();
            return;
        }

        var outline = GetOutline();
        if (ringOutline == outline && ReferenceEquals(ringPen?.Brush, brush) && ringPen?.Thickness == thickness)
            return;

        ringOutline = outline;
        ringPen = new Pen(brush, thickness);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (ringPen is { } pen && ringOutline.Rect.Width > 0 && ringOutline.Rect.Height > 0)
        {
            double half = pen.Thickness / 2;
            context.DrawRectangle(null, pen, ringOutline.Inflate(half, half));
        }
        base.Render(context);
    }
}

internal sealed class AvatarGroupPanel : Panel
{
    public static readonly StyledProperty<bool> IsGridProperty = AlderAvatarGroup.IsGridProperty.AddOwner<AvatarGroupPanel>();
    public static readonly StyledProperty<AvatarOverlap> OverlapProperty = AlderAvatarGroup.OverlapProperty.AddOwner<AvatarGroupPanel>();
    public static readonly StyledProperty<double> OverlapAmountProperty = AlderAvatarGroup.OverlapAmountProperty.AddOwner<AvatarGroupPanel>();
    public static readonly StyledProperty<double> SpacingProperty = AlderAvatarGroup.SpacingProperty.AddOwner<AvatarGroupPanel>();
    public static readonly StyledProperty<double> SeamThicknessProperty = AlderAvatarGroup.SeamThicknessProperty.AddOwner<AvatarGroupPanel>();
    public static readonly StyledProperty<IBrush?> RingBrushProperty = AlderAvatarGroup.RingBrushProperty.AddOwner<AvatarGroupPanel>();

    public bool IsGrid { get => GetValue(IsGridProperty); set => SetValue(IsGridProperty, value); }
    public AvatarOverlap Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }
    public double OverlapAmount { get => GetValue(OverlapAmountProperty); set => SetValue(OverlapAmountProperty, value); }
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public double SeamThickness { get => GetValue(SeamThicknessProperty); set => SetValue(SeamThicknessProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    static AvatarGroupPanel()
    {
        AffectsMeasure<AvatarGroupPanel>(IsGridProperty, OverlapAmountProperty, SpacingProperty);
        AffectsArrange<AvatarGroupPanel>(OverlapProperty, RingBrushProperty, SeamThicknessProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(Size.Infinity);
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        UpdateSeams();
        return finalSize;
    }

    private Size Layout(double widthLimit, bool arrange)
    {
        double width = 0, height = 0;
        for (int first = 0; first < Children.Count;)
        {
            double rowWidth = 0, rowHeight = 0, x = 0;
            int end = first;
            for (; end < Children.Count; end++)
            {
                var size = Children[end].DesiredSize;
                if (IsGrid && end > first && x + size.Width > widthLimit) break;

                rowWidth = Math.Max(rowWidth, x + size.Width);
                rowHeight = Math.Max(rowHeight, size.Height);
                x += Advance(size.Width);
            }

            if (arrange)
            {
                x = 0;
                for (int index = first; index < end; index++)
                {
                    var slot = Children[index];
                    var size = slot.DesiredSize;
                    slot.Arrange(new Rect(x, height + (rowHeight - size.Height) / 2, size.Width, size.Height));
                    x += Advance(size.Width);
                }
            }

            width = Math.Max(width, rowWidth);
            height += rowHeight;
            first = end;
            if (first < Children.Count) height += Spacing;
        }

        return new Size(width, height);
    }

    private double Advance(double width) => IsGrid ? width + Spacing : Math.Max(0, width - OverlapAmount);

    private void UpdateSeams()
    {
        var ring = !IsGrid && Overlap == AvatarOverlap.Ring ? RingBrush : null;
        for (int index = 0; index < Children.Count; index++)
        {
            var slot = (AvatarGroupSlot)Children[index];
            slot.SetRing(ring, SeamThickness);
            bool clipped = !IsGrid && Overlap == AvatarOverlap.Clip && index + 1 < Children.Count;
            slot.Clip = clipped ? CreateClip(slot, (AvatarGroupSlot)Children[index + 1]) : null;
            if (slot.Avatar is { } avatar)
                avatar.GroupFallbackInset = clipped ? new Thickness(0, 0, OverlapAmount * .35, 0) : default;
        }
    }

    private Geometry CreateClip(AvatarGroupSlot slot, AvatarGroupSlot next)
    {
        var outline = next.GetOutline();
        double seam = SeamThickness;
        var growth = new Vector(seam, seam);
        var rect = outline.Rect.Translate(next.Bounds.Position - slot.Bounds.Position).Inflate(seam);
        var expanded = new RoundedRect(rect, outline.RadiiTopLeft + growth, outline.RadiiTopRight + growth,
            outline.RadiiBottomRight + growth, outline.RadiiBottomLeft + growth);
        Geometry cutout = outline.IsUniform
            ? new RectangleGeometry(rect, expanded.RadiiTopLeft.X, expanded.RadiiTopLeft.Y)
            : CreateRoundedGeometry(expanded);
        return new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(slot.Bounds.Size)), cutout);
    }

    private static StreamGeometry CreateRoundedGeometry(RoundedRect outline)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        var rect = outline.Rect;
        var tl = outline.RadiiTopLeft;
        var tr = outline.RadiiTopRight;
        var br = outline.RadiiBottomRight;
        var bl = outline.RadiiBottomLeft;
        path.BeginFigure(new Point(rect.Left + tl.X, rect.Top));
        path.LineTo(new Point(rect.Right - tr.X, rect.Top));
        path.ArcTo(new Point(rect.Right, rect.Top + tr.Y), new Size(tr.X, tr.Y), 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(rect.Right, rect.Bottom - br.Y));
        path.ArcTo(new Point(rect.Right - br.X, rect.Bottom), new Size(br.X, br.Y), 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(rect.Left + bl.X, rect.Bottom));
        path.ArcTo(new Point(rect.Left, rect.Bottom - bl.Y), new Size(bl.X, bl.Y), 0, false, SweepDirection.Clockwise);
        path.LineTo(new Point(rect.Left, rect.Top + tl.Y));
        path.ArcTo(new Point(rect.Left + tl.X, rect.Top), new Size(tl.X, tl.Y), 0, false, SweepDirection.Clockwise);
        path.EndFigure(true);
        return geometry;
    }
}
