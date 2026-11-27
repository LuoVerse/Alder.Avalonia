using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Platform;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed record MapSceneMetric(string Value, string Label);
public sealed record MapSceneRow(string Title, string Value, IBrush Tint, ICommand? Command = null);

public sealed class MapScene : GalleryViewModel
{
    private readonly MapCoordinate home;
    private readonly double homeZoom;
    private readonly string titleKey;
    private readonly string subtitleKey;
    private int waypoint;

    internal MapScene(string titleKey, string subtitleKey, MapCoordinate home, double zoom, IReadOnlyList<AlderMapPath> basemap,
        IReadOnlyList<AlderMapRoadLayer> roads, IEnumerable<AlderMapMarker> markers, params AlderMapPath[] paths)
    {
        this.titleKey = titleKey;
        this.subtitleKey = subtitleKey;
        this.home = home;
        homeZoom = zoom;
        Center = home;
        Zoom = zoom;
        Basemap = basemap;
        Roads = roads;
        Markers = new(markers);
        Paths = paths;
        Recenter = ReactiveCommand.Create(() => { Center = home; Zoom = homeZoom; });
        Advance = ReactiveCommand.Create(() =>
        {
            if (Markers.Count == 0 || Paths.FirstOrDefault()?.Points is not { Count: > 0 } points) return;
            waypoint = (waypoint + 1) % points.Count;
            var followPopup = ReferenceEquals(SelectedMarker, Markers[0]);
            Markers[0] = Markers[0] with { Location = points[waypoint] };
            if (followPopup) SelectedMarker = Markers[0];
            Center = points[waypoint];
            Arrival = Format("ProMapArrivalMinutes", Math.Max(2, 9 - waypoint * 2));
        });
        Fullscreen = ReactiveCommand.CreateFromTask(OpenFullscreenAsync);
    }

    public string Title => Text(titleKey);
    public string Subtitle => Text(subtitleKey);
    public string Attribution => Text(Roads.Count == 0 ? "ProMapAttribution" : "ProMapCityAttribution");
    public MapCoordinate Center { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public double Zoom { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public AlderMapMarker? SelectedMarker { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? Arrival { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IReadOnlyList<AlderMapPath> Basemap { get; }
    public IReadOnlyList<AlderMapRoadLayer> Roads { get; }
    public AlderMapPath[] Paths { get; }
    public ObservableCollection<AlderMapMarker> Markers { get; }
    public ICommand Recenter { get; }
    public ICommand Advance { get; }
    public ICommand Fullscreen { get; }
    public IReadOnlyList<MapSceneMetric> Metrics { get; internal set; } = [];
    public IReadOnlyList<MapSceneRow> Rows { get; internal set; } = [];
    public bool HasMetrics => Metrics.Count > 0;
    public bool HasRows => Rows.Count > 0;
    public bool HasArrival => Arrival is not null;
    public bool HasHeatLegend { get; internal set; }
    public bool CanAdvance { get; internal set; }
    public bool Clusters { get; internal set; }
    public bool Labels { get; internal set; } = true;
    public bool ZoomControls { get; internal set; } = true;
    public bool Compass { get; internal set; } = true;

    public bool BlurZoomControls
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ZoomControlsBackdropBlurRadius));
        }
    }

    public double ZoomControlsBackdropBlurRadius => BlurZoomControls ? 16 : 0;

    internal MapSceneRow Place(AlderMapMarker marker, string value, IBrush tint) =>
        new(marker.Title ?? string.Empty, value, tint, ReactiveCommand.Create(() =>
        {
            Center = marker.Location;
            SelectedMarker = marker;
        }));

    private async Task OpenFullscreenAsync()
    {
        var map = new AlderMap
        {
            Basemap = Basemap, RoadLayers = Roads, Paths = Paths, Markers = Markers, ShowHeatmap = HasHeatLegend,
            ClusterMarkers = Clusters, ShowLabels = Labels, Attribution = Attribution, CornerRadius = new CornerRadius(0),
            IsFullscreen = true
        };
        map.FullscreenCommand = ReactiveCommand.Create(() => Dialog.Close(map));
        map.ShowCompass = Compass;
        map.ShowZoomControls = ZoomControls;
        map.ZoomControlsBackdropBlurRadius = ZoomControlsBackdropBlurRadius;
        map.Bind(AlderMap.CenterProperty, CompiledBinding.Create((MapScene scene) => scene.Center, source: this));
        map.Bind(AlderMap.ZoomProperty, CompiledBinding.Create((MapScene scene) => scene.Zoom, source: this));
        map.Bind(AlderMap.SelectedMarkerProperty, CompiledBinding.Create((MapScene scene) => scene.SelectedMarker, source: this));
        await Dialog.ShowModalAsync(map, new DialogOptions { Size = DialogSize.Full, IsContentFullBleed = true, ShowCloseButton = false });
    }
}

public sealed class MapViewModel : GalleryViewModel
{
    private sealed record CityMap(IReadOnlyList<AlderMapPath> Basemap, IReadOnlyList<AlderMapRoadLayer> Roads);

    public MapViewModel()
    {
        using var landDocument = LoadMap("NaturalEarthLand");
        using var cityDocument = LoadMap("TigerCityMaps");
        var world = ReadLand(landDocument.RootElement.GetProperty("world"));
        var bay = ReadLand(landDocument.RootElement.GetProperty("bay"));
        var cities = cityDocument.RootElement.GetProperty("cities");
        var sanFrancisco = ReadCity(cities.GetProperty("sanFrancisco"), bay);
        var seattle = ReadCity(cities.GetProperty("seattle"), world);
        var portland = ReadCity(cities.GetProperty("portland"), world);
        var austin = ReadCity(cities.GetProperty("austin"), world);
        var newYork = ReadCity(cities.GetProperty("newYork"), world);
        var earth = new CityMap(world, []);

        var blue = Brush.Parse("#1976E3");
        var violet = Brush.Parse("#8056CC");
        var green = Brush.Parse("#10855B");
        var orange = Brush.Parse("#D86927");
        var red = Brush.Parse("#D2424F");

        var start = Marker(new(37.7997, -122.3981), "ProMapRouteStart", "ProMapRouteStartDetail", green);
        var stop = Marker(new(37.7915, -122.3913), "ProMapRouteStop", "ProMapRouteStopDetail", blue);
        var end = Marker(new(37.7728, -122.3881), "ProMapRouteEnd", "ProMapRouteEndDetail", violet);
        var route = new AlderMapPath([start.Location, new(37.7956, -122.3931), stop.Location, new(37.7869, -122.3892),
            new(37.7823, -122.3909), new(37.7778, -122.3894), end.Location], Stroke: blue, Thickness: 3.5);
        Usage = Scene("Usage", new(37.7855, -122.3933), 13.7, sanFrancisco, [start, stop, end], route);
        Usage.SelectedMarker = stop;
        Usage.Metrics = [Metric(Format("ProMapArrivalMinutes", 7), "Eta"), Metric("3", "Stops"), Metric("3.7 km", "Distance")];

        Visitors = Scene("Visitors", new(24, -18), 1.5, earth,
            Clouds([new(40.71, -74.01), new(51.5, -.12), new(35.68, 139.69), new(-33.9, 151.2), new(-23.55, -46.63)], 17, 5, violet));
        Visitors.Clusters = true;
        Visitors.Labels = false;
        Visitors.ZoomControls = Visitors.Compass = false;

        var homes = new[]
        {
            Marker(new(37.7534, -122.4334), "ProMapRent1", "ProMapSaleDetail1", violet) with { Badge = "$1.28M" },
            Marker(new(37.7605, -122.4216), "ProMapRent2", "ProMapSaleDetail2", violet) with { Badge = "$895K" },
            Marker(new(37.7760, -122.4327), "ProMapRent3", "ProMapSaleDetail3", violet) with { Badge = "$1.05M" },
            Marker(new(37.8028, -122.4394), "ProMapSale4", "ProMapSaleDetail4", violet) with { Badge = "$2.4M" },
            Marker(new(37.7824, -122.4071), "ProMapSale5", "ProMapSaleDetail5", violet) with { Badge = "$650K" },
            Marker(new(37.7794, -122.4496), "ProMapSale6", "ProMapSaleDetail6", violet) with { Badge = "$1.18M" }
        };
        Properties = Scene("Properties", new(37.7772, -122.4275), 13.55, sanFrancisco, homes);
        Properties.Compass = false;
        Properties.SelectedMarker = homes[1];
        Properties.Rows = homes.Select(marker => Properties.Place(marker, marker.Badge!, violet)).ToArray();

        var ride = new AlderMapPath([new(37.7811, -122.4164), new(37.7853, -122.4103), new(37.7892, -122.4053),
            new(37.7882, -122.3987), new(37.7913, -122.3965)], Stroke: blue, Thickness: 3.5);
        Tracking = Scene("Tracking", new(37.7875, -122.405), 14.3, sanFrancisco,
            [Marker(ride.Points[0], "ProMapVehicle", "ProMapVehicleDetail", blue), Marker(ride.Points[^1], "ProMapRouteEnd", "ProMapRouteEndDetail", green)], ride);
        Tracking.Arrival = Format("ProMapArrivalMinutes", 9);
        Tracking.CanAdvance = true;

        var stores = new[]
        {
            Marker(new(45.5285, -122.6836), "ProMapStore1", "ProMapStoreDetail", blue),
            Marker(new(45.5180, -122.6788), "ProMapStore2", "ProMapStoreDetail", green),
            Marker(new(45.5265, -122.6546), "ProMapStore3", "ProMapStoreDetail", orange),
            Marker(new(45.5589, -122.6432), "ProMapStore4", "ProMapStoreDetail", violet)
        };
        Stores = Scene("Stores", new(45.536, -122.665), 13.45, portland, stores);
        Stores.Compass = false;
        Stores.SelectedMarker = stores[0];
        Stores.Rows = stores.Select((marker, index) => Stores.Place(marker, new[] { "0.8 mi", "1.6 mi", "2.4 mi", "3.1 mi" }[index], marker.Brush!)).ToArray();

        var dispatchRoute = new AlderMapPath([new(47.6093, -122.3484), new(47.6137, -122.3437), new(47.6153, -122.3389),
            new(47.6171, -122.3343), new(47.6258, -122.3303)], Stroke: blue, Thickness: 3);
        Fleet = Scene("Fleet", new(47.6128, -122.3349), 13.6, seattle,
            [Marker(dispatchRoute.Points[0], "ProMapVehicle", "ProMapVehicleDetail", blue),
             Marker(new(47.6213, -122.3504), "ProMapVehicle2", "ProMapVehicleDetail", green),
             Marker(new(47.6045, -122.3269), "ProMapRouteEnd", "ProMapRouteEndDetail", orange)], dispatchRoute);
        Fleet.Metrics = [Metric("18", "Active"), Metric("92%", "Sla"), Metric("4", "Holds")];
        Fleet.CanAdvance = true;
        Fleet.SelectedMarker = Fleet.Markers[0];

        var north = Marker(new(30.2839, -97.7563), "ProMapZoneNorth", "ProMapCoverageHint", blue) with { Badge = "84%" };
        var core = Marker(new(30.267, -97.7376), "ProMapZoneCore", "ProMapCoverageHint", green) with { Badge = "96%" };
        var south = Marker(new(30.2506, -97.7521), "ProMapZoneSouth", "ProMapCoverageHint", orange) with { Badge = "71%" };
        Coverage = Scene("Coverage", new(30.267, -97.745), 12.8, austin, [north, core, south],
            Zone(north.Location, blue, "#321976E3"), Zone(core.Location, green, "#3210855B"), Zone(south.Location, orange, "#32D86927"));
        Coverage.Rows = new[] { north, core, south }.Select(marker => Coverage.Place(marker, marker.Badge!, marker.Brush!)).ToArray();

        Heatmap = Scene("Heatmap", new(40.736, -73.9901), 13.6, newYork,
            Clouds([new(40.733, -73.995), new(40.762, -73.981), new(40.716, -74.003), new(40.702, -73.977)], 31, .026, orange));
        Heatmap.HasHeatLegend = true;
        Heatmap.Compass = false;

        var sfo = new AlderMapMarker(new(37.62, -122.39), "SFO");
        var jfk = new AlderMapMarker(new(40.64, -73.78), "JFK");
        var lhr = new AlderMapMarker(new(51.47, -.45), "LHR");
        var cdg = new AlderMapMarker(new(48.98, 2.55), "CDG");
        var hnd = new AlderMapMarker(new(35.55, 139.78), "HND");
        var sin = new AlderMapMarker(new(1.36, 103.99), "SIN");
        var syd = new AlderMapMarker(new(-33.94, 151.17), "SYD");
        var dxb = new AlderMapMarker(new(25.25, 55.36), "DXB");
        Flights = Scene("Flights", new(23, 7), 1.25, earth, [sfo, jfk, lhr, cdg, hnd, sin, syd, dxb],
            Arc(sfo, hnd, violet), Arc(hnd, sin, green), Arc(jfk, lhr, blue), Arc(lhr, dxb, orange), Arc(dxb, sin, blue), Arc(sin, syd, green));
        Flights.Metrics = [Metric("6", "Routes"), Metric("1.1K", "Seats"), Metric("94%", "Peak")];
        Flights.Rows = [new("SFO → HND", "82%", violet), new("HND → SIN", "76%", green), new("JFK → LHR", "91%", blue)];

        var incidents = new[]
        {
            Marker(new(40.7565,-73.9855), "ProMapRoadwork", "ProMapRoadworkDetail", red),
            Marker(new(40.7231,-73.9981), "ProMapMaintenance", "ProMapMaintenanceDetail", orange),
            Marker(new(40.7409,-73.9693), "ProMapInspection", "ProMapInspectionDetail", blue)
        };
        Incidents = Scene("Incidents", new(40.745,-73.986), 13.5, newYork, incidents);
        Incidents.Compass = false;
        Incidents.Metrics = [Metric("12", "Open"), Metric("3", "Critical"), Metric("5", "Warning")];
        Incidents.Rows = [Incidents.Place(incidents[0], "3", red), Incidents.Place(incidents[1], "5", orange), Incidents.Place(incidents[2], "4", blue)];
    }

    public MapScene Usage { get; }
    public MapScene Visitors { get; }
    public MapScene Properties { get; }
    public MapScene Tracking { get; }
    public MapScene Stores { get; }
    public MapScene Fleet { get; }
    public MapScene Coverage { get; }
    public MapScene Heatmap { get; }
    public MapScene Flights { get; }
    public MapScene Incidents { get; }

    private MapSceneMetric Metric(string value, string label) => new(value, Text($"ProMapMetric{label}"));

    private static MapScene Scene(string key, MapCoordinate center, double zoom, CityMap city, IEnumerable<AlderMapMarker> markers, params AlderMapPath[] paths) =>
        new($"ProMap{key}", $"ProMap{key}Hint", center, zoom, city.Basemap, city.Roads, markers, paths);

    private AlderMapMarker Marker(MapCoordinate point, string title, string detail, IBrush brush) =>
        new(point, Text(title), Text(detail), brush);

    private static AlderMapPath Arc(AlderMapMarker start, AlderMapMarker end, IBrush tint) =>
        new([start.Location, end.Location], Stroke: tint, Thickness: 2, Curved: true);

    private static AlderMapPath Zone(MapCoordinate center, IBrush tint, string fill)
    {
        var points = Enumerable.Range(0, 6).Select(i =>
        {
            var angle = (i * 60 + 12) * Math.PI / 180;
            return new MapCoordinate(center.Latitude + Math.Cos(angle) * .014, center.Longitude + Math.Sin(angle) * .016);
        }).ToArray();
        return new(points, true, tint, Brush.Parse(fill), 1.5);
    }

    private static JsonDocument LoadMap(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://Alder.Avalonia.Gallery/Assets/Map/{name}.json"));
        return JsonDocument.Parse(stream);
    }

    private static CityMap ReadCity(JsonElement data, IReadOnlyList<AlderMapPath> land)
    {
        var water = data.GetProperty("water").EnumerateArray()
            .Select(polygon => new AlderMapPath(ReadPoints(polygon), IsClosed: true, Thickness: .7) { IsWater = true });
        return new([.. land, .. water],
        [
            new(data.GetProperty("minor").EnumerateArray().Select(ReadPoints).ToArray(), 1, 11.2),
            new(data.GetProperty("major").EnumerateArray().Select(ReadPoints).ToArray(), 2.1, 9)
        ]);
    }

    private static IReadOnlyList<MapCoordinate> ReadPoints(JsonElement json)
    {
        var coordinates = json.EnumerateArray();
        var points = new MapCoordinate[json.GetArrayLength() / 2];
        for (int i = 0; i < points.Length; i++)
        {
            coordinates.MoveNext();
            double longitude = coordinates.Current.GetInt32() / 100000d;
            coordinates.MoveNext();
            points[i] = new MapCoordinate(coordinates.Current.GetInt32() / 100000d, longitude);
        }
        return points;
    }

    private static IReadOnlyList<AlderMapPath> ReadLand(JsonElement data) => data.EnumerateArray()
        .Select(polygon => new AlderMapPath(polygon.EnumerateArray()
            .Select(point => new MapCoordinate(point[1].GetDouble(), point[0].GetDouble())).ToArray(),
            IsClosed: true, Thickness: 1)).ToArray();

    private static IEnumerable<AlderMapMarker> Clouds(IEnumerable<MapCoordinate> centers, int count, double spread, IBrush brush)
    {
        var random = new Random(41);
        foreach (var center in centers)
            for (int i = 0; i < count; i++)
            {
                var longitude = center.Longitude + (random.NextDouble() - .5) * spread;
                var latitude = center.Latitude + (random.NextDouble() - .5) * spread;
                yield return new AlderMapMarker(new(latitude, longitude), Brush: brush, Weight: .35 + random.NextDouble());
            }
    }
}
