using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class NavbarStepperInteractions
{
    [AvaloniaFact]
    public void StepperTracksChangesToStepsAndRespondsToKeyboardActivation()
    {
        var account = new AlderStepperStep { Title = "Account" };
        var profile = new AlderStepperStep { Title = "Profile" };
        var review = new AlderStepperStep { Title = "Review" };
        var items = new ObservableCollection<AlderStepperStep> { account, profile, review };
        var stepper = new AlderStepper { ActiveStep = 1, ItemsSource = items };
        var window = Show(stepper);
        try
        {
            Assert.Equal(AlderStepStatus.Complete, account.Status);
            Assert.Equal(AlderStepStatus.Active, profile.Status);
            Assert.Equal(AlderStepStatus.Inactive, review.Status);
            Assert.Equal(28, Indicator(profile).Bounds.Height);
            Assert.True(review.Focus());
            Press(window, PhysicalKey.Space);
            Assert.Equal(2, stepper.ActiveStep);
            Assert.Equal(AlderStepStatus.Active, review.Status);

            items.Move(2, 0);
            Pump(window);
            Assert.Equal(1, review.Number);
            Assert.Equal(AlderStepStatus.Complete, review.Status);
            Assert.Equal(AlderStepStatus.Active, profile.Status);

            stepper.IsInteractive = false;
            stepper.Orientation = Orientation.Vertical;
            stepper.Size = AlderSize.Large;
            Pump(window);
            Assert.Equal(36, Indicator(profile).Bounds.Height);
            Assert.False(profile.Focusable);
            Assert.False(profile.IsHitTestVisible);
            Assert.True(profile.Bounds.Y > account.Bounds.Y);
            stepper.ActiveStep = items.Count;
            Assert.All(items, item => Assert.Equal(AlderStepStatus.Complete, item.Status));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task StepperAnimatesBothHalvesOfAConnectorAndHonorsReducedMotion()
    {
        var first = new AlderStepperStep { Title = "Account" };
        var second = new AlderStepperStep { Title = "Review", TrailingContent = "12:30" };
        var stepper = new AlderStepper { Items = { first, second } };
        var window = Show(stepper);
        try
        {
            var trailing = first.GetVisualDescendants().OfType<Control>().Single(part => part.Name == "TrailingRail");
            var leading = second.GetVisualDescendants().OfType<Control>().Single(part => part.Name == "LeadingRail");
            var progress = trailing.GetType().GetProperty("Progress")!;
            double Of(Control rail) => (double)progress.GetValue(rail)!;

            AlderMotion.SetReduced(window, false);
            Pump(window);
            stepper.ActiveStep = 1;
            Assert.Equal(AlderStepStatus.Complete, first.Status);
            Assert.True(Of(trailing) < 1);
            Assert.True(Of(leading) < 1);
            await Task.Delay(100);
            Pump(window);
            Assert.InRange(Of(trailing), 0.01, 0.99);
            Assert.Equal(Of(trailing), Of(leading), 3);
            await Task.Delay(260);
            Pump(window);
            Assert.Equal(1, Of(trailing));
            Assert.Equal(1, Of(leading));

            AlderMotion.SetReduced(window, true);
            stepper.ActiveStep = 0;
            Assert.Equal(0, Of(trailing));
            Assert.Equal(0, Of(leading));

            stepper.Orientation = Orientation.Vertical;
            Pump(window);
            var timestamp = second.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(part => part.Name == "TrailingContent");
            var title = second.GetVisualDescendants().OfType<TextBlock>().Single(part => part.Name == "Title");
            Assert.Equal(title.TranslatePoint(default, second)!.Value.Y, timestamp.TranslatePoint(default, second)!.Value.Y);
            Assert.True(timestamp.TranslatePoint(default, second)!.Value.X > title.TranslatePoint(default, second)!.Value.X);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavbarSwitchesLayoutAndClosesMenuAfterNavigationOrEscape()
    {
        var desktop = new AlderNavbarItem { Content = "Overview", Route = "overview" };
        var mobile = new AlderNavbarItem { Content = "Team", Route = "team", IsMenuItem = true };
        var navbar = new AlderNavbar
        {
            Brand = "Alder", Navigation = desktop, Menu = new StackPanel { Children = { mobile } },
            CurrentRoute = "overview", CollapseWidth = 500
        };
        var window = Show(new StackPanel { Children = { navbar } });
        try
        {
            Assert.False(navbar.IsCompact);
            Assert.True(desktop.IsEffectivelyVisible);
            window.Width = 360;
            Pump(window);
            Assert.True(navbar.IsCompact);
            Assert.False(desktop.IsEffectivelyVisible);
            var toggle = navbar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Toggle");
            Assert.True(toggle.IsEffectivelyVisible);
            Assert.True(toggle.Focus());
            Press(window, PhysicalKey.Space);
            Pump(window);
            Assert.True(navbar.IsMenuOpen);
            Assert.True(mobile.IsEffectivelyVisible);
            Assert.True(mobile.IsFocused);
            Press(window, PhysicalKey.Enter);
            Pump(window);
            Assert.Equal("team", navbar.CurrentRoute);
            Assert.False(navbar.IsMenuOpen);
            Assert.True(toggle.IsFocused);

            navbar.IsMenuOpen = true;
            Pump(window);
            Press(window, PhysicalKey.Escape);
            Pump(window);
            Assert.False(navbar.IsMenuOpen);
            Assert.True(toggle.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task NavbarMenuToggleAnimatesBothWaysAndHonorsReducedMotion()
    {
        var navbar = new AlderNavbar
        {
            AlwaysShowMenuToggle = true,
            Menu = new AlderNavbarItem { Content = "Team", IsMenuItem = true }
        };
        var window = Show(new StackPanel { Children = { navbar } });
        try
        {
            var glyph = navbar.GetVisualDescendants().OfType<Grid>().Single(part => part.Name == "MenuToggleGlyph");
            var top = (Border)glyph.Children[0];
            var bottom = (Border)glyph.Children[1];
            var position = glyph.TranslatePoint(new Point(8, 8), window)!.Value;
            void ClickToggle()
            {
                window.MouseDown(position, MouseButton.Left);
                window.MouseUp(position, MouseButton.Left);
            }

            AlderMotion.SetReduced(window, false);
            Pump(window);
            Assert.Equal(0, top.RenderTransform!.Value.M12);
            Assert.Equal(0, bottom.RenderTransform!.Value.M12);
            ClickToggle();
            Assert.True(navbar.IsMenuOpen);
            Assert.InRange(top.RenderTransform.Value.M12, 0, 0.7);
            await Task.Delay(210);
            Pump(window);
            Assert.Equal(0.707, top.RenderTransform.Value.M12, 3);
            Assert.Equal(-0.707, bottom.RenderTransform.Value.M12, 3);

            ClickToggle();
            Assert.False(navbar.IsMenuOpen);
            Assert.NotEqual(0, top.RenderTransform.Value.M12);
            await Task.Delay(210);
            Pump(window);
            Assert.Equal(0, top.RenderTransform.Value.M12);
            Assert.Equal(0, bottom.RenderTransform.Value.M12);

            AlderMotion.SetReduced(window, true);
            Pump(window);
            ClickToggle();
            Assert.Equal(0.707, top.RenderTransform.Value.M12, 3);
            Assert.Equal(-0.707, bottom.RenderTransform.Value.M12, 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavbarMenuOpensBelowHeaderAtBottomOfScrollViewport()
    {
        var navbar = new AlderNavbar
        {
            Position = AlderNavbarPosition.Static,
            AlwaysShowMenuToggle = true,
            Menu = new StackPanel
            {
                MinHeight = 224,
                Children = { new AlderNavbarItem { Content = "Team", IsMenuItem = true } }
            }
        };
        var scroll = new ScrollViewer
        {
            Content = new StackPanel { Children = { new Border { Height = 345 }, navbar, new Border { Height = 320 } } }
        };
        var window = Show(scroll);
        try
        {
            var header = navbar.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Header");
            var toggle = navbar.GetVisualDescendants().OfType<Button>().Single(part => part.Name == "PART_Toggle");
            Assert.True(toggle.Focus());
            Press(window, PhysicalKey.Space);
            Pump(window);
            Assert.True(navbar.IsMenuOpen);
            Assert.True(scroll.Offset.Y > 0);

            var menu = window.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "MenuSurface");
            var menuTop = menu.TranslatePoint(default, window)!.Value.Y;
            var headerBottom = header.TranslatePoint(new Point(0, header.Bounds.Height), window)!.Value.Y;
            Assert.Equal(headerBottom, menuTop, 0);
            Assert.Equal(256, menu.Bounds.Height);
            Assert.InRange(menuTop + menu.Bounds.Height, headerBottom, window.ClientSize.Height);

            var icon = toggle.GetVisualDescendants().OfType<Grid>().Single(part => part.Name == "MenuToggleGlyph");
            var iconCenter = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), toggle);
            Assert.Equal(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), iconCenter);

            window.Height -= 100;
            Pump(window);
            menuTop = menu.TranslatePoint(default, window)!.Value.Y;
            headerBottom = header.TranslatePoint(new Point(0, header.Bounds.Height), window)!.Value.Y;
            Assert.Equal(headerBottom, menuTop, 0);
            Assert.InRange(menu.Bounds.Height, 1, 255);
            Assert.InRange(menuTop + menu.Bounds.Height, headerBottom, window.ClientSize.Height);
            Press(window, PhysicalKey.Escape);
            Pump(window);
            Assert.False(navbar.IsMenuOpen);
            Assert.True(toggle.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavbarSticksToScrollHostAndTracksScrollDirection()
    {
        var navbar = new AlderNavbar { Brand = "Alder", HideOnScroll = true };
        var content = new StackPanel { Children = { navbar, new Border { Height = 900 } } };
        var scroll = new ScrollViewer { Content = content };
        var window = Show(scroll);
        try
        {
            var header = navbar.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Header");
            scroll.Offset = new Vector(0, 140);
            Pump(window);
            Assert.True(navbar.IsScrollHidden);

            scroll.Offset = new Vector(0, 100);
            Pump(window);
            Assert.False(navbar.IsScrollHidden);
            Assert.InRange(header.TranslatePoint(default, scroll)!.Value.Y, -1, 1);

            navbar.Position = AlderNavbarPosition.Static;
            Pump(window);
            Assert.True(header.TranslatePoint(default, scroll)!.Value.Y < -90);

            navbar.Position = AlderNavbarPosition.Floating;
            Pump(window);
            Assert.InRange(header.TranslatePoint(default, scroll)!.Value.Y, 5, 7);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NavbarCanAllowBackgroundScrollWhileMenuIsOpen()
    {
        var scroll = new ScrollViewer { Content = new Border { Height = 900 } };
        var navbar = new AlderNavbar
        {
            AlwaysShowMenuToggle = true, ScrollHost = scroll,
            Menu = new AlderNavbarItem { Content = "Team", IsMenuItem = true }
        };
        DockPanel.SetDock(navbar, Dock.Top);
        var window = Show(new DockPanel { Children = { navbar, scroll } });
        try
        {
            navbar.IsMenuOpen = true;
            Pump(window);
            var point = new Point(200, 365);
            window.MouseWheel(point, new Vector(0, -2), RawInputModifiers.None);
            Pump(window);
            Assert.Equal(0, scroll.Offset.Y);

            navbar.ShouldBlockScroll = false;
            window.MouseWheel(point, new Vector(0, -2), RawInputModifiers.None);
            Pump(window);
            Assert.True(scroll.Offset.Y > 0);
            Assert.True(navbar.IsMenuOpen);
        }
        finally { window.Close(); }
    }

    private static Border Indicator(AlderStepperStep step) =>
        step.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Indicator");

    private static Window Show(Control child)
    {
        var window = new Window { Width = 760, Height = 425, Content = child };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
    }
}
