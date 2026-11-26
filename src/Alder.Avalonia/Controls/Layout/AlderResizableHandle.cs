using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Selects the line and grip displayed by an adjustable separator.</summary>
public enum ResizableHandleType
{
    /// <summary>A one-DIP separator line.</summary>
    Line,
    /// <summary>A separator with a six-dot drag grip.</summary>
    Drag,
    /// <summary>A separator with a rounded pill grip.</summary>
    Pill,
    /// <summary>A standalone pill grip.</summary>
    Handle
}

/// <summary>A pointer, keyboard and automation-operable separator between two resizable panes.</summary>
public sealed partial class AlderResizableHandle : TemplatedControl
{
    /// <summary>Identifies the <see cref="Orientation"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderResizableHandle, Orientation> OrientationProperty =
        AvaloniaProperty.RegisterDirect<AlderResizableHandle, Orientation>(nameof(Orientation), control => control.Orientation);

    /// <summary>The orientation of the owning group.</summary>
    public Orientation Orientation { get => field; private set => SetAndRaise(OrientationProperty, ref field, value); }

    /// <summary>The line and grip style.</summary>
    [GeneratedStyledProperty]
    public partial ResizableHandleType Type { get; set; }

    /// <summary>The separator's contrast against its surface.</summary>
    [GeneratedStyledProperty]
    public partial SeparatorVariant Variant { get; set; }

    /// <summary>Displays a pill grip on a line separator.</summary>
    [GeneratedStyledProperty]
    public partial bool WithIndicator { get; set; }

    /// <summary>Optional custom grip content. Use an <see cref="AlderResizableIndicator"/> to compose a built-in grip.</summary>
    [GeneratedStyledProperty]
    public partial object? Indicator { get; set; }

    /// <summary>Raised when pointer dragging starts or ends.</summary>
    public event EventHandler? DraggingChanged;

    /// <summary>Identifies the <see cref="IsDragging"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderResizableHandle, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<AlderResizableHandle, bool>(nameof(IsDragging), control => control.IsDragging);

    /// <summary>Whether this handle currently owns a pointer resize gesture.</summary>
    public bool IsDragging => pointer is not null;

    private IPointer? pointer;
    private AlderResizable.ResizeSession? resize;
    private double origin;
    private AlderResizable? Group => Parent as AlderResizable;

    internal double HitWidth => 8;
    internal void SetOrientation(Orientation orientation) => Orientation = orientation;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || IsDragging || !IsEffectivelyEnabled || Group is not { } group ||
            (e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) ||
            group.BeginResize(this) is not { } session) return;

        Focus(NavigationMethod.Pointer);
        resize = session;
        origin = group.Primary(e.GetPosition(group));
        pointer = e.Pointer;
        pointer.Capture(this);
        e.PreventGestureRecognition();
        PseudoClasses.Set(":dragging", true);
        RaisePropertyChanged(IsDraggingProperty, false, true);
        DraggingChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointer != e.Pointer || Group is not { } group) return;
        resize?.Resize(group.Primary(e.GetPosition(group)) - origin);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer || (e.Pointer.Type != PointerType.Touch && e.InitialPressMouseButton != MouseButton.Left)) return;
        if (Group is { } group) resize?.Resize(group.Primary(e.GetPosition(group)) - origin);
        EndDrag();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointer == e.Pointer) EndDrag();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IndicatorProperty || change.Property == TypeProperty || change.Property == WithIndicatorProperty)
        {
            PseudoClasses.Set(":custom-indicator", Indicator is not null);
            PseudoClasses.Set(":indicator", Indicator is null && (Type != ResizableHandleType.Line || WithIndicator));
        }
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) EndDrag();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndDrag();
        base.OnDetachedFromVisualTree(e);
    }

    private void EndDrag()
    {
        if (pointer is not { } captured) return;
        pointer = null;
        resize = null;
        if (captured.Captured == this) captured.Capture(null);
        PseudoClasses.Set(":dragging", false);
        RaisePropertyChanged(IsDraggingProperty, true, false);
        DraggingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsDragging || !IsEffectivelyEnabled || Group is not { } group) return;
        if (e.Key == Key.Enter)
        {
            e.Handled = group.TogglePane(this);
            return;
        }
        if (group.BeginResize(this) is not { } session) return;
        bool horizontal = Orientation == Orientation.Horizontal;
        int direction = e.Key switch
        {
            Key.Left when horizontal => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right when horizontal => FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            Key.Up when !horizontal => -1,
            Key.Down when !horizontal => 1,
            Key.Home => -20,
            Key.End => 20,
            _ => 0
        };
        if (direction == 0) return;

        session.Resize(direction * session.KeyboardStep);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new HandlePeer(this);

    private sealed class HandlePeer(AlderResizableHandle owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        public bool IsReadOnly => !owner.IsEffectivelyEnabled;
        public double Minimum => owner.Group?.BeginResize(owner)?.Minimum ?? 0;
        public double Maximum => owner.Group?.BeginResize(owner)?.Maximum ?? 100;
        public double LargeChange => 5;
        public double SmallChange => 5;
        public double Value => owner.Group?.BeginResize(owner)?.Value ?? 0;

        public void SetValue(double value)
        {
            if (IsReadOnly) throw new InvalidOperationException("The separator is disabled.");
            if (!double.IsFinite(value) || value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            owner.Group?.BeginResize(owner)?.ResizeTo(value);
        }

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IRangeValueProvider) ? this : base.GetProviderCore(providerType);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;
    }
}

/// <summary>Selects the built-in appearance of a resize indicator.</summary>
public enum ResizableIndicatorType
{
    /// <summary>A rounded pill.</summary>
    Pill,
    /// <summary>A six-dot drag grip.</summary>
    Drag
}

/// <summary>A pill, drag grip or custom piece of content centered on a resizable handle.</summary>
public sealed partial class AlderResizableIndicator : ContentControl
{
    /// <summary>The orientation of the resizable group with which the indicator is used.</summary>
    [GeneratedStyledProperty]
    public partial Orientation Orientation { get; set; }

    /// <summary>The built-in grip used when no custom content is supplied.</summary>
    [GeneratedStyledProperty]
    public partial ResizableIndicatorType Type { get; set; }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) PseudoClasses.Set(":custom", Content is not null);
    }
}
