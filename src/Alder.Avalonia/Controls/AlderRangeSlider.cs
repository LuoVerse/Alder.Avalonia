using Avalonia.Media;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Alder.Avalonia.Controls;

/// <summary>Two independently focusable native range-value controls on one shared axis.</summary>
public class AlderRangeSlider : TemplatedControl
{
    /// <summary>Identifies Marks for styling and binding.</summary>
    public static readonly StyledProperty<IEnumerable<SliderMark>?> MarksProperty =
        AlderSlider.MarksProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Identifies MarkFormat for styling and binding.</summary>
    public static readonly StyledProperty<string?> MarkFormatProperty =
        AlderSlider.MarkFormatProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Identifies MarkCulture for styling and binding.</summary>
    public static readonly StyledProperty<CultureInfo?> MarkCultureProperty =
        AlderSlider.MarkCultureProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Identifies TrackBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AlderSlider.TrackBrushProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Identifies FillBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AlderSlider.FillBrushProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Identifies KnobBrush for styling and binding.</summary>
    public static readonly StyledProperty<IBrush?> KnobBrushProperty =
        AlderSlider.KnobBrushProperty.AddOwner<AlderRangeSlider>();

    /// <summary>Gets or sets the Marks appearance setting.</summary>
    public IEnumerable<SliderMark>? Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    /// <summary>Gets or sets the MarkFormat appearance setting.</summary>
    public string? MarkFormat { get => GetValue(MarkFormatProperty); set => SetValue(MarkFormatProperty, value); }
    /// <summary>Gets or sets the MarkCulture appearance setting.</summary>
    public CultureInfo? MarkCulture { get => GetValue(MarkCultureProperty); set => SetValue(MarkCultureProperty, value); }
    /// <summary>Gets or sets the TrackBrush appearance setting.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    /// <summary>Gets or sets the FillBrush appearance setting.</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    /// <summary>Gets or sets the KnobBrush appearance setting.</summary>
    public IBrush? KnobBrush { get => GetValue(KnobBrushProperty); set => SetValue(KnobBrushProperty, value); }

    /// <summary>Identifies the <see cref="Minimum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<AlderRangeSlider, double>(nameof(Minimum), 0, validate: double.IsFinite);
    /// <summary>Identifies the <see cref="Maximum"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<AlderRangeSlider, double>(nameof(Maximum), 100, validate: double.IsFinite,
            coerce: (o, v) => Math.Max(((AlderRangeSlider)o).Minimum, v));
    /// <summary>Identifies the <see cref="LowerValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<AlderRangeSlider, double>(nameof(LowerValue), 0, defaultBindingMode: BindingMode.TwoWay,
            validate: double.IsFinite, coerce: (o, v) =>
            {
                var c = (AlderRangeSlider)o;
                return Math.Clamp(v, c.Minimum, Math.Max(c.Minimum, Math.Min(c.Maximum, c.UpperValue)));
            });
    /// <summary>Identifies the <see cref="UpperValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<AlderRangeSlider, double>(nameof(UpperValue), 100, defaultBindingMode: BindingMode.TwoWay,
            validate: double.IsFinite, coerce: (o, v) =>
            {
                var c = (AlderRangeSlider)o;
                return Math.Clamp(v, Math.Min(c.Maximum, Math.Max(c.Minimum, c.LowerValue)), c.Maximum);
            });
    /// <summary>Identifies the <see cref="Step"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<AlderRangeSlider, double>(nameof(Step), 1, validate: x => x > 0 && double.IsFinite(x));
    /// <summary>Identifies the <see cref="ShowSteps"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowStepsProperty =
        AvaloniaProperty.Register<AlderRangeSlider, bool>(nameof(ShowSteps));
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AlderRangeSlider, Orientation>(nameof(Orientation));
    /// <summary>Identifies the <see cref="IsDirectionReversed"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDirectionReversedProperty = Slider.IsDirectionReversedProperty.AddOwner<AlderRangeSlider>();

    /// <summary>The lower bound of the supported value range.</summary>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>The upper bound of the supported value range.</summary>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>The value of the lower thumb, constrained by the upper thumb and minimum.</summary>
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    /// <summary>The value of the upper thumb, constrained by the lower thumb and maximum.</summary>
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    /// <summary>The increment used when snapping thumb positions to discrete values.</summary>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    /// <summary>Whether to draw the step marks on the track.</summary>
    public bool ShowSteps { get => GetValue(ShowStepsProperty); set => SetValue(ShowStepsProperty, value); }
    /// <summary>Whether the control is laid out horizontally or vertically.</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Whether the minimum and maximum swap ends along the track.</summary>
    public bool IsDirectionReversed { get => GetValue(IsDirectionReversedProperty); set => SetValue(IsDirectionReversedProperty, value); }

    private Canvas? track;
    private Border? rail, fill;
    private Slider? lower, upper, dragging;
    private IPointer? captured;
    private bool updating;
    private bool chooseOnDrag;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        captured?.Capture(null);
        if (track is not null) track.SizeChanged -= TrackResized;
        if (lower is not null) lower.ValueChanged -= ThumbChanged;
        if (upper is not null) upper.ValueChanged -= ThumbChanged;
        track = e.NameScope.Get<Canvas>("PART_Surface");
        rail = e.NameScope.Find<Border>("PART_Rail");
        fill = e.NameScope.Find<Border>("PART_Fill");
        lower = e.NameScope.Get<Slider>("PART_Lower");
        upper = e.NameScope.Get<Slider>("PART_Upper");
        track.SizeChanged += TrackResized;
        lower.ValueChanged += ThumbChanged;
        upper.ValueChanged += ThumbChanged;
        UpdateThumbs();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MinimumProperty) CoerceValue(MaximumProperty);
        if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue(LowerValueProperty);
            CoerceValue(UpperValueProperty);
        }
        if (change.Property == MinimumProperty || change.Property == MaximumProperty ||
            change.Property == LowerValueProperty || change.Property == UpperValueProperty ||
            change.Property == StepProperty || change.Property == OrientationProperty ||
            change.Property == IsDirectionReversedProperty)
            UpdateThumbs();
    }

    private void ThumbChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!updating)
            SetCurrentValue(sender == lower ? LowerValueProperty : UpperValueProperty, e.NewValue);
    }

    private void UpdateThumbs()
    {
        if (lower is null || upper is null) return;
        updating = true;
        try
        {
            lower.Minimum = Minimum;
            lower.Maximum = UpperValue;
            upper.Maximum = Maximum;
            upper.Minimum = LowerValue;
            lower.Value = LowerValue;
            upper.Value = UpperValue;
            lower.Orientation = upper.Orientation = Orientation;
            lower.IsDirectionReversed = upper.IsDirectionReversed = IsDirectionReversed;
            lower.SmallChange = upper.SmallChange = Step;
            lower.TickFrequency = upper.TickFrequency = Step;
        }
        finally { updating = false; }
        PositionParts();
    }

    private void TrackResized(object? sender, SizeChangedEventArgs e) => PositionParts();
    private bool Vertical => Orientation == Orientation.Vertical;
    private const double Radius = SliderSurface.ThumbLength / 2;
    private double Length => Math.Max(0, (Vertical ? track!.Bounds.Height : track!.Bounds.Width) - 2 * Radius);

    private double Offset(double value)
    {
        var ratio = Maximum == Minimum ? 0 : (value - Minimum) / (Maximum - Minimum);
        return SliderSurface.Position(Vertical ? track!.Bounds.Height : track!.Bounds.Width, ratio, Reversed);
    }

    private bool Reversed => Vertical != IsDirectionReversed;

    private void PositionParts()
    {
        if (track is null) return;
        var a = Offset(LowerValue);
        var b = Offset(UpperValue);
        var center = (Vertical ? track.Bounds.Width : track.Bounds.Height) / 2;
        const double thickness = SliderSurface.Thickness;
        var cross = center - thickness / 2;
        if (rail is not null) Place(rail, 0, cross, Length + 2 * Radius, thickness);
        if (fill is not null) Place(fill, Math.Min(a, b), cross, Math.Abs(a - b), thickness);
        Place(lower!, a - Radius, cross, SliderSurface.ThumbLength, thickness);
        Place(upper!, b - Radius, cross, SliderSurface.ThumbLength, thickness);
    }

    private void Place(Control c, double along, double cross, double length, double width)
    {
        Canvas.SetLeft(c, Vertical ? cross : along);
        Canvas.SetTop(c, Vertical ? along : cross);
        c.Width = Vertical ? width : length;
        c.Height = Vertical ? length : width;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (track is null || captured is not null || !e.GetCurrentPoint(track).Properties.IsLeftButtonPressed) return;
        var value = PointerValue(e);
        dragging = Math.Abs(value - LowerValue) < Math.Abs(value - UpperValue) ||
            (LowerValue == UpperValue && value < LowerValue) ? lower : upper;
        chooseOnDrag = LowerValue == UpperValue && value == LowerValue;
        dragging!.Classes.Add("alder-dragging");
        dragging.Focus(NavigationMethod.Pointer);
        captured = e.Pointer;
        captured.Capture(this);
        SetCurrentValue(dragging == lower ? LowerValueProperty : UpperValueProperty, value);
        e.Handled = true;
    }

    private double PointerValue(PointerEventArgs e)
    {
        var p = e.GetPosition(track);
        var ratio = Length == 0 ? 0 : Math.Clamp(((Vertical ? p.Y : p.X) - Radius) / Length, 0, 1);
        if (Reversed) ratio = 1 - ratio;
        if (ratio == 1) return Maximum;
        return Math.Clamp(Minimum + Math.Round(ratio * (Maximum - Minimum) / Step) * Step, Minimum, Maximum);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer != captured || dragging is null) return;
        var value = PointerValue(e);
        if (chooseOnDrag && value != LowerValue)
        {
            dragging.Classes.Remove("alder-dragging");
            dragging = value < LowerValue ? lower : upper;
            dragging!.Classes.Add("alder-dragging");
            dragging.Focus(NavigationMethod.Pointer);
            chooseOnDrag = false;
        }
        SetCurrentValue(dragging == lower ? LowerValueProperty : UpperValueProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer == captured) e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer == captured)
        {
            dragging?.Classes.Remove("alder-dragging");
            captured = null;
            dragging = null;
        }
    }
}
