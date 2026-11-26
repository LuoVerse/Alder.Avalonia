using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the shared drawing, animation, and pointer tooltip infrastructure for Alder plots.</summary>
public abstract class AlderPlotChart : TemplatedControl
{
    /// <summary>Identifies the <see cref="ShowTooltip"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowTooltipProperty =
        AvaloniaProperty.Register<AlderPlotChart, bool>(nameof(ShowTooltip), true);
    /// <summary>Identifies the <see cref="IsTooltipPinnedToTop"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsTooltipPinnedToTopProperty =
        AvaloniaProperty.Register<AlderPlotChart, bool>(nameof(IsTooltipPinnedToTop));
    /// <summary>Identifies the <see cref="IsAnimationEnabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<AlderPlotChart, bool>(nameof(IsAnimationEnabled), true);
    private static readonly StyledProperty<double> RevealProgressProperty =
        AvaloniaProperty.Register<AlderPlotChart, double>("RevealProgress", 1);
    /// <summary>Identifies the <see cref="ValueFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ValueFormatProperty =
        AvaloniaProperty.Register<AlderPlotChart, string?>(nameof(ValueFormat));
    /// <summary>Identifies the <see cref="TooltipTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> TooltipTemplateProperty =
        AvaloniaProperty.Register<AlderPlotChart, IDataTemplate?>(nameof(TooltipTemplate));
    /// <summary>Identifies the <see cref="GridBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<AlderPlotChart, IBrush?>(nameof(GridBrush));
    /// <summary>Identifies the <see cref="CursorBrush"/> Avalonia property.</summary>
    public static readonly StyledProperty<IBrush?> CursorBrushProperty =
        AvaloniaProperty.Register<AlderPlotChart, IBrush?>(nameof(CursorBrush));
    /// <summary>Identifies the <see cref="ActiveData"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderPlotChart, AlderChartTooltipData?> ActiveDataProperty =
        AvaloniaProperty.RegisterDirect<AlderPlotChart, AlderChartTooltipData?>(nameof(ActiveData), chart => chart.ActiveData);

    /// <summary>Whether to show the tooltip.</summary>
    public bool ShowTooltip { get => GetValue(ShowTooltipProperty); set => SetValue(ShowTooltipProperty, value); }
    /// <summary>Whether the tooltip stays at the top of the plot while the pointer moves.</summary>
    public bool IsTooltipPinnedToTop { get => GetValue(IsTooltipPinnedToTopProperty); set => SetValue(IsTooltipPinnedToTopProperty, value); }
    /// <summary>Whether the plot animates when first shown, subject to the reduced-motion preference.</summary>
    public bool IsAnimationEnabled { get => GetValue(IsAnimationEnabledProperty); set => SetValue(IsAnimationEnabledProperty, value); }
    /// <summary>The default numeric format string for chart tooltip values.</summary>
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    /// <summary>The optional data template for the active tooltip data.</summary>
    public IDataTemplate? TooltipTemplate { get => GetValue(TooltipTemplateProperty); set => SetValue(TooltipTemplateProperty, value); }
    /// <summary>The brush used to draw the grid.</summary>
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    /// <summary>The brush used to draw the cursor.</summary>
    public IBrush? CursorBrush { get => GetValue(CursorBrushProperty); set => SetValue(CursorBrushProperty, value); }
    /// <summary>The current tooltip data under the pointer, or null when there is no hit.</summary>
    public AlderChartTooltipData? ActiveData => activeData;

    private ContentPresenter? tooltip;
    private Point? tooltipPosition;
    private Point tooltipPointer;
    private int activeIndex = -1;
    private AlderChartTooltipData? activeData;
    private bool revealPending;
    private bool withinViewport;
    private CancellationTokenSource? reveal;

    private static readonly TimeSpan TooltipDuration = TimeSpan.FromMilliseconds(400);
    private static readonly SplineEasing ChartEasing = new(.25, .1, .25, 1);
    private static readonly TransformOperations TooltipOrigin = TooltipTranslation(default);

    static AlderPlotChart() => AffectsRender<AlderPlotChart>(
        GridBrushProperty, CursorBrushProperty, ForegroundProperty, BackgroundProperty,
        FontSizeProperty, FontFamilyProperty, FontStyleProperty, FontWeightProperty, FontStretchProperty, RevealProgressProperty);

    /// <summary>Initializes a new instance of <see cref="AlderPlotChart"/>.</summary>
    protected AlderPlotChart() => ActualThemeVariantChanged += (_, _) => ResetLayout();

    private protected virtual TimeSpan RevealDuration => TimeSpan.FromMilliseconds(1500);
    private protected virtual TimeSpan RevealDelay => TimeSpan.Zero;
    private protected double RevealProgress => GetValue(RevealProgressProperty);
    private protected int ActiveIndex => activeIndex;
    private protected virtual bool TracksPointerWithoutTooltip => false;
    private protected abstract Rect TooltipBounds { get; }
    private protected abstract int ItemCount { get; }
    private protected abstract int HitTestIndex(Point position);
    private protected abstract AlderChartTooltipData? GetTooltipData(int index);
    private protected virtual Point GetKeyboardAnchor(int index) => TooltipBounds.Center;
    private protected virtual Point GetTooltipAnchor(int index, Point pointer) => pointer;
    private protected virtual bool UsesVerticalNavigation => false;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 320, 300);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClearActive();
        base.OnApplyTemplate(e);
        tooltip = e.NameScope.Find<ContentPresenter>("PART_Tooltip");
        if (tooltip is not null)
        {
            tooltip.IsHitTestVisible = false;
            HideTooltip();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        withinViewport = false;
        revealPending = IsAnimationEnabled && !AlderMotion.GetReduced(this);
        if (revealPending)
            SetValue(RevealProgressProperty, 0);
        EffectiveViewportChanged += OnViewportChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EffectiveViewportChanged -= OnViewportChanged;
        withinViewport = false;
        FinishReveal();
        ClearActive();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        withinViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
        if (!withinViewport)
        {
            ClearActive();
            return;
        }

        if (revealPending)
            PlayReveal();
    }

    private protected void RestartReveal()
    {
        if (!withinViewport || !IsAnimationEnabled || AlderMotion.GetReduced(this))
            return;
        FinishReveal();
        SetValue(RevealProgressProperty, 0);
        PlayReveal();
    }

    private async void PlayReveal()
    {
        revealPending = false;
        using var token = new CancellationTokenSource();
        reveal = token;
        var animation = new Animation
        {
            Duration = RevealDuration,
            Delay = RevealDelay,
            Easing = ChartEasing,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RevealProgressProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RevealProgressProperty, 1d) } }
            }
        };
        try { await animation.RunAsync(this, token.Token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (reveal == token)
            {
                reveal = null;
                SetValue(RevealProgressProperty, 1);
            }
        }
    }

    private void FinishReveal()
    {
        revealPending = false;
        var token = reveal;
        reveal = null;
        token?.Cancel();
        SetValue(RevealProgressProperty, 1);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == IsAnimationEnabledProperty && !IsAnimationEnabled) ||
            (change.Property == AlderMotion.ReducedProperty && AlderMotion.GetReduced(this)))
        {
            FinishReveal();
            if (tooltip is not null)
            {
                tooltip.Transitions = null;
                if (tooltipPosition is { } anchor)
                    tooltip.RenderTransform = TooltipTranslation(anchor);
            }
        }
        if (change.Property == ValueFormatProperty || change.Property == IsTooltipPinnedToTopProperty ||
            (change.Property == ShowTooltipProperty && !ShowTooltip) ||
            (change.Property == IsVisibleProperty && !IsVisible) ||
            (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled))
            ClearActive();
        else if ((change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != Bounds.Size) ||
                 change.Property == PaddingProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontStyleProperty || change.Property == FontWeightProperty ||
                 change.Property == FontStretchProperty || change.Property == FlowDirectionProperty)
            ResetLayout();
    }

    private protected virtual void ResetLayout()
    {
        ClearActive();
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Background ?? Brushes.Transparent, null, new Rect(Bounds.Size));
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TrackPointer(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        TrackPointer(e.GetPosition(this));
    }

    private void TrackPointer(Point position)
    {
        int index = ShowTooltip || TracksPointerWithoutTooltip ? HitTestIndex(position) : -1;
        if (index >= 0)
            Activate(index, position);
        else
            ClearActive();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearActive();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ClearActive();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Key.Escape && activeIndex >= 0)
        {
            ClearActive();
            e.Handled = true;
            return;
        }
        if (!ShowTooltip || ItemCount == 0 || TooltipBounds.Width <= 0 || TooltipBounds.Height <= 0)
            return;

        int index = e.Key switch
        {
            Key.Home => 0,
            Key.End => ItemCount - 1,
            Key.Right when !UsesVerticalNavigation => activeIndex + 1,
            Key.Down when UsesVerticalNavigation => activeIndex + 1,
            Key.Left when !UsesVerticalNavigation => activeIndex < 0 ? ItemCount - 1 : activeIndex - 1,
            Key.Up when UsesVerticalNavigation => activeIndex < 0 ? ItemCount - 1 : activeIndex - 1,
            _ => -2
        };
        if (index == -2)
            return;

        index = Math.Clamp(index, 0, ItemCount - 1);
        Activate(index, GetKeyboardAnchor(index));
        e.Handled = true;
    }

    private void Activate(int index, Point position)
    {
        tooltipPointer = position;
        if (activeIndex != index)
        {
            activeIndex = index;
            SetAndRaise(ActiveDataProperty, ref activeData, GetTooltipData(index));
            InvalidateVisual();
        }

        if (!ShowTooltip || tooltip is not { } presenter)
            return;

        if (activeData is null)
        {
            HideTooltip();
            return;
        }

        if (!presenter.IsVisible)
        {
            // Enabling layout lets data templates settle while the surface is still hidden.
            presenter.LayoutUpdated += OnTooltipLayoutUpdated;
            presenter.IsVisible = true;
        }
        else if (tooltipPosition is not null)
            PositionTooltip();
    }

    private void OnTooltipLayoutUpdated(object? sender, EventArgs e) => PositionTooltip();

    private void PositionTooltip()
    {
        if (activeData is null || tooltip is not { IsMeasureValid: true, IsArrangeValid: true } presenter)
            return;
        var size = presenter.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var position = GetTooltipAnchor(activeIndex, tooltipPointer);
        var plot = TooltipBounds;
        const double gap = 10;
        double x = position.X;
        double y = position.Y;

        x = x + gap + size.Width <= plot.Right ? x + gap : x - gap - size.Width;
        y = IsTooltipPinnedToTop ? plot.Top
            : y + gap + size.Height <= plot.Bottom ? y + gap : y - gap - size.Height;
        var anchor = new Point(
            Math.Clamp(x, plot.Left, Math.Max(plot.Left, plot.Right - size.Width)),
            Math.Clamp(y, plot.Top, Math.Max(plot.Top, plot.Bottom - size.Height)));
        if (tooltipPosition == anchor)
            return;

        if (presenter.Transitions is null && IsAnimationEnabled && !AlderMotion.GetReduced(this))
        {
            presenter.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TooltipDuration,
                    Easing = ChartEasing
                }
            };
        }
        presenter.RenderTransform = TooltipTranslation(anchor);
        presenter.Opacity = 1;
        tooltipPosition = anchor;
    }

    private static TransformOperations TooltipTranslation(Point position)
    {
        var transform = TransformOperations.CreateBuilder(1);
        transform.AppendTranslate(position.X, position.Y);
        return transform.Build();
    }

    private void HideTooltip()
    {
        tooltipPosition = null;
        if (tooltip is not { } presenter)
            return;
        presenter.LayoutUpdated -= OnTooltipLayoutUpdated;
        presenter.Opacity = 0;
        presenter.IsVisible = false;
        presenter.Transitions = null;
        presenter.RenderTransform = TooltipOrigin;
    }

    private protected void ClearActive()
    {
        HideTooltip();
        if (activeIndex < 0)
            return;
        activeIndex = -1;
        SetAndRaise(ActiveDataProperty, ref activeData, null);
        InvalidateVisual();
    }

    private protected FormattedText Text(string label, double? fontSize = null) => new(label, CultureInfo.CurrentCulture, FlowDirection,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), fontSize ?? FontSize, Foreground ?? Brushes.Gray);

    private protected string FormatValue(double value, string? format = null) => value.ToString(format ?? ValueFormat ??
        (value != 0 && (Math.Abs(value) < .01 || Math.Abs(value) >= 1e9) ? "0.##E+0" : "#,0.##"),
        CultureInfo.CurrentCulture);

    private protected IBrush ResolveBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush ? brush : fallback;

    private protected IBrush ResolveSeriesBrush(int index, IBrush? brush, string? resourceKey) =>
        brush ?? ChartPalette.Resolve(this, index, resourceKey);
}
