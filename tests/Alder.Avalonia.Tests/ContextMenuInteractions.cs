using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ContextMenuInteractions
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextEditorsOpenAlderClipboardMenus(bool otp)
    {
        TextBox input = otp ? new AlderInputOtp { Text = "123456" } : new AlderInput { Text = "123456" };
        var window = Show(input);
        var copying = 0;
        input.CopyingToClipboard += (_, _) => copying++;
        try
        {
            Assert.True(input.Focus());
            input.SelectAll();
            var point = input.TranslatePoint(new Point(16, input.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Refresh(window);

            var flyout = Assert.IsType<AlderTextEditMenuFlyout>(input.ContextFlyout);
            Assert.True(flyout.IsOpen);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            var rows = presenter.Items.Cast<AlderMenuItem>().ToArray();
            Assert.Equal(3, rows.Length);
            Assert.True(rows[0].IsEffectivelyEnabled);
            Assert.True(rows[1].Focus());
            Press(window, PhysicalKey.Enter);
            Refresh(window);
            Assert.Equal(1, copying);
            Assert.False(flyout.IsOpen);

            input.IsReadOnly = true;
            Assert.True(input.Focus());
            input.SelectAll();
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Refresh(window);
            Assert.True(flyout.IsOpen);
            Assert.False(rows[0].IsEffectivelyEnabled);
            Assert.True(rows[1].IsEffectivelyEnabled);
            Assert.False(rows[2].IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task TouchHoldOpensTheHorizontalAlderClipboardMenu()
    {
        var input = new AlderInput { Text = "Hold to copy", IsReadOnly = true };
        var window = Show(input);
        try
        {
            var point = input.TranslatePoint(new Point(30, input.Bounds.Height / 2), window)!.Value;
            using var holding = window.TouchBegin(point);
            var wait = Application.Current!.PlatformSettings!.HoldWaitDuration + TimeSpan.FromMilliseconds(100);
            await Task.Delay(wait);
            Refresh(window);

            var flyout = Assert.IsType<AlderTextEditMenuFlyout>(input.ContextFlyout);
            Assert.True(flyout.IsOpen);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.Equal(Orientation.Horizontal, Assert.IsType<StackPanel>(presenter.ItemsPanelRoot).Orientation);
            var rows = presenter.Items.Cast<AlderMenuItem>().ToArray();
            Assert.False(rows[0].IsVisible);
            Assert.True(rows[2].IsVisible);
            Assert.False(rows[2].IsEffectivelyEnabled);
            window.TouchEnd(holding, point);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectableTextOpensTheAlderCopyMenuAfterSelection()
    {
        var text = new AlderSelectableTextBlock { Text = "Selectable text" };
        var window = Show(text);
        var copying = 0;
        text.CopyingToClipboard += (_, _) => copying++;
        try
        {
            Assert.True(text.Focus());
            text.SelectionStart = 0;
            text.SelectionEnd = text.Text!.Length;
            var flyout = Assert.IsType<AlderTextEditMenuFlyout>(text.ContextFlyout);
            flyout.ShowAt(text);
            Refresh(window);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            var copy = Assert.IsType<AlderMenuItem>(Assert.Single(presenter.Items));
            Assert.True(copy.Focus());
            Press(window, PhysicalKey.Enter);
            Refresh(window);
            Assert.Equal(1, copying);
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BoundContextMenuItemsUseAlderMenuItems()
    {
        var menu = new AlderContextMenu { ItemsSource = new[] { "Archive", "Move" } };
        var target = new Border { Background = Brushes.Transparent, ContextMenu = menu };
        var window = Show(target);
        try
        {
            menu.Open(target);
            Refresh(window);
            var row = Assert.IsType<AlderMenuItem>(menu.ContainerFromIndex(0));
            Assert.Equal("Archive", row.Header);
            Assert.NotNull(row.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_LayoutRoot"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PointerAndKeyboardOpenMenuAndReturnFocusAfterDismissal()
    {
        var action = new MenuItem { Header = "Open" };
        var menu = new AlderContextMenu { Items = { new MenuItem { Header = "Disabled", IsEnabled = false }, action } };
        var target = new Border { Focusable = true, Background = Brushes.Transparent, ContextMenu = menu };
        var window = Show(target);
        int invoked = 0;
        action.Click += (_, _) => invoked++;
        try
        {
            Assert.True(target.Focus());
            var pointer = new Point(25, 30);
            window.MouseDown(pointer, MouseButton.Right);
            window.MouseUp(pointer, MouseButton.Right);
            Refresh(window);

            Assert.True(menu.IsOpen);
            Assert.True(menu.IsMenuOpen);
            Assert.True(menu.IsFocused);
            Assert.Equal(new Point(pointer.X, pointer.Y + 2), window.PointToClient(menu.PointToScreen(default)));
            Assert.Equal(220, menu.Bounds.Width);
            Assert.Equal(36, action.Bounds.Height);
            Assert.Equal(new CornerRadius(24), menu.GetVisualDescendants().OfType<Border>().First().CornerRadius);

            Press(window, PhysicalKey.ArrowDown);
            Assert.True(action.IsFocused);
            Press(window, PhysicalKey.Enter);
            Refresh(window);
            Assert.Equal(1, invoked);
            Assert.False(menu.IsMenuOpen);
            Assert.True(target.IsFocused);

            Press(window, PhysicalKey.F10, RawInputModifiers.Shift);
            Refresh(window);
            Assert.True(menu.IsOpen);
            Press(window, PhysicalKey.Escape);
            Refresh(window);
            Assert.False(menu.IsOpen);
            Assert.True(target.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ControlledStateOpensOnAttachAndHonorsDisabledAndDismissal()
    {
        var target = new Border { Background = Brushes.Transparent };
        var menu = new AlderContextMenu
        {
            Items = { new MenuItem { Header = "Open" } }, PlacementTarget = target, IsMenuOpen = true
        };
        target.ContextMenu = menu;
        var window = Show(target);
        try
        {
            Assert.True(menu.IsOpen);
            menu.IsMenuOpen = false;
            Assert.False(menu.IsOpen);
            menu.IsEnabled = false;
            menu.Open(target);
            Assert.False(menu.IsOpen);
            menu.IsMenuOpen = true;
            Refresh(window);
            Assert.False(menu.IsOpen);
            Assert.False(menu.IsMenuOpen);

            var point = new Point(20, 30);
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Assert.False(menu.IsOpen);

            menu.IsEnabled = true;
            menu.IsMenuOpen = true;
            Refresh(window);
            Assert.True(menu.IsOpen);
            menu.Close();
            Assert.False(menu.IsMenuOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task PointerMenuAnimatesOutAndHonorsReducedMotion()
    {
        var menu = new AlderContextMenu { Items = { new MenuItem { Header = "Options" } } };
        var target = new Border { Background = Brushes.Transparent, ContextMenu = menu };
        var window = Show(target);
        try
        {
            AlderMotion.SetReduced(window, false);
            var point = new Point(30, 30);
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Refresh(window);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            var popup = Assert.IsType<Popup>(menu.Parent);
            menu.Close();
            Assert.True(menu.IsOpen);
            Assert.False(popup.IsHitTestVisible);
            await Task.Delay(160);
            Assert.False(menu.IsOpen);
            Assert.True(popup.IsHitTestVisible);

            AlderMotion.SetReduced(window, true);
            menu.Open(target);
            Refresh(window);
            Assert.True(menu.IsOpen);
            menu.Close();
            Assert.False(menu.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task HoldingOpensMenuAndMovingCancelsTheGesture()
    {
        var menu = new AlderContextMenu { Items = { new MenuItem { Header = "Copy" } } };
        var target = new Border { Background = Brushes.Transparent, ContextMenu = menu };
        InputElement.SetIsHoldingEnabled(target, true);
        var window = Show(target);
        try
        {
            var point = new Point(30, 30);
            var wait = Application.Current!.PlatformSettings!.HoldWaitDuration + TimeSpan.FromMilliseconds(100);
            using (var moving = window.TouchBegin(point))
            {
                window.TouchMove(moving, new Point(90, 90));
                await Task.Delay(wait);
                Assert.False(menu.IsOpen);
                window.TouchEnd(moving, new Point(90, 90));
            }

            var holdPoint = new Point(60, 50);
            using var holding = window.TouchBegin(holdPoint);
            await Task.Delay(wait);
            Refresh(window);
            Assert.True(menu.IsOpen);
            window.TouchEnd(holding, holdPoint);
            Assert.True(menu.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(FlowDirection.LeftToRight, PhysicalKey.ArrowRight)]
    [InlineData(FlowDirection.RightToLeft, PhysicalKey.ArrowLeft)]
    public void KeyboardNavigatesSectionsAndSubmenusAndCommitsSelection(FlowDirection direction, PhysicalKey submenuKey)
    {
        var first = new MenuItem { Header = "Grid", ToggleType = MenuItemToggleType.Radio, IsChecked = true };
        var second = new MenuItem { Header = "List", ToggleType = MenuItemToggleType.Radio };
        var checkbox = new MenuItem { Header = "Show labels", ToggleType = MenuItemToggleType.CheckBox, StaysOpenOnClick = true };
        var more = new MenuItem { Header = "More", Items = { checkbox } };
        var menu = new AlderContextMenu
        {
            Items = { new AlderContextMenuHeading { Text = "View" }, first, second, new Separator(), more }
        };
        var target = new Border { Background = Brushes.Transparent, ContextMenu = menu, FlowDirection = direction };
        var window = Show(target);
        try
        {
            menu.Open(target);
            Refresh(window);
            Press(window, PhysicalKey.ArrowDown);
            Assert.True(first.IsFocused);
            Press(window, PhysicalKey.ArrowDown);
            Assert.True(second.IsFocused);
            Press(window, PhysicalKey.Enter);
            Assert.False(first.IsChecked);
            Assert.True(second.IsChecked);
            Assert.False(menu.IsOpen);

            menu.Open(target);
            Refresh(window);
            Assert.True(menu.IsOpen);
            Assert.True(second.Focus());
            Press(window, PhysicalKey.ArrowDown);
            Assert.True(more.IsFocused, $"Focused: {window.FocusManager?.GetFocusedElement()}, selected: {menu.SelectedIndex}");
            Press(window, submenuKey);
            Refresh(window);
            Assert.True(more.IsSubMenuOpen);
            Assert.True(checkbox.IsFocused);
            Assert.Equal(36, checkbox.Bounds.Height);
            Press(window, PhysicalKey.Enter);
            Assert.True(checkbox.IsChecked);
            Assert.True(menu.IsOpen);
            Press(window, PhysicalKey.Escape);
            Assert.False(more.IsSubMenuOpen);
            Assert.True(more.IsFocused);
            Press(window, PhysicalKey.Escape);
            Assert.False(menu.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TallMenuStaysInShortWindowAndScrollsToTheSelectedItem()
    {
        var menu = new AlderContextMenu();
        for (int index = 0; index < 16; index++)
            menu.Items.Add(new MenuItem { Header = $"Action {index}" });
        var target = new Border { Background = Brushes.Transparent, ContextMenu = menu };
        var window = Show(target);
        try
        {
            window.Height = 220;
            Refresh(window);
            var point = new Point(35, 200);
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Refresh(window);
            Assert.True(menu.IsOpen);
            var origin = window.PointToClient(menu.PointToScreen(default));
            Assert.InRange(origin.Y, 0, window.ClientSize.Height - menu.Bounds.Height);
            var scroll = Assert.Single(menu.GetVisualDescendants().OfType<ScrollViewer>());
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            for (int index = 0; index < menu.ItemCount; index++) Press(window, PhysicalKey.ArrowDown);
            Refresh(window);
            Assert.True(scroll.Offset.Y > 0);
            Assert.True(((MenuItem)menu.Items[^1]!).IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ContextMenuVirtualizesLargeRootAndExpandedSubmenu()
    {
        var check = new MenuItem { Header = "Last enabled child", ToggleType = MenuItemToggleType.CheckBox, StaysOpenOnClick = true };
        var hidden = new MenuItem { Header = "Hidden child", IsVisible = false };
        var submenu = new MenuItem
        {
            Header = "More actions",
            ItemsSource = new object[] { new AlderContextMenuHeading { Text = "Children" } }
                .Concat(Enumerable.Range(0, 10_000).Select(index => (object)$"Child {index}"))
                .Concat(new object[] { check, new Separator(), hidden, new MenuItem { Header = "Disabled", IsEnabled = false } }).ToArray()
        };
        var menu = new AlderContextMenu
        {
            ItemsSource = new object[] { new AlderContextMenuHeading { Text = "Actions" } }
                .Concat(Enumerable.Range(0, 20_000).Select(index => (object)$"Action {index}"))
                .Concat(new object[] { new Separator(), submenu }).ToArray()
        };
        var target = new Border { Background = Brushes.Transparent, Focusable = true, ContextMenu = menu };
        var window = Show(target);
        AlderMotion.SetReduced(window, false);
        try
        {
            Assert.True(target.Focus());
            menu.Open(target);
            Refresh(window);
            Assert.True(menu.IsOpen);
            AssertVirtualized(menu);
            Assert.Null(menu.ContainerFromIndex(menu.ItemCount - 1));
            Assert.Empty(submenu.GetRealizedContainers());

            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal("Action 0", Assert.IsAssignableFrom<MenuItem>(window.FocusManager?.GetFocusedElement()).Header);
            Press(window, PhysicalKey.ArrowUp);
            Refresh(window);
            Assert.True(submenu.IsFocused);
            AssertVirtualized(menu);

            Press(window, PhysicalKey.ArrowRight);
            Refresh(window);
            Assert.True(submenu.IsSubMenuOpen);
            AssertVirtualized(submenu);
            Assert.Equal("Child 0", Assert.IsAssignableFrom<MenuItem>(window.FocusManager?.GetFocusedElement()).Header);
            Press(window, PhysicalKey.ArrowUp);
            Refresh(window);
            Assert.True(check.IsFocused);
            Assert.False(hidden.IsVisible);
            AssertVirtualized(submenu);
            Press(window, PhysicalKey.Enter);
            Assert.True(check.IsChecked);

            Press(window, PhysicalKey.Escape);
            Refresh(window);
            Assert.False(submenu.IsSubMenuOpen);
            Assert.True(submenu.IsFocused);
            Press(window, PhysicalKey.Escape);
            Assert.True(menu.IsOpen);
            await Task.Delay(170);
            Assert.False(menu.IsOpen);
            Assert.True(target.IsFocused);
        }
        finally { window.Close(); }

        static void AssertVirtualized(ItemsControl owner)
        {
            Assert.IsAssignableFrom<VirtualizingPanel>(owner.ItemsPanelRoot);
            Assert.InRange(owner.GetRealizedContainers().Count(), 1, 25);
            Assert.InRange(owner.ItemsPanelRoot!.Children.Count, 1, 25);
        }
    }

    private static Window Show(Control target)
    {
        var window = new Window { Width = 600, Height = 480, Content = target };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Refresh(window);
        return window;
    }

    private static void Refresh(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
    }
}
