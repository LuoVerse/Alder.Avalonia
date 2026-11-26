using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class FileTreeInteractions
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void DragPreviewLiftsTheWholeRowAndFollowsTheCapturedPointer(bool moveSelection, bool cancel)
    {
        var source = new AlderFileTreeNode("source", "Source");
        var other = new AlderFileTreeNode("other", "Other");
        var target = new AlderFileTreeNode("target", "Target");
        var move = new MoveCommand();
        var tree = new AlderFileTree
        {
            ItemsSource = new[] { source, other, target },
            MoveCommand = move,
            MoveSelectionTogether = moveSelection,
            SelectionMode = SelectionMode.Multiple,
            ShowSelectionCheckboxes = true
        };
        AlderMotion.SetReduced(tree, true);
        var window = new Window { Width = 340, Height = 200, Content = tree };
        window.Show();
        try
        {
            UpdateLayout();
            if (moveSelection)
            {
                tree.SelectedItems!.Add(other);
                tree.SelectedItems.Add(source);
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
            Assert.Equal(moveSelection ? 2 : 0, tree.SelectedItems!.Count);
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
                Assert.Equal<object>(moveSelection ? [source, other] : [source], request.Items);
            }

            Assert.False(Part<Border>(targetRow, "DropBefore").IsEffectivelyVisible);
            Assert.Equal(1, sourceRow.Opacity);
            Assert.Null(preview.GetVisualParent());
        }
        finally { window.Close(); }

        void UpdateLayout()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private sealed class MoveCommand : ICommand
    {
        public AlderFileTreeMove? Request { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is AlderFileTreeMove;
        public void Execute(object? parameter) => Request = (AlderFileTreeMove?)parameter;
    }
}
