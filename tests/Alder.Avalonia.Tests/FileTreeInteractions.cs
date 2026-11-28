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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class FileTreeInteractions
{
    [AvaloniaTheory]
    [InlineData(6, 60, 10)]
    [InlineData(12, 0, 1)]
    public void DraggingTheThumbToTheEndRevealsTheLastFile(int earlierFolderSize, int lastFolderSize, int dragSteps)
    {
        var nodes = Enumerable.Range(0, 12).Select(index => index switch
        {
            0 or 5 or 10 => ExpandedFolder(index, earlierFolderSize),
            11 when lastFolderSize > 0 => ExpandedFolder(index, lastFolderSize),
            _ => new AlderFileTreeNode($"File {index}", $"File {index}")
        }).ToArray();
        var tree = new AlderFileTree
        {
            ItemsSource = nodes, Width = 300, Height = 420, VerticalAlignment = VerticalAlignment.Top
        };
        AlderMotion.SetReduced(tree, true);
        var window = new Window { Width = 360, Height = 480, Content = tree };
        window.Show();
        window.SetRenderScaling(1.25);
        try
        {
            Pump(window);
            var scroll = Assert.Single(tree.GetVisualDescendants().OfType<AlderScrollViewer>());
            window.MouseMove(tree.TranslatePoint(new Point(295, 210), window)!.Value);
            Pump(window);
            var bar = Part<ScrollBar>(scroll, "PART_VerticalScrollBar");
            var track = Part<Track>(bar, "PART_Track");
            var thumb = track.Thumb!;
            var start = thumb.TranslatePoint(new Rect(thumb.Bounds.Size).Center, window)!.Value;
            var end = track.TranslatePoint(new Point(track.Bounds.Width / 2, track.Bounds.Height - 1), window)!.Value;

            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            for (int step = 1; step <= dragSteps; step++)
            {
                window.MouseMove(new Point(start.X, start.Y + (end.Y - start.Y) * step / dragSteps), RawInputModifiers.LeftMouseButton);
                Pump(window);
                Pump(window);
            }
            window.MouseUp(end, MouseButton.Left);
            Pump(window);
            Pump(window);

            var last = lastFolderSize > 0 ? nodes[^1].Children[^1] : nodes[^1];
            var row = Assert.IsType<AlderFileTreeItem>(tree.TreeContainerFromItem(last));
            var viewport = Part<ScrollContentPresenter>(scroll, "PART_ContentPresenter");
            var bottom = row.TranslatePoint(new Point(0, row.Bounds.Height), viewport)!.Value.Y;
            Assert.InRange(bottom, 1, viewport.Bounds.Height + 1);
            Assert.InRange(Math.Abs(scroll.ScrollBarMaximum.Y - scroll.Offset.Y), 0, 0.5);
        }
        finally { window.Close(); }

        static AlderFileTreeNode ExpandedFolder(int index, int childCount)
        {
            var folder = new AlderFileTreeNode($"Folder {index}", $"Folder {index}") { IsFolder = true, IsExpanded = true };
            folder.Children.AddRange(Enumerable.Range(0, childCount)
                .Select(child => new AlderFileTreeNode($"File {index}.{child}", $"File {index}.{child}")));
            return folder;
        }
    }

    [AvaloniaFact]
    public void VirtualizesRootsAndKeepsSelectionWhenScrollingAwayAndBack()
    {
        var nodes = Enumerable.Range(0, 2000).Select(i => new AlderFileTreeNode(i.ToString(), $"File {i}")).ToArray();
        var tree = new AlderFileTree { ItemsSource = nodes, SelectionMode = SelectionMode.Multiple };
        var window = new AlderTestWindow { Width = 320, Height = 165, Content = tree };
        window.Show();
        try
        {
            Pump(window);
            Assert.InRange(tree.GetRealizedContainers().Count(), 2, 12);
            var first = Assert.IsType<AlderFileTreeItem>(tree.ContainerFromIndex(0));
            Assert.True(first.Focus(NavigationMethod.Directional));
            for (int index = 1; index <= 12; index++)
            {
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                Pump(window);
                Assert.Same(nodes[index], tree.SelectedItem);
            }

            tree.ScrollIntoView(1999);
            Pump(window);
            Assert.NotNull(tree.ContainerFromIndex(1999));
            Assert.InRange(tree.GetRealizedContainers().Count(), 1, 12);
            Assert.Same(nodes[12], tree.SelectedItem);
            var last = Assert.IsType<AlderFileTreeItem>(tree.ContainerFromIndex(1999));
            Assert.True(last.Focus(NavigationMethod.Directional, KeyModifiers.Shift));
            Assert.Equal(nodes[12..], tree.SelectedItems.Cast<AlderFileTreeNode>());
            tree.ScrollIntoView(12);
            Pump(window);
            Assert.True(Assert.IsType<AlderFileTreeItem>(tree.ContainerFromIndex(12)).IsSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReverseTabReturnsToTheSelectedRowAndSelectionCanScrollToAnOffscreenDescendant()
    {
        var folder = new AlderFileTreeNode("folder", "Folder") { IsFolder = true, IsExpanded = true };
        folder.Children.AddRange(Enumerable.Range(0, 100).Select(i => new AlderFileTreeNode(i.ToString(), $"File {i}")));
        var selected = folder.Children[95];
        var tree = new AlderFileTree
        {
            ItemsSource = new[] { folder }, Height = 130, SelectedItem = selected, AutoScrollToSelectedItem = true
        };
        AlderMotion.SetReduced(tree, true);
        var before = new AlderButton { Content = "Before" };
        var after = new AlderButton { Content = "After" };
        var host = new StackPanel { Children = { before, tree, after } };
        var window = new AlderTestWindow { Width = 320, Height = 260, Content = host };
        window.Show();
        try
        {
            Pump(window);
            var row = Assert.IsType<AlderFileTreeItem>(tree.TreeContainerFromItem(selected));
            Assert.True(row.IsSelected);

            var scroll = tree.GetVisualDescendants().OfType<AlderScrollViewer>().First();
            scroll.Offset = default;
            Pump(window);
            Assert.Null(tree.TreeContainerFromItem(selected));
            Assert.True(after.Focus());
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Pump(window);
            Assert.True(Assert.IsType<AlderFileTreeItem>(tree.TreeContainerFromItem(selected)).IsFocused);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Pump(window);
            Assert.True(before.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void VirtualizesAnExpandedNestedFolderAndNavigatesAcrossFolderBoundaries()
    {
        var root = new AlderFileTreeNode("root", "Root") { IsFolder = true, IsExpanded = true };
        var branch = new AlderFileTreeNode("branch", "Branch") { IsFolder = true };
        branch.Children.AddRange(Enumerable.Range(0, 500).Select(i => new AlderFileTreeNode(i.ToString(), $"File {i}")));
        root.Children.Add(branch);
        var next = new AlderFileTreeNode("next", "Next");
        var tree = new AlderFileTree { ItemsSource = new[] { root, next } };
        AlderMotion.SetReduced(tree, true);
        var window = new AlderTestWindow { Width = 320, Height = 165, Content = tree };
        window.Show();
        try
        {
            Pump(window);
            var branchRow = Assert.IsType<AlderFileTreeItem>(tree.TreeContainerFromItem(branch));
            Assert.Empty(branchRow.GetRealizedContainers());
            Assert.True(branchRow.Focus(NavigationMethod.Directional));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump(window);
            Assert.True(branch.IsExpanded);
            Assert.InRange(branchRow.GetRealizedContainers().Count(), 2, 12);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump(window);
            Assert.Same(branch.Children[0], tree.SelectedItem);
            branchRow.ScrollIntoView(499);
            Pump(window);
            var last = Assert.IsType<AlderFileTreeItem>(branchRow.ContainerFromIndex(499));
            Assert.True(last.Focus(NavigationMethod.Directional));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump(window);
            Assert.Same(next, tree.SelectedItem);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Pump(window);
            Assert.Same(branch.Children[499], tree.SelectedItem);
            Assert.True(branch.IsExpanded);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ACustomTreeTemplateKeepsExpansionWhenItsRootIsVirtualizedAway()
    {
        var folder = new TestNode("Folder", [new("Child", [])]);
        var nodes = new[] { folder }.Concat(Enumerable.Range(0, 100).Select(i => new TestNode($"File {i}", []))).ToArray();
        var tree = new AlderFileTree
        {
            ItemsSource = nodes,
            ItemTemplate = new FuncTreeDataTemplate<TestNode>((node, _) => new TextBlock { Text = node.Name }, node => node.Children)
        };
        AlderMotion.SetReduced(tree, true);
        var window = new AlderTestWindow { Width = 320, Height = 165, Content = tree };
        window.Show();
        try
        {
            Pump(window);
            var first = Assert.IsType<AlderFileTreeItem>(tree.ContainerFromIndex(0));
            first.IsExpanded = true;
            Pump(window);
            Assert.NotNull(tree.TreeContainerFromItem(folder.Children[0]));

            tree.ScrollIntoView(100);
            Pump(window);
            Assert.Null(tree.ContainerFromIndex(0));
            tree.ScrollIntoView(0);
            Pump(window);

            Assert.True(Assert.IsType<AlderFileTreeItem>(tree.ContainerFromIndex(0)).IsExpanded);
            Assert.NotNull(tree.TreeContainerFromItem(folder.Children[0]));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void DragPreviewLiftsTheWholeRowAndFollowsTheCapturedPointer(bool moveSelection, bool cancel, bool selectOffscreen)
    {
        var source = new AlderFileTreeNode("source", "Source");
        var other = new AlderFileTreeNode("other", "Other");
        var target = new AlderFileTreeNode("target", "Target");
        var offscreen = new AlderFileTreeNode("offscreen", "Offscreen");
        AlderFileTreeNode[] nodes = selectOffscreen
            ? [source, other, target, .. Enumerable.Range(0, 20).Select(i => new AlderFileTreeNode(i.ToString(), $"File {i}")), offscreen]
            : [source, other, target];
        var move = new MoveCommand();
        var tree = new AlderFileTree
        {
            ItemsSource = nodes,
            MoveCommand = move,
            MoveSelectionTogether = moveSelection,
            SelectionMode = SelectionMode.Multiple,
            ShowSelectionCheckboxes = true
        };
        AlderMotion.SetReduced(tree, true);
        var window = new AlderTestWindow { Width = 340, Height = 200, Content = tree };
        window.Show();
        try
        {
            UpdateLayout();
            if (moveSelection)
            {
                tree.SelectedItems!.Add(other);
                tree.SelectedItems.Add(source);
                if (selectOffscreen)
                {
                    tree.SelectedItems.Add(offscreen);
                    Assert.Null(tree.TreeContainerFromItem(offscreen));
                }
                UpdateLayout();
            }
            var rows = tree.GetVisualDescendants().OfType<AlderFileTreeItem>().ToArray();
            var sourceRow = Part<Border>(rows[0], "PART_Row");
            var otherRow = Part<Border>(rows[1], "PART_Row");
            var targetRow = Part<Border>(rows[2], "PART_Row");
            var grip = Part<Border>(sourceRow, "PART_DragHandle");
            var check = Part<AlderCheckBox>(sourceRow, "PART_Selection");
            Assert.Equal(0, grip.Opacity);
            Assert.True(grip.Bounds.Right <= check.Bounds.Left);

            var start = grip.TranslatePoint(new Rect(grip.Bounds.Size).Center, window)!.Value;
            window.MouseMove(start);
            UpdateLayout();
            Assert.Equal(1, grip.Opacity);
            Assert.Equal(0, Part<Border>(targetRow, "PART_DragHandle").Opacity);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(9, 0), RawInputModifiers.LeftMouseButton);

            var drop = targetRow.TranslatePoint(new Point(92, 2), window)!.Value;
            window.MouseMove(drop, RawInputModifiers.LeftMouseButton);
            UpdateLayout();
            Assert.Equal(selectOffscreen ? 3 : moveSelection ? 2 : 0, tree.SelectedItems!.Count);
            Assert.Equal(.4, sourceRow.Opacity);
            Assert.Equal(moveSelection ? .4 : 1, otherRow.Opacity);
            Assert.True(Part<Border>(targetRow, "DropBefore").IsEffectivelyVisible);
            var preview = Part<Border>(window, "FileTreeDragPreview");
            Assert.Equal(sourceRow.Bounds.Size, preview.Bounds.Size);
            Assert.Equal(sourceRow.CornerRadius, preview.CornerRadius);
            Assert.Equal(default, preview.BorderThickness);
            Assert.NotEqual(default, preview.BoxShadow);
            Assert.NotNull(Assert.IsType<AlderImage>(preview.Child).Source);
            var translation = Assert.IsType<TranslateTransform>(preview.RenderTransform);
            var sourceOrigin = sourceRow.TranslatePoint(default, window)!.Value;
            var liftedOrigin = preview.TranslatePoint(default, window)!.Value;
            Assert.Equal(sourceOrigin.X + drop.X - start.X, liftedOrigin.X, 1);
            Assert.Equal(sourceOrigin.Y + drop.Y - start.Y, liftedOrigin.Y, 1);
            double x = translation.X;

            drop += new Vector(28, 0);
            window.MouseMove(drop, RawInputModifiers.LeftMouseButton);
            Assert.Equal(x + 28, translation.X, 1);
            if (cancel)
            {
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                UpdateLayout();
                Assert.Null(preview.GetVisualParent());
            }

            window.MouseUp(drop, MouseButton.Left);
            UpdateLayout();
            if (cancel) Assert.Null(move.Request);
            else
            {
                var request = Assert.IsType<AlderFileTreeMove>(move.Request);
                Assert.Same(source, request.Item);
                Assert.Same(target, request.Target);
                Assert.Equal(FileTreeDropPosition.Before, request.Position);
                Assert.Equal<object>(selectOffscreen ? [source, other, offscreen] : moveSelection ? [source, other] : [source], request.Items);
            }

            Assert.False(Part<Border>(targetRow, "DropBefore").IsEffectivelyVisible);
            Assert.Equal(1, sourceRow.Opacity);
            Assert.Null(preview.GetVisualParent());
        }
        finally { window.Close(); }

        void UpdateLayout() => Pump(window);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private sealed record TestNode(string Name, TestNode[] Children);

    private sealed class MoveCommand : ICommand
    {
        public AlderFileTreeMove? Request { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is AlderFileTreeMove;
        public void Execute(object? parameter) => Request = (AlderFileTreeMove?)parameter;
    }
}
