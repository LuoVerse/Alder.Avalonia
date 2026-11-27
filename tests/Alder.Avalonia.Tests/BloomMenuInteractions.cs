using System.ComponentModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class BloomMenuInteractions
{
    [AvaloniaFact]
    public void BoundHeaderAndIconUpdateBothCaptionsAndAllowHidingTheIcon()
    {
        var model = new CaptionModel();
        var menu = new AlderBloomMenu { Items = { new AlderBloomMenuItem { Content = "Document" } } };
        AlderMotion.SetReduced(menu, true);
        menu.Bind(AlderBloomMenu.HeaderProperty, CompiledBinding.Create((CaptionModel vm) => vm.Label, source: model));
        menu.Bind(AlderBloomMenu.IconProperty, CompiledBinding.Create((CaptionModel vm) => vm.Icon, source: model));
        var window = Show(menu);
        try
        {
            var trigger = menu.GetVisualDescendants().OfType<AlderBloomMenuTrigger>().Single();
            var caption = trigger.GetVisualDescendants().OfType<AlderBloomMenuCaption>().Single();
            var label = caption.GetVisualDescendants().OfType<TextBlock>().Single();
            var icon = caption.GetVisualDescendants().OfType<PathIcon>().Single();
            Assert.Equal(model.Label, label.Text);
            Assert.Same(model.Icon, icon.Data);

            Click(window, trigger);
            var panel = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            var body = panel.GetVisualDescendants().OfType<Border>().Single(view => view.Name == "PART_Body");
            var heading = body.GetVisualDescendants().OfType<TextBlock>().Single(view => view.Text == model.Label);
            var morph = panel.GetVisualDescendants().OfType<AlderBloomMenuCaption>().Single();
            var morphLabel = morph.GetVisualDescendants().OfType<TextBlock>().Single();
            var morphIcon = morph.GetVisualDescendants().OfType<PathIcon>().Single();

            var replacement = Geometry.Parse("M0,0 H20 V10 H0 Z");
            model.SetCaption("New document", replacement);
            Pump(window);
            Assert.Equal(model.Label, AutomationProperties.GetName(trigger));
            Assert.Equal(model.Label, heading.Text);
            Assert.Equal(model.Label, label.Text);
            Assert.Equal(model.Label, morphLabel.Text);
            Assert.Same(replacement, icon.Data);
            Assert.Same(replacement, morphIcon.Data);

            model.SetCaption("Create", null);
            Pump(window);
            Assert.False(caption.GetVisualDescendants().OfType<AlderIconPresenter>().Single().IsVisible);
            Assert.False(morph.GetVisualDescendants().OfType<AlderIconPresenter>().Single().IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task GridMorphKeepsAnchorSizeAndSupportsKeyboardSelection()
    {
        var menu = new AlderBloomMenu();
        var items = Enumerable.Range(0, 6).Select(i => new AlderBloomMenuItem { Content = $"Action {i}" }).ToArray();
        items[1].IsEnabled = false;
        string? selected = null;
        items[5].Click += (_, _) => selected = (string?)items[5].Content;
        foreach (var item in items) menu.Items.Add(item);
        var window = Show(menu);
        try
        {
            var trigger = menu.GetVisualDescendants().OfType<AlderBloomMenuTrigger>().Single();
            var anchorSize = menu.Bounds.Size;
            trigger.Focus();
            Key(window, PhysicalKey.ArrowDown);
            Assert.True(menu.IsOpen);
            Assert.True(items[0].IsFocused);

            var panel = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            var chrome = Assert.IsType<AlderBloomMenuChrome>(panel.Children[0]);
            var scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(panel.HasActiveMotion);
            await WaitFor(window, () => ElementComposition.GetElementChildVisual(chrome) is CompositionCustomVisual);
            Assert.Equal(420, chrome.Bounds.Width);
            var viewportBounds = scroll.Bounds;
            var caption = trigger.GetVisualDescendants().OfType<ContentControl>().Single(view => view.Name == "PART_TriggerCaption");
            var morphCaption = panel.GetVisualDescendants().OfType<ContentControl>().Single(view => view.Name == "PART_MorphCaption");
            AssertCaptionAligned(window, caption, morphCaption);
            Assert.Equal(anchorSize, menu.Bounds.Size);
            await WaitFor(window, () => !panel.HasActiveMotion);
            Assert.Equal(viewportBounds, scroll.Bounds);
            Assert.Equal(420, chrome.Bounds.Width);
            var centerOffset = Center(trigger, window) - Center(chrome, window);
            Assert.InRange(centerOffset.X, -1, 1);
            Assert.InRange(centerOffset.Y, -1, 1);
            Assert.Equal(anchorSize, menu.Bounds.Size);

            Key(window, PhysicalKey.ArrowRight);
            Assert.True(items[2].IsFocused);
            Key(window, PhysicalKey.ArrowDown);
            Assert.True(items[5].IsFocused);
            Key(window, PhysicalKey.Home);
            Assert.True(items[0].IsFocused);
            Key(window, PhysicalKey.End);
            Key(window, PhysicalKey.Enter);
            Assert.Equal("Action 5", selected);
            Assert.False(menu.IsOpen);
            Assert.True(trigger.IsFocused);
            Assert.Contains(panel, window.GetVisualDescendants());
            AssertCaptionAligned(window, caption, morphCaption);

            // Reopening during the exit keeps the same overlay and restores the first cell.
            Key(window, PhysicalKey.ArrowDown);
            Assert.True(menu.IsOpen);
            Assert.True(items[0].IsFocused);
            Assert.Same(panel, window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single());
            Key(window, PhysicalKey.Escape);
            await WaitFor(window, () => !panel.IsAttachedToVisualTree());
            Assert.True(trigger.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ReducedMotionUsesViewportWidthAndOutsideClickPassesThrough()
    {
        var outside = new Button { Content = "Outside", VerticalAlignment = VerticalAlignment.Top };
        var menu = new AlderBloomMenu { ItemsSource = new[] { "A", "B", "C", "D" } };
        var root = new Grid { Children = { outside, menu } };
        AlderMotion.SetReduced(root, true);
        var window = Show(root, 280);
        try
        {
            int clicks = 0;
            outside.Click += (_, _) => clicks++;
            var trigger = menu.GetVisualDescendants().OfType<AlderBloomMenuTrigger>().Single();
            Click(window, trigger);
            Assert.True(menu.IsOpen);
            var panel = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            var item = Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(0));
            Assert.Equal("A", item.Content);
            var content = item.GetVisualDescendants().OfType<StackPanel>().First();
            Assert.Null(content.Effect);
            Assert.Null(content.RenderTransform);

            await WaitFor(window, () => !panel.HasActiveMotion);
            Assert.InRange(panel.Children[0].Bounds.Width, 240, 242);
            Click(window, outside);
            Assert.False(menu.IsOpen);
            Assert.Equal(1, clicks);
            Assert.True(outside.IsFocused);
            await WaitFor(window, () => !panel.IsAttachedToVisualTree());

            trigger.Focus();
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.Tab);
            Assert.False(menu.IsOpen);
            Assert.True(outside.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task PointerSelectionAndReopeningKeepKeyboardFocusRingsHidden()
    {
        var menu = new AlderBloomMenu { Items = { new AlderBloomMenuItem { Content = "First" }, new AlderBloomMenuItem { Content = "Second" } } };
        var window = Show(menu);
        try
        {
            var trigger = menu.GetVisualDescendants().OfType<AlderBloomMenuTrigger>().Single();
            Click(window, trigger);
            var panel = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            await WaitFor(window, () => !panel.HasActiveMotion);
            var first = Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(0));
            Assert.True(first.IsFocused);
            Assert.DoesNotContain(":focus-visible", first.Classes);
            Click(window, first);

            Assert.False(menu.IsOpen);
            Assert.True(trigger.IsFocused);
            Assert.DoesNotContain(":focus-visible", trigger.Classes);
            await WaitFor(window, () => !panel.IsAttachedToVisualTree());

            Click(window, trigger);
            Assert.True(first.IsFocused);
            Assert.DoesNotContain(":focus-visible", first.Classes);
            Key(window, PhysicalKey.ArrowRight);
            var second = Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(1));
            Assert.Contains(":focus-visible", second.Classes);
            Key(window, PhysicalKey.Enter);
            Assert.Contains(":focus-visible", trigger.Classes);
            await WaitFor(window, () => !panel.IsAttachedToVisualTree());

            Click(window, trigger);
            Assert.True(first.IsFocused);
            Assert.DoesNotContain(":focus-visible", first.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task InitiallyOpenMenuScrollsToTheLastRowInAShortWindow()
    {
        var menu = new AlderBloomMenu
        {
            IsOpen = true,
            ItemsSource = Enumerable.Range(1, 12).Select(i => $"Action {i}").ToArray()
        };
        var window = Show(menu, width: 300, height: 250);
        try
        {
            Assert.True(menu.IsOpen);
            var first = Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(0));
            Assert.True(first.IsFocused);
            var panel = window.GetVisualDescendants().OfType<AlderBloomMenuSurface>().Single();
            var scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().First();
            await WaitFor(window, () => !panel.HasActiveMotion);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);

            Key(window, PhysicalKey.End);
            var last = Assert.IsType<AlderBloomMenuItem>(menu.ContainerFromIndex(11));
            Assert.True(last.IsFocused);
            Assert.True(scroll.Offset.Y > 0);
        }
        finally { window.Close(); }
    }

    private static void AssertCaptionAligned(Window window, ContentControl caption, ContentControl morphCaption)
    {
        var label = caption.GetVisualDescendants().OfType<TextBlock>().Single();
        var icon = caption.GetVisualDescendants().OfType<PathIcon>().Single();
        var morphLabel = morphCaption.GetVisualDescendants().OfType<TextBlock>().Single();
        var morphIcon = morphCaption.GetVisualDescendants().OfType<PathIcon>().Single();

        Assert.NotNull(icon.Data);
        Assert.Equal(Center(label, window), Center(morphLabel, window));
        Assert.Equal(Center(icon, window), Center(morphIcon, window));
        Assert.Equal(Center(label, window).Y, Center(icon, window).Y, 5);
    }

    private static Window Show(Control content, int width = 580, int height = 400)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Pump(window);
        return window;
    }

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;

    private static void Click(Window window, Control control)
    {
        var center = Center(control, window);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Pump(window);
    }

    private static void Key(Window window, PhysicalKey key)
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
        window.UpdateLayout();
    }

    private static async Task WaitFor(Window window, Func<bool> condition)
    {
        for (int frame = 0; frame < 90; frame++)
        {
            Pump(window);
            if (condition()) return;
            await Task.Delay(12);
        }
        Assert.True(condition());
    }

    private sealed class CaptionModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Label { get; private set; } = "Add";
        public Geometry? Icon { get; private set; } = Geometry.Parse("M0,0 L8,16 L16,0 Z");

        public void SetCaption(string label, Geometry? icon)
        {
            Label = label;
            Icon = icon;
            PropertyChanged?.Invoke(this, new(nameof(Label)));
            PropertyChanged?.Invoke(this, new(nameof(Icon)));
        }
    }
}
