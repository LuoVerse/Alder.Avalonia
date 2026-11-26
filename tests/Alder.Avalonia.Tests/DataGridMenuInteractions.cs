using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class DataGridMenuInteractions
{
    [AvaloniaFact]
    public void MenuFlyoutUsesTheDefaultAlderTemplateWithoutExplicitThemes()
    {
        var branch = new AlderMenuItem { Header = "More", Items = { "Child action" } };
        var flyout = new AlderMenuFlyout { Items = { "First action", branch }, VerticalOffset = 8 };
        var button = new AlderButton { Content = "Actions", Flyout = flyout };
        var window = new Window { Width = 500, Height = 380, Content = new Border { Child = button } };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            Assert.True(button.Focus());
            Press(window, PhysicalKey.Enter);
            Assert.True(flyout.IsOpen);

            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.NotNull(presenter.FindAncestorOfType<OverlayPopupHost>());
            var surface = presenter.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "Surface");
            Assert.Equal(new CornerRadius(22), surface.CornerRadius);
            Assert.False(surface.ClipToBounds);
            Assert.True(surface.BoxShadow.Count > 0);
            Assert.IsType<AlderMenuItem>(presenter.ContainerFromIndex(0));
            Assert.Contains(presenter.GetVisualDescendants(), c => c is AlderScrollViewer);

            Assert.True(branch.Focus());
            Press(window, PhysicalKey.ArrowRight);
            Assert.True(branch.IsSubMenuOpen);
            Assert.IsType<AlderMenuItem>(branch.ContainerFromIndex(0));
            Press(window, PhysicalKey.ArrowLeft);
            Assert.False(branch.IsSubMenuOpen);

            Press(window, PhysicalKey.Escape);
            Assert.False(flyout.IsOpen);
            Assert.True(button.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MenuFlyoutPresentsItemsAddedOnOpeningAndTracksReplacementSources()
    {
        var flyout = new AlderMenuFlyout();
        var button = new AlderButton { Content = "Actions", Flyout = flyout };
        var window = new Window { Width = 500, Height = 380, Content = button };
        AlderMotion.SetReduced(window, true);
        EventHandler populate = (_, _) => flyout.Items.Add("Added on opening");
        flyout.Opening += populate;
        window.Show();
        try
        {
            flyout.ShowAt(button);
            Pump(window);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.Equal("Added on opening", Assert.IsType<AlderMenuItem>(presenter.ContainerFromIndex(0)).Header);

            flyout.Hide();
            flyout.Opening -= populate;
            flyout.Items.Clear();
            var original = new ObservableCollection<string>();
            var replacement = new ObservableCollection<string>();
            flyout.ItemsSource = original;
            flyout.ItemsSource = replacement;
            original.Add("Old source");
            replacement.Add("Current source");
            flyout.ShowAt(button);
            Pump(window);

            Assert.Same(presenter, flyout.Popup.Child);
            Assert.Equal(1, presenter.ItemCount);
            Assert.Equal("Current source", Assert.IsType<AlderMenuItem>(presenter.ContainerFromIndex(0)).Header);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, 180)]
    [InlineData(true, 160)]
    public async Task FirstMouseClickOpensPreparedMenuAtTheWindowEdge(bool dark, int menuWidth)
    {
        var grid = CreateGrid([new Row(1), new Row(2)], menuWidth, new RowCommand(_ => { }));
        var window = new Window { Width = 540, Height = 340, Content = grid, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Pump(window);
            var button = ActionButton(grid, 0);
            var flyout = Assert.IsType<AlderDropdownFlyout>(button.Flyout);
            var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            var prepared = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.False(flyout.IsOpen);

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(flyout.IsOpen);
            Assert.Same(prepared, flyout.Popup.Child);
            Pump(window);

            Assert.NotNull(prepared.FindAncestorOfType<OverlayPopupHost>());
            var surface = prepared.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Surface");
            var origin = surface.TranslatePoint(default, window)!.Value;
            var anchor = button.TranslatePoint(new Point(0, button.Bounds.Height), window)!.Value;
            Assert.Equal(menuWidth, surface.Bounds.Width, 1);
            Assert.Equal(124, surface.Bounds.Height, 1);
            Assert.Equal(new CornerRadius(22), surface.CornerRadius);
            Assert.Equal(anchor.Y + 8, origin.Y, 1);
            Assert.Equal(window.ClientSize.Width - 12, origin.X + surface.Bounds.Width, 1);
            var view = Assert.IsType<MenuItem>(prepared.Items[0]);
            var icon = view.GetVisualDescendants().OfType<PathIcon>().Single(path => path.IsEffectivelyVisible);
            var label = view.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(presenter => presenter.Name == "PART_HeaderPresenter");
            Assert.True(icon.TranslatePoint(default, view)!.Value.X < label.TranslatePoint(default, view)!.Value.X);

            Press(window, PhysicalKey.Escape);
            await Task.Delay(160);
            Pump(window);
            Assert.False(flyout.IsOpen);
            Assert.True(button.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardOpensAfterScrollingAndTheActionUsesTheCurrentRow()
    {
        var executed = new List<int>();
        var grid = CreateGrid(Enumerable.Range(1, 60).Select(i => new Row(i)).ToArray(), 180, new RowCommand(executed.Add));
        var window = new Window { Width = 720, Height = 320, Content = grid };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            var table = grid.GetVisualDescendants().OfType<TableView>().Single();
            table.ScrollIntoView(59);
            Pump(window);
            var button = ActionButton(grid, 59);
            Assert.True(button.Focus());
            var flyout = Assert.IsType<AlderDropdownFlyout>(button.Flyout);
            Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);

            Press(window, PhysicalKey.Enter);
            Assert.True(flyout.IsOpen);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.Same(button.DataContext, presenter.DataContext);
            var item = Assert.IsType<MenuItem>(presenter.Items[0]);
            Assert.True(item.Focus());
            Press(window, PhysicalKey.Enter);
            Assert.Equal(60, Assert.Single(executed));
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(TableColumnPin.None, false)]
    [InlineData(TableColumnPin.End, false)]
    [InlineData(TableColumnPin.End, true)]
    public void StandardRowMenuGetsATemporaryAnchorAndPreservesExplicitTargets(TableColumnPin pin, bool explicitTarget)
    {
        var grid = CreateGrid([new Row(1)], 160, new RowCommand(_ => { }));
        grid.Columns[1].Pin = pin;
        var window = new Window { Width = 540, Height = 340, Content = grid };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Pump(window);
            var scroll = grid.GridScroll!;
            scroll.Offset = new Vector(scroll.ScrollBarMaximum.X, 0);
            Pump(window);
            var button = ActionButton(grid, 0);
            var menu = new MenuFlyout { Items = { new AlderMenuItem { Header = "Edit" } } };
            menu.Popup.ShouldUseOverlayLayer = true;
            if (explicitTarget) menu.OverlayInputPassThroughElement = grid;
            button.Flyout = menu;

            var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump(window);

            Assert.True(menu.IsOpen);
            Assert.Same(explicitTarget ? (Control)grid : button, menu.Popup.OverlayInputPassThroughElement);
            Assert.Same(explicitTarget ? grid : null, menu.OverlayInputPassThroughElement);
        }
        finally { window.Close(); }
    }

    private static AlderDataGrid CreateGrid(Row[] rows, double menuWidth, ICommand action)
    {
        var grid = new AlderDataGrid { ItemsSource = rows, RowKeySelector = row => ((Row)row).Id, RowHeight = 56 };
        grid.Columns.Add(new AlderTableColumn { Key = "name", Header = "Name", Width = new GridLength(900) });
        grid.Columns.Add(new AlderTableColumn
        {
            Key = "actions",
            Pin = TableColumnPin.End,
            Width = new GridLength(53),
            CellTheme = (ControlTheme)Application.Current!.FindResource("AlderDataGridActionsCellTheme")!,
            CellTemplate = new FuncDataTemplate<Row>((_, _) =>
            {
                var view = new MenuItem { Header = "View", Command = action, Icon = new PathIcon { Width = 16, Height = 16 } };
                view.Bind(MenuItem.CommandParameterProperty, new Binding(nameof(Row.Id)));
                return new AlderButton
                {
                    Theme = (ControlTheme)Application.Current!.FindResource("AlderDataGridRowActionsButtonTheme")!,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    Flyout = new AlderDropdownFlyout
                    {
                        MinMenuWidth = menuWidth,
                        Items = { view, new MenuItem { Header = "Edit" }, new MenuItem { Header = "Delete", Classes = { "danger" } } }
                    }
                };
            }, supportsRecycling: true)
        });
        return grid;
    }

    private static AlderButton ActionButton(AlderDataGrid grid, int index) =>
        grid.GetVisualDescendants().OfType<TableView>().Single().ContainerFromIndex(index)!
            .GetVisualDescendants().OfType<AlderButton>().Single();

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

    private sealed record Row(int Id);

    private sealed class RowCommand(Action<int> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is int;
        public void Execute(object? parameter) => execute((int)parameter!);
    }
}
