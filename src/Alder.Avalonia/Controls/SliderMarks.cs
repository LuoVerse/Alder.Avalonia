using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed class SliderMarks : Panel
{
    public static readonly StyledProperty<IEnumerable<SliderMark>?> MarksProperty = AlderSlider.MarksProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<string?> MarkFormatProperty = AlderSlider.MarkFormatProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<CultureInfo?> MarkCultureProperty = AlderSlider.MarkCultureProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<double> MinimumProperty = SliderSurface.MinimumProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<double> MaximumProperty = SliderSurface.MaximumProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<double> ValueProperty = SliderSurface.ValueProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<double?> LowerValueProperty = SliderSurface.LowerValueProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<Orientation> OrientationProperty = SliderSurface.OrientationProperty.AddOwner<SliderMarks>();
    public static readonly StyledProperty<bool> IsDirectionReversedProperty = SliderSurface.IsDirectionReversedProperty.AddOwner<SliderMarks>();

    public IEnumerable<SliderMark>? Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    public string? MarkFormat { get => GetValue(MarkFormatProperty); set => SetValue(MarkFormatProperty, value); }
    public CultureInfo? MarkCulture { get => GetValue(MarkCultureProperty); set => SetValue(MarkCultureProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double? LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool IsDirectionReversed { get => GetValue(IsDirectionReversedProperty); set => SetValue(IsDirectionReversedProperty, value); }

    private SliderMark[] items = [];
    private readonly List<(SliderMark Mark, TextBlock Label)> labels = [];
    private INotifyCollectionChanged? observable;
    private bool attached;
    private bool Vertical => Orientation == Orientation.Vertical;

    static SliderMarks()
    {
        AffectsMeasure<SliderMarks>(OrientationProperty);
        AffectsArrange<SliderMarks>(IsDirectionReversedProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        ObserveMarks();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnobserveMarks();
        attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!attached) return;
        if (change.Property == MarksProperty) ObserveMarks();
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == MarkFormatProperty || change.Property == MarkCultureProperty) BuildLabels();
        else if (change.Property == ValueProperty || change.Property == LowerValueProperty) UpdateSelection();
    }

    private void ObserveMarks()
    {
        UnobserveMarks();
        items = Marks?.ToArray() ?? [];
        foreach (var item in items) item.PropertyChanged += MarkChanged;
        observable = Marks as INotifyCollectionChanged;
        if (observable is not null) observable.CollectionChanged += CollectionChanged;
        BuildLabels();
    }

    private void UnobserveMarks()
    {
        if (observable is not null) observable.CollectionChanged -= CollectionChanged;
        observable = null;
        foreach (var item in items) item.PropertyChanged -= MarkChanged;
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ObserveMarks();
    private void MarkChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => BuildLabels();

    private void BuildLabels()
    {
        Children.Clear();
        labels.Clear();
        foreach (var mark in items)
        {
            if (!double.IsFinite(mark.Value) || mark.Value < Minimum || mark.Value > Maximum) continue;
            var label = new AlderTextBlock
            {
                Text = mark.Label ?? mark.Value.ToString(MarkFormat, MarkCulture ?? CultureInfo.CurrentCulture),
                IsHitTestVisible = false
            };
            labels.Add((mark, label));
            Children.Add(label);
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        double lower = LowerValue ?? Minimum;
        double tolerance = Math.Abs(Maximum - Minimum) * 1e-9;
        foreach (var (mark, label) in labels)
            label.Classes.Set("filled", mark.Value >= lower - tolerance && mark.Value <= Value + tolerance);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double cross = 0;
        foreach (var (_, label) in labels)
        {
            label.Measure(Size.Infinity);
            cross = Math.Max(cross, Vertical ? label.DesiredSize.Width : label.DesiredSize.Height);
        }
        if (labels.Count == 0) return default;
        return Vertical ? new Size(cross + 8, 0) : new Size(0, cross + 4);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double length = Vertical ? finalSize.Height : finalSize.Width;
        double range = Maximum - Minimum;
        foreach (var (mark, label) in labels)
        {
            double ratio = range > 0 ? (mark.Value - Minimum) / range : 0;
            var offset = SliderSurface.Position(length, ratio, Vertical != IsDirectionReversed);
            var size = label.DesiredSize;
            var origin = Vertical ? new Point(8, offset - size.Height / 2) : new Point(offset - size.Width / 2, 4);
            label.Arrange(new Rect(origin, size));
        }
        return finalSize;
    }
}
