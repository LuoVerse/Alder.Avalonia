using System.Globalization;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class NativeControlInteractions
{
    [AvaloniaFact]
    public void MenuOpensNestedSubmenuAndInvokesItsCommand()
    {
        int calls = 0;
        var save = new AlderMenuItem { Header = "PDF", Command = new ActionCommand(() => calls++) };
        var export = new AlderMenuItem { Header = "Export", Items = { save } };
        var file = new AlderMenuItem { Header = "_File", Items = { export } };
        var menu = new AlderMenu { Items = { file, new AlderMenuItem { Header = "_View" } } };
        var window = Show(menu);
        try
        {
            AlderMotion.SetReduced(window, false);
            Click(window, Part<Border>(file, "PART_LayoutRoot"));
            Assert.True(file.IsSubMenuOpen);
            var popup = Part<Popup>(file, "PART_Popup");
            Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, popup.Placement);
            Assert.True(export.Focus(NavigationMethod.Directional));
            Press(window, PhysicalKey.ArrowRight);
            Assert.True(export.IsSubMenuOpen);

            var fileRow = Part<Border>(file, "PART_LayoutRoot");
            var exportRow = Part<Border>(export, "PART_LayoutRoot");
            var saveRow = Part<Border>(save, "PART_LayoutRoot");
            var point = saveRow.TranslatePoint(new Rect(saveRow.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            Pump(window);
            var fileBackground = fileRow.Background;
            var exportBackground = exportRow.Background;
            var saveBackground = saveRow.Background;

            window.MouseDown(point, MouseButton.Left);
            Pump(window);
            Assert.Same(fileBackground, fileRow.Background);
            Assert.Same(exportBackground, exportRow.Background);
            Assert.NotSame(saveBackground, saveRow.Background);

            window.MouseUp(point, MouseButton.Left);
            Pump(window);
            Assert.Equal(1, calls);
            Assert.False(file.IsSubMenuOpen);
            Assert.False(export.IsSubMenuOpen);
            Assert.False(popup.IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TreeViewCreatesThemedItemsAndNavigatesBoundChildren()
    {
        var leaf = new TreeNode("Planning", []);
        var parent = new TreeNode("Projects", [leaf]);
        var tree = new AlderTreeView
        {
            ItemsSource = new[] { parent },
            ItemTemplate = new FuncTreeDataTemplate<TreeNode>((node, _) => new AlderTextBlock { Text = node.Name }, node => node.Children)
        };
        var window = Show(tree);
        try
        {
            var root = Assert.IsType<AlderTreeViewItem>(tree.TreeContainerFromItem(parent));
            Click(window, Part<AlderToggleButton>(root, "Expander"));
            Assert.True(root.IsExpanded);
            var child = Assert.IsType<AlderTreeViewItem>(tree.TreeContainerFromItem(leaf));
            Assert.Equal(1, child.Level);
            Assert.False(Part<AlderToggleButton>(child, "Expander").IsVisible);
            Click(window, Part<ContentPresenter>(child, "PART_HeaderPresenter"));
            Assert.Same(leaf, tree.SelectedItem);
            Assert.True(child.IsSelected);
            Assert.True(child.Focus(NavigationMethod.Directional));
            Press(window, PhysicalKey.ArrowLeft);
            Assert.True(root.IsFocused);
            Press(window, PhysicalKey.ArrowLeft);
            Assert.False(root.IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task TreeBranchAnimatesItsLayoutAndDisablesClosingChildren()
    {
        var leaf = new AlderTreeViewItem { Header = "Planning" };
        var branch = new AlderTreeViewItem { Header = "Projects", Items = { leaf } };
        var sibling = new AlderTreeViewItem { Header = "Archive" };
        var tree = new AlderTreeView { Items = { branch, sibling } };
        var window = Show(tree);
        try
        {
            var body = Part<AlderAccordionPanel>(branch, "Subtree");
            Assert.True(body.ClipToBounds);
            double collapsedTop = Top();
            AlderMotion.SetReduced(window, false);
            Pump(window);
            branch.IsExpanded = true;
            await AdvanceUntil(() => body.Progress > 0);
            Assert.InRange(body.Progress, .0001, .9999);
            double openingTop = Top();
            Assert.True(openingTop > collapsedTop);
            await AdvanceUntil(() => body.Progress == 1);
            double expandedTop = Top();
            Assert.True(expandedTop > openingTop);

            Assert.True(leaf.Focus(NavigationMethod.Directional));
            branch.IsExpanded = false;
            Assert.True(branch.IsFocused);
            Assert.Same(leaf, tree.SelectedItem);
            Assert.False(body.IsHitTestVisible);
            Assert.False(leaf.IsEffectivelyEnabled);
            await AdvanceUntil(() => body.Progress < 1);
            Assert.True(body.Progress > 0);
            branch.IsExpanded = true;
            await AdvanceUntil(() => body.Progress == 1);
            Assert.Equal(expandedTop, Top());

            AlderMotion.SetReduced(window, true);
            branch.IsExpanded = false;
            Pump(window);
            Assert.Equal(0, body.Progress);
            Assert.False(leaf.IsEffectivelyVisible);
            Assert.Equal(collapsedTop, Top());
        }
        finally { window.Close(); }

        double Top() => sibling.TranslatePoint(default, window)!.Value.Y;

        async Task AdvanceUntil(Func<bool> condition)
        {
            for (int frame = 0; frame < 40; frame++)
            {
                Pump(window);
                if (condition()) return;
                await Task.Delay(12);
            }
            Assert.True(condition());
        }
    }

    [AvaloniaFact]
    public void TimePickerCommitsValidDraftAndRestoresFocusOnEscape()
    {
        var picker = new AlderTimePicker
        {
            Value = new TimeOnly(13, 25, 30), Clock = TimeFieldClock.TwelveHour, Granularity = TimeFieldGranularity.Second,
            Minimum = new TimeOnly(9, 0), Maximum = new TimeOnly(17, 0)
        };
        var window = Show(picker);
        try
        {
            var trigger = Part<Button>(picker, "PART_TimeTrigger");
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Enter);
            Assert.True(picker.IsPickerOpen);
            var hours = Part<AlderListBox>(window, "HourOptions");
            var minutes = Part<AlderListBox>(window, "MinuteOptions");
            var seconds = Part<AlderListBox>(window, "SecondOptions");
            var periods = Part<AlderListBox>(window, "PeriodOptions");
            var apply = Part<AlderButton>(window, "ApplyTime");
            Assert.Equal(0, hours.SelectedIndex);
            Assert.Equal(1, periods.SelectedIndex);
            minutes.SelectedIndex = 27;
            seconds.SelectedIndex = 40;
            periods.SelectedIndex = 0;
            Assert.False(apply.IsEnabled);
            Assert.Equal(new TimeOnly(13, 25, 30), picker.Value);
            periods.SelectedIndex = 1;
            Click(window, apply);
            Assert.Equal(new TimeOnly(13, 27, 40), picker.Value);
            Assert.False(picker.IsPickerOpen);

            var segment = Part<StackPanel>(picker, "PART_Segments").Children.OfType<Button>().First();
            Assert.True(segment.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Assert.True(picker.IsPickerOpen);
            minutes = Part<AlderListBox>(window, "MinuteOptions");
            minutes.SelectedIndex = 50;
            Assert.True(minutes.Focus(NavigationMethod.Tab));
            Press(window, PhysicalKey.Escape);
            Assert.False(picker.IsPickerOpen);
            Assert.True(trigger.IsFocused);
            Assert.Equal(new TimeOnly(13, 27, 40), picker.Value);
            picker.IsReadOnly = true;
            picker.OpenPicker();
            Assert.False(picker.IsPickerOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TimePickerIgnoresQuotedClockLettersInLocalizedPatterns()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.ShortTimePattern = "HH 'h' mm";
        var picker = new AlderTimePicker { Culture = culture, Value = new TimeOnly(13, 25) };
        var window = Show(picker);
        try
        {
            picker.OpenPicker();
            Pump(window);
            Assert.Equal(13, Part<AlderListBox>(window, "HourOptions").SelectedIndex);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<AlderListBox>(), list => list.Name == "PeriodOptions");
        }
        finally { window.Close(); }
    }

    private static T Part<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static Window Show(Control control)
    {
        var window = new AlderTestWindow
        {
            Width = 500, Height = 440, Content = new StackPanel { Margin = new Thickness(32), Children = { control } }
        };
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed record TreeNode(string Name, TreeNode[] Children);

    private sealed class ActionCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
