using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ChartInteractions
{
    [AvaloniaFact]
    public void AreaTooltipFollowsThePointerAndClearsWhenThePointerLeaves()
    {
        var chart = new AlderAreaChart
        {
            Categories = ["Jan", "Feb", "Mar"],
            Series = [new("Sales", [2, 4, 6]), new("Returns", [-1, null, -3])]
        };
        var window = Show(chart);
        try
        {
            var tooltip = Tooltip(chart);
            MoveTo(window, chart, .5, .45);
            var data = Assert.IsType<AlderChartTooltipData>(chart.ActiveData);
            Assert.Equal("Feb", data.Label);
            Assert.Equal("Sales", Assert.Single(data.Entries).Name);
            Assert.Equal("4", data.Total);
            Assert.True(tooltip.IsVisible);

            MoveTo(window, chart, .94, .45);
            Assert.Equal("Mar", chart.ActiveData?.Label);
            Assert.Equal(2, chart.ActiveData!.Entries.Count);
            Assert.Equal("3", chart.ActiveData.Total);
            double right = tooltip.RenderTransform!.Value.M31 + tooltip.Bounds.Width;
            Assert.True(right <= chart.Bounds.Width, $"Tooltip ends at {right} for chart width {chart.Bounds.Width}.");

            window.MouseMove(new Point(2, 2));
            Pump(window);
            Assert.Null(chart.ActiveData);
            Assert.False(tooltip.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(typeof(AlderAreaChart))]
    [InlineData(typeof(AlderLineChart))]
    [InlineData(typeof(AlderComposedChart))]
    public void PointSeriesChartsHighlightTheNearestPointByDefaultWithoutATooltip(Type chartType)
    {
        var chart = (AlderCartesianChart)Activator.CreateInstance(chartType)!;
        chart.Width = 90;
        chart.Height = 36;
        chart.Padding = new Thickness(0, 2, 0, 0);
        chart.Categories = ["1", "2", "3"];
        chart.Series = [new("Requests", [73, 45, 80])];
        chart.ShowXAxis = false;
        chart.ShowYAxis = false;
        chart.ShowGrid = false;
        chart.ShowTooltip = false;
        chart.IsAnimationEnabled = false;

        if (chart is AlderComposedChart)
            chart.Series = [.. chart.Series, new("Targets", [50, 53, 55]) { Kind = AlderChartSeriesKind.Bar }];

        var window = Show(chart);
        try
        {
            var tooltip = Tooltip(chart);
            Assert.Null(chart.ActiveData);

            MoveTo(window, chart, .14, .02);
            Assert.Equal("1", chart.ActiveData?.Label);
            Assert.False(tooltip.IsVisible);

            MoveTo(window, chart, .83, .5);
            Assert.Equal("3", chart.ActiveData?.Label);
            Assert.False(tooltip.IsVisible);

            window.MouseMove(new Point(2, 2));
            Pump(window);
            Assert.Null(chart.ActiveData);

            MoveTo(window, chart, .5, .5);
            Assert.Equal("2", chart.ActiveData?.Label);
            chart.ShowHoverMarker = false;
            Assert.Null(chart.ActiveData);
            MoveTo(window, chart, .83, .5);
            Assert.Null(chart.ActiveData);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TooltipWaitsForItsFirstLayoutAndRestartsMotionOnReentry()
    {
        var chart = new AlderLineChart
        {
            Categories = ["Jan", "Feb", "Mar"],
            Series = [new("Sales", [12, 21, 30])]
        };
        var window = Show(chart, reducedMotion: false);
        try
        {
            var tooltip = Tooltip(chart);
            var presentedSizes = new List<Size>();
            var opacityAtLayoutEntry = new List<double>();
            tooltip.PropertyChanged += (_, change) =>
            {
                if (change.Property == Visual.IsVisibleProperty && tooltip.IsVisible)
                    opacityAtLayoutEntry.Add(tooltip.Opacity);
                if (change.Property == Visual.OpacityProperty && tooltip.Opacity == 1)
                    presentedSizes.Add(tooltip.Bounds.Size);
            };

            MoveTo(window, chart, .5, .35);
            Assert.Equal("Feb", chart.ActiveData?.Label);
            Assert.Equal(0, Assert.Single(opacityAtLayoutEntry));
            Assert.True(Assert.Single(presentedSizes).Width > 0);
            var transition = Assert.IsType<TransformOperationsTransition>(Assert.Single(tooltip.Transitions!));
            Assert.Equal(TimeSpan.FromMilliseconds(400), transition.Duration);

            window.MouseMove(new Point(2, 2));
            Pump(window);
            Assert.Null(chart.ActiveData);
            Assert.False(tooltip.IsVisible);
            Assert.Equal(Matrix.Identity, tooltip.RenderTransform?.Value);

            MoveTo(window, chart, .8, .3);
            Assert.NotNull(chart.ActiveData);
            Assert.Equal(new[] { 0d, 0d }, opacityAtLayoutEntry);
            Assert.Equal(2, presentedSizes.Count);
            Assert.Equal(1, tooltip.Opacity);
            Assert.Single(tooltip.Transitions!);

            AlderMotion.SetReduced(window, true);
            Pump(window);
            Assert.Null(tooltip.Transitions);
            Assert.True(tooltip.RenderTransform!.Value.M31 > 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TooltipDoesNotInterceptPointerWhenItsSurfaceOverlapsThePlot()
    {
        var chart = new AlderBarChart
        {
            Categories = ["Jan", "Feb", "Mar"],
            Series = [new("Sales", [12, 21, 30]), new("Returns with a longer label", [null, 3, null])]
        };
        var window = Show(chart);
        try
        {
            MoveTo(window, chart, .22, .2);
            var tooltip = Tooltip(chart);
            var tooltipCenter = tooltip.TranslatePoint(new Rect(tooltip.Bounds.Size).Center, window)!.Value;
            Assert.Same(chart, window.InputHitTest(tooltipCenter));

            window.MouseMove(tooltipCenter);
            Pump(window);
            Assert.NotNull(chart.ActiveData);
            Assert.True(tooltip.IsVisible);
            Assert.Equal(1, tooltip.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HorizontalChartUsesVerticalKeysAndResetsSelectionWhenDataChanges()
    {
        var chart = new AlderBarChart
        {
            Orientation = Orientation.Horizontal,
            IsStacked = true,
            Categories = ["First", "Second", "Third"],
            Series = [new("A", [2, -3, 5]), new("B", [4, -1, double.NaN])]
        };
        var window = Show(chart);
        try
        {
            Assert.True(chart.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Home);
            Assert.Equal("6", chart.ActiveData?.Total);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal("Second", chart.ActiveData?.Label);
            Assert.Equal("-4", chart.ActiveData?.Total);
            Press(window, PhysicalKey.End);
            Assert.Single(chart.ActiveData!.Entries);
            Press(window, PhysicalKey.Escape);
            Assert.Null(chart.ActiveData);
            Assert.False(Tooltip(chart).IsVisible);

            Press(window, PhysicalKey.Home);
            chart.Series = [new("New", [8])];
            Assert.Null(chart.ActiveData);
            Press(window, PhysicalKey.Home);
            Assert.Equal("8", chart.ActiveData?.Total);
            Press(window, PhysicalKey.End);
            Assert.Null(chart.ActiveData);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChartTooltipFormatsRowsAndHidesItsSurfaceWhenInactive()
    {
        var tooltip = new AlderChartTooltip
        {
            Label = "quarter",
            ItemsSource = [new("Revenue", 12.5), new("Missing", double.NaN)],
            Indicator = AlderChartTooltipIndicator.Line,
            LabelFormatter = label => label.ToUpperInvariant(),
            ValueFormatter = value => value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
        };
        var window = Show(tooltip);
        try
        {
            Assert.True(tooltip.HasContent);
            Assert.Equal("QUARTER", tooltip.Header);
            var row = Assert.Single(tooltip.Rows);
            Assert.Equal("12.5%", row.Entry.Value);
            Assert.Equal((4d, 12d), (row.MarkerWidth, row.MarkerHeight));

            tooltip.ShowHeader = false;
            tooltip.IsActive = false;
            Pump(window);
            Assert.Null(tooltip.Header);
            Assert.False(tooltip.HasContent);
            Assert.DoesNotContain(tooltip.GetVisualDescendants().OfType<Border>(), border => border.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ComposedChartPreservesSecondarySeriesFormatting()
    {
        var chart = new AlderComposedChart
        {
            Categories = ["April", "May"],
            Series =
            [
                new("Visits", [2000, 3000]) { Kind = AlderChartSeriesKind.Bar },
                new("Returning", [.33, .5]) { Kind = AlderChartSeriesKind.Area, UseSecondaryAxis = true, ValueFormat = "0%" },
                new("Orders", [42, 73]) { UseSecondaryAxis = true },
            ],
        };
        var window = Show(chart);
        try
        {
            Assert.True(chart.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.End);
            var data = Assert.IsType<AlderChartTooltipData>(chart.ActiveData);
            Assert.Equal("May", data.Label);
            Assert.Equal(new[] { "Visits", "Returning", "Orders" }, data.Entries.Select(entry => entry.Name));
            Assert.Equal(.5.ToString("0%", System.Globalization.CultureInfo.CurrentCulture), data.Entries[1].Value);
            Assert.True(Tooltip(chart).IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DonutHitTestingIgnoresTheHoleAndIncludesBothRings()
    {
        var chart = new AlderPieChart
        {
            InnerRadius = .4,
            Slices = [new("Outer", 20), new("Invalid", -1)],
            InnerSlices = [new("Inner", 10)]
        };
        var window = Show(chart);
        try
        {
            MoveTo(window, chart, .5, .5);
            Assert.Null(chart.ActiveData);

            MoveTo(window, chart, .5, .13);
            Assert.Equal("Outer", Assert.Single(chart.ActiveData!.Entries).Name);

            MoveTo(window, chart, .5, .28);
            Assert.Equal("Inner", Assert.Single(chart.ActiveData!.Entries).Name);

            Assert.True(chart.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Home);
            Assert.Equal("Outer", Assert.Single(chart.ActiveData!.Entries).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RadarSelectsTheNearestAxisAndSkipsMissingValues()
    {
        var chart = new AlderRadarChart
        {
            Categories = ["Design", "Build", "Test", "Release"],
            Series = [new("Current", [75, 82, 94, 64]), new("Target", [null, 90, 98, 80])],
        };
        var window = Show(chart);
        try
        {
            MoveTo(window, chart, .5, .23);
            Assert.Equal("Design", chart.ActiveData?.Label);
            Assert.Equal("75", Assert.Single(chart.ActiveData!.Entries).Value);

            MoveTo(window, chart, .64, .5);
            Assert.Equal("Build", chart.ActiveData?.Label);
            Assert.Equal(2, chart.ActiveData!.Entries.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RadialTooltipOnlyHitsTheFilledSweep()
    {
        var chart = new AlderRadialChart
        {
            Rings = [new("Steps", 2500, 10000)],
            RingThickness = 16,
        };
        var window = Show(chart);
        try
        {
            MoveTo(window, chart, .65, .22);
            Assert.Equal("Steps", Assert.Single(chart.ActiveData!.Entries).Name);

            MoveTo(window, chart, .65, .78);
            Assert.Null(chart.ActiveData);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control child, bool reducedMotion = true)
    {
        var window = new Window { Width = 520, Height = 300, Content = new Border { Padding = new Thickness(16), Child = child } };
        AlderMotion.SetReduced(window, reducedMotion);
        window.Show();
        Pump(window);
        return window;
    }

    private static ContentPresenter Tooltip(AlderPlotChart chart) => chart.GetVisualDescendants()
        .OfType<ContentPresenter>().Single(c => c.Name == "PART_Tooltip");

    private static void MoveTo(Window window, Control control, double x, double y)
    {
        var point = new Point(control.Bounds.Width * x, control.Bounds.Height * y);
        window.MouseMove(control.TranslatePoint(point, window)!.Value);
        Pump(window);
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
