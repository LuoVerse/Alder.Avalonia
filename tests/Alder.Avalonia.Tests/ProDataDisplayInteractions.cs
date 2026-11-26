using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ProDataDisplayInteractions
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void WidgetClipsTheContentWithoutClippingTheInnerSurfaceShadow(bool dark)
    {
        var content = new Border { Height = 28 };
        var widget = new AlderWidget { Title = "Usage", Content = content };
        var window = Show(widget);
        try
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Pump(window);
            var surface = widget.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ContentSurface");
            var clip = Assert.IsType<Border>(surface.Child);

            Assert.False(widget.ClipToBounds);
            Assert.False(surface.ClipToBounds);
            Assert.False(clip.ClipToBounds);
            Assert.Equal(new CornerRadius(12), clip.CornerRadius);
            Assert.Equal(new Point(16, 16), content.TranslatePoint(default, surface));
            var shadow = surface.BoxShadow;
            Assert.True(shadow.Count > 0);

            widget.ContentPadding = default;
            widget.ClipContent = true;
            Pump(window);

            Assert.True(clip.ClipToBounds);
            Assert.Equal(default(Point), content.TranslatePoint(default, surface));
            Assert.Equal(shadow, surface.BoxShadow);
            Assert.False(surface.ClipToBounds);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TableCanHideItsHeaderAndKeepStarColumnsSizedToTheViewport()
    {
        var table = new AlderTable
        {
            ItemsSource = new[] { "First", "Second" },
            Variant = TableVariant.Secondary,
            Columns =
            {
                new AlderTableColumn { Header = "Name", Width = GridLength.Star },
                new AlderTableColumn { Header = "Detail", Width = GridLength.Star }
            }
        };
        var window = Show(table);
        try
        {
            var header = table.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "HeaderSurface");
            var scroll = table.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer");
            Assert.True(header.Bounds.Height >= 36);

            table.ShowColumnHeaders = false;
            Pump(window);
            Assert.Equal(0, header.Bounds.Height);
            Assert.False(header.IsHitTestVisible);
            Assert.Equal(scroll.Viewport.Width, table.Columns.Sum(column => column.ActualWidth), 0);
            Assert.Equal(2, table.VisibleRowCount);

            table.ShowColumnHeaders = true;
            Pump(window);
            Assert.True(header.Bounds.Height >= 36);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ItemCardSurfacesKeepShadowsOutsideAndGroupHeadersInside()
    {
        var card = new AlderItemCard { Title = "Profile" };
        var row = new AlderItemCard { Title = "General" };
        var group = new AlderItemCardGroup { Title = "Settings", Items = { row } };
        var window = Show(new StackPanel { Children = { card, group } });
        try
        {
            window.RequestedThemeVariant = ThemeVariant.Light;
            Pump(window);
            var cardSurface = card.GetVisualChildren().OfType<Border>().Single();
            var groupSurface = group.GetVisualChildren().OfType<Border>().Single();
            var rowSurface = row.GetVisualChildren().OfType<Border>().Single();

            Assert.False(card.ClipToBounds);
            Assert.False(group.ClipToBounds);
            Assert.Equal(0, cardSurface.BorderThickness.Left);
            Assert.NotEqual(default, cardSurface.BoxShadow);
            Assert.Equal(cardSurface.BoxShadow, groupSurface.BoxShadow);
            Assert.Equal(default, rowSurface.BoxShadow);
            Assert.Contains(groupSurface.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Settings");

            card.Variant = ItemCardVariant.Outline;
            group.Variant = ItemCardVariant.Outline;
            Pump(window);
            Assert.Equal(new Thickness(1), cardSurface.BorderThickness);
            Assert.Equal(new Thickness(1), groupSurface.BorderThickness);
            Assert.Equal(default, cardSurface.BoxShadow);
            Assert.Equal(default, groupSurface.BoxShadow);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ItemCardKeepsItsTwoCommandsIndependent()
    {
        int rowInvocations = 0;
        int actionInvocations = 0;
        var action = new Button { Content = "Settings", Command = new TestCommand(_ => actionInvocations++) };
        var card = new AlderItemCard
        {
            Title = "Workspace", Description = "Edit preferences", Action = action,
            Command = new TestCommand(_ => rowInvocations++)
        };
        var window = Show(card);
        try
        {
            Click(window, Center(action, window));
            Assert.Equal(1, actionInvocations);
            Assert.Equal(0, rowInvocations);
            Click(window, new Point(24, 24));
            Assert.Equal(1, rowInvocations);
            Assert.Equal(1, actionInvocations);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MetricsFormatMissingValuesAndWrapAtNarrowWidths()
    {
        var metric = new AlderKpi { Value = 1234.56, Prefix = "$", ValueFormat = "N2", Culture = CultureInfo.InvariantCulture };
        var other = new AlderKpi { Title = "Visitors", Value = 42 };
        var group = new AlderKpiGroup { Items = { metric, other }, Columns = 2, MinColumnWidth = 200 };
        var window = Show(group, width: 560);
        try
        {
            Assert.Equal("$1,234.56", metric.DisplayValue);
            Assert.Equal(metric.TranslatePoint(default, window)!.Value.Y, other.TranslatePoint(default, window)!.Value.Y);
            metric.Value = null;
            Assert.Equal("—", metric.DisplayValue);
            window.Width = 320;
            Pump(window);
            Assert.True(Center(other, window).Y > Center(metric, window).Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetricsOwnTheirCardsAndGroupsKeepOneSharedCardWithOrWithoutDividers(bool dark)
    {
        var metric = new AlderKpi { Title = "Revenue", Value = 42 };
        var standalone = new AlderKpi { Title = "Standalone", BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(1) };
        var group = new AlderKpiGroup
        {
            Items = { metric, new AlderKpi() }, ShowDividers = false,
            BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(1)
        };
        var window = Show(new StackPanel { Children = { group, new FlexPanel { Children = { standalone } } } }, width: 560);
        try
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Pump(window);
            var card = Card(metric);
            var groupCard = Card(group);
            var standaloneCard = Card(standalone);
            Assert.Equal(new CornerRadius(16), standaloneCard.CornerRadius);
            Assert.False(standalone.ClipToBounds);
            Assert.False(standaloneCard.ClipToBounds);
            Assert.Same(Brushes.DodgerBlue, standaloneCard.BorderBrush);
            Assert.Equal(new Thickness(1), standaloneCard.BorderThickness);
            Assert.NotEqual(Colors.Transparent, Fill(standaloneCard));
            if (!dark) Assert.True(standaloneCard.BoxShadow.Count > 0);

            foreach (bool showDividers in new[] { false, true })
            {
                group.ShowDividers = showDividers;
                Pump(window);
                Assert.Equal(new CornerRadius(16), groupCard.CornerRadius);
                Assert.Equal(Fill(standaloneCard), Fill(groupCard));
                Assert.Equal(standaloneCard.BoxShadow.Count, groupCard.BoxShadow.Count);
                Assert.False(group.ClipToBounds);
                Assert.False(groupCard.ClipToBounds);
                Assert.True(Assert.IsType<Border>(groupCard.Child).ClipToBounds);
                Assert.Same(Brushes.DodgerBlue, groupCard.BorderBrush);
                Assert.Equal(new Thickness(1), groupCard.BorderThickness);
                Assert.Equal(default, card.CornerRadius);
                Assert.Equal(Colors.Transparent, Fill(card));
                Assert.Equal(0, card.BoxShadow.Count);
            }
        }
        finally { window.Close(); }

        static Border Card(Control control) => Assert.Single(control.GetVisualChildren().OfType<Border>());
        static Color Fill(Border border) => Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color;
    }

    [AvaloniaFact]
    public void ListViewLeavesDisabledAndEmbeddedActionsOutsideSelection()
    {
        int invoked = 0;
        var action = new Button { Content = "Open", Command = new TestCommand(_ => invoked++) };
        var first = new AlderListViewItem { Title = "First", Action = action };
        var unavailable = new AlderListViewItem { Title = "Unavailable", IsEnabled = false };
        var list = new AlderListView { Items = { first, unavailable } };
        var window = Show(list);
        try
        {
            Click(window, Center(action, window));
            Assert.Equal(1, invoked);
            Assert.Null(list.SelectedItem);
            Click(window, Center(unavailable, window));
            Assert.Null(list.SelectedItem);
            Click(window, Center(first, window));
            Assert.Same(first, list.SelectedItem);
            list.SelectionEnabled = false;
            Assert.Null(list.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ListViewSelectionUsesTheSharedCheckBoxAndKeepsInputOnTheRow()
    {
        var item = new AlderListViewItem { Title = "Documents" };
        var list = new AlderListView { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle, Items = { item } };
        var window = Show(list);
        try
        {
            var check = item.GetVisualDescendants().OfType<AlderCheckBox>().Single(control => control.Name == "SelectMark");
            var stroke = check.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single(path => path.Name == "CheckStroke");
            var fill = check.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SelectionFill");
            var uncheckedOffset = stroke.StrokeDashOffset;

            AlderMotion.SetReduced(window, false);
            Assert.Single(stroke.Transitions!);
            Assert.NotEmpty(fill.Transitions!);
            AlderMotion.SetReduced(window, true);
            Assert.Null(stroke.Transitions);
            Assert.Null(fill.Transitions);

            var point = Center(check, window);
            Click(window, point);
            Assert.Same(item, list.SelectedItem);
            Assert.True(check.IsChecked);
            Assert.Equal(0, stroke.StrokeDashOffset);

            Click(window, point);
            Assert.Null(list.SelectedItem);
            Assert.False(check.IsChecked);
            Assert.Equal(uncheckedOffset, stroke.StrokeDashOffset);

            Assert.True(item.Focus(NavigationMethod.Tab));
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.True(check.IsChecked);

            list.ShowSelectionIndicator = false;
            Assert.False(check.IsVisible);
            Assert.Same(item, list.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimelineReconnectsBothEndsAfterMutation()
    {
        var first = new AlderTimelineItem { Header = "Proposed" };
        var last = new AlderTimelineItem { Header = "Approved" };
        var items = new ObservableCollection<AlderTimelineItem> { first, last };
        var timeline = new AlderTimeline { ItemsSource = items };
        var window = Show(timeline);
        try
        {
            Assert.Contains("connect-after", first.Classes);
            Assert.DoesNotContain("connect-after", last.Classes);
            items.Add(new AlderTimelineItem { Header = "Shipped" });
            Pump(window);
            Assert.Contains("connect-after", last.Classes);
            items.RemoveAt(0);
            Pump(window);
            Assert.DoesNotContain("connect-before", last.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimelineMovesTheRailAndAlternatingEntriesWhenCrossingTheCompactBreakpoint()
    {
        var first = new AlderTimelineItem { Header = "Proposed" };
        var next = new AlderTimelineItem { Header = "Approved" };
        var timeline = new AlderTimeline
        {
            Alignment = TimelineAlignment.Center,
            CompactBreakpoint = 400,
            Items = { first, next }
        };
        var window = Show(timeline, width: 600);
        try
        {
            var rail = first.GetVisualDescendants().OfType<Grid>().Single(element => element.Name == "PART_Rail");
            var entry = next.GetVisualDescendants().OfType<StackPanel>().Single(element => element.Name == "Entry");
            double RailX() => Center(rail, window).X;
            double EntryX() => entry.TranslatePoint(default, window)!.Value.X;

            Assert.Equal(300, RailX());
            Assert.True(EntryX() < RailX());

            window.Width = 320;
            Pump(window);
            Assert.Equal(11, RailX());
            Assert.True(EntryX() > RailX());
            Assert.Contains("connect-after", first.Classes);
            Assert.Contains("connect-before", next.Classes);

            window.Width = 600;
            Pump(window);
            Assert.Equal(300, RailX());
            Assert.True(EntryX() < RailX());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KanbanColumnAddButtonUsesTheOutlinedFooterAndInvokesItsCommand()
    {
        int calls = 0;
        var column = new AlderKanbanColumn { AddLabel = "Add a task", AddCommand = new TestCommand(_ => calls++) };
        var window = Show(new AlderKanban { Items = { column } });
        try
        {
            var footer = column.GetVisualDescendants().OfType<AlderButton>().Single(button => button.Name == "PART_AddButton");
            var surface = column.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "ColumnSurface");
            var icon = footer.GetVisualDescendants().OfType<PathIcon>().Single(element => element.Name == "LeadingIcon");
            Assert.Equal(ButtonVariant.Outline, footer.Variant);
            Assert.NotNull(footer.Icon);
            Assert.True(icon.IsVisible);
            Assert.Equal(new Thickness(0), icon.Margin);
            Assert.False(footer.ClipToBounds);
            Assert.Equal(new Thickness(1), footer.BorderThickness);
            Assert.Equal(36, footer.Bounds.Height);
            Assert.Equal(surface.Bounds.Width - 16, footer.Bounds.Width);
            Assert.Equal(8, footer.TranslatePoint(default, surface)!.Value.X);
            Assert.Equal(8, surface.Bounds.Height - footer.TranslatePoint(default, surface)!.Value.Y - footer.Bounds.Height);

            Click(window, Center(footer, window));
            Assert.Equal(1, calls);

            column.AddCommand = null;
            Pump(window);
            Assert.False(footer.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KanbanCreatesColumnContainersForDataItems()
    {
        var data = new object();
        var explicitColumn = new AlderKanbanColumn { Header = "Complete" };
        var board = new AlderKanban { ItemsSource = new object[] { data, explicitColumn } };
        var window = Show(board, width: 620);
        try
        {
            var dataColumn = Assert.IsType<AlderKanbanColumn>(board.ContainerFromIndex(0));
            Assert.Same(data, dataColumn.DataContext);
            Assert.Same(explicitColumn, board.ContainerFromIndex(1));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KanbanDropsIntoAnEmptyColumnAndCancelsWithoutMutation()
    {
        var requests = new List<KanbanMoveRequest>();
        var card = new AlderKanbanCard { Header = "Verify rotation", Content = "Test the preview across scaling levels." };
        var planned = new AlderKanbanColumn { Key = "planned", Header = "Planned", Items = { card } };
        var done = new AlderKanbanColumn { Key = "done", Header = "Done", EmptyContent = "Drop here" };
        var board = new AlderKanban { Items = { planned, done }, ReorderCommand = new TestCommand(value => requests.Add(Assert.IsType<KanbanMoveRequest>(value))) };
        var window = Show(board, width: 590);
        try
        {
            window.SetRenderScaling(2);
            Pump(window);
            var start = card.TranslatePoint(new Point(30, 15), window)!.Value;
            var end = done.TranslatePoint(new Point(74, 72), window)!.Value;
            StartDrag();
            Assert.Contains(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "KanbanDragPreview");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.MouseUp(end, MouseButton.Left);
            Assert.Empty(requests);

            StartDrag();
            window.MouseUp(end, MouseButton.Left);
            var request = Assert.Single(requests);
            Assert.Equal(new KanbanMoveRequest(card, "planned", "done", 0, 0), request);
            Assert.Same(card, Assert.Single(planned.Items));
            Assert.Empty(done.Items);

            void StartDrag()
            {
                window.MouseMove(start);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                Pump(window);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KanbanOpensAFullHeightSlotAndKeepsThePreviewUnderThePointer()
    {
        var requests = new List<KanbanMoveRequest>();
        var card = new AlderKanbanCard { Header = "Artwork card", Height = 102 };
        var sibling = new AlderKanbanCard { Header = "Target", Height = 58 };
        var first = new AlderKanbanColumn { Key = "first", Items = { card, new AlderKanbanCard { Header = "Unchanged" } } };
        var second = new AlderKanbanColumn { Key = "second", Items = { sibling, new AlderKanbanCard { Header = "Last" } } };
        var board = new AlderKanban
        {
            Items = { first, second },
            ReorderCommand = new TestCommand(value => requests.Add(Assert.IsType<KanbanMoveRequest>(value)))
        };
        var window = Show(board, 670);
        try
        {
            var localGrab = new Point(30, 21);
            var start = card.TranslatePoint(localGrab, window)!.Value;
            var destination = sibling.TranslatePoint(new Point(40, 3), window)!.Value;
            var before = sibling.TranslatePoint(default, window)!.Value;
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(destination, RawInputModifiers.LeftMouseButton);
            Pump(window);

            var preview = Assert.Single(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "KanbanDragPreview");
            Assert.Equal(card.Bounds.Size, preview.Bounds.Size);
            Assert.Equal(destination, preview.TranslatePoint(localGrab, window));
            var shifted = sibling.TranslatePoint(default, window)!.Value;
            Assert.True(shifted.Y - before.Y >= card.Bounds.Height);
            Assert.Empty(requests);

            var insideSlot = destination + new Vector(12, 23);
            window.MouseMove(insideSlot, RawInputModifiers.LeftMouseButton);
            Pump(window);
            Assert.Equal(insideSlot, preview.TranslatePoint(localGrab, window));
            Assert.Equal(shifted, sibling.TranslatePoint(default, window));
            window.MouseUp(insideSlot, MouseButton.Left);
            Pump(window);

            Assert.DoesNotContain(preview, window.GetVisualDescendants());
            Assert.Equal(new KanbanMoveRequest(card, "first", "second", 0, 0), Assert.Single(requests));
            Assert.Equal(before, sibling.TranslatePoint(default, window));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task KanbanGrowsAndClosesTheSlotAcrossColumnsAndAtTheOriginalPosition()
    {
        var requests = new List<KanbanMoveRequest>();
        var card = new AlderKanbanCard { Header = "Dragged", Height = 84 };
        var target = new AlderKanbanCard { Header = "Target", Height = 70 };
        var first = new AlderKanbanColumn
        {
            Items = { new AlderKanbanCard { Header = "Above", Height = 48 }, card, new AlderKanbanCard { Header = "Below", Height = 48 } }
        };
        var second = new AlderKanbanColumn { Items = { target } };
        var board = new AlderKanban
        {
            Height = 425,
            Items = { first, second },
            ReorderCommand = new TestCommand(value => requests.Add(Assert.IsType<KanbanMoveRequest>(value)))
        };
        var page = new ScrollViewer { Content = new StackPanel { Children = { board, new Border { Height = 150 } } } };
        var window = Show(page, 670);
        try
        {
            AlderMotion.SetReduced(window, false);
            double sourceTop = card.Bounds.Top;
            double targetTop = target.Bounds.Top;
            var start = card.TranslatePoint(new Point(26, 21), window)!.Value;
            var destination = target.TranslatePoint(new Point(48, 24), window)!.Value;
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(destination, RawInputModifiers.LeftMouseButton);

            await WaitForFrame(() => target.Bounds.Top > targetTop && target.Bounds.Top < targetTop + card.Bounds.Height);
            await WaitForFrame(() => target.Bounds.Top >= targetTop + card.Bounds.Height);
            Assert.Equal(default, page.Offset);
            Assert.Equal(sourceTop, card.Bounds.Top);

            var returnPoint = start + new Vector(10, 0);
            window.MouseMove(returnPoint, RawInputModifiers.LeftMouseButton);
            await WaitForFrame(() => card.Bounds.Top > sourceTop && card.Bounds.Top < sourceTop + card.Bounds.Height);
            await WaitForFrame(() => target.Bounds.Top == targetTop && card.Bounds.Top >= sourceTop + card.Bounds.Height);

            window.MouseUp(returnPoint, MouseButton.Left);
            await WaitForFrame(() => card.Bounds.Top > sourceTop && card.Bounds.Top < sourceTop + card.Bounds.Height);
            await WaitForFrame(() => card.Bounds.Top == sourceTop);
            Assert.Empty(requests);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "KanbanDragPreview");
        }
        finally { window.Close(); }

        async Task WaitForFrame(Func<bool> condition)
        {
            for (int frame = 0; frame < 84; frame++)
            {
                Pump(window);
                if (condition()) return;
                await Task.Delay(8);
            }
            Assert.Fail($"Expected a drag frame while the source was at {card.Bounds.Top} and the target was at {target.Bounds.Top}.");
        }
    }

    [AvaloniaFact]
    public void MapZoomKeepsTheLocationUnderThePointerAndSelectsMarkers()
    {
        var coordinate = new MapCoordinate(37.78, -122.42);
        var marker = new AlderMapMarker(coordinate, "Marker", "Preview");
        var other = new AlderMapMarker(new MapCoordinate(37.79, -122.447), "Second marker", "Another preview");
        var map = new AlderMap { Center = coordinate, Zoom = 12, Markers = new[] { marker, other }, ShowControls = false };
        var window = Show(map);
        try
        {
            AlderMotion.SetReduced(window, false);
            var pointer = new Point(135, 89);
            var expected = map.PointToLocation(pointer);
            window.MouseMove(pointer);
            window.MouseWheel(pointer, new Vector(0, 1));
            Pump(window);
            Assert.True(map.Zoom > 12);
            Assert.Equal(expected.Latitude, map.PointToLocation(pointer).Latitude, 7);
            Assert.Equal(expected.Longitude, map.PointToLocation(pointer).Longitude, 7);
            Click(window, map.LocationToPoint(coordinate));
            Assert.Same(marker, map.SelectedMarker);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, marker.Detail));
            Click(window, map.LocationToPoint(other.Location));
            Assert.Same(other, map.SelectedMarker);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, other.Detail));
            window.Content = null;
            Pump(window);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, other.Detail));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MapPopupTracksItsMarkerAndWheelPassesThroughWhileUnfocused()
    {
        var center = new MapCoordinate(37.78, -122.42);
        var marker = new AlderMapMarker(center, "Property", "Map details") { Badge = "$895K" };
        var map = new AlderMap
        {
            Center = center, Zoom = 12, Markers = new[] { marker }, SelectedMarker = marker,
            WheelZoomRequiresFocus = true
        };
        var window = Show(map);
        try
        {
            var frame = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_MarkerPopup");
            var position = frame.TranslatePoint(default, window)!.Value;
            Assert.True(frame.IsVisible);

            var point = new Point(70, 100);
            window.MouseMove(point);
            window.MouseWheel(point, new Vector(0, 1));
            Pump(window);
            Assert.Equal(12, map.Zoom);

            map.Focus();
            window.MouseWheel(point, new Vector(0, 1));
            Pump(window);
            Assert.True(map.Zoom > 12);
            Assert.True(frame.IsVisible);
            Assert.NotEqual(position, frame.TranslatePoint(default, window));

            var close = frame.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_ClosePopup");
            Click(window, Center(close, window));
            Assert.False(frame.IsVisible);
            Assert.Null(map.SelectedMarker);

            var rotationEnd = point + new Vector(32, 0);
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Right);
            window.MouseMove(rotationEnd, RawInputModifiers.RightMouseButton);
            window.MouseUp(rotationEnd, MouseButton.Right);
            Pump(window);
            Assert.NotEqual(0, map.Bearing);
            var compass = map.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_ResetBearing");
            Click(window, Center(compass, window));
            Assert.Equal(0, map.Bearing);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control control, int width = 490)
    {
        var window = new Window { Width = width, Height = 300, Content = control };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class TestCommand(Action<object?> action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
    }
}
