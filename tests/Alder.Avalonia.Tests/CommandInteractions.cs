using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class CommandInteractions
{
    [AvaloniaFact]
    public async Task CommandHotkeyOpensModalAndRestoresFocusAfterNestedAction()
    {
        var action = new TestCommand();
        var recent = new AlderCommandItem
        {
            Label = "Open recent",
            Groups = [new() { Items = [new() { Label = "项目计划", Command = action, CommandParameter = "plan" }] }]
        };
        var palette = new AlderCommand
        {
            Groups = [new() { Heading = "Navigation", Items = [recent] }],
            Header = new AlderButton { Content = "Custom header action" }
        };
        var trigger = new AlderButton { Content = "Open", HotKey = new(Key.K, KeyModifiers.Control) };
        Task<DialogResult>? opening = null;
        trigger.Click += (_, _) => opening = palette.ShowAsync(trigger);
        var window = new AlderTestWindow { Width = 640, Height = 480, Content = new DialogHost { Content = trigger } };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Assert.True(trigger.Focus());
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            Refresh(window);
            Assert.NotNull(opening);
            var search = palette.GetVisualDescendants().OfType<AlderSearchField>().Single();
            Assert.True(search.IsFocused);
            window.KeyTextInput("oprec");
            Press(window, PhysicalKey.Enter);
            Assert.True(string.IsNullOrEmpty(palette.Query));

            window.KeyTextInput("计划");
            var presenter = search.GetVisualDescendants().OfType<TextPresenter>().Single();
            presenter.SetCurrentValue(TextPresenter.PreeditTextProperty, "规划");
            Press(window, PhysicalKey.Enter);
            Assert.False(opening!.IsCompleted);
            Assert.Empty(action.Executions);
            presenter.SetCurrentValue(TextPresenter.PreeditTextProperty, "");

            Press(window, PhysicalKey.Escape);
            Assert.Equal("oprec", palette.Query);
            Press(window, PhysicalKey.Enter);
            window.KeyTextInput("计划");
            Press(window, PhysicalKey.Enter);
            Assert.Equal(DialogResult.OK, await opening.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(["plan"], action.Executions);
            Assert.True(trigger.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LargeCommandPagePreservesCultureSensitiveMatchesAndResultOrder()
    {
        var first = new AlderCommandItem { Label = "Open café report" };
        var second = new AlderCommandItem { Label = "Open café review" };
        var items = Enumerable.Range(0, 5000).Select(i => new AlderCommandItem { Label = $"Task {i}" })
            .Append(first).Append(second).ToArray();
        var palette = new AlderCommand { Groups = items.Chunk(1000).Select(group => new AlderCommandGroup { Items = group }).ToArray() };
        var window = new AlderTestWindow { Width = 400, Height = 420, Content = palette };
        window.Show();
        try
        {
            palette.Query = "cafe rprt";
            Refresh(window);
            Assert.Same(first, palette.ActiveItem);
            palette.Query = "cafe";
            Refresh(window);
            Assert.Same(first, palette.ActiveItem);
            palette.Query = "unmatched";
            Refresh(window);
            Assert.Null(palette.ActiveItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommandUpdatesResultsAndInvokesOnlyEnabledRows()
    {
        var action = new TestCommand { Enabled = false };
        var items = new ObservableCollection<AlderCommandItem>
        {
            new() { Label = "Delete project", Command = action, CommandParameter = "delete" },
            new() { Label = "Open notes", Keywords = "workspace document" }
        };
        var palette = new AlderCommand { Groups = [new() { Heading = "Workspace", Items = items }] };
        var invoked = new List<string>();
        palette.ItemInvoked += (_, item) => invoked.Add(item.Label);
        var window = new AlderTestWindow { Width = 400, Height = 420, Content = palette };
        window.Show();
        try
        {
            Refresh(window);
            var search = palette.GetVisualDescendants().OfType<AlderSearchField>().Single();
            Assert.True(search.Focus());
            palette.Query = "delproj";
            Press(window, PhysicalKey.Enter);
            Assert.Empty(action.Executions);
            action.Enabled = true;
            Press(window, PhysicalKey.Enter);
            Assert.Equal(["delete"], action.Executions);

            palette.Query = "workspace notes";
            items.Add(new() { Label = "New workspace notes" });
            Refresh(window);
            var row = palette.GetVisualDescendants().OfType<ListBoxItem>()
                .Single(item => AutomationProperties.GetName(item) == "New workspace notes");
            var point = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            Assert.True(row.IsSelected);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(["Delete project", "New workspace notes"], invoked);
            Assert.True(search.IsFocused);

            palette.Query = "not found";
            Refresh(window);
            Press(window, PhysicalKey.Enter);
            Assert.Empty(palette.GetVisualDescendants().OfType<ListBox>().Single().Items);
            Assert.Equal(2, invoked.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommandVirtualizesRowsAndSupportsKeyboardAndAutomation()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(i => new AlderCommandItem { Label = $"Action {i}", IsEnabled = i != 1 }).ToArray();
        var palette = new AlderCommand { Groups = [new() { Items = items }] };
        AlderCommandItem? invoked = null;
        palette.ItemInvoked += (_, item) => invoked = item;
        var window = new AlderTestWindow { Width = 440, Height = 440, Content = palette };
        window.Show();
        try
        {
            Refresh(window);
            var list = palette.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 30);
            Assert.True(palette.GetVisualDescendants().OfType<AlderSearchField>().Single().Focus());
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(2, list.SelectedIndex);
            Press(window, PhysicalKey.ArrowUp);
            Press(window, PhysicalKey.ArrowUp);
            Refresh(window);
            Assert.Equal(999, list.SelectedIndex);
            var row = Assert.IsAssignableFrom<ListBoxItem>(list.ContainerFromIndex(999));
            var peer = ControlAutomationPeer.CreatePeerForElement(row);
            Assert.Equal("Action 999", peer.GetName());
            Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
            Assert.Same(items[999], invoked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommandVirtualizesLargeGroupsAndMatchesWithoutCheckingOffscreenCommands()
    {
        var action = new TestCommand();
        var groups = Enumerable.Range(0, 100).Select(group => new AlderCommandGroup
        {
            Heading = $"Section {group}",
            Items = Enumerable.Range(0, 1_000).Select(index => new AlderCommandItem
            {
                Label = $"Command {group * 1_000 + index:D6}",
                Keywords = index % 2 == 0 ? "match" : null,
                IsEnabled = index != 998,
                Command = action,
                CommandParameter = group * 1_000 + index
            }).ToArray()
        }).ToArray();
        var palette = new AlderCommand { Groups = groups, Filter = (item, query) => item.Keywords == query };
        var window = new AlderTestWindow { Width = 440, Height = 440, Content = palette };
        window.Show();
        try
        {
            Refresh(window);
            var list = palette.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.Equal(100_100, list.ItemCount);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 24);
            Assert.InRange(action.CanExecuteCalls, 1, 90);

            action.CanExecuteCalls = 0;
            var search = palette.GetVisualDescendants().OfType<AlderSearchField>().Single();
            Assert.True(search.Focus());
            palette.Query = "match";
            Refresh(window);
            Assert.Equal(50_100, list.ItemCount);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 24);
            Assert.InRange(action.CanExecuteCalls, 1, 90);
            Assert.Equal(2, AutomationProperties.GetHeadingLevel(Assert.IsAssignableFrom<Control>(list.ContainerFromIndex(0))));

            Press(window, PhysicalKey.ArrowUp);
            Refresh(window);
            Assert.Same(groups[^1].Items[^4], palette.ActiveItem);
            var lastVisible = Assert.IsAssignableFrom<ListBoxItem>(list.ContainerFromIndex(list.SelectedIndex));
            Assert.Equal("Command 099996", AutomationProperties.GetName(lastVisible));
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 24);
            Press(window, PhysicalKey.Enter);
            Assert.Equal(99_996, Assert.Single(action.Executions));
            Assert.True(search.IsFocused);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Same(groups[0].Items[0], palette.ActiveItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1280, 512)]
    [InlineData(390, 358)]
    public async Task CommandKeepsItsTopAnchorWhileFilteringAndClosesFromEscapeButton(int width, int expectedWidth)
    {
        var trigger = new AlderButton { Content = "Open" };
        var palette = new AlderCommand
        {
            Groups = [new() { Heading = "Actions", Items = [new() { Label = "Open" }, new() { Label = "Save" }] }]
        };
        var window = new AlderTestWindow { Width = width, Height = 720, Content = new DialogHost { Content = trigger } };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            Assert.True(trigger.Focus());
            var opening = palette.ShowAsync(trigger);
            Refresh(window);
            Assert.Equal((double)expectedWidth, palette.Bounds.Width);
            Assert.Equal(108, palette.TranslatePoint(default, window)!.Value.Y);
            var before = palette.Bounds.Height;
            var search = palette.GetVisualDescendants().OfType<AlderSearchField>().Single();
            Assert.Equal(44, search.Bounds.Height);
            var entry = palette.GetVisualDescendants().OfType<ListBoxItem>()
                .Single(row => AutomationProperties.GetName(row) == "Open");
            Assert.Equal(36, entry.Bounds.Height);

            palette.Query = "not found";
            Refresh(window);
            Assert.True(palette.Bounds.Height < before);
            Assert.Equal(108, palette.TranslatePoint(default, window)!.Value.Y);
            var dismiss = palette.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Dismiss");
            Assert.Equal(24, dismiss.Bounds.Height);
            var point = dismiss.TranslatePoint(new Point(dismiss.Bounds.Width / 2, dismiss.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(DialogResult.Cancel, await opening.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(trigger.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CommandClearButtonReservesSpaceAndReturnsFocusToSearch()
    {
        var trigger = new AlderButton { Content = "Open" };
        var palette = new AlderCommand
        {
            Groups = [new() { Items = [new() { Label = "Open" }, new() { Label = "Save" }] }]
        };
        var window = new AlderTestWindow { Width = 640, Height = 720, Content = new DialogHost { Content = trigger } };
        AlderMotion.SetReduced(window, true);
        window.Show();
        try
        {
            var opening = palette.ShowAsync(trigger);
            Refresh(window);
            var input = palette.GetVisualDescendants().OfType<AlderSearchField>().Single();
            var clear = Assert.IsType<AlderButton>(input.InnerRightContent);
            var editor = input.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_ScrollViewer");
            Assert.False(clear.IsVisible);
            var editorWidth = editor.Bounds.Width;

            window.KeyTextInput("open");
            Refresh(window);
            Assert.True(clear.IsVisible);
            Assert.Equal(new Size(20, 20), clear.Bounds.Size);
            Assert.False(KeyboardNavigation.GetIsTabStop(clear));
            Assert.Equal(editorWidth, editor.Bounds.Width);
            var dismiss = palette.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Dismiss");
            Assert.Equal(20, dismiss.TranslatePoint(default, window)!.Value.X -
                clear.TranslatePoint(new Point(clear.Bounds.Width, 0), window)!.Value.X);

            var point = clear.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Refresh(window);
            Assert.Equal("", palette.Query);
            Assert.False(clear.IsVisible);
            Assert.Equal(editorWidth, editor.Bounds.Width);
            Assert.True(input.IsFocused);
            Assert.Equal(2, palette.GetVisualDescendants().OfType<ListBox>().Single().Items.Count);

            Press(window, PhysicalKey.Escape);
            Assert.Equal(DialogResult.Cancel, await opening.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CommandSplitPreviewFollowsKeyboardSelectionAndFiltering()
    {
        var button = new AlderCommandItem { Label = "Button" };
        var input = new AlderCommandItem { Label = "Input" };
        var palette = new AlderCommand
        {
            Groups = [new() { Items = [button, input] }],
            PreviewTemplate = new FuncDataTemplate<AlderCommandItem>((item, _) =>
                new TextBlock { Name = "PreviewText", Text = item?.Label })
        };
        var window = new AlderTestWindow { Width = 576, Height = 440, Content = palette };
        window.Show();
        try
        {
            Refresh(window);
            Assert.Same(button, palette.ActiveItem);
            Assert.True(palette.GetVisualDescendants().OfType<AlderSearchField>().Single().Focus());
            Press(window, PhysicalKey.ArrowDown);
            Refresh(window);
            Assert.Same(input, palette.ActiveItem);
            Assert.Equal("Input", palette.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PreviewText").Text);

            palette.Query = "not found";
            Refresh(window);
            Assert.Null(palette.ActiveItem);
        }
        finally { window.Close(); }
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
    }

    private static void Refresh(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class TestCommand : ICommand
    {
        public bool Enabled
        {
            get => field;
            set { field = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        } = true;

        public List<object?> Executions { get; } = [];
        public int CanExecuteCalls { get; set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter)
        {
            CanExecuteCalls++;
            return Enabled;
        }
        public void Execute(object? parameter) => Executions.Add(parameter);
    }
}
