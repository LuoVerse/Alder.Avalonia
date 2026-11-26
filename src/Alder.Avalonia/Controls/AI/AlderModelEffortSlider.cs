using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Handles the model effort meter's discrete pointer interaction.</summary>
internal sealed class AlderModelEffortSlider : AlderSlider
{
    private AlderModelEffortTrack? meter;
    private IPointer? pointer;
    private double dragStartX;
    private double dragStartValue;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        pointer?.Capture(null);
        meter = e.NameScope.Find<AlderModelEffortTrack>("EffortTrack");
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (e.Handled || pointer is not null || !IsEffectivelyEnabled || meter is null ||
            !point.Properties.IsLeftButtonPressed || Bounds.Width <= AlderModelEffortTrack.ThumbWidth ||
            Maximum <= Minimum)
            return;

        Focus(NavigationMethod.Pointer);
        dragStartX = point.Position.X;
        dragStartValue = Value;
        pointer = e.Pointer;
        pointer.Capture(this);
        PseudoClasses.Set(":effort-dragging", true);

        var thumbStart = meter.ThumbStart;
        var thumbWidth = AlderModelEffortTrack.ThumbWidth;
        if (dragStartX < thumbStart || dragStartX > thumbStart + thumbWidth)
        {
            var fraction = (dragStartX - thumbWidth / 2) / (Bounds.Width - thumbWidth);
            SelectNearest(Minimum + (Maximum - Minimum) * fraction);
            dragStartValue = Value;
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointer != e.Pointer || Bounds.Width <= 0) return;

        var distance = e.GetPosition(this).X - dragStartX;
        SelectNearest(dragStartValue + (Maximum - Minimum) * distance / Bounds.Width);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointer != e.Pointer) return;

        pointer = null;
        PseudoClasses.Set(":effort-dragging", false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            pointer?.Capture(null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        pointer?.Capture(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void SelectNearest(double value) =>
        SetCurrentValue(ValueProperty, Math.Clamp(Math.Floor(value + .5), Minimum, Maximum));
}
