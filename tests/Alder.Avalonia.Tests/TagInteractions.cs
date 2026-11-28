using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class TagInteractions
{
    [AvaloniaFact]
    public void SingleAndMultipleSelectionKeepDirectionalFocusIndependent()
    {
        var first = new AlderTag { Content = "Design" };
        var disabled = new AlderTag { Content = "Unavailable", IsEnabled = false };
        var last = new AlderTag { Content = "Engineering" };
        var group = new AlderTagGroup { Size = AlderSize.Large, Variant = TagVariant.Surface, Items = { first, disabled, last } };
        var before = new AlderButton { Content = "Before" };
        var after = new AlderButton { Content = "After" };
        var window = Show(new StackPanel { Children = { before, group, after } });
        try
        {
            Assert.Equal(AlderSize.Large, first.Size);
            Assert.Equal(TagVariant.Surface, first.Variant);
            Assert.True(before.Focus());
            Press(window, PhysicalKey.Tab);
            Assert.True(first.IsFocused);
            Click(window, first);
            Press(window, PhysicalKey.Space);
            Assert.Empty(group.Selection.SelectedItems);

            group.SelectionMode = TagSelectionMode.Single;
            Press(window, PhysicalKey.Space);
            Assert.Same(first, group.SelectedItem);
            Press(window, PhysicalKey.ArrowRight);
            Assert.True(last.IsFocused);
            Assert.Same(first, group.SelectedItem);
            Press(window, PhysicalKey.Enter);
            Assert.Same(last, group.SelectedItem);
            Assert.False(first.IsSelected);

            group.SelectionMode = TagSelectionMode.Multiple;
            Click(window, first);
            Assert.True(first.IsSelected && last.IsSelected);
            Click(window, last);
            Assert.Same(first, Assert.Single(group.Selection.SelectedItems));
            Press(window, PhysicalKey.Tab);
            Assert.True(after.IsFocused);
            group.SelectionMode = TagSelectionMode.None;
            Assert.Empty(group.Selection.SelectedItems);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RemovalUpdatesBoundItemsAndRestoresFocusThroughEmptyState()
    {
        var items = new ObservableCollection<string> { "Design", "Travel", "Books" };
        var group = new AlderTagGroup
        {
            ItemsSource = items, SelectionMode = TagSelectionMode.Multiple, SelectedIndex = 0,
            IsRemovable = true, RemoveButtonLabel = "Delete topic", EmptyContent = "No tags left"
        };
        var after = new AlderButton { Content = "After" };
        var window = Show(new StackPanel { Children = { group, after } });
        try
        {
            var middle = TagAt(group, 1);
            var button = middle.GetVisualDescendants().OfType<Button>().Single();
            Assert.Equal("Delete topic", AutomationProperties.GetName(button));
            int requests = 0;
            group.RemoveRequested += (_, e) =>
            {
                requests++;
                Assert.Equal(items[e.Index], e.Item);
                if (requests == 1)
                {
                    Assert.Same(middle, e.Tag);
                    Assert.Equal("Design", Assert.Single(group.Selection.SelectedItems));
                }
                items.RemoveAt(e.Index);
                e.Handled = true;
            };

            Click(window, button);
            Assert.Equal(new[] { "Design", "Books" }, items);
            Assert.True(TagAt(group, 1).IsFocused);
            Press(window, PhysicalKey.Backspace);
            Assert.True(TagAt(group, 0).IsFocused);
            Press(window, PhysicalKey.Delete);
            Assert.Empty(items);
            Assert.Equal(3, requests);
            Assert.True(group.IsFocused);
            Assert.True(group.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "EmptyPresenter").IsVisible);
            Press(window, PhysicalKey.Tab);
            Assert.True(after.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WrappedRowsNavigateVerticallyAndExternalRemovalRespectsFocus()
    {
        var first = new AlderTag { Content = "Design", Width = 64 };
        var second = new AlderTag { Content = "Travel", Width = 64 };
        var third = new AlderTag { Content = "Books", Width = 64 };
        var group = new AlderTagGroup { Width = 150, Items = { first, second, third } };
        var after = new AlderButton { Content = "After" };
        var window = Show(new StackPanel { Children = { group, after } });
        try
        {
            Assert.Equal(first.Bounds.Y, second.Bounds.Y);
            Assert.True(third.Bounds.Y > first.Bounds.Y);
            Assert.True(first.Focus());
            Press(window, PhysicalKey.ArrowDown);
            Assert.True(third.IsFocused);
            Press(window, PhysicalKey.ArrowUp);
            Assert.True(first.IsFocused);

            group.Items.Remove(first);
            Assert.True(after.Focus());
            Pump(window);
            Assert.True(after.IsFocused);

            Assert.True(second.Focus());
            group.Items.Remove(second);
            Pump(window);
            Assert.True(third.IsFocused);
        }
        finally { window.Close(); }
    }

    private static AlderTag TagAt(AlderTagGroup group, int index) => Assert.IsType<AlderTag>(group.ContainerFromIndex(index));

    private static Window Show(Control content)
    {
        var window = new AlderTestWindow { Width = 340, Height = 210, Content = content };
        window.Show();
        Pump(window);
        return window;
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
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
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
    }
}
