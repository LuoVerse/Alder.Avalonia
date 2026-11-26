using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>Specifies the directions in which the editor's resize handle permits dragging.</summary>
public enum TextAreaResize
{
    /// <summary>Disables the resize handle.</summary>
    None,
    /// <summary>Allows height changes.</summary>
    Vertical,
    /// <summary>Allows width changes.</summary>
    Horizontal,
    /// <summary>Allows both width and height changes.</summary>
    Both
}

/// <summary>A multiline AlderInput with an optional native pointer resize handle.</summary>
public sealed class AlderTextArea : AlderInput
{
    /// <summary>Identifies the <see cref="Rows"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<AlderTextArea, int>(nameof(Rows), 2, validate: rows => rows > 0);
    /// <summary>Identifies the <see cref="ResizeMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<TextAreaResize> ResizeModeProperty =
        AvaloniaProperty.Register<AlderTextArea, TextAreaResize>(nameof(ResizeMode), TextAreaResize.Vertical);

    /// <summary>The number of text rows used to size the editor.</summary>
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    /// <summary>The directions in which the user can resize the editor.</summary>
    public TextAreaResize ResizeMode { get => GetValue(ResizeModeProperty); set => SetValue(ResizeModeProperty, value); }

    private Border? handle;
    private TopLevel? dragRoot;
    private IPointer? dragPointer;
    private Point dragPoint;
    private Size dragSize;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        StopDrag();
        base.OnApplyTemplate(e);
        handle = null;
        if (e.NameScope.Find<Panel>("FieldPanel") is not { } panel) return;
        var grip = new AlderPathIcon { Width = 9, Height = 9, IsHitTestVisible = false };
        grip[!PathIcon.DataProperty] = new DynamicResourceExtension("AlderIconResize");
        grip[!ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        handle = new Border
        {
            Child = grip,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 2, 2),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        handle.PointerPressed += StartDrag;
        handle.PointerMoved += Resize;
        handle.PointerReleased += (_, _) => StopDrag();
        handle.PointerCaptureLost += (_, _) => StopDrag();
        panel.Children.Add(handle);
        UpdateHandle();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ResizeModeProperty || change.Property == IsReadOnlyProperty ||
            change.Property == IsEffectivelyEnabledProperty || change.Property == FullWidthProperty)
            UpdateHandle();
    }

    private bool CanResizeWidth => !FullWidth && ResizeMode is TextAreaResize.Horizontal or TextAreaResize.Both;
    private bool CanResizeHeight => ResizeMode is TextAreaResize.Vertical or TextAreaResize.Both;

    private void UpdateHandle()
    {
        if (handle is null) return;
        handle.IsVisible = IsEffectivelyEnabled && !IsReadOnly && (CanResizeWidth || CanResizeHeight);
        if (!handle.IsVisible) StopDrag();
        handle.Cursor = new Cursor(CanResizeWidth
            ? CanResizeHeight ? StandardCursorType.BottomRightCorner : StandardCursorType.SizeWestEast
            : StandardCursorType.SizeNorthSouth);
    }

    private void StartDrag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed ||
            TopLevel.GetTopLevel(this) is not { } topLevel || handle is null) return;
        dragRoot = topLevel;
        dragPointer = e.Pointer;
        dragPoint = e.GetPosition(topLevel);
        dragSize = Bounds.Size;
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    private void Resize(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != dragPointer || dragRoot is null) return;
        var delta = e.GetPosition(dragRoot) - dragPoint;
        if (CanResizeHeight)
            SetCurrentValue(HeightProperty, Math.Clamp(dragSize.Height + delta.Y, Math.Min(MinHeight, MaxHeight), MaxHeight));
        if (CanResizeWidth)
        {
            double dx = FlowDirection == FlowDirection.RightToLeft ? -delta.X : delta.X;
            double minimum = Math.Min(Math.Max(MinWidth, 96), MaxWidth);
            SetCurrentValue(WidthProperty, Math.Clamp(dragSize.Width + dx, minimum, MaxWidth));
        }
        e.Handled = true;
    }

    private void StopDrag()
    {
        var pointer = dragPointer;
        dragPointer = null;
        dragRoot = null;
        if (pointer?.Captured == handle) pointer?.Capture(null);
    }
}
