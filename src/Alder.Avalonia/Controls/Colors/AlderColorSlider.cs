using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A native slider with a color gradient. ColorValue binds the color and Value reports the active channel.</summary>
[PseudoClasses(":translucent-track")]
public sealed class AlderColorSlider : Slider
{
    /// <summary>Identifies the <see cref="ColorValue"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderColorValue> ColorValueProperty =
        AvaloniaProperty.Register<AlderColorSlider, AlderColorValue>(nameof(ColorValue), AlderColorValue.Default, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ColorSpace"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorSpace> ColorSpaceProperty = AlderColorField.ColorSpaceProperty.AddOwner<AlderColorSlider>();
    /// <summary>Identifies the <see cref="Channel"/> Avalonia property.</summary>
    public static readonly StyledProperty<ColorChannel> ChannelProperty =
        AvaloniaProperty.Register<AlderColorSlider, ColorChannel>(nameof(Channel), ColorChannel.Hue);
    /// <summary>Identifies the <see cref="Gradient"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderColorSlider, IBrush?> GradientProperty =
        AvaloniaProperty.RegisterDirect<AlderColorSlider, IBrush?>(nameof(Gradient), slider => slider.Gradient);
    /// <summary>Identifies the <see cref="ChannelText"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderColorSlider, string> ChannelTextProperty =
        AvaloniaProperty.RegisterDirect<AlderColorSlider, string>(nameof(ChannelText), slider => slider.ChannelText);

    /// <summary>The color whose specified channel is changed by the slider.</summary>
    public AlderColorValue ColorValue { get => GetValue(ColorValueProperty); set => SetValue(ColorValueProperty, value); }
    /// <summary>The color model used to interpret the editable components.</summary>
    public ColorSpace ColorSpace { get => GetValue(ColorSpaceProperty); set => SetValue(ColorSpaceProperty, value); }
    /// <summary>The color component edited by the slider.</summary>
    public ColorChannel Channel { get => GetValue(ChannelProperty); set => SetValue(ChannelProperty, value); }
    /// <summary>The gradient showing available values of that channel.</summary>
    public IBrush? Gradient => gradient;
    /// <summary>The formatted text for the currently selected channel value.</summary>
    public string ChannelText => channelText;
    /// <summary>Raised when an interactive slider change is complete.</summary>
    public event EventHandler? ColorChangeCompleted;

    private IBrush? gradient;
    private string channelText = "";
    private bool syncing;
    private bool dragActive;
    private AlderColorValue gestureStart;
    private (AlderColorValue Color, ColorChannel Channel, Orientation Orientation, bool Reversed, double Cap)? gradientKey;

    /// <summary>Initializes a new instance of <see cref="AlderColorSlider"/>.</summary>
    public AlderColorSlider()
    {
        SyncFromColor();
        AddHandler(PointerPressedEvent, OnGestureStarted, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnGestureEnded, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(Thumb.DragCompletedEvent, (_, _) => CompleteGesture(), handledEventsToo: true);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CompleteGesture();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (syncing) return;
        if (change.Property == ColorValueProperty || change.Property == ColorSpaceProperty || change.Property == ChannelProperty)
            SyncFromColor();
        else if (change.Property == OrientationProperty || change.Property == IsDirectionReversedProperty || change.Property == BoundsProperty)
            UpdatePresentation();
        else if (change.Property == ValueProperty)
        {
            var space = AlderColorValue.SpaceFor(Channel, ColorSpace);
            double maximum = AlderColorValue.Maximum(Channel);
            var previous = ColorValue;
            SetCurrentValue(ColorValueProperty, previous.Edit(space, Channel, Math.Clamp(Value / maximum, 0, 1)));
            if (!dragActive && ColorValue != previous) ColorChangeCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ColorSliderPeer(this);

    private void SyncFromColor()
    {
        syncing = true;
        try
        {
            SetCurrentValue(MaximumProperty, AlderColorValue.Maximum(Channel));
            SetCurrentValue(ValueProperty, ColorValue.GetChannel(Channel, ColorSpace));
        }
        finally { syncing = false; }
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        var culture = CultureInfo.CurrentCulture;
        string label = Channel switch
        {
            ColorChannel.Hue => Value.ToString("0.##", culture) + "°",
            ColorChannel.Red or ColorChannel.Green or ColorChannel.Blue => Value.ToString("0.##", culture),
            _ => (Value / 100).ToString("0.##%", culture)
        };
        SetAndRaise(ChannelTextProperty, ref channelText, label);

        var space = AlderColorValue.SpaceFor(Channel, ColorSpace);
        int index = AlderColorValue.Index(space, Channel);
        var value = ColorValue.InSpace(space);
        PseudoClasses.Set(":translucent-track",
            Channel == ColorChannel.Alpha || (Channel != ColorChannel.Hue && value.Alpha < 1));
        // Hue needs a full spectrum even for grey or black.
        var baseColor = Channel == ColorChannel.Hue
            ? space == ColorSpace.Hsl ? AlderColorValue.FromHsl(0, 1, .5) : AlderColorValue.FromHsb(0, 1, 1)
            : value.With(index, 0);
        double length = Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
        double cap = 10 / Math.Max(length, 21);
        var key = (baseColor, Channel, Orientation, IsDirectionReversed, cap);
        if (gradientKey == key) return;
        gradientKey = key;

        var start = Orientation == Orientation.Horizontal ? new RelativePoint(0, .5, RelativeUnit.Relative)
                                                          : new RelativePoint(.5, 1, RelativeUnit.Relative);
        var end = Orientation == Orientation.Horizontal ? new RelativePoint(1, .5, RelativeUnit.Relative)
                                                        : new RelativePoint(.5, 0, RelativeUnit.Relative);
        var brush = new LinearGradientBrush
        {
            StartPoint = IsDirectionReversed ? end : start,
            EndPoint = IsDirectionReversed ? start : end
        };
        // This includes the six hue stops and the HSL lightness midpoint.
        for (int i = 0; i <= 12; i++)
        {
            double offset = i / 12d;
            brush.GradientStops.Add(new GradientStop(baseColor.With(index, offset).ToColor(), cap + (1 - 2 * cap) * offset));
        }
        SetAndRaise(GradientProperty, ref gradient, brush);
    }

    private void OnGestureStarted(object? sender, PointerPressedEventArgs e)
    {
        if (dragActive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        gestureStart = ColorValue;
        dragActive = true;
        PseudoClasses.Set(":color-dragging", true);
    }

    private void OnGestureEnded(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) CompleteGesture();
    }

    private void CompleteGesture()
    {
        if (!dragActive) return;
        dragActive = false;
        PseudoClasses.Set(":color-dragging", false);
        if (ColorValue != gestureStart) ColorChangeCompleted?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ColorSliderPeer(AlderColorSlider owner) : RangeBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
        protected override string? GetNameCore() => base.GetNameCore() ??
            (owner.TryFindResource($"AlderText.Color{owner.Channel}", out var label) ? label as string : owner.Channel.ToString());
    }
}
