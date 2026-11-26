using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public class CheckboxButtonGroupInteractions
{
    [AvaloniaFact]
    public void CardSurfaceAndKeyboardShareSelectionWithTheGroup()
    {
        var security = Option("security");
        var storage = Option("storage");
        var group = new AlderCheckboxButtonGroup { SelectedKeys = ["security"], Children = { security, storage } };
        var window = new Window { Width = 420, Height = 280, Content = group };
        AlderMotion.SetReduced(window, true);
        window.Show();

        try
        {
            var indicator = Part<AlderCheckBox>(security, "DefaultCheck");
            var stroke = Part<ShapePath>(indicator, "CheckStroke");
            Assert.True(indicator.IsChecked);
            Assert.Equal(0d, stroke.StrokeDashOffset);
            Assert.False(indicator.Focusable);
            Assert.False(indicator.IsHitTestVisible);
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(indicator));

            var target = storage.TranslatePoint(new Point(storage.Bounds.Width - 6, storage.Bounds.Height - 6), window)!.Value;
            window.MouseMove(target);
            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            Assert.Equal(new[] { "security", "storage" }, group.SelectedKeys);

            Assert.True(security.Focus(NavigationMethod.Tab));
            Assert.True(Part<Border>(security, "CardFocusRing").IsVisible);
            PressSpace(window);
            Assert.Equal(new[] { "storage" }, group.SelectedKeys);
            Assert.False(indicator.IsChecked);
            Assert.Equal(8.8, stroke.StrokeDashOffset);

            group.IsReadOnly = true;
            PressSpace(window);
            Assert.Equal(new[] { "storage" }, group.SelectedKeys);
            group.SelectedKeys = ["security"];
            Assert.True(indicator.IsChecked);

            group.IsReadOnly = false;
            group.IsEnabled = false;
            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            Assert.Equal(new[] { "security" }, group.SelectedKeys);
            Assert.False(storage.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GridReflowsAndCustomIndicatorFollowsSelection()
    {
        var first = Option("first");
        var second = Option("second");
        var third = Option("third");
        var custom = new TextBlock { Text = "✓" };
        second.Indicator = custom;
        third.ShowIndicator = false;
        var group = new AlderCheckboxButtonGroup
        {
            Layout = CheckboxButtonGroupLayout.Grid,
            Children = { first, second, third }
        };
        var window = new Window { Width = 600, Height = 350, Content = group };
        AlderMotion.SetReduced(window, true);
        window.Show();

        try
        {
            Assert.Equal(first.Bounds.Y, third.Bounds.Y);
            Assert.Equal(first.Bounds.Width, third.Bounds.Width, 6);
            Assert.False(Part<AlderCheckBox>(second, "DefaultCheck").IsVisible);
            Assert.False(Part<Panel>(third, "IndicatorSlot").IsVisible);

            var presenter = Part<ContentPresenter>(second, "CustomIndicator");
            Assert.Same(custom, presenter.Content);
            Assert.Equal(0d, presenter.Opacity);
            group.SelectedKeys = ["second"];
            Assert.Equal(1d, presenter.Opacity);

            window.Width = 280;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(first.Bounds.X, third.Bounds.X);
            Assert.True(third.Bounds.Top > second.Bounds.Bottom);
            Assert.True(second.IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    private static AlderCheckboxButtonGroupItem Option(string key)
    {
        var item = new AlderCheckboxButtonGroupItem { Content = key, Description = "Select this option." };
        AlderChoiceGroup.SetKey(item, key);
        return item;
    }

    private static T Part<T>(Control parent, string name) where T : Control =>
        parent.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static void PressSpace(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
    }
}
