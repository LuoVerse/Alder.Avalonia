using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Edits two channels. Arrow keys adjust the axes and Shift increases the step size.</summary>
public sealed class AlderColorArea : TemplatedControl
{
    /// <summary>Identifies the <see cref="Value"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderColorValue> ValueProperty =
        AvaloniaProperty.Register<AlderColorArea, AlderColorValue>(nameof(Value), AlderColorValue.Default, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ColorSpace"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSpace> ColorSpaceProperty = AlderColorField.ColorSpaceProperty.AddOwner<AlderColorArea>();
    /// <summary>Identifies the <see cref="XChannel"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorChannel?> XChannelProperty =
        AvaloniaProperty.Register<AlderColorArea, ColorChannel?>(nameof(XChannel));
    /// <summary>Identifies the <see cref="YChannel"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorChannel?> YChannelProperty =
        AvaloniaProperty.Register<AlderColorArea, ColorChannel?>(nameof(YChannel));
    /// <summary>Identifies the <see cref="ShowDots"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowDotsProperty =
        AvaloniaProperty.Register<AlderColorArea, bool>(nameof(ShowDots));

    /// <summary>The color updated by moving in the two-dimensional channel area.</summary>
    public AlderColorValue Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>The color model used to interpret the editable channels.</summary>
    public ColorSpace ColorSpace { get => GetValue(ColorSpaceProperty); set => SetValue(ColorSpaceProperty, value); }
    /// <summary>The channel varied along the horizontal axis.</summary>
    public ColorChannel? XChannel { get => GetValue(XChannelProperty); set => SetValue(XChannelProperty, value); }
    /// <summary>The channel varied along the vertical axis.</summary>
    public ColorChannel? YChannel { get => GetValue(YChannelProperty); set => SetValue(YChannelProperty, value); }
    /// <summary>Whether to draw dots over the color area.</summary>
    public bool ShowDots { get => GetValue(ShowDotsProperty); set => SetValue(ShowDotsProperty, value); }
    /// <summary>Raised when an interactive change in the color area is complete.</summary>
    public event EventHandler? ColorChangeCompleted;

    private const int PlaneSize = 96;
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
    private static readonly IBrush SaturationGradient = Gradient(Brushes.White.Color, Color.FromArgb(0, 255, 255, 255), true);
    private static readonly IBrush BrightnessGradient = Gradient(Color.FromArgb(0, 0, 0, 0), Colors.Black, false);
    private Border? thumb;
    private IBrush? surfaceMask;
    private WriteableBitmap? plane;
    private (AlderColorValue Base, int X, int Y) planeKey;
    private IPointer? pointer;
    private AlderColorValue dragStart;

    static AlderColorArea() => AffectsRender<AlderColorArea>(ValueProperty, ColorSpaceProperty,
        XChannelProperty, YChannelProperty, ShowDotsProperty, CornerRadiusProperty, BorderBrushProperty);

    private Rect Surface => new(Bounds.Size);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (thumb is not null) thumb.PropertyChanged -= OnThumbPropertyChanged;
        base.OnApplyTemplate(e);
        thumb = e.NameScope.Find<Border>("PART_Thumb");
        if (thumb is not null) thumb.PropertyChanged += OnThumbPropertyChanged;
        UpdateThumbPosition();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty ||
            (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != Bounds.Size))
            surfaceMask = AlderRoundedFrame.CreateMask(Bounds.Size, CornerRadius);
        if (change.Property == BoundsProperty || change.Property == ValueProperty || change.Property == ColorSpaceProperty ||
            change.Property == XChannelProperty || change.Property == YChannelProperty)
            UpdateThumbPosition();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var rect = Surface;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var (x, y) = GetAxes();
        var color = Value.InSpace(ColorSpace);
        var rounded = new RoundedRect(rect, CornerRadius);
        using (surfaceMask is { } mask ? context.PushOpacityMask(mask, rect) : context.PushClip(rect))
        {
            if (ColorSpace == ColorSpace.Hsb && x == 1 && y == 2 && color.Alpha == 1)
            {
                // The common saturation/brightness plane is three paints. It needs no bitmap on first open.
                context.FillRectangle(new SolidColorBrush(AlderColorValue.FromHsb(color.Hue, 1, 1).ToColor()), rect);
                context.FillRectangle(SaturationGradient, rect);
                context.FillRectangle(BrightnessGradient, rect);
            }
            else
            {
                UpdatePlane(color, x, y);
                if (color.Alpha < 1 || x == 3 || y == 3)
                    context.FillRectangle(AlderColorRendering.Checkerboard, rect);
                context.DrawImage(plane!, rect);
            }

            if (ShowDots)
                for (double row = rect.Y + 4; row < rect.Bottom; row += 8)
                    for (double column = rect.X + 4; column < rect.Right; column += 8)
                        context.DrawEllipse(DotBrush, null, new Point(column, row), 1, 1);

            if (BorderBrush is { } border)
                context.DrawRectangle(null, new Pen(border, 2), rounded);
        }
    }

    private void UpdateThumbPosition()
    {
        if (thumb is null) return;
        var (x, y) = GetAxes();
        var color = Value.InSpace(ColorSpace);
        double width = double.IsFinite(thumb.Width) ? thumb.Width : 16;
        double height = double.IsFinite(thumb.Height) ? thumb.Height : 16;
        Canvas.SetLeft(thumb, color.At(x) * Bounds.Width - width / 2);
        Canvas.SetTop(thumb, (1 - color.At(y)) * Bounds.Height - height / 2);
    }

    private void OnThumbPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == WidthProperty || change.Property == HeightProperty)
            UpdateThumbPosition();
    }

    private static IBrush Gradient(Color start, Color end, bool horizontal) =>
        new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            EndPoint = horizontal ? new RelativePoint(1, 0, RelativeUnit.Relative)
                                  : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(start, 0), new GradientStop(end, 1) }
        };

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || pointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        dragStart = Value;
        pointer = e.Pointer;
        PseudoClasses.Set(":color-dragging", true);
        pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointer != e.Pointer) return;
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
        UpdateFromPoint(e.GetPosition(this));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointer != e.Pointer) return;
        pointer = null;
        PseudoClasses.Set(":color-dragging", false);
        if (Value != dragStart) ColorChangeCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var (x, y) = GetAxes();
        var (axis, direction) = e.Key switch
        {
            Key.Left => (x, -1), Key.Right => (x, 1),
            Key.Down => (y, -1), Key.Up => (y, 1),
            _ => (-1, 0)
        };
        if (e.Handled || axis < 0 || (e.KeyModifiers & ~KeyModifiers.Shift) != 0) return;
        double step = ColorSpace == ColorSpace.Rgb && axis < 3 ? 1d / 255 :
            axis == 0 && ColorSpace != ColorSpace.Rgb ? 1d / 360 : .01;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) step *= 10;
        var color = Value.InSpace(ColorSpace);
        if (SetValueFromEdit(color.With(axis, Math.Clamp(color.At(axis) + direction * step, 0, 1))))
            ColorChangeCompleted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        plane?.Dispose();
        plane = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AreaPeer(this);

    private void UpdateFromPoint(Point point)
    {
        var rect = Surface;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var (x, y) = GetAxes();
        SetValueFromEdit(Value.InSpace(ColorSpace)
            .With(x, Math.Clamp((point.X - rect.X) / rect.Width, 0, 1))
            .With(y, Math.Clamp((rect.Bottom - point.Y) / rect.Height, 0, 1)));
    }

    private bool SetValueFromEdit(AlderColorValue color)
    {
        color = color.WithPreviousHue(Value.Hue);
        if (color == Value) return false;
        SetCurrentValue(ValueProperty, color);
        return true;
    }

    private (int X, int Y) GetAxes()
    {
        int x = XChannel is { } horizontal ? AlderColorValue.Index(ColorSpace, horizontal) : -1;
        int y = YChannel is { } vertical ? AlderColorValue.Index(ColorSpace, vertical) : -1;
        int defaultX = ColorSpace == ColorSpace.Rgb ? 0 : 1;
        int defaultY = ColorSpace == ColorSpace.Rgb ? 1 : 2;
        if (x < 0) x = y == defaultX ? defaultY : defaultX;
        if (y < 0 || y == x) y = x == defaultY ? defaultX : defaultY;
        return (x, y);
    }

    private void UpdatePlane(AlderColorValue color, int x, int y)
    {
        var baseline = color.With(x, 0).With(y, 0).WithPreviousHue(0);
        var key = (baseline, x, y);
        if (plane is not null && planeKey == key) return;
        planeKey = key;
        plane ??= new WriteableBitmap(new PixelSize(PlaneSize, PlaneSize), new Vector(96, 96),
                                     PixelFormat.Bgra8888, AlphaFormat.Premul);

        using var pixels = plane.Lock();
        var rowBytes = new byte[4 * PlaneSize];
        for (int row = 0; row < PlaneSize; row++)
        {
            var rowColor = baseline.With(y, 1 - row / (PlaneSize - 1d));
            for (int column = 0; column < PlaneSize; column++)
            {
                var sample = rowColor.With(x, column / (PlaneSize - 1d)).ToColor();
                int index = 4 * column;
                rowBytes[index] = Premultiply(sample.B, sample.A);
                rowBytes[index + 1] = Premultiply(sample.G, sample.A);
                rowBytes[index + 2] = Premultiply(sample.R, sample.A);
                rowBytes[index + 3] = sample.A;
            }
            Marshal.Copy(rowBytes, 0, pixels.Address + row * pixels.RowBytes, rowBytes.Length);
        }
    }

    private static byte Premultiply(byte color, byte alpha) => (byte)((color * alpha + 127) / 255);

    private sealed class AreaPeer(AlderColorArea owner) : ControlAutomationPeer(owner), IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string? GetNameCore() => base.GetNameCore() ??
            (owner.TryFindResource("AlderText.ColorArea", out var label) ? label as string : null);
        public bool IsReadOnly => !owner.IsEffectivelyEnabled;
        public string Value => owner.Value.ToString();

        public void SetValue(string? value)
        {
            if (IsReadOnly) throw new InvalidOperationException("The color area is disabled.");
            if (owner.SetValueFromEdit(AlderColorValue.Parse(value ?? "")))
                owner.ColorChangeCompleted?.Invoke(owner, EventArgs.Empty);
        }
    }
}
