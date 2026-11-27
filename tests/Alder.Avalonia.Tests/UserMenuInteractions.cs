using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class UserMenuInteractions
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AccountMenuKeepsItsGapAndAlignmentWhenItFlips(bool prefersTop)
    {
        var menu = new AlderUserMenu
        {
            MenuPlacement = prefersTop ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedRight,
            Items = { "Profile", "Settings", "Create team" },
            Title = "Account", Width = 120, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = prefersTop ? VerticalAlignment.Top : VerticalAlignment.Bottom
        };
        var window = Show(menu);
        try
        {
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            Assert.NotNull(flyout.Popup.Child);
            Assert.False(flyout.IsOpen);
            var point = menu.TranslatePoint(new Rect(menu.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(flyout.IsOpen);
            Refresh(window);

            var surface = flyout.Popup.Child!.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Surface");
            var origin = surface.TranslatePoint(default, window)!.Value;
            var anchor = menu.TranslatePoint(default, window)!.Value;
            if (prefersTop)
            {
                Assert.Equal(anchor.Y + menu.Bounds.Height + menu.MenuGap, origin.Y, 1);
                Assert.Equal(anchor.X, origin.X, 1);
            }
            else
            {
                Assert.Equal(anchor.Y - menu.MenuGap, origin.Y + surface.Bounds.Height, 1);
                Assert.Equal(anchor.X + menu.Bounds.Width, origin.X + surface.Bounds.Width, 1);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AlignedPopoverUpdatesItsGapAfterFlipping()
    {
        var popover = new AlderPopover { Placement = PlacementMode.BottomEdgeAlignedRight, Content = new Border { Width = 170, Height = 100 } };
        var button = new AlderButton
        {
            Content = "Open", Flyout = popover, Width = 80, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var window = Show(button);
        try
        {
            var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(popover.IsOpen);

            var surface = popover.Popup.Child!;
            var anchor = button.TranslatePoint(new Point(button.Bounds.Width, 0), window)!.Value;
            CheckPlacement(8);
            popover.Offset = 13;
            CheckPlacement(13);

            void CheckPlacement(double gap)
            {
                Refresh(window);
                var origin = surface.TranslatePoint(default, window)!.Value;
                Assert.Equal(anchor.Y - gap, origin.Y + surface.Bounds.Height, 1);
                Assert.Equal(anchor.X, origin.X + surface.Bounds.Width, 1);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, "#EBEBEC")]
    [InlineData(true, "#27272A")]
    public async Task NavbarAccountButtonKeepsItsTintUntilDismissal(bool dark, string expectedColor)
    {
        var flyout = new AlderMenuFlyout { Items = { new MenuItem { Header = "Your account" } } };
        flyout.Popup.ShouldUseOverlayLayer = true;
        var account = new AlderButton
        {
            Theme = (ControlTheme)Application.Current!.FindResource("AlderNavbarAccountButtonTheme")!,
            Content = new AlderAvatar { Width = 28, Height = 28, Fallback = "AL" },
            Flyout = flyout,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = Show(new AlderNavbar { Actions = account });
        try
        {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            AlderMotion.SetReduced(window, false);
            Refresh(window);
            var tint = account.GetVisualDescendants().OfType<ButtonTint>().Single();
            var face = account.GetVisualDescendants().OfType<ButtonFeedback>().Single();
            Assert.Equal(Color.Parse(expectedColor), Assert.IsAssignableFrom<ISolidColorBrush>(tint.Background).Color);
            Assert.Equal(new Size(36, 36), account.Bounds.Size);
            Assert.Equal(0, tint.Opacity);

            var point = account.TranslatePoint(new Rect(account.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            Assert.Equal(1, tint.Opacity);
            window.MouseDown(point, MouseButton.Left);
            Assert.True(account.IsPressed);
            Assert.Equal(1, tint.Opacity);
            Assert.Equal(.97, face.PressScale);
            window.MouseUp(point, MouseButton.Left);
            Refresh(window);
            Assert.True(flyout.IsOpen);

            var outside = new Point(5, 5);
            window.MouseMove(outside);
            Assert.False(account.IsPointerOver);
            Assert.Equal(1, tint.Opacity);
            Assert.Equal(.97, face.ActiveScale);

            flyout.Hide();
            Assert.True(flyout.IsOpen);
            Assert.False(flyout.Popup.IsHitTestVisible);
            Assert.Equal(0, tint.Opacity);
            Assert.Equal(1, face.ActiveScale);
            await Task.Delay(160);
            Assert.False(flyout.IsOpen);
            Assert.Equal(0, tint.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task NavbarAccountMenuUsesItsPreparedPresenterOnTheFirstClick()
    {
        var menu = new AlderUserMenu
        {
            IsIconOnly = true, Content = "AL", HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            MenuTheme = (ControlTheme)Application.Current!.FindResource("AlderNavbarDropdownTheme")!,
            Items = { new MenuItem { Header = "Your account" } }
        };
        var window = Show(menu);
        try
        {
            AlderMotion.SetReduced(window, false);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            var point = menu.TranslatePoint(new Rect(menu.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(flyout.IsOpen);
            Assert.Same(presenter, flyout.Popup.Child);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            var anchor = menu.TranslatePoint(new Point(menu.Bounds.Width, menu.Bounds.Height), window)!.Value;
            var topLeft = presenter.TranslatePoint(default, window)!.Value;
            Assert.Equal(200, presenter.Bounds.Width);
            Assert.Equal(anchor.X, topLeft.X + presenter.Bounds.Width);
            Assert.Equal(anchor.Y + 8, topLeft.Y);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            await Task.Delay(180);
            Assert.False(flyout.IsOpen);
            Assert.True(menu.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UserMenuTriggerStaysCompactUntilDismissalAndSeparatesTheLogOutAction()
    {
        var menu = new AlderUserMenu
        {
            Title = "Account", ShowLogOut = true, ShowLogOutSeparator = true,
            Items = { new MenuItem { Header = "Profile" } }
        };
        var window = Show(menu);
        try
        {
            AlderMotion.SetReduced(window, false);
            var chevron = FindTriggerChevron(menu);
            var rotation = ElementComposition.GetElementVisual(chevron)!;
            Assert.Equal(0, rotation.RotationAngle);
            var surface = menu.GetVisualDescendants().OfType<ButtonFeedback>().Single();
            var visual = ElementComposition.GetElementVisual(surface)!;
            var bounds = menu.Bounds;
            var point = menu.TranslatePoint(new Rect(menu.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            Assert.Equal(.97, surface.PressScale);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, surface.PressScale);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            Assert.True(flyout.IsOpen);
            Assert.Equal(MathF.PI, rotation.RotationAngle);
            Assert.NotNull(rotation.ImplicitAnimations);
            var outside = new Point(5, 5);
            window.MouseMove(outside);
            Refresh(window);
            Assert.False(menu.IsPointerOver);
            Assert.Equal(.97, visual.Scale.X, 4);
            Assert.Equal(bounds, menu.Bounds);
            Assert.NotNull(visual.ImplicitAnimations);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            var items = presenter.Items.OfType<MenuItem>().ToArray();
            Assert.Collection(items,
                item => Assert.Equal("Profile", item.Header),
                item => { Assert.Equal("-", item.Header); Assert.False(item.Focusable); Assert.Equal(1, item.Bounds.Height); },
                item => Assert.Equal("Log out", item.Header));

            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            Assert.True(flyout.IsOpen);
            Assert.False(flyout.Popup.IsHitTestVisible);
            Assert.Equal(0, rotation.RotationAngle);
            Assert.Equal(1, visual.Scale.X);
            await Task.Delay(180);
            Assert.False(flyout.IsOpen);
            Assert.Equal(bounds, menu.Bounds);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void KeyboardUserMenuSnapsToCompactScaleWithReducedMotion()
    {
        var menu = new AlderUserMenu { Title = "Account", Items = { new MenuItem { Header = "Profile" } } };
        var outside = new Button { Content = "Outside" };
        var window = Show(new StackPanel { Children = { menu, outside } });
        try
        {
            var chevron = FindTriggerChevron(menu);
            var rotation = ElementComposition.GetElementVisual(chevron)!;
            var surface = menu.GetVisualDescendants().OfType<ButtonFeedback>().Single();
            var visual = ElementComposition.GetElementVisual(surface)!;
            Assert.True(menu.Focus(NavigationMethod.Tab));
            Assert.Equal(1, visual.Scale.X);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Refresh(window);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            Assert.True(flyout.IsOpen);
            Assert.Equal(MathF.PI, rotation.RotationAngle);
            Assert.Null(rotation.ImplicitAnimations);
            Assert.Equal(.97, visual.Scale.X, 4);
            Assert.Null(visual.ImplicitAnimations);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Refresh(window);
            Assert.False(flyout.IsOpen);
            Assert.Equal(0, rotation.RotationAngle);
            Assert.True(menu.IsFocused);
            Assert.Equal(1, visual.Scale.X);

            menu.Open();
            Refresh(window);
            Assert.Equal(.97, visual.Scale.X, 4);
            Assert.True(outside.Focus());
            Refresh(window);
            Assert.False(flyout.IsOpen);
            Assert.True(outside.IsFocused);
            Assert.Equal(1, visual.Scale.X);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void UserMenuKeepsCustomCommandsAndUpdatesItsObservableSourceWhileOpen()
    {
        var command = new TestCommand();
        var row = new MenuItem { Header = "Profile", CommandParameter = "profile" };
        row.Bind(MenuItem.CommandProperty, new Binding(nameof(CommandModel.Open)) { Source = new CommandModel(command) });
        var items = new ObservableCollection<MenuItem> { row };
        var menu = new AlderUserMenu { Title = "Account", ItemsSource = items };
        var window = Show(menu);
        try
        {
            menu.Open();
            Refresh(window);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
            Assert.Same(row, Assert.Single(presenter.Items));
            var settings = new MenuItem { Header = "Settings" };
            items.Add(settings);
            Refresh(window);
            Assert.Contains(settings, presenter.Items);

            Click(window, row);
            Assert.Equal("profile", command.LastParameter);
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BuiltInThemeSegmentBindsTheModeRaisesAnEventAndKeepsTheMenuOpen()
    {
        var app = Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var menu = new AlderUserMenu { ShowThemeSwitcher = true, AutoApplyTheme = false };
        var events = new List<AlderUserThemeMode>();
        menu.ThemeModeChanged += (_, e) => events.Add(e.NewMode);
        var window = Show(menu);
        try
        {
            menu.Open();
            Refresh(window);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            var segment = flyout.Popup.Child!.GetVisualDescendants().OfType<AlderSegment>().Single();
            var dark = segment.Items.OfType<AlderSegmentItem>().Single(item => Equals(item.Key, AlderUserThemeMode.Dark));
            Click(window, dark);
            Assert.Equal(AlderUserThemeMode.Dark, menu.ThemeMode);
            Assert.Equal(new[] { AlderUserThemeMode.Dark }, events);
            Assert.Same(previousTheme, app.RequestedThemeVariant);
            Assert.True(flyout.IsOpen);

            menu.AutoApplyTheme = true;
            menu.ThemeMode = AlderUserThemeMode.Light;
            Assert.Same(ThemeVariant.Light, app.RequestedThemeVariant);
            menu.ThemeMode = AlderUserThemeMode.System;
            Assert.Same(ThemeVariant.Default, app.RequestedThemeVariant);
            Assert.Same(ThemeVariant.Default, window.RequestedThemeVariant);
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaFact]
    public void BuiltInLogOutHonorsCanExecuteAndRaisesItsEventAfterTheCommand()
    {
        var command = new TestCommand();
        command.SetEnabled(false);
        var menu = new AlderUserMenu { ShowLogOut = true, LogOutCommand = command, LogOutCommandParameter = 7 };
        object? recorded = null;
        menu.LogOutRequested += (_, _) => recorded = command.LastParameter;
        var window = Show(menu);
        try
        {
            menu.Open();
            Refresh(window);
            var flyout = Assert.IsType<AlderMenuFlyout>(menu.Flyout);
            var row = Assert.IsType<AlderMenuItem>(Assert.Single(flyout.Items));
            Assert.False(row.IsEffectivelyEnabled);
            command.SetEnabled(true);
            Refresh(window);
            Assert.True(row.IsEffectivelyEnabled);
            Click(window, row);
            Assert.Equal(7, recorded);
            Assert.False(flyout.IsOpen);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 700, Height = 460, Content = new Border { Padding = new Thickness(30), Child = content } };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Refresh(window);
        return window;
    }

    private static AlderUserMenuChevron FindTriggerChevron(AlderUserMenu menu) =>
        menu.GetVisualDescendants().OfType<AlderUserMenuChevron>().Single();

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Refresh(window);
    }

    private static void Refresh(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class CommandModel(ICommand command)
    {
        public ICommand Open { get; } = command;
    }

    private sealed class TestCommand : ICommand
    {
        private bool enabled = true;
        public object? LastParameter { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => enabled;
        public void Execute(object? parameter) => LastParameter = parameter;
        public void SetEnabled(bool value)
        {
            enabled = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
