using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class DataGridInteractions
{
    [AvaloniaFact]
    public void PinnedHeaderAndBodyStayAlignedDuringScrollAndColumnChanges()
    {
        var start = Column("Start", 106, TableColumnPin.Start);
        var center = Column("Center", 317);
        var centerTail = Column("Other", 225);
        var end = Column("End", 108, TableColumnPin.End);
        // The pinned groups can be declared among the scrolling columns.
        var grid = CreateGrid(40, center, end, centerTail, start);
        grid.CanResizeColumns = true;
        var window = Show(grid);
        try
        {
            var view = View(grid);
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            var headStart = Header(view, start);
            var headEnd = Header(view, end);
            double startX = Position(headStart, grid).X;
            double endX = Position(headEnd, grid).X;
            double headerY = Position(headStart, grid).Y;
            double centerX = Position(Header(view, center), grid).X;
            Assert.True(startX < centerX && centerX < endX);
            AssertAligned(view, grid, start, end, center, centerTail);
            scroll.Offset = new Vector(135, 270);
            Pump(window);
            Assert.Equal(135, scroll.Offset.X);
            Assert.True(scroll.Offset.Y > 0);
            Assert.True(view.GetRealizedContainers().Min(row => view.IndexFromContainer(row)) > 0);
            Assert.Equal(startX, Position(headStart, grid).X);
            Assert.Equal(endX, Position(headEnd, grid).X);
            Assert.Equal(headerY, Position(headStart, grid).Y);
            Assert.True(Position(Header(view, center), grid).X < centerX);
            AssertAligned(view, grid, start, end, center, centerTail);

            var resize = headStart.GetVisualDescendants().OfType<Thumb>().Single();
            Assert.True(resize.Focus());
            Press(window, PhysicalKey.ArrowRight);
            centerTail.IsVisible = false;
            Pump(window);
            Assert.Equal(114, Header(view, start).Bounds.Width);
            AssertAligned(view, grid, start, end, center);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TableViewCell>(), cell => cell.Column == centerTail && cell.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerResizeTracksTheGestureAndStopsAtTheColumnMinimum()
    {
        var customer = Column("Customer", 240);
        customer.MinWidth = 200;
        var secondary = Column("Other", 135);
        var actions = Column("Actions", 75, TableColumnPin.End);
        actions.CanUserResize = false;
        var grid = CreateGrid(30, customer, secondary, Column("Region", 350), actions);
        grid.CanResizeColumns = true;
        var window = Show(grid);
        try
        {
            var view = View(grid);
            var header = Header(view, customer);
            var grip = header.GetVisualDescendants().OfType<Thumb>().Single();
            var point = grip.TranslatePoint(new Point(grip.Bounds.Width / 2 - 3, grip.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(new Point(point.X + 35, point.Y));
            Pump(window);
            Assert.Equal(275, customer.ActualWidth);
            Assert.Contains("column-resizing", header.Classes);

            window.MouseMove(new Point(point.X + 55, point.Y));
            Pump(window);
            Assert.Equal(295, customer.ActualWidth);
            window.MouseMove(new Point(point.X - 150, point.Y));
            Pump(window);
            Assert.Equal(200, customer.ActualWidth);

            window.MouseMove(new Point(point.X - 60, point.Y));
            Pump(window);
            Assert.Equal(200, customer.ActualWidth);
            var end = new Point(point.X + 12, point.Y);
            window.MouseMove(end);
            Pump(window);
            Assert.Equal(252, customer.ActualWidth);
            AssertAligned(view, grid, customer, secondary, actions);
            window.MouseUp(end + new Vector(18, 0), MouseButton.Left);
            Pump(window);
            Assert.Equal(270, customer.ActualWidth);
            AssertAligned(view, grid, customer, secondary, actions);
            Assert.DoesNotContain("column-resizing", header.Classes);
            Assert.False(Header(view, actions).GetVisualDescendants().OfType<Thumb>().Single().IsVisible);

            var otherGrip = Header(view, secondary).GetVisualDescendants().OfType<Thumb>().Single();
            Assert.True(otherGrip.Focus());
            for (int i = 0; i < 20; i++) Press(window, PhysicalKey.ArrowLeft);
            Assert.Equal(48, secondary.ActualWidth);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SlimOverlayScrollbarsKeepBothAxesDraggable()
    {
        var grid = CreateGrid(32, Column("Name", 350), Column("Details", 420));
        grid.RowHeight = 36;
        grid.HeadingHeight = 52;
        var window = Show(grid);
        try
        {
            var scroll = Assert.IsType<AlderScrollViewer>(View(grid).Scroll);
            var rows = scroll.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "RowsSurface");
            var rowsTop = Position(rows, scroll).Y;
            Assert.Equal(52, rowsTop, 1);
            var bars = scroll.GetVisualDescendants().OfType<ScrollBar>().ToArray();
            Assert.Equal(2, bars.Length);
            foreach (var bar in bars)
            {
                bool vertical = bar.Orientation == Orientation.Vertical;
                Assert.True(bar.IsVisible);
                Assert.Equal(10, vertical ? bar.Bounds.Width : bar.Bounds.Height);
                Assert.Equal(0, Grid.GetColumn(bar));
                var barTop = Position(bar, scroll).Y;
                Assert.InRange(barTop, rowsTop, rowsTop + rows.Bounds.Height - bar.Bounds.Height);
                Assert.All(bar.GetVisualDescendants().OfType<RepeatButton>(), button => Assert.Contains("Page", button.Name!));
                var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
                var fill = thumb.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "ThumbFill");
                Assert.Equal(new CornerRadius(3), fill.CornerRadius);
                Assert.Equal(6, vertical ? fill.Bounds.Width : fill.Bounds.Height);
                var point = thumb.TranslatePoint(new Rect(thumb.Bounds.Size).Center, window)!.Value;
                var end = vertical ? new Point(point.X, point.Y + 40) : new Point(point.X + 40, point.Y);
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseMove(end);
                Pump(window);
                window.MouseUp(end, MouseButton.Left);
                Pump(window);
                Assert.True(vertical ? scroll.Offset.Y > 0 : scroll.Offset.X > 0);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(TableVariant.Primary)]
    [InlineData(TableVariant.Secondary)]
    public void TableVerticalScrollbarStartsAtTheFirstDataRow(TableVariant variant)
    {
        var table = new AlderTable
        {
            Variant = variant,
            ItemsSource = Enumerable.Range(1, 40).ToArray(),
            Columns = { Column("Name", 240) },
        };
        var window = new AlderTestWindow { Width = 490, Height = 272, Content = table };
        window.Show();
        try
        {
            Pump(window);
            var view = table.GetVisualDescendants().OfType<TableView>().Single();
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            var vertical = ScrollBarFor(scroll, Orientation.Vertical);
            var rows = scroll.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "RowsSurface");
            Assert.True(vertical.IsVisible);
            Assert.True(Position(rows, scroll).Y > 0);
            Assert.Equal(Position(rows, scroll).Y, Position(vertical, scroll).Y, 1);
            Assert.Equal(rows.Bounds.Height, vertical.Bounds.Height, 1);

            table.ShowColumnHeaders = false;
            Pump(window);
            Assert.Equal(0, Position(vertical, scroll).Y, 1);
            Assert.Equal(rows.Bounds.Height, vertical.Bounds.Height, 1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PaginatedGridKeepsItsVerticalThumbBetweenTheHeaderAndFooterAtTheFarRight()
    {
        var grid = CreateGrid(10, Column("Member", 528), Column("Country", 149),
            Column("Start date", 160), Column("Teams", 330));
        grid.Height = 614;
        grid.MinRowHeight = 57;
        grid.SelectionMode = TableSelectionMode.Multiple;
        grid.ShowSelectionCheckboxes = true;
        var footer = new AlderPagination { PageCount = 10, Summary = "1 to 10 of 100" };
        grid.Footer = footer;
        var window = new AlderTestWindow { Width = 936, Height = 700, Content = grid };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(2);
        try
        {
            Pump(window);
            var scroll = Assert.IsType<AlderScrollViewer>(View(grid).Scroll);
            Assert.True(scroll.ScrollBarMaximum.X > 0 && scroll.ScrollBarMaximum.Y > 0);
            var bar = ScrollBarFor(scroll, Orientation.Vertical);
            var rows = scroll.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "RowsSurface");
            var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();

            scroll.Offset = new Vector(scroll.ScrollBarMaximum.X, 0);
            Pump(window);
            window.MouseMove(Position(thumb, window) + new Vector(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2));
            Pump(window);
            Assert.Equal(1, bar.Opacity);
            Assert.True(Position(thumb, window).Y >= Position(rows, window).Y);

            scroll.Offset = scroll.ScrollBarMaximum;
            Pump(window);
            Assert.True(Position(thumb, window).Y + thumb.Bounds.Height <= Position(footer, window).Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task OverlayScrollbarsRevealOnlyTheHoveredOrRecentlyScrolledAxis()
    {
        var grid = CreateGrid(50, Column("Wide", 800));
        grid.Margin = new Thickness(16);
        var window = new AlderTestWindow { Width = 490, Height = 272, Content = grid };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            var scroll = Assert.IsType<AlderScrollViewer>(View(grid).Scroll);
            var horizontal = ScrollBarFor(scroll, Orientation.Horizontal);
            var vertical = ScrollBarFor(scroll, Orientation.Vertical);
            Assert.True(horizontal.IsVisible && vertical.IsVisible);

            void Check(double horizontalOpacity, double verticalOpacity) =>
                Assert.Equal((horizontalOpacity, verticalOpacity), (horizontal.Opacity, vertical.Opacity));

            Check(0, 0);

            var header = scroll.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "HeaderSurface");
            window.MouseMove(new Point(Position(vertical, window).X + vertical.Bounds.Width / 2,
                Position(header, window).Y + header.Bounds.Height / 2));
            Pump(window);
            Check(0, 0);

            var inside = grid.TranslatePoint(new Point(30, 60), window)!.Value;
            window.MouseMove(inside);
            Pump(window);
            Check(0, 0);

            window.MouseMove(horizontal.TranslatePoint(new Rect(horizontal.Bounds.Size).Center, window)!.Value);
            Pump(window);
            Check(1, 0);
            var verticalTrack = vertical.TranslatePoint(new Rect(vertical.Bounds.Size).Center, window)!.Value;
            window.MouseMove(verticalTrack);
            Pump(window);
            Check(0, 1);
            window.MouseMove(inside);
            Pump(window);
            Check(0, 0);

            scroll.Offset = new Vector(60, 0);
            Pump(window);
            Check(1, 0);
            window.MouseWheel(inside, new Vector(0, -1));
            Pump(window);
            Assert.True(scroll.Offset.Y > 0);
            Check(1, 1);
            window.MouseMove(verticalTrack);
            await Task.Delay(720);
            Pump(window);
            Check(0, 1);
            window.MouseMove(inside);
            Pump(window);
            Check(0, 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0, 180, false, false)]
    [InlineData(2, 180, false, false)]
    [InlineData(2, 800, true, false)]
    [InlineData(50, 180, false, true)]
    public void OverlayScrollbarsRequireOverflowOnTheirOwnAxis(int rowCount, double columnWidth, bool horizontal, bool vertical)
    {
        var column = Column("Task", columnWidth);
        var grid = CreateGrid(rowCount, column);
        grid.RowHeight = 42;
        var window = Show(grid);
        try
        {
            var scroll = Assert.IsType<AlderScrollViewer>(View(grid).Scroll);
            var horizontalBar = ScrollBarFor(scroll, Orientation.Horizontal);
            var verticalBar = ScrollBarFor(scroll, Orientation.Vertical);
            scroll.Offset = new Vector(65, 65);
            Pump(window);
            Assert.Equal(horizontal, horizontalBar.IsVisible);
            Assert.Equal(vertical, verticalBar.IsVisible);

            grid.ItemsSource = new[] { 1, 2 };
            column.Width = new GridLength(180);
            Pump(window);
            Assert.Equal(default, scroll.ScrollBarMaximum);
            Assert.False(horizontalBar.IsVisible);
            Assert.False(verticalBar.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VirtualizationRecyclesRowsAndUsesAnExplicitRowHeight()
    {
        var start = Column("Name", 100, TableColumnPin.Start);
        var end = Column("Actions", 90, TableColumnPin.End);
        var grid = CreateGrid(100_000, start, Column("Description", 580), end);
        grid.RowHeight = 40;
        grid.HeadingHeight = 48;
        var window = Show(grid);
        try
        {
            var view = View(grid);
            Assert.Equal(100_000, grid.VisibleRowCount);
            Assert.InRange(view.GetRealizedContainers().Count(), 2, 15);
            Assert.All(view.GetRealizedContainers(), row => Assert.Equal(40, row.Bounds.Height));
            Assert.Equal(48, Header(view, grid.Columns[0]).Bounds.Height);
            view.ScrollIntoView(99_999);
            Pump(window);
            Assert.Contains(view.GetRealizedContainers(), row => view.IndexFromContainer(row) == 99_999);
            Assert.InRange(view.GetRealizedContainers().Count(), 2, 15);
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            scroll.Offset = new Vector(220, 0);
            Pump(window);
            Assert.Contains(view.GetRealizedContainers(), row => view.IndexFromContainer(row) == 0);
            AssertAligned(view, grid, start, end);

            grid.ItemsSource = Enumerable.Range(1, 24).ToArray();
            grid.Virtualized = false;
            Pump(window);
            Assert.Equal(24, view.GetRealizedContainers().Count());
            Assert.All(view.GetRealizedContainers(), row => Assert.True(double.IsNaN(row.Height)));
            grid.Virtualized = true;
            Pump(window);
            Assert.InRange(view.GetRealizedContainers().Count(), 2, 15);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FixedHeightRowsDeferOffscreenCellTemplatesAndRestoreThemOnScroll()
    {
        var columns = Enumerable.Range(0, 30).Select(i => Column($"Column {i}", 100)).ToArray();
        var grid = CreateGrid(1000, columns);
        grid.RowHeight = 36;
        var window = Show(grid);
        try
        {
            var view = View(grid);
            var row = Assert.IsType<TableViewRow>(view.ContainerFromIndex(0));
            var cells = row.GetVisualDescendants().OfType<TableViewCell>().ToArray();
            Assert.Equal(30, cells.Length);
            Assert.True(cells[0].IsVisible);
            Assert.False(cells[^1].IsVisible);
            Assert.Empty(cells[^1].GetVisualChildren());
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            scroll.Offset = new Vector(scroll.Extent.Width - scroll.Viewport.Width, 0);
            Pump(window);
            Assert.False(cells[0].IsVisible);
            Assert.True(cells[^1].IsVisible);
            Assert.NotEmpty(cells[^1].GetVisualChildren());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TableAutoHeightMeasuresWideCellsAndFixedHeightDefersTheirTemplates()
    {
        var table = new AlderTable { ItemsSource = new[] { 1, 2, 3 } };
        var columns = Enumerable.Range(0, 12).Select(i => Column($"Column {i}", 100)).ToArray();
        columns[^1].CellTemplate = new FuncDataTemplate<int>((_, _) => new Border { Height = 90 });
        table.Columns.AddRange(columns);
        var window = Show(table);
        try
        {
            var view = View(table);
            var row = Assert.IsType<TableViewRow>(view.ContainerFromIndex(0));
            var cells = row.GetVisualDescendants().OfType<TableViewCell>().ToArray();
            Assert.True(row.Bounds.Height >= 90);
            Assert.NotEmpty(cells[^1].GetVisualChildren());

            table.RowHeight = 40;
            Pump(window);
            Assert.Equal(40, row.Bounds.Height);
            Assert.False(cells[^1].IsVisible);
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            scroll.Offset = new Vector(scroll.Extent.Width - scroll.Viewport.Width, 0);
            Pump(window);
            Assert.True(cells[^1].IsVisible);
            Assert.False(cells[0].IsVisible);

            table.RowHeight = double.NaN;
            Pump(window);
            Assert.True(row.Bounds.Height >= 90);
            Assert.True(cells[0].IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExternalSortPublishesDescriptorAndPreservesHostOrderAndSelection()
    {
        var column = Column("Amount", 180);
        column.CanSort = true;
        column.SortComparison = (left, right) => ((int)left).CompareTo((int)right);
        var grid = CreateGrid(3, column);
        grid.ItemsSource = new[] { 3, 1, 2 };
        grid.SortMode = DataGridSortMode.External;
        grid.SelectionMode = TableSelectionMode.Multiple;
        grid.SelectedKeys = [2];
        var changed = new List<AlderTableSort>();
        grid.SortChangedCommand = new CallbackCommand(value => changed.Add(Assert.IsType<AlderTableSort>(value)));
        var window = Show(grid);
        try
        {
            var view = View(grid);
            Assert.True(Header(view, column).GetVisualDescendants().OfType<Button>().Single(x => Equals(x.Content, "Amount")).Focus());
            Press(window, PhysicalKey.Enter);
            Assert.Equal(new AlderTableSort(column.Key!, TableSortDirection.Ascending), Assert.Single(changed));
            Assert.Equal(new[] { 3, 1, 2 }, view.ItemsView.OfType<int>());
            grid.ItemsSource = new[] { 1, 2, 3 };
            Pump(window);
            Assert.Equal(2, view.SelectedItem);
            grid.SortMode = DataGridSortMode.Local;
            grid.SortDescriptor = new AlderTableSort(column.Key!, TableSortDirection.Descending);
            Pump(window);
            Assert.Equal(new[] { 3, 2, 1 }, view.ItemsView.OfType<int>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardAndPointerReorderSendTheSameKeyedMoveAndEscapeCancels()
    {
        var grid = CreateGrid(5, Column("Task", 250));
        grid.RowHeight = 38;
        grid.CanReorderRows = true;
        var moves = new List<DataGridReorderRequest>();
        grid.ReorderCommand = new CallbackCommand(value => moves.Add(Assert.IsType<DataGridReorderRequest>(value)));
        var window = Show(grid);
        try
        {
            var view = View(grid);
            var handle = DragHandle(view, 0);
            Assert.True(handle.Focus());
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Contains(view.GetRealizedContainers(), row => row.Classes.Contains("grid-drop-after"));
            Press(window, PhysicalKey.Escape);
            Assert.Empty(moves);
            Assert.True(handle.IsFocused);

            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            var move = Assert.Single(moves);
            Assert.Equal(new DataGridReorderRequest(1, 2, DataGridDropPosition.After, null), move);

            var source = handle.TranslatePoint(new Point(handle.Bounds.Width / 2, handle.Bounds.Height / 2), window)!.Value;
            var row = view.ContainerFromIndex(1)!;
            var target = new Point(source.X, row.TranslatePoint(new Point(0, row.Bounds.Height * .8), window)!.Value.Y);
            window.MouseMove(source);
            window.MouseDown(source, MouseButton.Left);
            window.MouseMove(target, RawInputModifiers.LeftMouseButton);
            Pump(window);
            Assert.Equal(0.5, view.ContainerFromIndex(0)!.GetVisualDescendants()
                .Single(control => control.GetType().Name == "AlderDataGridCellsPresenter").Opacity);
            Assert.Contains("grid-drop-after", row.Classes);
            window.MouseUp(target, MouseButton.Left);
            Pump(window);
            Assert.Equal(new[] { move, move }, moves);
            Assert.Equal(1, view.ItemsView[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RowSurfaceSupportsReorderAndEmbeddedButtonStillClicks()
    {
        int clicks = 0;
        var task = Column("Task", 200);
        var actions = Column("Action", 120);
        actions.CellTemplate = new FuncDataTemplate<int>((_, _) =>
        {
            var button = new AlderButton { Content = "Action" };
            button.Click += (_, _) => clicks++;
            return button;
        });
        var grid = CreateGrid(5, task, actions);
        grid.RowHeight = 42;
        grid.CanReorderRows = true;
        var moves = new List<DataGridReorderRequest>();
        grid.ReorderCommand = new CallbackCommand(value => moves.Add(Assert.IsType<DataGridReorderRequest>(value)));
        var window = Show(grid);
        try
        {
            var view = View(grid);
            var first = view.ContainerFromIndex(0)!;
            var action = first.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Action"));
            var click = action.TranslatePoint(new Rect(action.Bounds.Size).Center, window)!.Value;
            window.MouseMove(click);
            window.MouseDown(click, MouseButton.Left);
            window.MouseUp(click, MouseButton.Left);
            Pump(window);
            Assert.Equal(1, clicks);
            Assert.Empty(moves);

            var taskCell = first.GetVisualDescendants().OfType<TableViewCell>().Single(cell => cell.Column == task);
            var origin = taskCell.TranslatePoint(new Rect(taskCell.Bounds.Size).Center, window)!.Value;
            var target = view.ContainerFromIndex(2)!;
            var landing = new Point(origin.X, target.TranslatePoint(new Point(0, target.Bounds.Height * .9), window)!.Value.Y);
            window.MouseMove(origin);
            window.MouseDown(origin, MouseButton.Left);
            window.MouseMove(landing, RawInputModifiers.LeftMouseButton);
            Pump(window);
            Assert.Contains("grid-drag-source", first.Classes);
            Assert.Contains("grid-drop-after", target.Classes);
            window.MouseUp(landing, MouseButton.Left);
            Pump(window);
            Assert.Equal(new DataGridReorderRequest(1, 3, DataGridDropPosition.After, null), Assert.Single(moves));
            Assert.DoesNotContain("grid-drag-source", first.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public Task DraggedRowPreviewFollowsPointerWhileTheBodyAutoScrolls() => VerifyDraggedRowPreview();

    internal static async Task VerifyDraggedRowPreview(Action<Window>? configureWindow = null)
    {
        var column = Column("Task", 340);
        column.CellTemplate = new FuncDataTemplate<int>((id, _) => new TextBlock { Text = $"Task {id}" });
        var grid = CreateGrid(50, column);
        grid.RowHeight = 42;
        grid.CanReorderRows = true;
        var moves = new List<DataGridReorderRequest>();
        grid.ReorderCommand = new CallbackCommand(value => moves.Add(Assert.IsType<DataGridReorderRequest>(value)));
        var window = Show(grid);
        try
        {
            configureWindow?.Invoke(window);
            Pump(window);
            var view = View(grid);
            var row = view.ContainerFromIndex(0)!;
            var grip = DragHandle(view, 0);
            var origin = grip.TranslatePoint(new Rect(grip.Bounds.Size).Center, window)!.Value;
            window.MouseMove(origin);
            window.MouseDown(origin, MouseButton.Left);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "DataGridDragPreview");

            var first = new Point(origin.X + 38, origin.Y + 55);
            window.MouseMove(first, RawInputModifiers.LeftMouseButton);
            Pump(window);
            var preview = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "DataGridDragPreview");
            var image = preview.GetVisualDescendants().OfType<Image>().Single();
            var source = Assert.IsType<global::Avalonia.Media.Imaging.RenderTargetBitmap>(image.Source);
            var expected = Position(row, window) + (first - origin);
            Assert.Null(preview.RenderTransform);
            var actual = PreviewPosition(preview, window);
            Assert.Equal(expected.X - 1, actual.X, 1);
            Assert.Equal(expected.Y - 1, actual.Y, 1);
            Assert.False(preview.IsHitTestVisible);

            var nearBottom = new Point(first.X + 45, window.ClientSize.Height - 12);
            window.MouseMove(nearBottom, RawInputModifiers.LeftMouseButton);
            Pump(window);
            var atBottom = PreviewPosition(preview, window);
            Assert.Equal(atBottom.X - actual.X, nearBottom.X - first.X, 1);
            Assert.Equal(atBottom.Y - actual.Y, nearBottom.Y - first.Y, 1);

            await Task.Delay(210);
            Pump(window);
            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(atBottom, PreviewPosition(preview, window));
            Assert.Same(source, image.Source);

            // Native backends can send the last move without the button flag before dispatching release.
            var released = nearBottom - new Vector(12, 6);
            window.MouseMove(released);
            Pump(window);
            Assert.Equal(atBottom - new Vector(12, 6), PreviewPosition(preview, window));
            window.MouseUp(released, MouseButton.Left);
            Pump(window);
            Assert.Equal(1, Assert.Single(moves).RowKey);
            Assert.Null(preview.GetVisualParent());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void PointerDropAnimatesOnlyTheAffectedRealizedRows(bool reducedMotion, bool commitMove)
    {
        var rows = new ObservableCollection<int>(Enumerable.Range(1, 500));
        var grid = CreateGrid(0, Column("Task", 340));
        grid.ItemsSource = rows;
        grid.RowHeight = 42;
        grid.CanReorderRows = true;
        grid.ReorderCommand = new CallbackCommand(_ =>
        {
            if (commitMove) Dispatcher.UIThread.Post(() => rows.Move(0, 3), DispatcherPriority.Background);
        });
        var window = Show(grid);
        try
        {
            AlderMotion.SetReduced(window, reducedMotion);
            var view = View(grid);
            var handle = DragHandle(view, 0);
            var origin = handle.TranslatePoint(new Rect(handle.Bounds.Size).Center, window)!.Value;
            var target = view.ContainerFromIndex(3)!;
            var landing = target.TranslatePoint(new Point(20, target.Bounds.Height * .75), window)!.Value;
            window.MouseMove(origin);
            window.MouseDown(origin, MouseButton.Left);
            window.MouseMove(landing, RawInputModifiers.LeftMouseButton);
            window.MouseUp(landing, MouseButton.Left);
            Pump(window);

            Assert.Equal(commitMove ? new[] { 2, 3, 4, 1, 5 } : [1, 2, 3, 4, 5], view.ItemsView.OfType<int>().Take(5));
            var movedRow = view.ContainerFromIndex(commitMove ? 3 : 0)!;
            Assert.Equal(commitMove && !reducedMotion, movedRow.Classes.Contains("grid-drop-settling"));
            var field = typeof(AlderDataGrid).GetField("movingRows", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var moving = Assert.IsType<HashSet<Control>>(field.GetValue(grid));
            Assert.Equal(commitMove && !reducedMotion ? new[] { 2, 3, 4 } : [], moving.Select(row => (int)row.DataContext!).Order());

            var scroll = Assert.IsType<AlderScrollViewer>(view.Scroll);
            scroll.Offset = new Vector(0, 4200);
            Pump(window);
            Assert.Empty(moving);
            Assert.DoesNotContain("grid-drop-settling", movedRow.Classes);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "DataGridDragPreview");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollingToEndLoadsMoreWithoutReenteringTheCommand()
    {
        var rows = new ObservableCollection<int>(Enumerable.Range(1, 20));
        var grid = CreateGrid(0, Column("Invoice", 250));
        grid.RowHeight = 38;
        grid.ItemsSource = rows;
        grid.HasMoreItems = true;
        int calls = 0;
        grid.LoadMoreCommand = new CallbackCommand(_ =>
        {
            calls++;
            grid.IsLoading = true;
        });
        var window = Show(grid);
        try
        {
            var scroll = Assert.IsType<AlderScrollViewer>(View(grid).Scroll);
            Assert.Equal(0, calls);
            scroll.Offset = new Vector(0, scroll.ScrollBarMaximum.Y);
            Pump(window);
            Assert.Equal(1, calls);
            Pump(window);
            Assert.Equal(1, calls);
            for (int i = 21; i <= 30; i++) rows.Add(i);
            grid.IsLoading = false;
            Pump(window);
            Assert.Equal(30, grid.VisibleRowCount);
            Assert.Equal(1, calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LoadingWaitsUntilGridEntersOuterViewport()
    {
        var grid = CreateGrid(1, Column("Invoice", 250));
        grid.Height = 150;
        grid.HasMoreItems = true;
        int calls = 0;
        grid.LoadMoreCommand = new CallbackCommand(_ => calls++);
        var outer = new AlderScrollViewer
        {
            Content = new StackPanel
            {
                Children = { new Border { Height = 600 }, grid }
            }
        };
        var window = new AlderTestWindow { Width = 490, Height = 272, Content = outer };
        window.Show();
        try
        {
            Pump(window);
            Assert.Equal(0, calls);
            outer.ScrollToEnd();
            Pump(window);
            Assert.Equal(1, calls);
        }
        finally { window.Close(); }
    }

    private static AlderDataGrid CreateGrid(int rows, params AlderTableColumn[] columns)
    {
        var grid = new AlderDataGrid { RowKeySelector = item => item, ItemsSource = Enumerable.Range(1, rows).ToArray() };
        foreach (var column in columns) grid.Columns.Add(column);
        return grid;
    }

    private static AlderTableColumn Column(string title, double width, TableColumnPin pin = TableColumnPin.None) =>
        new() { Header = title, Key = title.ToLowerInvariant(), Width = new GridLength(width), Pin = pin };

    private static TableView View(AlderTable grid) => grid.GetVisualDescendants().OfType<TableView>().Single();
    private static ScrollBar ScrollBarFor(ScrollViewer scroll, Orientation orientation) =>
        scroll.GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Orientation == orientation);
    private static TableViewColumnHeader Header(TableView view, TableViewColumn column) =>
        view.GetVisualDescendants().OfType<TableViewColumnHeader>().Single(header => header.Column == column);
    private static Point Position(Control control, Control relativeTo) => control.TranslatePoint(default, relativeTo)!.Value;

    private static Point PreviewPosition(Control preview, Control relativeTo)
    {
        var visual = ElementComposition.GetElementVisual(preview)!;
        return Position(preview, relativeTo) + new Vector(visual.Translation.X, visual.Translation.Y);
    }

    private static Button DragHandle(TableView view, int index) =>
        view.ContainerFromIndex(index)!.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Reorder row");

    private static void AssertAligned(TableView view, AlderDataGrid grid, params TableViewColumn[] columns)
    {
        var row = view.GetRealizedContainers().First();
        foreach (var column in columns)
        {
            var cell = row.GetVisualDescendants().OfType<TableViewCell>().Single(item => item.Column == column);
            Assert.Equal(Position(Header(view, column), grid).X, Position(cell, grid).X, 1);
        }
    }

    private static Window Show(AlderTable grid)
    {
        var window = new AlderTestWindow { Width = 490, Height = 272, Content = grid };
        window.Show();
        Pump(window);
        return window;
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

    private sealed class CallbackCommand(Action<object?> callback) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => callback(parameter);
    }
}
