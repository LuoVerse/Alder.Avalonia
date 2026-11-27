using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A customization for an individual item in a rating.</summary>
public sealed class AlderRatingItem
{
    /// <summary>The rating represented by the item.</summary>
    public double Value { get; set; }
    /// <summary>An optional icon for this item.</summary>
    public object? Icon { get; set; }
    /// <summary>An optional template receiving a <see cref="RatingItemState"/> value.</summary>
    public IDataTemplate? ContentTemplate { get; set; }
}

/// <summary>Describes the value and fill state presented to a custom rating item template.</summary>
/// <param name="Value">The value represented by the item.</param>
/// <param name="IsActive">Whether the item is fully filled.</param>
/// <param name="IsPartial">Whether the item is partially filled.</param>
/// <param name="PartialPercent">The percentage of the item that is filled.</param>
/// <param name="IsPreview">Whether the current state is previewed by a pointer.</param>
public sealed record RatingItemState(double Value, bool IsActive, bool IsPartial, double PartialPercent, bool IsPreview);

/// <summary>A selectable rating with icon customization and fractional read-only display.</summary>
public sealed partial class AlderRating : TemplatedControl
{
    /// <summary>The committed rating. Zero is unrated, and fractional values support display ratings.</summary>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay, ValidateMethodName = nameof(IsFinite))]
    public partial double Value { get; set; }
    /// <summary>The number of items when no custom items are supplied.</summary>
    [GeneratedStyledProperty(DefaultValue = 5, ValidateMethodName = nameof(IsValidCount))]
    public partial int Count { get; set; }
    /// <summary>The size of each icon and its spacing.</summary>
    [GeneratedStyledProperty(DefaultValue = AlderSize.Medium)]
    public partial AlderSize Size { get; set; }
    /// <summary>Displays the rating without accepting user edits.</summary>
    [GeneratedStyledProperty]
    public partial bool IsReadOnly { get; set; }
    /// <summary>Whether tapping the currently selected item clears the rating.</summary>
    [GeneratedStyledProperty]
    public partial bool AllowClear { get; set; }
    /// <summary>The geometry or icon control used when an item has no custom icon.</summary>
    [GeneratedStyledProperty]
    public partial object? Icon { get; set; }
    /// <summary>The foreground applied to active portions of rating items.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? ActiveBrush { get; set; }
    /// <summary>The foreground applied to inactive portions of rating items.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? InactiveBrush { get; set; }
    /// <summary>An optional template applied to all items, receiving their <see cref="RatingItemState"/>.</summary>
    [GeneratedStyledProperty]
    public partial IDataTemplate? ItemTemplate { get; set; }
    /// <summary>An optional label displayed above the rating.</summary>
    [GeneratedStyledProperty]
    public partial object? Label { get; set; }
    /// <summary>A command receiving the committed value after an interaction.</summary>
    [GeneratedStyledProperty]
    public partial ICommand? ValueChangedCommand { get; set; }

    /// <summary>Custom items. When empty, items from one to <see cref="Count"/> are generated.</summary>
    public AvaloniaList<AlderRatingItem> Items { get; } = [];
    /// <summary>Raised when the committed rating changes.</summary>
    public event EventHandler? ValueChanged;

    private readonly List<Glyph> glyphs = [];
    private StackPanel? itemsHost;
    private Canvas? iconSources;
    private IPointer? pointer;
    private double? preview;
    private double? pressedValue;

    /// <summary>Initializes the rating control.</summary>
    public AlderRating()
    {
        Items.CollectionChanged += ItemsChanged;
    }

    private bool CanEdit => IsEffectivelyEnabled && !IsReadOnly;
    private double Maximum => glyphs.Count == 0 ? Count : glyphs[^1].Value;
    private static bool IsFinite(double value) => double.IsFinite(value);
    private static bool IsValidCount(int value) => value is >= 1 and <= 100;

    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ClearIconSources();
        itemsHost = e.NameScope.Find<StackPanel>("PART_Items");
        iconSources = e.NameScope.Find<Canvas>("PART_IconSources");
        Rebuild();
    }

    private void Rebuild()
    {
        if (itemsHost is null) return;
        ClearIconSources();
        itemsHost.Children.Clear();
        glyphs.Clear();
        var items = Items.Count != 0 ? Items.Where(item => item.Value > 0 && double.IsFinite(item.Value))
            .OrderBy(item => item.Value).DistinctBy(item => item.Value) :
            Enumerable.Range(1, Count).Select(value => new AlderRatingItem { Value = value });

        var (diameter, space) = Size switch
        {
            AlderSize.Small => (16d, 4d),
            AlderSize.Large => (28d, 8d),
            _ => (20d, 5d)
        };
        itemsHost.Spacing = space;
        var masks = new Dictionary<Control, VisualBrush>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            var template = item.ContentTemplate ?? ItemTemplate;
            var icon = item.Icon ?? Icon;
            VisualBrush? mask = null;
            if (template is null && icon is Control control && iconSources is not null &&
                !masks.TryGetValue(control, out mask))
            {
                // One live source supplies all rating cells that use this icon.
                var source = new AlderIconPresenter { Content = control, Width = diameter, Height = diameter };
                iconSources.Children.Add(source);
                mask = new VisualBrush(source);
                masks.Add(control, mask);
            }
            var visual = new Glyph(item.Value, diameter, AlderIconPresenter.AsGeometry(icon), mask, InactiveBrush, ActiveBrush, template);
            glyphs.Add(visual);
            itemsHost.Children.Add(visual);
        }
        UpdateGlyphs();
    }

    private void ClearIconSources()
    {
        if (iconSources is null) return;
        foreach (var source in iconSources.Children.OfType<AlderIconPresenter>())
            source.Content = null;
        iconSources.Children.Clear();
    }

    private void UpdateGlyphs()
    {
        double value = preview ?? Value;
        double previous = 0;
        bool reducedMotion = AlderMotion.GetReduced(this);
        foreach (var glyph in glyphs)
        {
            double fraction = Math.Clamp((value - previous) / (glyph.Value - previous), 0, 1);
            glyph.Update(fraction, preview.HasValue, pressedValue == glyph.Value, reducedMotion);
            previous = glyph.Value;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == SizeProperty || change.Property == IconProperty ||
            change.Property == ActiveBrushProperty || change.Property == InactiveBrushProperty || change.Property == ItemTemplateProperty)
            Rebuild();
        else if (change.Property == ValueProperty)
        {
            UpdateGlyphs();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == AlderMotion.ReducedProperty) UpdateGlyphs();
        else if ((change.Property == IsEffectivelyEnabledProperty || change.Property == IsReadOnlyProperty) && !CanEdit)
            ReleasePointer();
    }

    private (double? Value, bool InsideTrack) ItemAt(PointerEventArgs e)
    {
        if (itemsHost is null) return default;
        var point = e.GetPosition(itemsHost);
        if (!new Rect(itemsHost.Bounds.Size).Contains(point)) return default;
        foreach (var glyph in glyphs)
            if (glyph.Bounds.Contains(point)) return (glyph.Value, true);
        return (null, true);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || pointer is not null || !CanEdit ||
            (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) ||
            ItemAt(e).Value is not { } candidate) return;
        Focus(NavigationMethod.Pointer);
        pointer = e.Pointer;
        pointer.Capture(this);
        e.PreventGestureRecognition();
        pressedValue = preview = candidate;
        UpdateGlyphs();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!CanEdit || pointer != e.Pointer && (pointer is not null || e.Pointer.Type == PointerType.Touch)) return;
        var (candidate, insideTrack) = ItemAt(e);
        // Crossing the spacing keeps the previous preview until another glyph is entered.
        if (candidate.HasValue || !insideTrack) SetPreview(candidate);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetPreview(null);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer) return;
        var (candidate, insideTrack) = ItemAt(e);
        var hover = insideTrack && e.Pointer.Type != PointerType.Touch ? candidate ?? preview : null;
        if (CanEdit && candidate is { } value)
            Commit(AllowClear && value == Value ? 0 : value);
        ReleasePointer(CanEdit ? hover : null);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointer == e.Pointer) ReleasePointer();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ReleasePointer();
        base.OnDetachedFromVisualTree(e);
    }

    private void ReleasePointer(double? hover = null)
    {
        var contact = pointer;
        pointer = null;
        pressedValue = null;
        if (contact?.Captured == this) contact.Capture(null);
        preview = hover;
        UpdateGlyphs();
    }

    private void SetPreview(double? value)
    {
        if (preview == value) return;
        preview = value;
        UpdateGlyphs();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !CanEdit || glyphs.Count == 0) return;
        if (e.Key == Key.Escape && pointer is not null)
        {
            ReleasePointer();
            e.Handled = true;
            return;
        }
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        int direction = e.Key switch
        {
            Key.Up => 1, Key.Down => -1,
            Key.Left => rtl ? 1 : -1, Key.Right => rtl ? -1 : 1, _ => 0
        };
        double? target = e.Key switch
        {
            Key.Home => glyphs[0].Value,
            Key.End => Maximum,
            Key.Delete or Key.Back when AllowClear => 0,
            Key.Space or Key.Enter when Value == 0 => glyphs[0].Value,
            _ => null
        };
        if (direction == 1) target = glyphs.FirstOrDefault(glyph => glyph.Value > Value)?.Value ?? Maximum;
        if (direction == -1) target = glyphs.LastOrDefault(glyph => glyph.Value < Value)?.Value ?? (AllowClear ? 0 : glyphs[0].Value);
        if (target is null) return;
        SetPreview(null);
        Commit(target.Value);
        e.Handled = true;
    }

    private void Commit(double value)
    {
        if (Value == value) return;
        SetCurrentValue(ValueProperty, value);
        if (ValueChangedCommand is { } command && command.CanExecute(value)) command.Execute(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RatingPeer(this);

    private sealed class RatingPeer(AlderRating owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.IsReadOnly ? AutomationControlType.Text : AutomationControlType.Slider;
        protected override object? GetProviderCore(Type providerType) => providerType == typeof(IRangeValueProvider) ? this : base.GetProviderCore(providerType);
        protected override string? GetNameCore()
        {
            var name = base.GetNameCore() ?? owner.Label?.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = owner.TryFindResource("AlderText.RatingLabel", out var localized) ? localized as string ?? "Rating" : "Rating";
            return owner.IsReadOnly
                ? string.Create(CultureInfo.CurrentCulture, $"{name}: {Value:G} / {Maximum:G}")
                : name;
        }
        public bool IsReadOnly => !owner.CanEdit;
        public double Minimum => 0;
        public double Maximum => owner.Maximum;
        public double Value => Math.Clamp(owner.Value, Minimum, Maximum);
        public double SmallChange => 1;
        public double LargeChange => 1;
        public void SetValue(double value)
        {
            if (IsReadOnly) throw new InvalidOperationException();
            if (!double.IsFinite(value) || value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            double nearest = owner.glyphs.MinBy(glyph => Math.Abs(glyph.Value - value))?.Value ?? Math.Round(value);
            owner.Commit(value == 0 && owner.AllowClear ? 0 : nearest);
        }
    }

    private sealed class Glyph : Panel
    {
        private readonly ContentPresenter? custom;
        private readonly Control? fill;
        private readonly RectangleGeometry? fillClip;
        private readonly double diameter;
        private static readonly TransformOperations Rest = TransformOperations.Parse("scale(1)");
        private static readonly TransformOperations Pressed = TransformOperations.Parse("scale(0.8)");
        private readonly Transitions pressMotion = new() { new TransformOperationsTransition
        {
            Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(150),
            Easing = new SplineEasing(.25, .1, .25, 1)
        } };
        private readonly Transitions fillMotion = new() { new DoubleTransition
        {
            Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(100),
            Easing = new SplineEasing(0, 0, .58, 1)
        } };

        internal Glyph(double value, double diameter, Geometry? icon, VisualBrush? mask,
            IBrush? empty, IBrush? active, IDataTemplate? template)
        {
            Value = value;
            this.diameter = diameter;
            Width = Height = diameter;
            RenderTransform = Rest;
            IsHitTestVisible = false;
            if (template is not null)
            {
                custom = new ContentPresenter { ContentTemplate = template, HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center };
                Children.Add(custom);
            }
            else
            {
                Children.Add(CreateLayer(empty));
                fillClip = new RectangleGeometry();
                fill = CreateLayer(active);
                fill.Clip = fillClip;
                fill.Opacity = 0;
                Children.Add(fill);
            }

            Control CreateLayer(IBrush? brush) => mask is null
                ? new AlderPathIcon { Data = icon, Foreground = brush, Width = diameter, Height = diameter }
                : new Border { Background = brush, OpacityMask = mask, Width = diameter, Height = diameter };
        }

        internal double Value { get; }
        internal void Update(double fraction, bool preview, bool pressed, bool reducedMotion)
        {
            if (reducedMotion) Transitions = null;
            RenderTransform = pressed && !reducedMotion ? Pressed : Rest;
            if (!reducedMotion) Transitions = pressMotion;
            if (fill is { } foreground && fillClip is { } clip)
            {
                // Leave the previous fraction in place until the outgoing fade is transparent.
                if (fraction > 0) clip.Rect = new Rect(0, 0, diameter * fraction, diameter);
                if (reducedMotion) foreground.Transitions = null;
                foreground.Opacity = fraction > 0 ? 1 : 0;
                if (!reducedMotion) foreground.Transitions = fillMotion;
            }
            double percent = fraction * 100;
            if (custom is not null && (custom.Content is not RatingItemState state ||
                                       state.PartialPercent != percent || state.IsPreview != preview))
                custom.Content = new RatingItemState(Value, fraction == 1, fraction is > 0 and < 1, percent, preview);
        }
    }
}
