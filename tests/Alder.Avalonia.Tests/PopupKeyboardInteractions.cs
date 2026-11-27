using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public class PopupKeyboardInteractions
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopoverCyclesBetweenActionsAndRestoresKeyboardFocusOnClose(bool commit)
    {
        var before = new AlderButton { Content = "Before" };
        var after = new AlderButton { Content = "After" };
        var first = new AlderButton { Content = "Close" };
        var last = new AlderButton { Content = "Last" };
        var popover = new AlderPopover { Content = new StackPanel { Children = { first, last } } };
        var trigger = new AlderButton { Content = "Open", Flyout = popover };
        first.Click += (_, _) => popover.Hide();
        var window = Show(before, trigger, after);
        try
        {
            Assert.True(after.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.True(trigger.IsFocused);
            Press(window, PhysicalKey.Enter);
            Assert.True(popover.IsOpen);
            Assert.True(first.IsFocused);

            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.True(last.IsFocused);
            if (commit)
            {
                Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
                Assert.True(first.IsFocused);
            }
            Press(window, commit ? PhysicalKey.Enter : PhysicalKey.Escape);

            Assert.False(popover.IsOpen);
            Assert.True(trigger.IsFocused);
            Assert.Contains(":focus-visible", trigger.Classes);
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.True(before.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TextPopoverLetsShiftTabContinueToPreviousControl()
    {
        var before = new AlderButton { Content = "Before" };
        var popover = new AlderPopover { Content = "Read-only preview" };
        var trigger = new AlderButton { Content = "Open", Flyout = popover };
        var window = Show(before, trigger);
        try
        {
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Enter);
            Assert.True(popover.IsOpen);

            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);

            Assert.False(popover.IsOpen);
            Assert.True(before.IsFocused);
            Assert.Contains(":focus-visible", before.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("dropdown")]
    [InlineData("color")]
    [InlineData("date")]
    [InlineData("emoji")]
    public void EscapePreservesKeyboardFeedbackWhenReturningFromPopup(string kind)
    {
        var before = new AlderButton { Content = "Before" };
        Control picker = kind switch
        {
            "dropdown" => new AlderButton
            {
                Content = "Open", Flyout = new AlderDropdownFlyout { Items = { new AlderMenuItem { Header = "Option" } } }
            },
            "color" => new AlderCellColorPicker { Label = "Color", CommitOnApply = true },
            "date" => new AlderDatePicker { Value = new DateOnly(2026, 11, 27) },
            _ => new AlderEmojiPicker()
        };
        var window = Show(before, picker);
        try
        {
            var trigger = picker as Button ?? picker.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == (kind == "date" ? "PART_CalendarTrigger" : "PART_Trigger"));
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Enter);
            Assert.Single(window.GetVisualDescendants().OfType<OverlayPopupHost>());
            Press(window, PhysicalKey.Escape);

            Assert.Empty(window.GetVisualDescendants().OfType<OverlayPopupHost>());
            Assert.True(trigger.IsFocused);
            Assert.Contains(":focus-visible", trigger.Classes);
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.NotSame(trigger, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModalReturnsToTheKeyboardTriggerAfterReverseNavigation(bool drawer)
    {
        var before = new AlderButton { Content = "Before" };
        var trigger = new AlderButton { Content = "Open" };
        var action = new AlderButton { Content = "Inside" };
        Task<DialogResult>? result = null;
        trigger.Click += (_, _) => result = drawer ? Drawer.ShowAsync(action, owner: trigger) : Dialog.ShowModalAsync(action, owner: trigger);
        var window = Show(new DialogHost { Content = new StackPanel { Children = { before, trigger } } });
        try
        {
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Enter);
            Assert.True(action.IsFocused);
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.False(action.IsFocused);
            Assert.False(trigger.IsFocused);

            Press(window, PhysicalKey.Escape);

            Assert.True(result?.IsCompletedSuccessfully);
            Assert.True(trigger.IsFocused);
            Assert.Contains(":focus-visible", trigger.Classes);
            Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.True(before.IsFocused);
        }
        finally { window.Close(); }
    }

    private static Window Show(params Control[] controls)
    {
        var stack = new StackPanel { Margin = new Thickness(24), Spacing = 10 };
        stack.Children.AddRange(controls);
        var window = new AlderTestWindow { Content = stack, Width = 500, Height = 540 };
        AlderMotion.SetReduced(window, true);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
