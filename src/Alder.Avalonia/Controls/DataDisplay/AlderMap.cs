using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A position in WGS 84 coordinates, expressed in degrees.</summary>
public readonly record struct MapCoordinate(double Latitude, double Longitude)
{
    internal bool IsFinite => double.IsFinite(Latitude) && double.IsFinite(Longitude);
}

/// <summary>A geographically positioned line or area. Closed paths can be filled.</summary>
public sealed record AlderMapPath(
    IReadOnlyList<MapCoordinate> Points, bool IsClosed = false, IBrush? Stroke = null, IBrush? Fill = null,
    double Thickness = 2, bool Curved = false)
{
    /// <summary>Fills the closed feature with the map's water color.</summary>
    public bool IsWater { get; init; }
}

/// <summary>A location with optional label, popup content, tint and heat weight.</summary>
public sealed record AlderMapMarker(
    MapCoordinate Location, string? Title = null, object? Detail = null, IBrush? Brush = null, double Weight = 1)
{
    /// <summary>Displays a short value in a pill at the location, for example a listing price.</summary>
    public string? Badge { get; init; }
}

/// <summary>A set of road lines with the same stroke width and zoom threshold.</summary>
public sealed record AlderMapRoadLayer(IReadOnlyList<IReadOnlyList<MapCoordinate>> Lines, double Width = 1, double MinimumZoom = 9);

/// <summary>A native Web Mercator viewport for host-supplied geography, routes and markers.</summary>
public sealed partial class AlderMap : TemplatedControl
{
    /// <summary>Identifies the <see cref="Center"/> property.</summary>
    public static readonly StyledProperty<MapCoordinate> CenterProperty =
        AvaloniaProperty.Register<AlderMap, MapCoordinate>(nameof(Center), new MapCoordinate(20, 0), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="Zoom"/> property.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<AlderMap, double>(nameof(Zoom), 2, defaultBindingMode: BindingMode.TwoWay, validate: value => double.IsFinite(value) && value is >= 0 and <= 22);
    /// <summary>Identifies the <see cref="Bearing"/> property.</summary>
    public static readonly StyledProperty<double> BearingProperty =
        AvaloniaProperty.Register<AlderMap, double>(nameof(Bearing), 0, defaultBindingMode: BindingMode.TwoWay, validate: double.IsFinite);
    /// <summary>Identifies the <see cref="Basemap"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<AlderMapPath>?> BasemapProperty =
        AvaloniaProperty.Register<AlderMap, IEnumerable<AlderMapPath>?>(nameof(Basemap));
    /// <summary>Identifies the <see cref="Paths"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<AlderMapPath>?> PathsProperty =
        AvaloniaProperty.Register<AlderMap, IEnumerable<AlderMapPath>?>(nameof(Paths));
    /// <summary>Identifies the <see cref="Markers"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<AlderMapMarker>?> MarkersProperty =
        AvaloniaProperty.Register<AlderMap, IEnumerable<AlderMapMarker>?>(nameof(Markers));
    /// <summary>Identifies the <see cref="SelectedMarker"/> property.</summary>
    public static readonly StyledProperty<AlderMapMarker?> SelectedMarkerProperty =
        AvaloniaProperty.Register<AlderMap, AlderMapMarker?>(nameof(SelectedMarker), null, defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ClusterMarkers"/> property.</summary>
    public static readonly StyledProperty<bool> ClusterMarkersProperty =
        AvaloniaProperty.Register<AlderMap, bool>(nameof(ClusterMarkers));
    /// <summary>Identifies the <see cref="ShowHeatmap"/> property.</summary>
    public static readonly StyledProperty<bool> ShowHeatmapProperty =
        AvaloniaProperty.Register<AlderMap, bool>(nameof(ShowHeatmap));
    /// <summary>Identifies the <see cref="ShowLabels"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<AlderMap, bool>(nameof(ShowLabels), true);
    /// <summary>Identifies the <see cref="ShowControls"/> property.</summary>
    public static readonly StyledProperty<bool> ShowControlsProperty =
        AvaloniaProperty.Register<AlderMap, bool>(nameof(ShowControls), true);
    /// <summary>Identifies the <see cref="IsInteractive"/> property.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<AlderMap, bool>(nameof(IsInteractive), true);
    /// <summary>Identifies the <see cref="LandBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LandBrushProperty =
        AvaloniaProperty.Register<AlderMap, IBrush?>(nameof(LandBrush));
    /// <summary>Identifies the <see cref="CoastlineBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CoastlineBrushProperty =
        AvaloniaProperty.Register<AlderMap, IBrush?>(nameof(CoastlineBrush));
    /// <summary>Identifies the <see cref="LabelBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<AlderMap, IBrush?>(nameof(LabelBrush));
    /// <summary>Identifies the <see cref="MarkerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MarkerBrushProperty =
        AvaloniaProperty.Register<AlderMap, IBrush?>(nameof(MarkerBrush));
    /// <summary>Identifies the <see cref="MarkerPopupTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> MarkerPopupTemplateProperty =
        AvaloniaProperty.Register<AlderMap, IDataTemplate?>(nameof(MarkerPopupTemplate));
    /// <summary>Identifies the <see cref="MarkerCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> MarkerCommandProperty =
        AvaloniaProperty.Register<AlderMap, ICommand?>(nameof(MarkerCommand));
    /// <summary>Identifies the <see cref="LocateCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> LocateCommandProperty =
        AvaloniaProperty.Register<AlderMap, ICommand?>(nameof(LocateCommand));
    /// <summary>Identifies the <see cref="FullscreenCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> FullscreenCommandProperty =
        AvaloniaProperty.Register<AlderMap, ICommand?>(nameof(FullscreenCommand));
    /// <summary>Identifies the <see cref="Attribution"/> property.</summary>
    public static readonly StyledProperty<string?> AttributionProperty =
        AvaloniaProperty.Register<AlderMap, string?>(nameof(Attribution));
    /// <summary>Identifies the <see cref="OverlayContent"/> property.</summary>
    public static readonly StyledProperty<object?> OverlayContentProperty =
        AvaloniaProperty.Register<AlderMap, object?>(nameof(OverlayContent));
    /// <summary>Identifies the <see cref="EmptyContent"/> property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<AlderMap, object?>(nameof(EmptyContent));

    /// <summary>Gets or sets the center.</summary>
    public MapCoordinate Center { get => GetValue(CenterProperty); set => SetValue(CenterProperty, value); }
    /// <summary>Gets or sets the zoom.</summary>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    /// <summary>Gets or sets the bearing.</summary>
    public double Bearing { get => GetValue(BearingProperty); set => SetValue(BearingProperty, value); }
    /// <summary>Gets or sets the basemap.</summary>
    public IEnumerable<AlderMapPath>? Basemap { get => GetValue(BasemapProperty); set => SetValue(BasemapProperty, value); }
    /// <summary>Gets or sets the paths.</summary>
    public IEnumerable<AlderMapPath>? Paths { get => GetValue(PathsProperty); set => SetValue(PathsProperty, value); }
    /// <summary>Gets or sets the markers.</summary>
    public IEnumerable<AlderMapMarker>? Markers { get => GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    /// <summary>Gets or sets the selected marker.</summary>
    public AlderMapMarker? SelectedMarker { get => GetValue(SelectedMarkerProperty); set => SetValue(SelectedMarkerProperty, value); }
    /// <summary>Gets or sets the cluster markers.</summary>
    public bool ClusterMarkers { get => GetValue(ClusterMarkersProperty); set => SetValue(ClusterMarkersProperty, value); }
    /// <summary>Gets or sets the show heatmap.</summary>
    public bool ShowHeatmap { get => GetValue(ShowHeatmapProperty); set => SetValue(ShowHeatmapProperty, value); }
    /// <summary>Gets or sets the show labels.</summary>
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <summary>Gets or sets the show controls.</summary>
    public bool ShowControls { get => GetValue(ShowControlsProperty); set => SetValue(ShowControlsProperty, value); }
    /// <summary>Gets or sets the is interactive.</summary>
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }
    /// <summary>Gets or sets the land brush.</summary>
    public IBrush? LandBrush { get => GetValue(LandBrushProperty); set => SetValue(LandBrushProperty, value); }
    /// <summary>Gets or sets the coastline brush.</summary>
    public IBrush? CoastlineBrush { get => GetValue(CoastlineBrushProperty); set => SetValue(CoastlineBrushProperty, value); }
    /// <summary>Gets or sets the label brush.</summary>
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    /// <summary>Gets or sets the marker brush.</summary>
    public IBrush? MarkerBrush { get => GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    /// <summary>Gets or sets the marker popup template.</summary>
    public IDataTemplate? MarkerPopupTemplate { get => GetValue(MarkerPopupTemplateProperty); set => SetValue(MarkerPopupTemplateProperty, value); }
    /// <summary>Gets or sets the marker command.</summary>
    public ICommand? MarkerCommand { get => GetValue(MarkerCommandProperty); set => SetValue(MarkerCommandProperty, value); }
    /// <summary>Gets or sets the locate command.</summary>
    public ICommand? LocateCommand { get => GetValue(LocateCommandProperty); set => SetValue(LocateCommandProperty, value); }
    /// <summary>Gets or sets the fullscreen command.</summary>
    public ICommand? FullscreenCommand { get => GetValue(FullscreenCommandProperty); set => SetValue(FullscreenCommandProperty, value); }
    /// <summary>Gets or sets the attribution.</summary>
    public string? Attribution { get => GetValue(AttributionProperty); set => SetValue(AttributionProperty, value); }
    /// <summary>Gets or sets the overlay content.</summary>
    public object? OverlayContent { get => GetValue(OverlayContentProperty); set => SetValue(OverlayContentProperty, value); }
    /// <summary>Gets or sets the empty content.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    /// <summary>Shows the compass button and current bearing.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowCompass { get; set; }
    /// <summary>Shows the pair of zoom buttons.</summary>
    [GeneratedStyledProperty(DefaultValue = true)]
    public partial bool ShowZoomControls { get; set; }
    /// <summary>The blur radius in DIPs behind the zoom button group. Zero disables the effect.</summary>
    [GeneratedStyledProperty(ValidateMethodName = nameof(IsValidZoomControlsBackdropBlurRadius))]
    public partial double ZoomControlsBackdropBlurRadius { get; set; }
    /// <summary>Changes the fullscreen control's icon and label to indicate exit.</summary>
    [GeneratedStyledProperty]
    public partial bool IsFullscreen { get; set; }
    /// <summary>Lets the parent scroll until the map has keyboard focus or the zoom modifier is held.</summary>
    [GeneratedStyledProperty]
    public partial bool WheelZoomRequiresFocus { get; set; }
    /// <summary>Sets the horizontal position of the map's supplemental content.</summary>
    [GeneratedStyledProperty(DefaultValue = HorizontalAlignment.Left)]
    public partial HorizontalAlignment OverlayHorizontalAlignment { get; set; }
    /// <summary>Sets the vertical position of the map's supplemental content.</summary>
    [GeneratedStyledProperty(DefaultValue = VerticalAlignment.Top)]
    public partial VerticalAlignment OverlayVerticalAlignment { get; set; }
    /// <summary>Describes how to present the map's supplemental content.</summary>
    [GeneratedStyledProperty]
    public partial IDataTemplate? OverlayContentTemplate { get; set; }
    /// <summary>Themed surface behind a marker badge.</summary>
    [GeneratedStyledProperty]
    public partial IBrush? MarkerBadgeBrush { get; set; }

    private static bool IsValidZoomControlsBackdropBlurRadius(double radius) => double.IsFinite(radius) && radius >= 0;

    private sealed record Hit(Point Position, Rect Bounds, IReadOnlyList<AlderMapMarker> Markers);
    private sealed record Flight(Point Start, Point End, double StartZoom, double EndZoom)
    {
        public TimeSpan? StartTime { get; set; }
    }

    private static readonly RadialGradientBrush Heat = new()
    {
        GradientStops =
        [
            new(Color.FromArgb(225, 249, 106, 53), 0),
            new(Color.FromArgb(160, 248, 167, 77), .34),
            new(Color.FromArgb(76, 67, 152, 252), .72),
            new(Colors.Transparent, 1)
        ]
    };

    private static readonly FuncDataTemplate<string> PopupTextTemplate = new((text, _) =>
        new AlderTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 17 });

    private readonly List<Hit> hits = [];
    private readonly Dictionary<IPointer, Point> contacts = new();
    private readonly HashSet<INotifyCollectionChanged> sources = [];
    private Border? markerPopup;
    private TextBlock? popupTitle;
    private ContentPresenter? popupContent;
    private readonly RotateTransform compassRotation = new();
    private Flight? flight;
    private int focusedMarker = -1;
    private bool moved;
    private bool rotating;
    private double travel;

    static AlderMap() => AffectsRender<AlderMap>(BackgroundProperty, CenterProperty, ZoomProperty, BearingProperty,
        BasemapProperty, PathsProperty, MarkersProperty, SelectedMarkerProperty, LandBrushProperty, CoastlineBrushProperty,
        MarkerBrushProperty, LabelBrushProperty, MarkerBadgeBrushProperty, ClusterMarkersProperty, ShowHeatmapProperty,
        ShowLabelsProperty, CornerRadiusProperty, RoadLayersProperty, RoadBrushProperty);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        markerPopup = e.NameScope.Find<Border>("PART_MarkerPopup");
        popupTitle = e.NameScope.Find<TextBlock>("PART_PopupTitle");
        popupContent = e.NameScope.Find<ContentPresenter>("PART_PopupContent");
        if (e.NameScope.Find<Control>("PART_Compass") is { } compass) compass.RenderTransform = compassRotation;
        compassRotation.Angle = Bearing;
        Attach(e, "PART_ZoomIn", () => AnimateZoomAt(1, new Rect(Bounds.Size).Center));
        Attach(e, "PART_ZoomOut", () => AnimateZoomAt(-1, new Rect(Bounds.Size).Center));
        Attach(e, "PART_ResetBearing", () => SetCurrentValue(BearingProperty, 0));
        Attach(e, "PART_ClosePopup", () =>
        {
            SetCurrentValue(SelectedMarkerProperty, null);
            Focus();
        });
        RefreshPopupContent();
    }

    private static void Attach(TemplateAppliedEventArgs e, string name, Action action)
    {
        if (e.NameScope.Find<Button>(name) is { } button)
            button.Click += (_, _) => action();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachSources();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachSources();
        flight = null;
        contacts.Clear();
        hits.Clear();
        ResetClusters();
        clusterFrameQueued = false;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BasemapProperty || change.Property == PathsProperty || change.Property == MarkersProperty)
        {
            focusedMarker = -1;
            if (this.IsAttachedToVisualTree()) AttachSources();
        }
        if (change.Property == RoadLayersProperty) projectedRoads = null;
        if (change.Property == MarkersProperty || change.Property == ClusterMarkersProperty || change.Property == ShowHeatmapProperty)
            ResetClusters();
        if (change.Property == BearingProperty) compassRotation.Angle = Bearing;
        if (change.Property == ZoomControlsBackdropBlurRadiusProperty)
            PseudoClasses.Set(":zoom-backdrop", ZoomControlsBackdropBlurRadius > 0);
        if (change.Property == SelectedMarkerProperty || change.Property == MarkerPopupTemplateProperty) RefreshPopupContent();
        if (change.Property == CenterProperty || change.Property == ZoomProperty || change.Property == BearingProperty)
            PositionPopup();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PositionPopup();
    }

    private void AttachSources()
    {
        DetachSources();
        foreach (var source in new object?[] { Basemap, Paths, Markers }.OfType<INotifyCollectionChanged>())
            if (sources.Add(source)) source.CollectionChanged += CollectionChanged;
    }

    private void DetachSources()
    {
        foreach (var source in sources) source.CollectionChanged -= CollectionChanged;
        sources.Clear();
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        focusedMarker = -1;
        if (ReferenceEquals(sender, Markers) && SelectedMarker is { } marker &&
            (e.Action == NotifyCollectionChangedAction.Reset || e.OldItems?.Contains(marker) == true) &&
            Markers?.Any(item => ReferenceEquals(item, marker)) != true)
            SetCurrentValue(SelectedMarkerProperty, null);
        InvalidateVisual();
    }

    /// <summary>Projects a location to a pixel position within this viewport.</summary>
    public Point LocationToPoint(MapCoordinate location) => GetViewportProjection().ProjectMarker(location);

    /// <summary>Converts a position within the viewport back to geographic coordinates.</summary>
    public MapCoordinate PointToLocation(Point point)
    {
        var offset = Rotate(point - new Rect(Bounds.Size).Center, -Bearing) / World;
        return Unproject(Project(Center) + offset);
    }

    /// <summary>Moves the camera to a location with a short ease-out animation.</summary>
    public void FlyTo(MapCoordinate center, double zoom)
    {
        if (!center.IsFinite || !double.IsFinite(zoom)) return;
        zoom = Math.Clamp(zoom, 0, 22);
        flight = null;
        if (AlderMotion.GetReduced(this) || TopLevel.GetTopLevel(this) is not { } root)
        {
            SetCamera(center, zoom);
            return;
        }

        var start = Project(Center);
        var end = Project(center);
        var active = new Flight(start, new Point(start.X + WrapDelta(end.X - start.X), end.Y), Zoom, zoom);
        flight = active;
        root.RequestAnimationFrame(Tick);

        void Tick(TimeSpan time)
        {
            if (flight != active) return;
            var began = active.StartTime ??= time;
            var progress = Math.Clamp((time - began).TotalMilliseconds / 240, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var coordinate = Unproject(active.Start + (active.End - active.Start) * eased);
            SetCamera(coordinate, active.StartZoom + (active.EndZoom - active.StartZoom) * eased);
            if (progress < 1) root.RequestAnimationFrame(Tick);
            else flight = null;
        }
    }

    private double World => 256 * Math.Pow(2, Zoom);

    private void SetCamera(MapCoordinate center, double zoom)
    {
        SetCurrentValue(CenterProperty, center);
        SetCurrentValue(ZoomProperty, zoom);
    }

    private void ZoomAt(double zoom, Point point)
    {
        flight = null;
        SetCamera(CenterForZoom(zoom, point), zoom);
    }

    private void AnimateZoomAt(double delta, Point point)
    {
        var zoom = Math.Clamp((flight?.EndZoom ?? Zoom) + delta, 0, 22);
        if (zoom != Zoom) FlyTo(CenterForZoom(zoom, point), zoom);
    }

    private MapCoordinate CenterForZoom(double zoom, Point point)
    {
        var anchored = Project(PointToLocation(point));
        var offset = Rotate(point - new Rect(Bounds.Size).Center, -Bearing) / (256 * Math.Pow(2, zoom));
        return Unproject(anchored - offset);
    }

    private void Pan(Vector offset)
    {
        flight = null;
        SetCurrentValue(CenterProperty, Unproject(Project(Center) - Rotate(offset, -Bearing) / World));
    }

    private static double WrapDelta(double value) => value - Math.Floor(value + .5);

    private static Point Project(MapCoordinate coordinate)
    {
        double latitude = (coordinate.IsFinite ? Math.Clamp(coordinate.Latitude, -85.05112878, 85.05112878) : 0) * Math.PI / 180;
        double longitude = coordinate.IsFinite ? WrapDelta(coordinate.Longitude / 360) : 0;
        var y = .5 - Math.Asinh(Math.Tan(latitude)) / (2 * Math.PI);
        return new Point(longitude + .5, Math.Clamp(y, 0, 1));
    }

    private static MapCoordinate Unproject(Point point)
    {
        var y = Math.Clamp(point.Y, 0, 1);
        return new MapCoordinate(Math.Atan(Math.Sinh(Math.PI - 2 * Math.PI * y)) * 180 / Math.PI, WrapDelta(point.X - .5) * 360);
    }

    private static Vector Rotate(Vector value, double degrees)
    {
        var (sine, cosine) = Math.SinCos(degrees * Math.PI / 180);
        return new Vector(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
    }

    private ViewportProjection GetViewportProjection()
    {
        var (sine, cosine) = Math.SinCos(Bearing * Math.PI / 180);
        return new ViewportProjection(Project(Center), new Rect(Bounds.Size).Center, World, sine, cosine);
    }

    private readonly record struct ViewportProjection(Point Center, Point Origin, double World, double Sine, double Cosine)
    {
        public Point ProjectMarker(MapCoordinate location) => ProjectWorldMarker(Project(location));

        public Point ProjectWorldMarker(Point world) => ToViewport(WrapDelta(world.X - Center.X), world.Y - Center.Y);

        public Point ToViewport(double longitudeOffset, double latitudeOffset)
        {
            double x = longitudeOffset * World;
            double y = latitudeOffset * World;
            return Origin + new Vector(x * Cosine - y * Sine, x * Sine + y * Cosine);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsInteractive || e.Source != this) return;
        var buttons = e.GetCurrentPoint(this).Properties;
        if (!buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed) return;
        if (contacts.Count == 0)
        {
            flight = null;
            moved = false;
            rotating = buttons.IsRightButtonPressed;
            travel = 0;
            Focus();
            if (e.ClickCount == 2 && !rotating && HitAt(e.GetPosition(this)) is null)
            {
                moved = true;
                AnimateZoomAt(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1, e.GetPosition(this));
            }
        }
        contacts[e.Pointer] = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (!contacts.TryGetValue(e.Pointer, out var previous))
        {
            PseudoClasses.Set(":marker-over", IsInteractive && e.Source == this && HitAt(position) is not null);
            return;
        }
        var delta = (Vector)(position - previous);
        contacts[e.Pointer] = position;
        travel += delta.Length;
        if (contacts.Count >= 2)
        {
            moved = true;
            var second = contacts.First(entry => entry.Key != e.Pointer).Value;
            double distance = ((Vector)(previous - second)).Length;
            double ratio = ((Vector)(position - second)).Length / Math.Max(1, distance);
            var center = second + (previous - second) / 2;
            if (ratio > 0) ZoomAt(Math.Clamp(Zoom + Math.Log2(ratio), 0, 22), center);
            var before = (Vector)(previous - second);
            var after = (Vector)(position - second);
            var rotation = Math.Atan2(after.Y, after.X) - Math.Atan2(before.Y, before.X);
            SetCurrentValue(BearingProperty, NormalizeBearing(Bearing + rotation * 180 / Math.PI));
            Pan(delta / 2);
        }
        else if (travel > 3)
        {
            moved = true;
            PseudoClasses.Set(":panning", true);
            if (rotating) SetCurrentValue(BearingProperty, NormalizeBearing(Bearing + delta.X * .55));
            else Pan(delta);
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!contacts.Remove(e.Pointer)) return;
        if (e.Pointer.Captured == this) e.Pointer.Capture(null);
        if (contacts.Count == 0)
        {
            PseudoClasses.Set(":panning", false);
            var point = e.GetPosition(this);
            var hit = HitAt(point);
            PseudoClasses.Set(":marker-over", hit is not null);
            if (!moved && !rotating)
            {
                if (hit is not null) OpenHit(hit);
                else SetCurrentValue(SelectedMarkerProperty, null);
            }
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        contacts.Remove(e.Pointer);
        if (contacts.Count == 0) PseudoClasses.Set(":panning", false);
        base.OnPointerCaptureLost(e);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (!IsInteractive || e.Source != this || e.Delta.Y == 0 ||
            (WheelZoomRequiresFocus && !IsKeyboardFocusWithin && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)) return;
        ZoomAt(Math.Clamp(Zoom + e.Delta.Y * .4, 0, 22), e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsInteractive || e.Source != this)
        {
            base.OnKeyDown(e);
            return;
        }

        var delta = e.Key switch
        {
            Key.Up => new Vector(0, 36), Key.Down => new Vector(0, -36),
            Key.Left => new Vector(36, 0), Key.Right => new Vector(-36, 0), _ => default
        };
        if (delta != default) Pan(delta);
        else if (e.Key == Key.Add || e.Key == Key.OemPlus) AnimateZoomAt(1, new Rect(Bounds.Size).Center);
        else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) AnimateZoomAt(-1, new Rect(Bounds.Size).Center);
        else if ((e.Key is Key.Enter or Key.Space) && hits.Count > 0)
        {
            focusedMarker = (focusedMarker + 1) % hits.Count;
            OpenHit(hits[focusedMarker]);
        }
        else if (e.Key == Key.Escape && !IsFullscreen && SelectedMarker is not null)
            SetCurrentValue(SelectedMarkerProperty, null);
        else
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
    }

    private static double NormalizeBearing(double bearing) => (bearing % 360 + 540) % 360 - 180;

    private Hit? HitAt(Point position) => hits.LastOrDefault(hit => hit.Bounds.Contains(position));

    private void OpenHit(Hit hit)
    {
        if (hit.Markers.Count > 1)
        {
            FlyTo(PointToLocation(hit.Position), Math.Min(22, Zoom + 1.75));
            return;
        }
        var marker = hit.Markers[0];
        var next = ReferenceEquals(marker, SelectedMarker) ? null : marker;
        SetCurrentValue(SelectedMarkerProperty, next);
        if (MarkerCommand?.CanExecute(marker) == true) MarkerCommand.Execute(marker);
    }

    private void RefreshPopupContent()
    {
        if (popupTitle is not { } title || popupContent is not { } content) return;
        var marker = SelectedMarker;
        title.Text = marker?.Title;
        title.IsVisible = !string.IsNullOrWhiteSpace(title.Text);
        content.ContentTemplate = MarkerPopupTemplate ?? (marker?.Detail is string ? PopupTextTemplate : null);
        content.Content = MarkerPopupTemplate is null ? marker?.Detail : marker;
        AutomationProperties.SetHelpText(this, marker?.Title ?? string.Empty);
        PositionPopup();
    }

    private void PositionPopup()
    {
        if (markerPopup is not { } frame) return;
        if (SelectedMarker is not { } marker ||
            (MarkerPopupTemplate is null && marker.Title is null && marker.Detail is null) ||
            Bounds.Width <= 16 || Bounds.Height <= 16)
        {
            frame.IsVisible = false;
            return;
        }
        var point = LocationToPoint(marker.Location);
        frame.IsVisible = new Rect(Bounds.Size).Contains(point);
        if (!frame.IsVisible) return;
        frame.MaxWidth = Math.Min(244, Bounds.Width - 16);
        frame.Measure(new Size(frame.MaxWidth, Bounds.Height));
        var size = frame.DesiredSize;
        const double inset = 8;
        var x = Math.Clamp(point.X - size.Width / 2, inset, Math.Max(inset, Bounds.Width - size.Width - inset));
        var y = point.Y + 16;
        if (y + size.Height > Bounds.Height - inset) y = point.Y - 16 - size.Height;
        Canvas.SetLeft(frame, x);
        Canvas.SetTop(frame, Math.Max(inset, y));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var outer = new Rect(Bounds.Size);
        // Capture the camera once so all layers use the same scale and rotation for this frame.
        var projection = GetViewportProjection();
        using var clip = context.PushClip(new RoundedRect(outer, CornerRadius));
        if (Background is { } background) context.FillRectangle(background, outer);
        if (Basemap is { } layers)
            foreach (var path in layers) DrawPath(context, path, outer, projection, true);
        DrawRoads(context, projection);
        if (Paths is { } features)
            foreach (var path in features) DrawPath(context, path, outer, projection, false);
        DrawMarkers(context, outer, projection);
    }

    private const double PathCoordinateScale = 65536;
    private readonly ConditionalWeakTable<AlderMapPath, ProjectedPath> projectedPaths = new();

    private void DrawPath(DrawingContext context, AlderMapPath path, Rect viewport, in ViewportProjection projection, bool land)
    {
        var cached = projectedPaths.GetValue(path, static _ => new());
        cached.Update(path);
        if (cached.Geometry is not { } geometry) return;

        var origin = projection.ProjectWorldMarker(cached.Origin);
        double scale = projection.World / PathCoordinateScale;
        double cosine = projection.Cosine * scale, sine = projection.Sine * scale;
        var transform = new Matrix(cosine, sine, -sine, cosine, origin.X, origin.Y);
        if (!cached.Bounds.TransformToAABB(transform).Inflate(Math.Max(32, path.Thickness)).Intersects(viewport)) return;
        var fill = path.IsClosed ? path.Fill ?? (path.IsWater ? Background : LandBrush) : null;
        var stroke = path.Stroke ?? (land ? CoastlineBrush : MarkerBrush);
        // Keep relative gradients and other non-solid brushes in viewport coordinates.
        if (fill is not (null or ISolidColorBrush) || stroke is not (null or ISolidColorBrush))
        {
            var transformed = geometry.Clone();
            transformed.Transform = new MatrixTransform(transform);
            context.DrawGeometry(fill, cached.GetPen(stroke, path.Thickness), transformed);
            return;
        }
        using var placement = context.PushTransform(transform);
        context.DrawGeometry(fill, cached.GetPen(stroke, path.Thickness / scale), geometry);
    }

    private sealed class ProjectedPath
    {
        private MapCoordinate[] coordinates = [];
        private Pen? pen;
        public StreamGeometry? Geometry { get; private set; }
        public Point Origin { get; private set; }
        public Rect Bounds { get; private set; }

        public Pen? GetPen(IBrush? brush, double thickness)
        {
            if (brush is null || !(thickness > 0)) return null;
            if (pen is null || !ReferenceEquals(pen.Brush, brush) || thickness != pen.Thickness)
                pen = new Pen(brush, thickness);
            return pen;
        }

        public void Update(AlderMapPath path)
        {
            // Callers can mutate the supplied list in place. Check its coordinates
            // before reusing world geometry across camera changes.
            if (HasSameCoordinates(path.Points)) return;
            coordinates = path.Points.ToArray();
            if (coordinates.Length < 2 || Array.Exists(coordinates, static point => !point.IsFinite))
            {
                Geometry = null;
                return;
            }

            Origin = Project(coordinates[0]);
            double longitude = Origin.X;
            var previous = new Point();
            var geometry = new StreamGeometry();
            using (var draw = geometry.Open())
            {
                draw.BeginFigure(previous, path.IsClosed);
                for (int i = 1; i < coordinates.Length; i++)
                {
                    var point = Project(coordinates[i]);
                    longitude += WrapDelta(point.X - longitude);
                    var next = new Point((longitude - Origin.X) * PathCoordinateScale, (point.Y - Origin.Y) * PathCoordinateScale);
                    if (path.Curved && !path.IsClosed)
                    {
                        var delta = next - previous;
                        draw.QuadraticBezierTo(previous + delta * .5 + new Vector(delta.Y, -delta.X) * .24, next);
                    }
                    else draw.LineTo(next);
                    previous = next;
                }
                draw.EndFigure(path.IsClosed);
            }
            Geometry = geometry;
            Bounds = geometry.Bounds;
        }

        private bool HasSameCoordinates(IReadOnlyList<MapCoordinate> points)
        {
            if (points is MapCoordinate[] array) return coordinates.AsSpan().SequenceEqual(array);
            if (points.Count != coordinates.Length) return false;
            for (int i = 0; i < coordinates.Length; i++)
                if (coordinates[i] != points[i]) return false;
            return true;
        }
    }

    private void DrawMarkers(DrawingContext context, Rect outer, ViewportProjection projection)
    {
        hits.Clear();
        if (Markers is not { } markers) return;
        if (ClusterMarkers && !ShowHeatmap)
        {
            DrawClusters(context, outer, projection, markers);
            return;
        }
        var visibleBounds = outer.Inflate(ClusterMarkers ? ClusterCellSize : 22);
        var visible = markers.Where(marker => marker.Location.IsFinite)
            .Select(marker => (Marker: marker, Point: projection.ProjectMarker(marker.Location)))
            .Where(entry => visibleBounds.Contains(entry.Point)).ToArray();

        if (ShowHeatmap)
        {
            foreach (var (marker, point) in visible)
            {
                if (!double.IsFinite(marker.Weight) || marker.Weight <= 0) continue;
                var radius = Math.Clamp(marker.Weight * 31, 14, 86);
                using var opacity = context.PushOpacity(Math.Clamp(marker.Weight * .55, .12, .85));
                context.DrawEllipse(Heat, null, point, radius, radius);
            }
            return;
        }

        foreach (var (marker, point) in visible) DrawMarker(context, point, [marker]);
    }

    private void DrawMarker(DrawingContext context, Point point, IReadOnlyList<AlderMapMarker> markers, bool hitTarget = true)
    {
        var marker = markers[0];
        var fill = marker.Brush ?? MarkerBrush ?? Brushes.DodgerBlue;
        bool cluster = markers.Count > 1;
        bool selected = ReferenceEquals(marker, SelectedMarker);
        var font = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);
        if (!cluster && !string.IsNullOrWhiteSpace(marker.Badge))
        {
            var foreground = selected ? Brushes.White : LabelBrush ?? Brushes.Black;
            var badge = new FormattedText(marker.Badge, CultureInfo.CurrentCulture, FlowDirection, font, 12, foreground);
            var box = new Rect(point.X - badge.Width / 2 - 8, point.Y - 12, badge.Width + 16, 24);
            context.DrawRectangle(selected ? fill : MarkerBadgeBrush, new Pen(fill, 1), box, 12, 12);
            context.DrawText(badge, new Point(box.Center.X - badge.Width / 2, box.Center.Y - badge.Height / 2));
            if (hitTarget) hits.Add(new Hit(point, box.Inflate(2), markers));
            return;
        }

        double radius = cluster ? Math.Clamp(10 + Math.Sqrt(markers.Count), 12, 23) : 5.5;
        if (selected || cluster)
        {
            using var opacity = context.PushOpacity(.24);
            context.DrawEllipse(fill, null, point, radius + 4, radius + 4);
        }
        var hitBounds = new Rect(point.X - radius - 4, point.Y - radius - 4, (radius + 4) * 2, (radius + 4) * 2);
        context.DrawEllipse(fill, new Pen(Brushes.White, 1.5), point, radius, radius);
        var label = cluster ? markers.Count.ToString(CultureInfo.CurrentCulture) : ShowLabels ? marker.Title : null;
        if (!string.IsNullOrWhiteSpace(label))
        {
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection, font, 11,
                cluster ? Brushes.White : LabelBrush ?? Brushes.Black);
            var y = cluster ? point.Y - text.Height / 2 : point.Y - radius - text.Height - 3;
            var location = new Point(point.X - text.Width / 2, y);
            context.DrawText(text, location);
            hitBounds = hitBounds.Union(new Rect(location, new Size(text.Width, text.Height)));
        }
        if (hitTarget) hits.Add(new Hit(point, hitBounds, markers));
    }
}
