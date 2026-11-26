using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Fade edges along the scroll axis. Left and Right are the horizontal start and end in the current flow direction.</summary>
public enum ScrollShadowVisibility
{
    /// <summary>Shows shadows on edges where more content can be scrolled into view.</summary>
    Auto,
    /// <summary>Allows shadows at both ends of the scrolling axis.</summary>
    Both,
    /// <summary>Allows a shadow at the top edge.</summary>
    Top,
    /// <summary>Allows a shadow at the bottom edge.</summary>
    Bottom,
    /// <summary>Allows a shadow at the left edge.</summary>
    Left,
    /// <summary>Allows a shadow at the right edge.</summary>
    Right,
    /// <summary>Suppresses all scroll shadows.</summary>
    None
}

/// <summary>A native scroll viewer that fades content at the edges where more content is available.</summary>
public sealed partial class AlderScrollShadow : AlderScrollViewer
{
    /// <summary>Identifies the <see cref="VisibleShadows"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderScrollShadow, ScrollShadowVisibility> VisibleShadowsProperty =
        AvaloniaProperty.RegisterDirect<AlderScrollShadow, ScrollShadowVisibility>(nameof(VisibleShadows), control => control.VisibleShadows);

    private ScrollContentPresenter? presenter;
    private LinearGradientBrush? fadeMask;
    private IDisposable? maskValue;

    static AlderScrollShadow()
    {
        FocusableProperty.OverrideDefaultValue<AlderScrollShadow>(true);
        HorizontalScrollBarVisibilityProperty.OverrideMetadata<AlderScrollShadow>(new StyledPropertyMetadata<ScrollBarVisibility>(
            ScrollBarVisibility.Auto, coerce: (owner, value) => CoerceScrollBar((AlderScrollShadow)owner, Orientation.Horizontal, value)));
        VerticalScrollBarVisibilityProperty.OverrideMetadata<AlderScrollShadow>(new StyledPropertyMetadata<ScrollBarVisibility>(
            ScrollBarVisibility.Auto, coerce: (owner, value) => CoerceScrollBar((AlderScrollShadow)owner, Orientation.Vertical, value)));
    }

    /// <summary>Initializes a new instance of <see cref="AlderScrollShadow"/>.</summary>
    public AlderScrollShadow() => CoerceValue(HorizontalScrollBarVisibilityProperty);

    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    [GeneratedStyledProperty(DefaultValue = Orientation.Vertical)]
    public partial Orientation Orientation { get; set; }
    /// <summary>The depth of the fade at each scrollable edge in device-independent pixels.</summary>
    [GeneratedStyledProperty(DefaultValue = 40d, ValidateMethodName = nameof(IsNonnegativeFinite))]
    public partial double ShadowSize { get; set; }
    /// <summary>Distance from a scroll boundary before its fade begins, in device-independent pixels.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsNonnegativeFinite))]
    public partial double ShadowOffset { get; set; }
    /// <summary>Whether the scroll bar is hidden while scrolling remains available.</summary>
    [GeneratedStyledProperty]
    public partial bool HideScrollBar { get; set; }
    /// <summary>Enables automatic fade detection. Explicit shadow visibility remains available.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool IsShadowEnabled { get; set; }
    /// <summary>The scroll edges at which the fade is allowed to appear.</summary>
    [GeneratedStyledProperty(DefaultValue = ScrollShadowVisibility.Auto)]
    public partial ScrollShadowVisibility ShadowVisibility { get; set; }
    /// <summary>The currently visible edges, as determined from the viewport and configuration.</summary>
    public ScrollShadowVisibility VisibleShadows { get => field; private set => SetAndRaise(VisibleShadowsProperty, ref field, value); } = ScrollShadowVisibility.None;

    /// <summary>Raised when the currently visible scroll shadows change.</summary>
    public event EventHandler<ScrollShadowVisibility>? ShadowVisibilityChanged;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClearMask();
        base.OnApplyTemplate(e);
        presenter = e.NameScope.Find<ScrollContentPresenter>("PART_ContentPresenter");
        UpdateShadows();
    }

    /// <inheritdoc />
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        UpdateShadows();
        base.OnScrollChanged(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrientationProperty || change.Property == HideScrollBarProperty)
        {
            CoerceValue(HorizontalScrollBarVisibilityProperty);
            CoerceValue(VerticalScrollBarVisibilityProperty);
        }

        if (change.Property == OrientationProperty || change.Property == ShadowSizeProperty ||
            change.Property == ShadowOffsetProperty || change.Property == IsShadowEnabledProperty ||
            change.Property == ShadowVisibilityProperty)
            UpdateShadows();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled) return;
        if (!ReferenceEquals(e.Source, this) || e.KeyModifiers != KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }

        bool vertical = Orientation == Orientation.Vertical;
        bool canScroll = vertical ? CanVerticallyScroll : CanHorizontallyScroll;
        double offset = vertical ? Offset.Y : Offset.X;
        double maximum = vertical ? ScrollBarMaximum.Y : ScrollBarMaximum.X;
        if (!canScroll || maximum <= 0) return;

        bool scrollByLine = vertical ? e.Key is Key.Up or Key.Down : e.Key is Key.Left or Key.Right;
        if (scrollByLine)
        {
            var previousOffset = Offset;
            base.OnKeyDown(e);
            // Leave the key available to the parent when scrolling reaches an edge.
            e.Handled = Offset != previousOffset;
            return;
        }

        double page = vertical ? LargeChange.Height : LargeChange.Width;
        double next = e.Key switch
        {
            Key.Home => 0,
            Key.End => maximum,
            Key.PageUp => offset - page,
            Key.PageDown => offset + page,
            _ => offset
        };
        next = Math.Clamp(next, 0, maximum);
        if (next == offset) return;

        SetCurrentValue(OffsetProperty, vertical ? Offset.WithY(next) : Offset.WithX(next));
        e.Handled = true;
    }

    private static bool IsNonnegativeFinite(double value) => value >= 0 && double.IsFinite(value);

    private static ScrollBarVisibility CoerceScrollBar(AlderScrollShadow owner, Orientation axis, ScrollBarVisibility visibility) =>
        axis != owner.Orientation || visibility == ScrollBarVisibility.Disabled ? ScrollBarVisibility.Disabled :
        owner.HideScrollBar ? ScrollBarVisibility.Hidden : visibility;

    private void UpdateShadows()
    {
        bool vertical = Orientation == Orientation.Vertical;
        double viewport = vertical ? Viewport.Height : Viewport.Width;
        double offset = vertical ? Offset.Y : Offset.X;
        double maximum = vertical ? ScrollBarMaximum.Y : ScrollBarMaximum.X;
        var start = vertical ? ScrollShadowVisibility.Top : ScrollShadowVisibility.Left;
        var end = vertical ? ScrollShadowVisibility.Bottom : ScrollShadowVisibility.Right;

        double before = 0;
        double after = 0;
        if (presenter is not null && viewport > 0 && ShadowSize > 0)
        {
            if (ShadowVisibility == ScrollShadowVisibility.Auto && IsShadowEnabled && maximum > 0)
            {
                before = Math.Clamp(offset - ShadowOffset, 0, ShadowSize);
                after = Math.Clamp(maximum - offset - ShadowOffset, 0, ShadowSize);
            }
            else
            {
                if (ShadowVisibility == start || ShadowVisibility == ScrollShadowVisibility.Both) before = ShadowSize;
                if (ShadowVisibility == end || ShadowVisibility == ScrollShadowVisibility.Both) after = ShadowSize;
            }
        }

        var visible = before > 0
            ? after > 0 ? ScrollShadowVisibility.Both : start
            : after > 0 ? end : ScrollShadowVisibility.None;

        if (visible == ScrollShadowVisibility.None)
            ClearMask();
        else
            ApplyMask(before, after, viewport, vertical);

        if (visible == VisibleShadows) return;
        VisibleShadows = visible;
        ShadowVisibilityChanged?.Invoke(this, visible);
    }

    private void ApplyMask(double before, double after, double viewport, bool vertical)
    {
        // The presenter covers only the viewport, so the gradient stays anchored while its content moves.
        // Scrollbars are siblings in the native template and retain their original opacity.
        fadeMask ??= new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            GradientStops = [new(Colors.Black, 0), new(Colors.Black, 0), new(Colors.Black, 1), new(Colors.Black, 1)]
        };
        var end = new RelativePoint(vertical ? 0 : 1, vertical ? 1 : 0, RelativeUnit.Relative);
        if (fadeMask.EndPoint != end) fadeMask.EndPoint = end;

        double leading = Math.Min(before / viewport, 1);
        double trailing = Math.Min(after / viewport, 1);
        double scale = Math.Max(1, leading + trailing);
        var stops = fadeMask.GradientStops;
        stops[0].Color = before > 0 ? Colors.Transparent : Colors.Black;
        stops[1].Offset = leading / scale;
        stops[2].Offset = 1 - trailing / scale;
        stops[3].Color = after > 0 ? Colors.Transparent : Colors.Black;
        maskValue ??= presenter!.SetValue(OpacityMaskProperty, fadeMask, BindingPriority.StyleTrigger);
    }

    private void ClearMask()
    {
        maskValue?.Dispose();
        maskValue = null;
    }
}
