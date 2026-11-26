using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Alder.Avalonia.Controls;
using Xunit;

namespace Alder.Avalonia.Tests;

public class MapProjectionInteractions
{
    [AvaloniaFact]
    public void MapKeepsClusterMembershipAcrossFractionalZoomChanges()
    {
        var center = new MapCoordinate(0, 1);
        var first = new AlderMapMarker(new MapCoordinate(0, 0));
        var markers = new List<AlderMapMarker> { first, new(new MapCoordinate(0, 2)) };
        var map = new AlderMap
        {
            Center = center,
            Zoom = 1.1,
            ClusterMarkers = true,
            Markers = markers,
            ShowControls = false
        };
        var window = new Window { Width = 400, Height = 300, Content = map };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            foreach (var zoom in new[] { 1.1, 1.2, 1.8, 1.1 })
            {
                map.Center = center;
                map.Zoom = zoom;
                ClickMarkerAt(center);
                Assert.Equal(zoom + 1.75, map.Zoom, 8);
                Assert.Null(map.SelectedMarker);
            }

            markers.RemoveAt(1);
            map.Center = center;
            map.Zoom = 1.4;
            ClickMarkerAt(first.Location);
            Assert.Same(first, map.SelectedMarker);
            Assert.Equal(1.4, map.Zoom, 8);
        }
        finally { window.Close(); }

        void ClickMarkerAt(MapCoordinate location)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var point = map.TranslatePoint(map.LocationToPoint(location), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
    }

    [AvaloniaFact]
    public void RotatedMapKeepsWrappedMarkersAlignedAfterCameraChanges()
    {
        var marker = new AlderMapMarker(new MapCoordinate(0, -179));
        var map = new AlderMap
        {
            Center = new MapCoordinate(0, 179),
            Zoom = 4,
            Bearing = 90,
            Background = Brushes.White,
            ShowControls = false,
            Markers = [marker],
            Paths = [new AlderMapPath([new(0, 179), marker.Location])]
        };
        var window = new Window { Width = 400, Height = 300, Content = map };
        window.Show();
        try
        {
            Assert.Equal(map.Bounds.Width / 2, map.LocationToPoint(marker.Location).X, 8);
            Assert.True(map.LocationToPoint(marker.Location).Y > map.Bounds.Height / 2);
            ClickProjectedMarker();

            map.SelectedMarker = null;
            map.Center = new MapCoordinate(1, -178);
            map.Zoom = 5;
            map.Bearing = -30;
            ClickProjectedMarker();
        }
        finally { window.Close(); }

        void ClickProjectedMarker()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var point = map.LocationToPoint(marker.Location);
            var roundTrip = map.PointToLocation(point);
            Assert.Equal(marker.Location.Latitude, roundTrip.Latitude, 8);
            Assert.Equal(marker.Location.Longitude, roundTrip.Longitude, 8);
            var click = map.TranslatePoint(point, window)!.Value;
            window.MouseDown(click, MouseButton.Left);
            window.MouseUp(click, MouseButton.Left);
            Assert.Same(marker, map.SelectedMarker);
        }
    }
}
