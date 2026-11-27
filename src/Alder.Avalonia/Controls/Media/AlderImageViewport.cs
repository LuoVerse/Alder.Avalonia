using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

internal sealed partial class AlderImageViewport : Panel
{
    private static readonly Cursor OpenHand = new(StandardCursorType.Hand);
    private static readonly Cursor DragHand = new(StandardCursorType.SizeAll);
    private static readonly Easing EaseOut = new CubicEaseOut();
    private ImageLayer _currentLayer = new() { ZIndex = 1 };
    private ImageLayer _outgoingLayer = new();
    private Image ActiveImage => _currentLayer.Picture;
    private IImage? _source;
    private Size _layoutSize;
    private double _layoutScaling;
    private Vector _offset;
    private IPointer? _dragPointer;
    private Point _dragStart;
    private Vector _dragOrigin;

    public AlderImageViewport()
    {
        ClipToBounds = true;
        Focusable = true;
        FocusAdorner = null;
        Children.Add(_outgoingLayer);
        Children.Add(_currentLayer);
        Loaded += (_, _) => ResizeView();
        SizeChanged += (_, _) => ResizeView();
    }

    public double Scale { get; private set; } = 1;
    public double FitScale { get; private set; } = 1;
    public bool IsFit { get; private set; } = true;
    public Rect ImageBounds { get; private set; }
    public event EventHandler? ViewChanged;

    internal static Size GetUnitSize(IImage? image) => image switch
    {
        Bitmap bitmap => new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
        CroppedBitmap { SourceRect: { Width: > 0, Height: > 0 } crop } => new Size(crop.Width, crop.Height),
        _ => image?.Size ?? default
    };

    private double RenderScaling => _source is Bitmap or CroppedBitmap ? TopLevel.GetTopLevel(this)?.RenderScaling ?? 1 : 1;
    private Size PixelSize => GetUnitSize(_source);
    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);
    private double DisplayFactor => Scale / RenderScaling;
    private bool CanPan => ImageBounds.Width > Bounds.Width + 0.5 || ImageBounds.Height > Bounds.Height + 0.5;

    public void ShowImage(IImage? source, AlderImageViewTransition transition, int direction)
    {
        _dragPointer?.Capture(null);
        FinishImageTransition();
        bool animate = transition != AlderImageViewTransition.None && !AlderMotion.GetReduced(this) &&
                       _source is not null && source is not null && !ReferenceEquals(_source, source) &&
                       ImageBounds.Width > 0 && ImageBounds.Height > 0 &&
                       source.Size.Width > 0 && source.Size.Height > 0 &&
                       ElementComposition.GetElementVisual(_currentLayer) is not null &&
                       ElementComposition.GetElementVisual(_outgoingLayer) is not null;
        if (animate)
        {
            // Preserve the outgoing image and its zoom and pan transform.
            (_currentLayer, _outgoingLayer) = (_outgoingLayer, _currentLayer);
            _outgoingLayer.ZIndex = 0;
            _currentLayer.ZIndex = 1;
        }

        _source = source;
        ActiveImage.Source = source;
        ActiveImage.Width = PixelSize.Width;
        ActiveImage.Height = PixelSize.Height;
        IsFit = true;
        _offset = default;
        if (animate) StartImageTransition(transition, direction);
        ResizeView(force: true);
    }

    public void Fit() => SetView(FitScale, default, fit: true, animate: true);

    public void ActualSize() => SetView(1, default, fit: false, animate: true);

    public void ZoomBy(double factor) => ZoomAt(Scale * factor, Center, animate: true);

    public void AnimateLayoutFrom(Rect previousBounds)
    {
        if (previousBounds.Width > 0 && ImageBounds.Width > 0 && !AlderMotion.GetReduced(this))
            CommitTransform(animate: true, previousBounds, milliseconds: 200);
    }

    public void CompleteMotion()
    {
        CommitTransform(false);
        FinishImageTransition();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        FinishImageTransition();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var pointer = e.GetPosition(this);
        if (_source is null || e.Delta.Y == 0 || !ImageBounds.Contains(pointer) ||
            (IsFit && e.Delta.Y < 0)) return;
        ZoomAt(Scale * Math.Pow(1.18, e.Delta.Y), pointer, animate: false);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !ImageBounds.Contains(point)) return;
        Focus(NavigationMethod.Pointer);
        if (e.ClickCount == 2)
        {
            if (!IsFit) Fit();
            else if (FitScale < 1) ZoomAt(1, point, animate: true);
            else ActualSize();
            e.Handled = true;
            return;
        }
        if (!CanPan || _dragPointer is not null) return;
        CompleteMotion();
        _dragStart = point;
        _dragOrigin = _offset;
        _dragPointer = e.Pointer;
        e.Pointer.Capture(this);
        UpdateCursor();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _dragPointer)) return;
        var movement = e.GetPosition(this) - _dragStart;
        var offset = _dragOrigin + new Vector(
            ImageBounds.Width > Bounds.Width ? movement.X : 0,
            ImageBounds.Height > Bounds.Height ? movement.Y : 0);
        SetView(Scale, offset, fit: false, animate: false);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _dragPointer)) return;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _dragPointer = null;
        UpdateCursor();
    }

    public void ResizeView(bool force = false)
    {
        var size = Bounds.Size;
        var scaling = RenderScaling;
        if (!force && size == _layoutSize && scaling == _layoutScaling) return;
        _layoutSize = size;
        _layoutScaling = scaling;
        if (_source is null || PixelSize.Width <= 0 || PixelSize.Height <= 0 || size.Width <= 0 || size.Height <= 0)
        {
            Scale = FitScale = 1;
            ImageBounds = default;
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        const double inset = 20;
        FitScale = Math.Clamp(Math.Min((size.Width - 2 * inset) / PixelSize.Width,
                                      (size.Height - 2 * inset) / PixelSize.Height) * scaling, 0.001, 1);
        SetView(IsFit ? FitScale : Math.Max(Scale, FitScale), _offset, IsFit, animate: false);
    }

    private void ZoomAt(double scale, Point anchor, bool animate)
    {
        scale = Math.Clamp(scale, FitScale, 4);
        if (scale <= FitScale + 0.00001)
        {
            SetView(FitScale, default, fit: true, animate);
            return;
        }

        var pointFromCenter = anchor - Center;
        var offset = pointFromCenter - (pointFromCenter - _offset) * (scale / Scale);
        SetView(scale, offset, fit: false, animate);
    }

    private void SetView(double scale, Vector offset, bool fit, bool animate)
    {
        if (_source is null) return;
        Scale = scale;
        IsFit = fit;
        var imageSize = PixelSize * DisplayFactor;
        // Give anchored zoom room to shift without moving a small picture out of view
        // or dragging a large picture beyond its edge.
        var limitX = Math.Abs(imageSize.Width - Bounds.Width) / 2;
        var limitY = Math.Abs(imageSize.Height - Bounds.Height) / 2;
        _offset = new Vector(Math.Clamp(offset.X, -limitX, limitX), Math.Clamp(offset.Y, -limitY, limitY));
        ImageBounds = new Rect(Center + _offset - new Vector(imageSize.Width / 2, imageSize.Height / 2), imageSize);
        CommitTransform(animate);
        UpdateCursor();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCursor() => Cursor = _dragPointer is not null ? DragHand : CanPan ? OpenHand : null;

    private void CommitTransform(bool animate, Rect? previousBounds = null, int milliseconds = 160)
    {
        var factor = DisplayFactor;
        var x = ImageBounds.X;
        var y = ImageBounds.Y;
        if (ElementComposition.GetElementVisual(ActiveImage) is not { } visual)
        {
            ActiveImage.RenderTransformOrigin = RelativePoint.TopLeft;
            ActiveImage.RenderTransform = new MatrixTransform(Matrix.CreateScale(factor, factor) * Matrix.CreateTranslation(x, y));
            return;
        }

        ActiveImage.RenderTransform = null;
        var scale = new Vector3D(factor, factor, 1);
        var translation = new Vector3D(x, y, 0);
        if (!animate || AlderMotion.GetReduced(this))
        {
            visual.StopAnimation(nameof(CompositionVisual.Scale));
            visual.StopAnimation(nameof(CompositionVisual.Translation));
            visual.Scale = scale;
            visual.Translation = translation;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(milliseconds);
        var previousScale = previousBounds is { } initial ? initial.Width / PixelSize.Width : (double?)null;
        AnimateVector(visual, nameof(CompositionVisual.Scale), duration, scale,
            previousScale is { } value ? new Vector3D(value, value, 1) : null);
        AnimateVector(visual, nameof(CompositionVisual.Translation), duration, translation,
            previousBounds is { } bounds ? new Vector3D(bounds.X, bounds.Y, 0) : null);
    }

    private static void AnimateVector(CompositionVisual visual, string property, TimeSpan duration,
        Vector3D to, Vector3D? from)
    {
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.Duration = duration;
        if (from is { } start) animation.InsertKeyFrame(0, start);
        animation.InsertKeyFrame(1, to, EaseOut);
        visual.StartAnimation(property, animation);
    }
}
