using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class EmojiInteractions
{
    [AvaloniaFact]
    public void ReactionTogglesAndKeepsCountBounded()
    {
        var button = new AlderEmojiReactionButton { Count = 12, IsChecked = true, Emoji = "❤️" };
        var window = Show(button);
        try
        {
            Assert.Equal(24, button.Bounds.Height);
            Click(window, button);
            Assert.False(button.IsChecked);
            Assert.Equal(11, button.Count);

            button.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            Assert.True(button.IsChecked);
            Assert.Equal(12, button.Count);

            button.IsReadOnly = true;
            Click(window, button);
            Press(window, PhysicalKey.Space);
            Assert.True(button.IsChecked);
            Assert.Equal(12, button.Count);

            button.IsReadOnly = false;
            button.AutoUpdateCount = false;
            Click(window, button);
            Assert.False(button.IsChecked);
            Assert.Equal(12, button.Count);

            button.Count = -5;
            Assert.Equal(0, button.Count);
            button.AutoUpdateCount = true;
            button.Count = int.MaxValue;
            Click(window, button);
            Assert.Equal(int.MaxValue, button.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DropdownSearchChoosesToneAndUpdatesSharedRecents()
    {
        var recents = new ObservableCollection<string>();
        var picker = new AlderEmojiPicker { SelectedEmoji = "😀", RecentEmojis = recents };
        var scope = new ThemeVariantScope { Child = picker, RequestedThemeVariant = ThemeVariant.Dark };
        var window = Show(scope);
        var picks = new List<string>();
        picker.EmojiPicked += (_, e) => picks.Add(e.Emoji);
        try
        {
            Click(window, Part<Button>(picker, "PART_Trigger"));
            Assert.True(picker.IsDropDownOpen);
            var panel = Assert.Single(window.GetVisualDescendants().OfType<AlderEmojiPickerPanel>());
            var search = Part<AlderSearchField>(panel, "PART_Search");
            var grid = Part<ListBox>(panel, "PART_Grid");
            Assert.True(grid.ItemCount > 250);
            Assert.True(grid.GetRealizedContainers().Count() < 40);
            Assert.Equal(ThemeVariant.Dark, panel.ActualThemeVariant);

            search.Text = "not-an-emoji";
            Pump(window);
            Assert.True(Part<TextBlock>(panel, "Empty").IsVisible);
            search.Text = "thumbs up";
            Pump(window);
            Assert.Contains(VisibleItems(grid), cell => cell.Glyph == "👍");

            Click(window, Part<Button>(panel, "PART_Tone"));
            var dark = window.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == "Dark skin tone");
            Click(window, dark);
            Assert.Equal(AlderEmojiSkinTone.Dark, picker.SkinTone);
            Assert.True(picker.IsDropDownOpen);
            Assert.Contains(VisibleItems(grid), cell => cell.Glyph == "👍🏿");

            search.Focus();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            Assert.Equal("👍🏿", picker.SelectedEmoji);
            Assert.Equal(["👍🏿"], picks);
            Assert.Equal(["👍🏿"], recents);
            Assert.False(picker.IsDropDownOpen);
            Assert.True(Part<Button>(picker, "PART_Trigger").IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlinePickerNavigatesCategoriesAndSupportsCustomSearch()
    {
        var picker = new AlderEmojiPicker { IsInline = true, Size = AlderSize.Small };
        var window = Show(picker);
        try
        {
            var panel = Assert.Single(picker.GetVisualDescendants().OfType<AlderEmojiPickerPanel>());
            var grid = Part<ListBox>(panel, "PART_Grid");
            var footer = Part<UniformGrid>(panel, "PART_Categories");
            var activities = footer.Children.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Activities");
            var search = Part<AlderSearchField>(panel, "PART_Search");
            search.Text = "thumb";
            Pump(window);
            Click(window, activities);
            var scroll = Assert.IsType<AlderScrollViewer>(grid.Scroll);
            Assert.True(scroll.Offset.Y > 1_000, $"Category jump stopped at {scroll.Offset.Y}. Extent {scroll.Extent}, viewport {scroll.Viewport}.");
            Assert.Equal("", search.Text);

            search.Text = "火箭";
            Pump(window);
            Assert.Contains(VisibleItems(grid), cell => cell.Glyph == "🚀");

            picker.Categories =
            [
                new("celebrate", "Celebrations", "🎉", [new("🎉", "Party popper", "launch")])
            ];
            picker.Filter = (item, query) => item.Keywords == query;
            picker.ShowRecent = false;
            search.Text = "launch";
            Pump(window);
            var item = Assert.Single(VisibleItems(grid));
            Assert.Equal("🎉", item.Glyph);
            var button = Assert.Single(grid.GetVisualDescendants().OfType<Button>(), candidate => candidate.DataContext == item);
            Click(window, button);
            Assert.Equal("🎉", picker.SelectedEmoji);
            Assert.False(picker.IsDropDownOpen);
            Assert.Single(footer.Children);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LargeDropdownFitsTheViewportWithItsShadowAtTwoTimesScale()
    {
        var picker = new AlderEmojiPicker
        {
            Size = AlderSize.Large,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 100, 36)
        };
        var window = new AlderTestWindow { Width = 320, Height = 620, Content = picker };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.SetRenderScaling(2);
        try
        {
            Pump(window);
            Click(window, Part<Button>(picker, "PART_Trigger"));
            var panel = Assert.Single(window.GetVisualDescendants().OfType<AlderEmojiPickerPanel>());
            var shadow = AnchoredSurface.ShadowMargin;
            AssertFits();

            window.Width = 280;
            Pump(window);
            AssertFits();

            void AssertFits()
            {
                var origin = panel.TranslatePoint(default, window)!.Value;
                Assert.True(origin.X >= shadow.Left && origin.X + panel.Bounds.Width <= window.ClientSize.Width - shadow.Right);
            }
        }
        finally { window.Close(); }
    }

    private static IEnumerable<EmojiPickerCell> VisibleItems(ListBox grid) =>
        grid.ItemsSource!.Cast<EmojiPickerRow>().SelectMany(row => row.Cells);

    private static T Part<T>(Control owner, string name) where T : Control =>
        owner.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static Window Show(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.VerticalAlignment = VerticalAlignment.Center;
        var window = new AlderTestWindow { Width = 540, Height = 620, Content = control, RequestedThemeVariant = ThemeVariant.Light };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
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
