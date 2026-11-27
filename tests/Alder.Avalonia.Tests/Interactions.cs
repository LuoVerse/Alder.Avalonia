using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Threading;
using Alder.Avalonia.Themes;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Alder.Avalonia.Controls;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Alder.Avalonia.Tests.TestApp))]

namespace Alder.Avalonia.Tests;

public sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new AlderTheme());
    }
}

public class Interactions
{
    [AvaloniaFact]
    public void ScrollShadowTracksDistanceAndClearsWhenContentShrinks()
    {
        var content = new Border { Height = 400 };
        var scroll = new AlderScrollShadow { Height = 100, ShadowOffset = 10, Content = content };
        Assert.Equal(40, scroll.GetValue(AlderScrollShadow.ShadowSizeProperty));
        Assert.Throws<ArgumentException>(() => scroll.ShadowSize = -1);
        Assert.Throws<ArgumentException>(() => scroll.ShadowOffset = double.NaN);
        var changes = new List<ScrollShadowVisibility>();
        scroll.ShadowVisibilityChanged += (_, visibility) => changes.Add(visibility);
        var window = Show(scroll);
        window.UpdateLayout();

        var presenter = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollContentPresenter>());
        var mask = Assert.IsType<LinearGradientBrush>(presenter.OpacityMask);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollShadowVisibility.Bottom, scroll.VisibleShadows);
        Assert.Equal(.6, mask.GradientStops[2].Offset, 6);
        scroll.Offset = new Vector(0, 10);
        window.UpdateLayout();
        Assert.Equal(ScrollShadowVisibility.Bottom, scroll.VisibleShadows);

        scroll.Offset = new Vector(0, 20);
        window.UpdateLayout();
        Assert.Equal(ScrollShadowVisibility.Both, scroll.VisibleShadows);
        Assert.Same(mask, presenter.OpacityMask);
        Assert.Equal(.1, mask.GradientStops[1].Offset, 6);

        scroll.Offset = new Vector(0, 50);
        window.UpdateLayout();
        Assert.Equal(.4, mask.GradientStops[1].Offset, 6);
        Assert.Equal(new[] { ScrollShadowVisibility.Bottom, ScrollShadowVisibility.Both }, changes);
        scroll.ScrollToEnd();
        window.UpdateLayout();
        Assert.Equal(ScrollShadowVisibility.Top, scroll.VisibleShadows);

        content.Height = 40;
        window.UpdateLayout();
        Assert.Null(presenter.OpacityMask);
        Assert.Equal(new[] { ScrollShadowVisibility.Bottom, ScrollShadowVisibility.Both, ScrollShadowVisibility.Top, ScrollShadowVisibility.None }, changes);
        window.Close();
    }

    [AvaloniaFact]
    public void ScrollShadowKeepsKeyboardScrollingWithHiddenBarAndSupportsManualEdges()
    {
        var scroll = new AlderScrollShadow
        {
            Width = 180, Height = 80, Orientation = Orientation.Horizontal, HideScrollBar = true,
            FlowDirection = FlowDirection.RightToLeft,
            Content = new Border { Width = 640 }
        };
        var window = Show(scroll);
        window.UpdateLayout();
        var presenter = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollContentPresenter>());
        var horizontal = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(), x => x.Orientation == Orientation.Horizontal);
        Assert.Equal(ScrollBarVisibility.Hidden, scroll.HorizontalScrollBarVisibility);
        Assert.False(horizontal.IsVisible);
        Assert.Equal(ScrollShadowVisibility.Right, scroll.VisibleShadows);

        Assert.True(scroll.Focus());
        Press(window, PhysicalKey.ArrowLeft);
        window.UpdateLayout();
        Assert.True(scroll.Offset.X > 0);
        Assert.Equal(ScrollShadowVisibility.Both, scroll.VisibleShadows);
        Press(window, PhysicalKey.End);
        window.UpdateLayout();
        Assert.Equal(ScrollShadowVisibility.Left, scroll.VisibleShadows);

        var end = scroll.Offset;
        var arrowAtEnd = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left };
        scroll.RaiseEvent(arrowAtEnd);
        Assert.False(arrowAtEnd.Handled);
        Assert.Equal(end, scroll.Offset);
        Press(window, PhysicalKey.PageUp);
        window.UpdateLayout();
        Assert.True(scroll.Offset.X < end.X);

        scroll.IsShadowEnabled = false;
        Assert.Null(presenter.OpacityMask);
        scroll.ShadowVisibility = ScrollShadowVisibility.Both;
        Assert.Equal(ScrollShadowVisibility.Both, scroll.VisibleShadows);
        scroll.ShadowVisibility = ScrollShadowVisibility.Right;
        Assert.Equal(ScrollShadowVisibility.Right, scroll.VisibleShadows);
        scroll.ShadowVisibility = ScrollShadowVisibility.None;
        Assert.Null(presenter.OpacityMask);

        scroll.HideScrollBar = false;
        Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
        scroll.Orientation = Orientation.Vertical;
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        window.Close();
    }

    [AvaloniaFact]
    public void BadgeTracksCornerWithoutChangingAnchorSize()
    {
        var badge = new AlderBadge { Size = AlderSize.Small, Color = AlderStatusColor.Danger, Content = "5" };
        Assert.Equal(12, badge.GetValue(AlderBadge.IconSizeProperty));
        var anchor = new AlderBadgeAnchor { Children = { new Border { Width = 40, Height = 40 }, badge } };
        var chip = new AlderChip { Content = "Pending", Color = AlderStatusColor.Warning, Variant = ChipVariant.Soft };
        var window = Show(new StackPanel { Children = { anchor, chip } });
        window.UpdateLayout();

        Assert.Equal(new Size(40, 40), anchor.Bounds.Size);
        Assert.Equal(new Rect(28, -4, 16, 16), badge.Bounds);
        badge.Placement = BadgePlacement.BottomLeft;
        window.UpdateLayout();
        Assert.Equal(new Rect(-4, 28, 16, 16), badge.Bounds);
        Assert.Equal(24, chip.Bounds.Height);
        Assert.Equal(0x26, Assert.IsAssignableFrom<ISolidColorBrush>(chip.Background).Color.A);
        window.Close();
    }

    [AvaloniaFact]
    public void TableRecyclesRowsAndPreservesKeyedSelectionWhenSortedOrReplaced()
    {
        var table = new AlderTable
        {
            Height = 136,
            ItemsSource = Enumerable.Range(1, 10_000).Select(index => new TableTestRow(index, $"Row {index}")).ToArray(),
            RowKeySelector = row => ((TableTestRow)row).Id,
            SelectionMode = TableSelectionMode.Multiple,
            SelectedKeys = new object[] { 2 },
            Columns =
            {
                new AlderTableColumn
                {
                    Key = "name",
                    Header = "Name",
                    Width = new GridLength(160),
                    CanSort = true,
                    SortComparison = (x, y) => ((TableTestRow)x).Id.CompareTo(((TableTestRow)y).Id)
                }
            }
        };
        var window = Show(table);
        window.UpdateLayout();
        var view = table.GetVisualDescendants().OfType<TableView>().Single();
        Assert.Equal(10_000, table.VisibleRowCount);
        Assert.InRange(view.GetRealizedContainers().Count(), 1, 64);
        Assert.Equal(2, Assert.IsType<TableTestRow>(view.SelectedItem).Id);

        var header = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Name"));
        Assert.True(header.Focus());
        Press(window, PhysicalKey.Enter);
        Assert.Equal(new AlderTableSort("name", TableSortDirection.Ascending), table.SortDescriptor);
        Press(window, PhysicalKey.Enter);
        Assert.Equal(new AlderTableSort("name", TableSortDirection.Descending), table.SortDescriptor);
        Assert.Equal(10_000, Assert.IsType<TableTestRow>(view.ItemsView[0]).Id);
        Assert.Equal(2, Assert.IsType<TableTestRow>(view.SelectedItem).Id);

        table.SelectedKeys = [2, 9999, 10001];
        Assert.Equal([1, 9998], view.Selection.SelectedIndexes);

        table.ItemsSource = new[] { new TableTestRow(2, "Replacement") };
        Assert.Equal("Replacement", Assert.IsType<TableTestRow>(view.SelectedItem).Name);
        window.Close();
    }

    [AvaloniaFact]
    public void CollapsedTableRowsOnlyProbeForAChildUntilExpanded()
    {
        var root = new TableTestRow(1, "Folder");
        var children = Enumerable.Range(2, 32).Select(id => new TableTestRow(id, $"File {id}")).ToArray();
        int visited = 0;
        var table = new AlderTable
        {
            ItemsSource = new[] { root },
            RowKeySelector = row => ((TableTestRow)row).Id,
            ChildrenSelector = row => ReferenceEquals(row, root) ? EnumerateChildren() : null,
            Columns = { new AlderTableColumn { Key = "name", Header = "Name", Width = new GridLength(180) } }
        };
        var window = Show(table);
        try
        {
            Assert.Equal(1, table.VisibleRowCount);
            Assert.InRange(visited, 1, children.Length - 1);
            table.ExpandedKeys = [root.Id];
            Assert.Equal(children.Length + 1, table.VisibleRowCount);
        }
        finally { window.Close(); }

        IEnumerable<TableTestRow> EnumerateChildren()
        {
            foreach (var child in children)
            {
                visited++;
                yield return child;
            }
        }
    }

    [AvaloniaFact]
    public void TableExpandsThroughItsCellAndSelectAllSkipsDisabledRows()
    {
        var root = new TableTestRow(1, "Folder", [new(2, "File")]);
        var table = new AlderTable
        {
            Height = 136,
            ItemsSource = new[] { root, new TableTestRow(3, "Unavailable") },
            RowKeySelector = row => ((TableTestRow)row).Id,
            ChildrenSelector = row => ((TableTestRow)row).Children,
            DisabledKeys = new object[] { 3 },
            SelectionMode = TableSelectionMode.Multiple,
            ShowSelectionCheckboxes = true,
            Columns = { new AlderTableColumn { Key = "name", Header = "Name", Width = new GridLength(180) } }
        };
        var window = Show(table);
        window.UpdateLayout();
        Assert.Equal(2, table.VisibleRowCount);
        var expander = table.GetVisualDescendants().OfType<Button>().Single(x =>
            x.IsEffectivelyVisible && AutomationProperties.GetName(x) == "Expand row");
        Assert.True(expander.Focus());
        Press(window, PhysicalKey.Enter);
        window.UpdateLayout();
        Assert.Equal(3, table.VisibleRowCount);
        Assert.Equal(1, Assert.Single(table.ExpandedKeys!));
        Assert.True(expander.IsFocused);
        Assert.Equal("Collapse row", AutomationProperties.GetName(expander));

        var checkAll = table.GetVisualDescendants().OfType<CheckBox>().Single(x => AutomationProperties.GetName(x) == "Select all rows");
        Assert.True(checkAll.Focus());
        Press(window, PhysicalKey.Space);
        Assert.Equal(new object[] { 1, 2 }, table.SelectedKeys!.OrderBy(key => key));
        window.Content = null;
        window.RequestedThemeVariant = ThemeVariant.Dark;
        window.Content = table;
        window.UpdateLayout();
        Assert.Single(table.GetVisualDescendants().OfType<CheckBox>(),
            x => AutomationProperties.GetName(x) == "Select all rows" && x.IsEffectivelyVisible);
        window.Close();
    }

    private sealed record TableTestRow(int Id, string Name, IReadOnlyList<TableTestRow>? Children = null);

    [AvaloniaFact]
    public void KbdRendersInlineAndUpdatesLocalizedKeyName()
    {
        var key = new AlderKbdKey { KeyValue = KbdKey.Command };
        var shortcut = new AlderKbd { Items = { key, "K" } };
        var paragraph = new TextBlock
        {
            Inlines = new InlineCollection
            {
                new Run("Press "),
                new InlineUIContainer(shortcut) { BaselineAlignment = BaselineAlignment.Center }
            }
        };
        var window = Show(paragraph);
        window.UpdateLayout();

        var peer = ControlAutomationPeer.CreatePeerForElement(key);
        Assert.Equal("⌘", key.Glyph);
        Assert.Equal("Command", peer.GetName());
        Assert.Equal("Command", AlderToolTip.GetTip(key));
        Assert.Equal(24, shortcut.Bounds.Height);
        Assert.Equal(new CornerRadius(8), shortcut.CornerRadius);
        Assert.Equal(2, shortcut.GetRealizedContainers().Count());

        window.Resources["AlderText.KbdShift"] = "Shift key";
        key.KeyValue = KbdKey.Shift;
        shortcut.Variant = KbdVariant.Light;
        window.UpdateLayout();
        Assert.Equal("⇧", key.Glyph);
        Assert.Equal("Shift key", peer.GetName());
        Assert.Equal("Shift key", AlderToolTip.GetTip(key));
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(shortcut.Background).Color);

        window.Resources["AlderText.KbdShift"] = "Maj";
        Assert.Equal("Maj", AlderToolTip.GetTip(key));
        key.Title = "Modifier";
        Assert.Equal("Modifier", peer.GetName());
        window.Close();
    }

    [AvaloniaFact]
    public void TypographyTracksHeadingSemanticsAndNativeTruncation()
    {
        var heading = new AlderHeading { Text = "Overview" };
        var paragraph = new AlderParagraph
        {
            Size = TypographyParagraphSize.Small,
            MaxWidth = 112,
            Truncate = true,
            Text = "A long description with more than one line of content."
        };
        var code = new AlderCode { Text = "set value" };
        var window = Show(new AlderProse { Children = { heading, paragraph, code } });
        window.UpdateLayout();

        var peer = ControlAutomationPeer.CreatePeerForElement(heading);
        Assert.Equal(1, peer.GetHeadingLevel());
        Assert.Equal(36, heading.FontSize);
        heading.Level = 4;
        window.UpdateLayout();
        Assert.Equal(4, peer.GetHeadingLevel());
        Assert.Equal(20, heading.FontSize);
        AutomationProperties.SetHeadingLevel(heading, 2);
        Assert.Equal(2, peer.GetHeadingLevel());
        Assert.Equal(20, heading.FontSize);

        Assert.Equal(14, paragraph.FontSize);
        Assert.Equal(TextTrimming.CharacterEllipsis, paragraph.TextTrimming);
        Assert.Single(paragraph.TextLayout.TextLines);
        paragraph.Truncate = false;
        window.UpdateLayout();
        Assert.True(paragraph.TextLayout.TextLines.Count > 1);
        Assert.Equal(TypographyType.Code, code.Type);
        Assert.Equal(20, code.LineHeight);
        Assert.Equal(new Thickness(6, 2), code.Padding);
        window.RequestedThemeVariant = ThemeVariant.Light;
        Assert.Equal(Color.Parse("#EBEBEC"), Assert.IsAssignableFrom<ISolidColorBrush>(code.CodeBackground).Color);
        window.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Equal(Color.Parse("#27272A"), Assert.IsAssignableFrom<ISolidColorBrush>(code.CodeBackground).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void ProseWrapsLongLinksWithoutTrimming()
    {
        const string label = "Open the Typography reference for layout and alignment";
        var link = new AlderLink { Content = label };
        var window = Show(new AlderProse { Width = 125, Children = { link } });
        window.UpdateLayout();

        var text = Assert.Single(link.GetVisualDescendants().OfType<TextBlock>());
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(text.TextDecorations!).Location);
        Assert.True(text.TextLayout.TextLines.Count > 1);
        Assert.All(text.TextLayout.TextLines, line => Assert.False(line.HasCollapsed || line.HasOverflowed));
        Assert.True(text.TextLayout.TextLines.Sum(line => line.Length) >= label.Length);
        window.Close();
    }

    [AvaloniaFact]
    public void ProseCodeScrollsFromTheStartInRightToLeftLayouts()
    {
        var code = new SelectableTextBlock { Text = "dotnet add package Alder.Avalonia\ndotnet build" };
        var scroll = new AlderScrollViewer
        {
            Content = code,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var block = new Border { Classes = { "prose-pre" }, Child = scroll };
        var window = Show(new AlderProse { FlowDirection = FlowDirection.RightToLeft, Width = 145, Children = { block } });
        window.UpdateLayout();

        Assert.Equal(FlowDirection.LeftToRight, scroll.FlowDirection);
        Assert.Equal(TextWrapping.NoWrap, code.TextWrapping);
        Assert.Equal(2, code.TextLayout.TextLines.Count);
        Assert.True(scroll.Extent.Width > scroll.Viewport.Width);
        Assert.Equal(0, scroll.Offset.X);
        window.Close();
    }

    [AvaloniaFact]
    public void AvatarGroupClipsAgainstNextAvatarsSizeAndRadius()
    {
        var first = new AlderAvatar { Size = AlderSize.Large, Fallback = "AB" };
        var next = new AlderAvatar { Size = AlderSize.Small, Fallback = "CD" };
        var group = new AlderAvatarGroup
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Items = { first, next }
        };
        var window = Show(group);
        window.UpdateLayout();

        var firstSlot = first.GetVisualParent()!.GetVisualParent()!;
        var clip = Assert.IsType<CombinedGeometry>(firstSlot.Clip);
        var cutout = Assert.IsType<RectangleGeometry>(clip.Geometry2);
        Assert.Equal(new Rect(38, 6, 36, 36), cutout.Rect);
        Assert.Equal(18, cutout.RadiusX);

        next.CornerRadius = new CornerRadius(4);
        group.SeamThickness = 3;
        window.UpdateLayout();
        clip = Assert.IsType<CombinedGeometry>(firstSlot.Clip);
        cutout = Assert.IsType<RectangleGeometry>(clip.Geometry2);
        Assert.Equal(new Rect(37, 5, 38, 38), cutout.Rect);
        Assert.Equal(7, cutout.RadiusX);

        group.Overlap = AvatarOverlap.Ring;
        window.UpdateLayout();
        Assert.Null(firstSlot.Clip);
        window.Close();
    }

    [AvaloniaFact]
    public void AvatarGroupWrapsAtTwelveDipGapsAndKeepsExplicitCountOutsideMax()
    {
        var first = new AlderAvatar { Fallback = "AB" };
        var second = new AlderAvatar { Size = AlderSize.Small, Fallback = "CD" };
        var last = new AlderAvatar { Fallback = "EF" };
        var count = new AlderAvatar { Fallback = "+9", Color = AvatarColor.Accent };
        var group = new AlderAvatarGroup
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MaxWidth = 100,
            IsGrid = true,
            Overlap = AvatarOverlap.Ring,
            Max = 2,
            CountContent = count,
            Items = { first, second, last }
        };
        var window = Show(group);
        window.UpdateLayout();

        Assert.Equal(1, group.OverflowCount);
        Assert.Equal(new[] { first, second, count }, group.GetVisualDescendants().OfType<AlderAvatar>().ToArray());
        Assert.Equal(new Point(52, 4), second.TranslatePoint(default, group));
        Assert.Equal(new Point(0, 52), count.TranslatePoint(default, group));
        Assert.Equal(AvatarColor.Accent, count.Color);

        group.Max = 0;
        window.UpdateLayout();
        Assert.Equal(3, group.OverflowCount);
        Assert.Same(count, Assert.Single(group.GetVisualDescendants().OfType<AlderAvatar>()));
        group.CountContent = null;
        window.UpdateLayout();
        Assert.Equal("+3", Assert.Single(group.GetVisualDescendants().OfType<AlderAvatar>()).Fallback);
        window.Close();
    }

    [AvaloniaFact]
    public void SelectKeepsCommittedChecksSeparateFromKeyboardFocus()
    {
        var select = new AlderSelect
        {
            ItemsSource = new[] { "Alpha", "Beta", "Gamma" },
            VerticalAlignment = VerticalAlignment.Top,
            SelectedIndex = 0,
            ShowClearButton = true,
            ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = $"Option {value}" })
        };
        AlderMotion.SetReduced(select, true);
        var window = Show(select);
        window.Height = 400;
        window.UpdateLayout();
        var trigger = select.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
        var point = trigger.TranslatePoint(new Rect(trigger.Bounds.Size).Center, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(select.IsDropDownOpen);
        var list = window.GetVisualDescendants().OfType<ListBox>().Single();
        var rows = list.GetRealizedContainers().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Contains(rows[0].GetVisualDescendants().OfType<TextBlock>(), text => text.IsVisible && text.Text == "Option Alpha");
        Assert.All(rows, row => Assert.False(row.ClipToBounds));
        Assert.All(rows, row => Assert.Equal(Brushes.Transparent, OptionSurface(row).Background));
        Assert.Equal(1, CheckMark(rows[0]).Opacity);
        Assert.Equal(0, CheckMark(rows[1]).Opacity);
        Press(window, PhysicalKey.ArrowDown);
        Assert.Equal(rows[1].FindResource(rows[1].ActualThemeVariant, "AlderNeutralHover"), OptionSurface(rows[1]).Background);
        Assert.Equal(0, select.SelectedIndex);
        Press(window, PhysicalKey.Enter);
        Assert.Equal(1, select.SelectedIndex);
        Assert.False(select.IsDropDownOpen);
        Press(window, PhysicalKey.Delete);
        Assert.Equal(-1, select.SelectedIndex);
        window.Close();

        static Border OptionSurface(Control row) => row.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "OptionSurface");
        static global::Avalonia.Controls.Shapes.Path CheckMark(Control row) => row.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single();
    }

    [AvaloniaFact]
    public async Task SelectLightDismissUpdatesItsStateBeforeTheExitAnimationEnds()
    {
        var select = new AlderSelect { ItemsSource = new[] { "Alpha", "Beta" }, VerticalAlignment = VerticalAlignment.Top };
        var window = Show(new Panel { Background = Brushes.Transparent, Children = { select } });
        select.IsDropDownOpen = true;
        await Task.Delay(160);
        var popup = window.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "AlderPopoverSurface");
        var outside = new Point(380, 100);
        window.MouseDown(outside, MouseButton.Left);
        window.MouseUp(outside, MouseButton.Left);

        Assert.False(select.IsDropDownOpen);
        Assert.True(popup.IsAttachedToVisualTree());
        Assert.False(popup.IsHitTestVisible);
        await Task.Delay(160);
        Assert.False(popup.IsAttachedToVisualTree());
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(nameof(AlderSelect))]
    [InlineData(nameof(AlderComboBox))]
    [InlineData(nameof(AlderAutocomplete))]
    public async Task PickerVirtualizesLargeGroupedAndFilteredLists(string pickerType)
    {
        const int count = 100_000;
        var options = Enumerable.Range(0, count).Select(index => new AlderPickerOption
        {
            Text = $"Option {index:D6}", Group = $"Section {index / 1_000}",
            IsEnabled = index < count - 2,
            Description = index % 7 == 0 ? "An option with a description" : null
        }).ToArray();
        int captionsRead = 0;
        int matchesChecked = 0;
        AlderPicker picker = pickerType switch
        {
            nameof(AlderSelect) => new AlderSelect(),
            nameof(AlderComboBox) => new AlderComboBox(),
            _ => new AlderAutocomplete()
        };
        picker.ItemsSource = options;
        picker.VerticalAlignment = VerticalAlignment.Top;
        picker.MaxDropDownHeight = 280;
        picker.TextSelector = item =>
        {
            captionsRead++;
            return ((AlderPickerOption)item!).Text;
        };
        picker.Filter = (query, item) =>
        {
            matchesChecked++;
            return query == "even" && ((AlderPickerOption)item!).Text[^1] is '0' or '2' or '4' or '6' or '8';
        };

        var window = new AlderTestWindow { Width = 600, Height = 500, Content = picker };
        window.Show();
        try
        {
            picker.IsDropDownOpen = true;
            Pump();
            var popup = window.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "AlderPopoverSurface");
            var list = popup.GetVisualDescendants().OfType<ListBox>().Single();
            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
            const int sectionRows = 199;
            Assert.Equal(count + sectionRows, list.ItemCount);
            AssertVirtualized(list);
            Assert.InRange(captionsRead, 1, 32);
            Assert.Equal(0, matchesChecked);
            Assert.Contains(list.GetRealizedContainers(), row => row.Classes.Contains("section") && !row.IsEnabled);

            list.ScrollIntoView(75_000);
            Pump();
            Assert.True(scroll.Offset.Y > scroll.Viewport.Height * 1_000);
            AssertVirtualized(list);

            int expectedIndex;
            if (picker is AlderSelect)
            {
                Press(window, PhysicalKey.End);
                expectedIndex = count - 3;
            }
            else
            {
                var input = picker is AlderComboBox
                    ? picker.GetVisualDescendants().OfType<AlderInput>().Single(control => control.Name == "PART_Editor")
                    : popup.GetVisualDescendants().OfType<AlderSearchField>().Single();
                Assert.True(input.Focus());
                captionsRead = 0;
                input.SetCurrentValue(TextBox.TextProperty, "even");
                Pump();
                Assert.Equal(count, matchesChecked);
                Assert.Equal(count / 2 + sectionRows, list.ItemCount);
                Assert.InRange(captionsRead, 1, 32);
                Assert.Equal(0, scroll.Offset.Y);
                AssertVirtualized(list);
                Assert.Same(input, window.FocusManager?.GetFocusedElement());

                list.SelectedIndex = list.ItemCount - 3;
                Press(window, PhysicalKey.ArrowDown);
                expectedIndex = count - 4;
            }
            Pump();
            Press(window, PhysicalKey.ArrowDown);
            Pump();
            AssertVirtualized(list);
            Assert.Equal(options[expectedIndex].Text, AutomationProperties.GetName(list.ContainerFromIndex(list.SelectedIndex)!));
            Assert.Null(picker.SelectedItem);
            Press(window, PhysicalKey.Enter);
            Assert.Same(options[expectedIndex], picker.SelectedItem);
            Assert.False(picker.IsDropDownOpen);
            Assert.True(popup.IsAttachedToVisualTree());
            Assert.False(popup.IsHitTestVisible);
            await Task.Delay(180);
            Assert.False(popup.IsAttachedToVisualTree());

            picker.IsDropDownOpen = true;
            Pump();
            int committedIndex = expectedIndex + sectionRows;
            list.ScrollIntoView(committedIndex);
            Pump();
            var committed = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(committedIndex));
            Assert.Equal(options[expectedIndex].Text, AutomationProperties.GetName(committed));
            if (picker is AlderAutocomplete)
                Assert.Contains(committed.GetVisualDescendants().OfType<PathIcon>(), check => check.IsEffectivelyVisible);
            else
                Assert.Equal(1, committed.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single().Opacity);
            AssertVirtualized(list);
        }
        finally { window.Close(); }

        void Pump()
        {
            for (int i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }

        static void AssertVirtualized(ListBox list)
        {
            Assert.IsType<VirtualizingStackPanel>(list.ItemsPanelRoot);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 24);
        }
    }

    [AvaloniaFact]
    public async Task SelectKeepsTheAnchorGapWhenTheMenuFlipsAbove()
    {
        var select = new AlderSelect
        {
            ItemsSource = new[] { "Alpha", "Beta" },
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 6)
        };
        AlderMotion.SetReduced(select, true);
        var window = new AlderTestWindow { Width = 420, Height = 260, Content = new Panel { Children = { select } } };
        window.Show();
        select.IsDropDownOpen = true;
        await Task.Delay(50);
        var trigger = select.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
        var popup = window.GetVisualDescendants().OfType<Control>().Single(x => x.Name == "AlderPopoverSurface");
        var triggerTop = trigger.TranslatePoint(default, window)!.Value.Y;
        var popupBottom = popup.TranslatePoint(new Point(0, popup.Bounds.Height), window)!.Value.Y;
        Assert.True(Math.Abs(triggerTop - popupBottom - 8) < .01,
            $"Expected an 8 DIP gap above the trigger at {triggerTop}, but the popup bottom was {popupBottom} in a {window.ClientSize.Height} DIP viewport.");
        window.Close();
    }

    [AvaloniaFact]
    public void ComboBoxReopensFromAnEditorThatKeptFocusAfterLightDismiss()
    {
        var combo = new AlderComboBox { ItemsSource = new[] { "Alpha", "Beta" }, VerticalAlignment = VerticalAlignment.Top };
        AlderMotion.SetReduced(combo, true);
        var window = Show(new Panel { Background = Brushes.Transparent, Children = { combo } });
        var editor = combo.GetVisualDescendants().OfType<AlderInput>().Single(x => x.Name == "PART_Editor");
        var inside = editor.TranslatePoint(new Point(12, 18), window)!.Value;
        var outside = new Point(380, 100);

        Click(inside);
        Assert.True(combo.IsDropDownOpen);
        Assert.True(editor.IsFocused);

        Click(outside);
        Assert.False(combo.IsDropDownOpen);
        Assert.True(editor.IsFocused);

        Click(inside);
        Assert.True(combo.IsDropDownOpen);

        Click(outside);
        combo.MenuTrigger = PickerMenuTrigger.Input;
        Click(inside);
        Assert.False(combo.IsDropDownOpen);
        window.Close();

        void Click(Point point)
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void ComboBoxPreservesUnmatchedDraftUntilItIsRevertedOrCommitted()
    {
        var combo = new AlderComboBox
        {
            ItemsSource = new[] { "Alpha", "Beta" }, SelectedIndex = 0, VerticalAlignment = VerticalAlignment.Top
        };
        AlderMotion.SetReduced(combo, true);
        var window = Show(combo);
        var editor = combo.GetVisualDescendants().OfType<AlderInput>().Single(x => x.Name == "PART_Editor");
        Assert.True(editor.Focus());
        Dispatcher.UIThread.RunJobs();
        Assert.True(combo.IsDropDownOpen);

        editor.SetCurrentValue(TextBox.TextProperty, "xyz");
        Dispatcher.UIThread.RunJobs();
        Assert.False(combo.IsDropDownOpen);
        Assert.True(editor.IsFocused);
        Assert.Equal("xyz", combo.Text);
        Assert.Equal(0, combo.SelectedIndex);

        editor.SetCurrentValue(TextBox.TextProperty, "be");
        Dispatcher.UIThread.RunJobs();
        Assert.True(combo.IsDropDownOpen);
        Press(window, PhysicalKey.Escape);
        Assert.Equal("Alpha", combo.Text);

        combo.AllowsCustomValue = true;
        editor.SetCurrentValue(TextBox.TextProperty, "Custom");
        Dispatcher.UIThread.RunJobs();
        Press(window, PhysicalKey.Enter);
        Assert.Equal("Custom", combo.Text);
        Assert.Equal(-1, combo.SelectedIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void ComboBoxMultipleSelectionKeepsFocusAndDisplaysSelectedValuesBelowTheInput()
    {
        var combo = new AlderComboBox
        {
            ItemsSource = new[] { "Alpha", "Beta" },
            SelectionMode = SelectionMode.Multiple,
            SelectionPlaceholderText = "No items selected",
            VerticalAlignment = VerticalAlignment.Top
        };
        AlderMotion.SetReduced(combo, true);
        var window = new AlderTestWindow { Width = 420, Height = 400, Content = combo };
        window.Show();
        var editor = combo.GetVisualDescendants().OfType<AlderInput>().Single(x => x.Name == "PART_Editor");
        var placeholder = combo.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "MultiplePlaceholder");
        Assert.True(placeholder.IsVisible);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        editor.SetCurrentValue(TextBox.TextProperty, "al");

        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.Enter);
        Assert.True(combo.IsDropDownOpen);
        Assert.True(editor.IsFocused);
        Assert.True(string.IsNullOrEmpty(combo.Text));
        Assert.False(placeholder.IsVisible);
        Assert.Equal("Alpha", combo.SelectionText);
        window.UpdateLayout();
        var list = window.GetVisualDescendants().OfType<ListBox>().Single();
        var second = list.GetRealizedContainers().Last();
        var point = second.TranslatePoint(new Rect(second.Bounds.Size).Center, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.IsFocused);
        Assert.Equal(2, combo.SelectedItems!.Count);
        Assert.Equal("Alpha and Beta", combo.SelectionText);
        window.Close();
    }

    [AvaloniaFact]
    public void AutocompleteUsesUiThreadLabelsAndPreservesLargeSearchResultOrder()
    {
        var options = Enumerable.Range(0, 5000).Select(i => $"Option {i}").Append("Café first").Append("Café second").ToArray();
        var picker = new AlderAutocomplete
        {
            ItemsSource = options,
            TextSelector = value =>
            {
                Assert.True(Dispatcher.UIThread.CheckAccess());
                return (string)value!;
            }
        };
        AlderMotion.SetReduced(picker, true);
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var popup = window.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "AlderPopoverSurface");
            var list = popup.GetVisualDescendants().OfType<ListBox>().Single();
            var search = popup.GetVisualDescendants().OfType<AlderSearchField>().Single();
            search.SetCurrentValue(TextBox.TextProperty, "CAFE");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(2, list.ItemCount);
            Assert.Collection(list.ItemsView.Cast<PickerEntry>(),
                item => Assert.Equal("Café first", item.Item), item => Assert.Equal("Café second", item.Item));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutocompleteLetsTagsWrapAndRemoveOnlyTheirOwnSelectedItem()
    {
        var autocomplete = new AlderAutocomplete
        {
            ItemsSource = new[] { "New Hampshire", "North Carolina", "Washington" },
            SelectionMode = SelectionMode.Multiple,
            ShowSelectionTags = true,
            ShowClearButton = false,
            Width = 200
        };
        AlderMotion.SetReduced(autocomplete, true);
        var window = Show(autocomplete);
        var trigger = autocomplete.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
        var wrap = autocomplete.GetVisualDescendants().OfType<WrapPanel>().Single(x => x.Name == "PART_SelectionTags");
        Assert.Equal(36, trigger.Bounds.Height);

        autocomplete.SelectedItems!.Add(autocomplete.Items[0]);
        autocomplete.SelectedItems!.Add(autocomplete.Items[1]);
        window.UpdateLayout();
        Assert.True(trigger.Bounds.Height > 36);
        Assert.Equal(2, wrap.Children.Count);

        var remove = wrap.GetVisualDescendants().OfType<Button>().First();
        var location = remove.TranslatePoint(new Rect(remove.Bounds.Size).Center, window)!.Value;
        window.MouseDown(location, MouseButton.Left);
        window.MouseUp(location, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(autocomplete.SelectedItems!);
        Assert.Equal("North Carolina", autocomplete.SelectionText);
        Assert.False(autocomplete.IsDropDownOpen);
        var remaining = Assert.Single(wrap.GetVisualDescendants().OfType<Button>());
        Assert.True(trigger.IsFocused);
        Assert.DoesNotContain(":focus-visible", trigger.Classes);
        Assert.DoesNotContain(":focus-visible", remaining.Classes);

        remaining.Focus(NavigationMethod.Tab);
        Assert.Contains(":focus-visible", remaining.Classes);
        Press(window, PhysicalKey.Space);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(autocomplete.SelectedItems!);
        Assert.True(trigger.IsFocused);
        Assert.Contains(":focus-visible", trigger.Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void AutocompletePointerRemovalKeepsDropdownOpenAndReturnsFocusToTrigger()
    {
        var autocomplete = new AlderAutocomplete
        {
            ItemsSource = new[] { "Florida", "Delaware", "California", "Texas" },
            SelectionMode = SelectionMode.Multiple,
            ShowSelectionTags = true,
            ShowClearButton = false,
            Width = 256,
            Label = "States to Visit"
        };
        AlderMotion.SetReduced(autocomplete, true);
        var window = Show(autocomplete);
        AlderMotion.SetReduced(window, true);
        try
        {
            foreach (var item in autocomplete.Items.Take(3))
                autocomplete.SelectedItems!.Add(item);
            autocomplete.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var search = window.GetVisualDescendants().OfType<AlderSearchField>().Single();
            var trigger = autocomplete.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            var tags = autocomplete.GetVisualDescendants().OfType<WrapPanel>().Single(x => x.Name == "PART_SelectionTags");
            var remove = tags.GetVisualDescendants().OfType<AlderButton>().First();
            Assert.True(search.Focus(NavigationMethod.Tab));
            Assert.True(remove.Focus(NavigationMethod.Tab));
            Assert.Contains(":focus-visible", remove.Classes);
            var point = remove.TranslatePoint(new Rect(remove.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { "Delaware", "California" }, autocomplete.SelectedItems!.Cast<string>());
            Assert.True(autocomplete.IsDropDownOpen);
            Assert.True(trigger.IsFocused);
            Assert.DoesNotContain(":focus-visible", trigger.Classes);
            Assert.False(search.IsFocused);
            Assert.All(tags.GetVisualDescendants().OfType<AlderButton>(),
                button => Assert.False(button.IsFocused));

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            Assert.Contains("Florida", autocomplete.SelectedItems!.Cast<string>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutocompleteClearPreservesTheInputMethod()
    {
        var autocomplete = new AlderAutocomplete
        {
            ItemsSource = new[] { "Alpha", "Beta" },
            ShowClearButton = true,
            SelectedIndex = 0
        };
        AlderMotion.SetReduced(autocomplete, true);
        var window = Show(autocomplete);
        try
        {
            var trigger = autocomplete.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Trigger");
            var clear = autocomplete.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Clear");
            var point = clear.TranslatePoint(new Rect(clear.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(-1, autocomplete.SelectedIndex);
            Assert.True(trigger.IsFocused);
            Assert.DoesNotContain(":focus-visible", trigger.Classes);

            autocomplete.SelectedIndex = 1;
            window.UpdateLayout();
            clear.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(-1, autocomplete.SelectedIndex);
            Assert.True(trigger.IsFocused);
            Assert.Contains(":focus-visible", trigger.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisclosureCollapsesToItsTriggerAndRetainsInteractiveContent()
    {
        var editor = new AlderInput { Text = "Keep this value" };
        var disclosure = new AlderDisclosure { Header = "Details", Content = editor, IsExpanded = true };
        AlderMotion.SetReduced(disclosure, true);
        var window = Show(disclosure);
        var trigger = DisclosureTrigger(disclosure);
        var panel = disclosure.GetVisualDescendants().OfType<AlderAccordionPanel>().Single();

        Assert.True(editor.Focus());
        disclosure.IsExpanded = false;
        Assert.True(trigger.IsFocused);
        Assert.Equal(0, panel.Progress);
        Assert.False(panel.IsHitTestVisible);
        Assert.False(editor.IsEffectivelyEnabled);

        Press(window, PhysicalKey.Space);
        Assert.True(disclosure.IsExpanded);
        Assert.Equal(1, panel.Progress);
        Assert.True(editor.IsEffectivelyEnabled);
        Assert.Equal("Keep this value", editor.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void DisclosureGroupSeedsKeysAndSkipsSectionsWithoutTriggers()
    {
        var first = new AlderDisclosure { Key = "first", Header = "First" };
        var disabled = new AlderDisclosure { Key = "disabled", Header = "Disabled", IsEnabled = false };
        var last = new AlderDisclosure { Key = "last", Header = "Last" };
        var divider = new Border { Height = 1 };
        var group = new AlderDisclosureGroup
        {
            DefaultExpandedKeys = ["first"],
            Items = { first, divider, disabled, last }
        };
        AlderMotion.SetReduced(group, true);
        var window = Show(group);

        Assert.True(first.IsExpanded);
        Assert.False(last.IsExpanded);
        Assert.Contains(divider, group.GetRealizedContainers());
        DisclosureTrigger(first).Focus();
        Press(window, PhysicalKey.ArrowDown);
        Assert.True(DisclosureTrigger(last).IsFocused);
        Press(window, PhysicalKey.Space);
        Assert.Equal(["last"], group.ExpandedKeys);
        Assert.False(first.IsExpanded);
        Assert.True(last.IsExpanded);
        Press(window, PhysicalKey.Space);
        Assert.Empty(group.ExpandedKeys!);
        Assert.False(first.IsExpanded);
        Assert.False(last.IsExpanded);

        group.AllowsMultipleExpanded = true;
        group.ExpandedKeys = ["first", "last"];
        Assert.True(first.IsExpanded);
        Assert.True(last.IsExpanded);
        group.IsEnabled = false;
        Assert.False(DisclosureTrigger(first).IsEffectivelyEnabled);
        Assert.False(DisclosureTrigger(last).IsEffectivelyEnabled);
        window.Close();
    }

    private static ToggleButton DisclosureTrigger(AlderDisclosure item) =>
        item.GetVisualDescendants().OfType<ToggleButton>().Single(x => x.Name == "PART_Trigger");

    [AvaloniaFact]
    public void AccordionSynchronizesKeyboardSelectionAndPanelVisibility()
    {
        var first = new AlderAccordionItem { Key = "first", Header = "First", Content = new AlderButton { Content = "Body" } };
        var blocked = new AlderAccordionItem { Key = "blocked", Header = "Blocked", IsEnabled = false };
        var last = new AlderAccordionItem { Key = "last", Header = "Last", Content = "Last body" };
        var accordion = new AlderAccordion { Items = { first, blocked, last }, ExpandedKeys = ["first"] };
        AlderMotion.SetReduced(accordion, true);
        var window = Show(accordion);

        Assert.True(first.IsExpanded);
        var firstHeader = DisclosureTrigger(first);
        var body = first.GetVisualDescendants().OfType<AlderAccordionPanel>().Single();
        Assert.Equal(1, body.Progress);
        Assert.Contains("with-separator", first.Classes);
        Assert.DoesNotContain("with-separator", last.Classes);
        firstHeader.Focus();
        Press(window, PhysicalKey.ArrowDown);
        Assert.True(DisclosureTrigger(last).IsFocused);
        Press(window, PhysicalKey.Space);
        Assert.Equal(["last"], accordion.ExpandedKeys);
        Assert.False(first.IsExpanded);
        Assert.Equal(0, body.Progress);
        Assert.False(body.IsHitTestVisible);
        Assert.True(last.IsExpanded);

        accordion.AllowsMultipleExpanded = true;
        accordion.ExpandedKeys = ["first", "last"];
        Assert.True(first.IsExpanded);
        Assert.True(last.IsExpanded);
        var focusableBody = Assert.IsType<AlderButton>(first.Content);
        Assert.True(focusableBody.Focus());
        first.IsExpanded = false;
        Assert.True(firstHeader.IsFocused);
        Assert.Equal(["last"], accordion.ExpandedKeys);

        accordion.HideSeparator = true;
        Assert.DoesNotContain("with-separator", first.Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void BreadcrumbsUpdateCurrentPageAndSeparatorsWhenPathChanges()
    {
        var home = new AlderBreadcrumbItem { Content = "Home" };
        var category = new AlderBreadcrumbItem { Content = "Category" };
        var leaf = new AlderBreadcrumbItem { Content = "Current" };
        var path = new AlderBreadcrumbs { Separator = "/", Items = { home, category, leaf } };
        var window = Show(path);
        var link = ControlAutomationPeer.CreatePeerForElement(home)!;
        var current = ControlAutomationPeer.CreatePeerForElement(leaf)!;
        Assert.Equal(AutomationControlType.Hyperlink, link.GetAutomationControlType());
        Assert.Equal(AutomationControlType.Text, current.GetAutomationControlType());
        Assert.True(leaf.IsCurrent);
        Assert.False(leaf.IsEffectivelyEnabled);
        Assert.Equal("/", home.Separator);
        Assert.Contains("with-separator", home.Classes);
        Assert.DoesNotContain("with-separator", leaf.Classes);

        int followed = 0;
        home.Click += (_, _) => followed++;
        home.Focus();
        Press(window, PhysicalKey.Enter);
        Assert.Equal(1, followed);
        path.IsEnabled = false;
        Assert.False(home.IsEffectivelyEnabled);
        path.IsEnabled = true;

        leaf.IsVisible = false;
        Assert.True(category.IsCurrent);
        Assert.DoesNotContain("with-separator", category.Classes);
        path.Items.Remove(category);
        Assert.True(home.IsCurrent);
        Assert.False(category.IsCurrent);
        path.Items.Clear();
        Assert.False(home.IsCurrent);
        window.Close();
    }

    private static Window Show(Control control)
    {
        var window = new AlderTestWindow { Content = control, Width = 420, Height = 140 };
        window.Show();
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    [AvaloniaFact]
    public void LabelThemeFollowsNativeFieldStateAndTransfersFocus()
    {
        Assert.True(Application.Current!.TryFindResource("AlderLabelTheme", out var resource));
        var input = new AlderInput { IsRequired = true };
        var label = new Label { Content = "_Email", Theme = Assert.IsType<ControlTheme>(resource), Target = input };
        var other = new AlderButton { Content = "Other" };
        AutomationProperties.SetLabeledBy(input, label);
        var window = Show(new StackPanel { Margin = new Thickness(8), Spacing = 4, Children = { label, input, other } });
        window.RequestedThemeVariant = ThemeVariant.Light;

        var mark = label.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "RequiredMark");
        Assert.True(mark.IsVisible);
        Assert.Equal("_Email", label.Content);
        var peer = ControlAutomationPeer.CreatePeerForElement(input)!;
        Assert.Equal("Email", peer.GetName());
        Assert.True(AutomationProperties.GetIsRequiredForForm(input));

        var point = label.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.True(input.IsFocused);

        input.IsInvalid = true;
        Assert.Equal(Color.Parse("#FF383C"), Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
        DataValidationErrors.SetErrors(input, ["Invalid"]);
        input.IsInvalid = false;
        Assert.True(AlderLabel.GetIsInvalid(label));
        DataValidationErrors.ClearErrors(input);
        Assert.False(AlderLabel.GetIsInvalid(label));
        Assert.Equal(Color.Parse("#18181B"), Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);

        input.IsEnabled = false;
        Assert.False(label.IsEffectivelyEnabled);
        Assert.Equal(.5, label.Opacity);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.False(input.IsFocused);

        input.IsEnabled = true;
        input.IsRequired = false;
        Assert.True(label.IsEffectivelyEnabled);
        Assert.False(mark.IsVisible);
        Assert.True(other.Focus());
        window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.Alt);
        window.KeyReleaseQwerty(PhysicalKey.E, RawInputModifiers.Alt);
        Assert.True(input.IsFocused);
        label.Target = null;
        Assert.False(AlderLabel.GetIsInvalid(label));
        Assert.False(mark.IsVisible);
        AlderLabel.SetIsRequired(label, true);
        AlderLabel.SetIsInvalid(label, true);
        Assert.True(mark.IsVisible);
        Assert.True(label.IsEffectivelyEnabled);
        Assert.Equal(Color.Parse("#FF383C"), Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
        window.Close();
    }

    [AvaloniaFact]
    public async Task MenuFlyoutDefersNativeDismissalUntilItsExitFinishes()
    {
        var flyout = new AlderMenuFlyout { Items = { new MenuItem { Header = "Merge" } } };
        flyout.Popup.ShouldUseOverlayLayer = true;
        var button = new AlderButton { Content = "Options", Flyout = flyout };
        var window = Show(new StackPanel { Children = { button } });
        int closes = 0;
        flyout.Closed += (_, _) => closes++;

        flyout.ShowAt(button);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var presenter = Assert.IsType<AlderMenuFlyoutPresenter>(flyout.Popup.Child);
        presenter.Close();
        Assert.True(flyout.IsOpen);
        Assert.False(flyout.Popup.IsHitTestVisible);
        Assert.True(presenter.IsAttachedToVisualTree());
        await Task.Delay(180);
        Assert.False(flyout.IsOpen);
        Assert.False(presenter.IsAttachedToVisualTree());
        Assert.True(flyout.Popup.IsHitTestVisible);
        Assert.Equal(1, closes);

        flyout.ShowAt(button);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        presenter.Close();
        AlderMotion.SetReduced(window, true);
        Assert.False(flyout.IsOpen);
        Assert.Equal(2, closes);
        flyout.ShowAt(button);
        presenter.Close();
        Assert.False(flyout.IsOpen);
        Assert.Equal(3, closes);
        window.Close();
    }

    [AvaloniaFact]
    public async Task InputOtpEditsOneCodeAndCompletesAfterTransformedPaste()
    {
        var completed = new RecordCodeCommand();
        var input = new AlderInputOtp
        {
            Pattern = AlderInputOtp.DigitsPattern,
            PasteTransformer = text => text.Replace("-", ""),
            CompletedCommand = completed
        };
        var window = Show(new StackPanel { Children = { input } });
        AlderMotion.SetReduced(window, true);
        Assert.True(input.Focus());
        Assert.Single(input.GetVisualDescendants().OfType<TextPresenter>());
        var slots = input.GetVisualDescendants().OfType<Control>().Where(c => c.Classes.Contains("otp-slot")).ToArray();
        Assert.Equal(6, slots.Length);
        Assert.All(slots, slot => Assert.False(slot.ClipToBounds));
        Assert.Single(input.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("otp-separator"));

        window.KeyTextInput("12");
        window.KeyTextInput("A");
        Assert.Equal("12", input.Text);
        Press(window, PhysicalKey.ArrowLeft);
        window.KeyTextInput("9");
        Assert.Equal("19", input.Text);
        Press(window, PhysicalKey.Backspace);
        Assert.Equal("1", input.Text);

        input.SelectAll();
        await window.Clipboard!.SetTextAsync("123-456");
        input.Paste();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Equal("123456", input.Text);
        Assert.Equal(["123456"], completed.Codes);
        Press(window, PhysicalKey.ArrowLeft);
        window.KeyTextInput("8");
        Assert.Equal("123458", input.Text);
        Assert.Single(completed.Codes);

        input.MaxLength = 4;
        input.GroupSize = 0;
        window.UpdateLayout();
        Assert.Equal("1234", input.Text);
        Assert.Equal(4, input.GetVisualDescendants().Count(v => v is Control c && c.Classes.Contains("otp-slot")));
        Assert.DoesNotContain(input.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("otp-separator"));
        input.IsEnabled = false;
        Assert.False(input.Focus());
        window.Close();
    }

    private sealed class RecordCodeCommand : System.Windows.Input.ICommand
    {
        public List<string> Codes { get; } = [];
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is string;
        public void Execute(object? parameter) => Codes.Add((string)parameter!);
    }

    [AvaloniaFact]
    public void SearchSubmitsOutsideCompositionAndReturnsFocusOnClear()
    {
        var command = new RecordCodeCommand();
        var input = new AlderSearchField { SubmitCommand = command };
        var window = Show(new StackPanel { Margin = new Thickness(8), Children = { input } });
        var clear = Assert.IsType<AlderButton>(input.InnerRightContent);
        var presenter = input.GetVisualDescendants().OfType<TextPresenter>().Single();
        Assert.NotNull(Assert.IsType<AlderPathIcon>(input.InnerLeftContent).Data);
        Assert.NotNull(Assert.IsType<AlderPathIcon>(clear.Content).Data);
        Assert.False(clear.IsVisible);

        Assert.True(input.Focus());
        window.KeyTextInput("project");
        Assert.True(clear.IsVisible);
        presenter.SetCurrentValue(TextPresenter.PreeditTextProperty, "編");
        Press(window, PhysicalKey.Enter);
        Assert.Empty(command.Codes);
        presenter.SetCurrentValue(TextPresenter.PreeditTextProperty, "");
        Press(window, PhysicalKey.Enter);
        Assert.Equal(["project"], command.Codes);

        var point = clear.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.Equal("", input.Text);
        Assert.True(input.IsFocused);
        Assert.False(clear.IsVisible);

        window.KeyTextInput("document");
        Press(window, PhysicalKey.Escape);
        Assert.Equal("", input.Text);
        input.Text = "archived";
        input.IsReadOnly = true;
        Assert.False(clear.IsVisible);
        Press(window, PhysicalKey.Escape);
        Assert.Equal("archived", input.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void NumberFieldKeepsDecimalValuesAndPlacesVerticalStepsNextToEditor()
    {
        var number = new AlderNumberField
        {
            VerticalStepper = true, FormatString = "P0", NumberFormat = CultureInfo.GetCultureInfo("en-US").NumberFormat,
            Value = .5m, Increment = .01m, Minimum = 0, Maximum = 1, ClipValueToMinMax = true
        };
        var window = Show(new StackPanel { Margin = new Thickness(8), Children = { number } });
        var editor = number.GetVisualDescendants().OfType<AlderInput>().Single();
        var up = number.GetVisualDescendants().OfType<RepeatButton>().Single(x => x.Name == "PART_IncreaseButton");
        var down = number.GetVisualDescendants().OfType<RepeatButton>().Single(x => x.Name == "PART_DecreaseButton");
        Assert.Equal("50%", editor.Text);
        Assert.NotNull(Assert.IsType<AlderPathIcon>(up.Content).Data);
        Assert.Equal(24, up.Bounds.Width);
        Assert.Equal(18, up.Bounds.Height);
        var upPoint = up.TranslatePoint(new Point(12, 9), window)!.Value;
        var downPoint = down.TranslatePoint(new Point(12, 9), window)!.Value;
        Assert.Equal(upPoint.X, downPoint.X);
        Assert.Equal(18, downPoint.Y - upPoint.Y);

        Assert.True(editor.Focus());
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal(.51m, number.Value);
        window.MouseDown(downPoint, MouseButton.Left);
        window.MouseUp(downPoint, MouseButton.Left);
        Assert.Equal(.5m, number.Value);
        Assert.True(editor.IsFocused);

        editor.SelectAll();
        window.KeyTextInput("75%");
        Press(window, PhysicalKey.Enter);
        Assert.Equal(.75m, number.Value);
        number.Value = 1;
        Assert.False(up.IsEnabled);
        number.IsReadOnly = true;
        Press(window, PhysicalKey.ArrowDown);
        Assert.Equal(1m, number.Value);

        number.IsReadOnly = false;
        number.Minimum = -20;
        number.Maximum = 20;
        var unitFormat = (NumberFormatInfo)CultureInfo.GetCultureInfo("de-DE").NumberFormat.Clone();
        unitFormat.CurrencySymbol = "kg";
        number.NumberFormat = unitFormat;
        number.FormatString = "C2";
        number.Value = 12.5m;
        Assert.Equal("12,50 kg", editor.Text);
        editor.SelectAll();
        window.KeyTextInput("-12,75 kg");
        Press(window, PhysicalKey.Enter);
        Assert.Equal(-12.75m, number.Value);
        window.Close();
    }

    [AvaloniaFact]
    public void TextAreaUsesNativeNewlinesAndCanResizeItsViewport()
    {
        var area = new AlderTextArea { Rows = 3, Width = 200, MaxHeight = 115 };
        var window = Show(new StackPanel { Margin = new Thickness(8), Children = { area } });
        Assert.True(area.Focus());
        window.KeyTextInput("First");
        Press(window, PhysicalKey.Enter);
        window.KeyTextInput("Second");
        Assert.Equal("First\nSecond", area.Text!.ReplaceLineEndings("\n"));
        Assert.True(area.Bounds.Height >= 70);
        var handle = area.GetVisualDescendants().OfType<Border>().Single(x => x.Child is PathIcon);
        Assert.NotNull(((PathIcon)handle.Child!).Data);
        var point = handle.TranslatePoint(new Point(8, 8), window)!.Value;
        var end = point + new Point(0, 28);
        var height = area.Bounds.Height;
        window.MouseDown(point, MouseButton.Left);
        window.MouseMove(end);
        window.MouseUp(end, MouseButton.Left);
        Assert.Equal(height + 28, area.Height);
        area.IsReadOnly = true;
        Assert.False(handle.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void DateAndTimeSegmentsCommitOnlyValidValues()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var date = new AlderDateField { Culture = culture, Value = new DateOnly(2024, 2, 29) };
        var time = new AlderTimeField { Culture = culture, Clock = TimeFieldClock.TwelveHour, Value = new TimeOnly(14, 25, 45) };
        var window = Show(new StackPanel { Spacing = 8, Children = { date, time } });

        Assert.False(date.ClipToBounds);
        Assert.False(time.ClipToBounds);

        Segment(date, "Year").Focus();
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal(new DateOnly(2025, 2, 28), date.Value);
        Segment(date, "Day").Focus();
        window.KeyTextInput("31");
        Assert.True(date.HasInvalidInput);
        Assert.Equal(new DateOnly(2025, 2, 28), date.Value);
        Press(window, PhysicalKey.Escape);
        Assert.False(date.HasInvalidInput);
        Assert.Equal("28", Segment(date, "Day").Content);

        Segment(time, "Hour").Focus();
        Press(window, PhysicalKey.ArrowRight);
        Assert.True(Segment(time, "Minute").IsFocused);
        window.KeyTextInput("05");
        Assert.Equal(new TimeOnly(14, 5, 45), time.Value);
        Assert.True(Segment(time, "Period").IsFocused);
        window.KeyTextInput("a");
        Assert.Equal(new TimeOnly(2, 5, 45), time.Value);
        time.IsReadOnly = true;
        Segment(time, "Hour").Focus();
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal(new TimeOnly(2, 5, 45), time.Value);
        window.Close();

        static Button Segment(AlderTemporalField field, string name) => field.GetVisualDescendants().OfType<Button>()
            .Single(x => AutomationProperties.GetName(x)?.StartsWith(name + ", ", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public async Task DatePickerHighlightsCalendarDaysOnlyForKeyboardNavigation()
    {
        var selectedDate = new DateOnly(2026, 11, 25);
        var picker = new AlderDatePicker { Value = selectedDate };
        AlderMotion.SetReduced(picker, true);
        var window = new AlderTestWindow { Content = new StackPanel { Margin = new Thickness(16), Children = { picker } }, Width = 460, Height = 460 };
        window.Show();
        try
        {
            var trigger = picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_CalendarTrigger");
            Assert.True(trigger.Focus(NavigationMethod.Tab));
            var point = trigger.TranslatePoint(new Point(12, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            var selected = Day(selectedDate);
            Assert.True(selected.IsFocused);
            Assert.Contains("selected", selected.Classes);
            Assert.Equal(selected.FindResource(selected.ActualThemeVariant, "AlderAccent"), DayFill(selected).Fill);
            Assert.Equal(new Size(36, 36), DayFill(selected).Bounds.Size);

            Press(window, PhysicalKey.ArrowRight);
            var next = Day(selectedDate.AddDays(1));
            Assert.True(next.IsFocused);
            Assert.Equal(next.FindResource(next.ActualThemeVariant, "AlderNeutral"), DayFill(next).Fill);
            Assert.False(next.ClipToBounds);
            Assert.Equal(selectedDate, picker.Value);

            Press(window, PhysicalKey.Escape);
            Assert.True(trigger.IsFocused);
            Press(window, PhysicalKey.Enter);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            Assert.True(Day(selectedDate).IsFocused);
            Assert.Equal(selected.FindResource(selected.ActualThemeVariant, "AlderAccentHover"), DayFill(Day(selectedDate)).Fill);
            Press(window, PhysicalKey.Escape);
            picker.IsReadOnly = true;
            Assert.False(trigger.IsEnabled);
        }
        finally
        {
            window.Close();
        }

        AlderCalendarDayButton Day(DateOnly date) => window.GetVisualDescendants().OfType<AlderCalendarDayButton>()
            .Single(button => Equals(button.Tag, date) && !button.Classes.Contains("outside"));
        static Ellipse DayFill(AlderCalendarDayButton cell) => cell.GetVisualDescendants().OfType<Ellipse>().Single(shape => shape.Name == "DayFill");
    }

    [AvaloniaFact]
    public void GeneratedCalendarPropertiesKeepSettingsOnDerivedControls()
    {
        var calendar = new AlderCalendar();
        var picker = new AlderDatePicker();
        var rangePicker = new AlderDateRangePicker();
        Assert.Equal(BindingMode.TwoWay, AlderCalendarBase.DisplayDateProperty.GetMetadata(calendar).DefaultBindingMode);
        Assert.Equal(BindingMode.TwoWay, AlderCalendarBase.FocusedDateProperty.GetMetadata(calendar).DefaultBindingMode);
        Assert.Equal(BindingMode.TwoWay, AlderDateRangePicker.ValueProperty.GetMetadata(rangePicker).DefaultBindingMode);
        Assert.True(AlderDateRangePicker.ValueProperty.GetMetadata(rangePicker).EnableDataValidation);
        Assert.Same(AlderCalendarBase.VisibleMonthsProperty, AlderDatePicker.VisibleMonthsProperty);
        Assert.Same(AlderCalendarBase.VisibleMonthsProperty, AlderDateRangePicker.VisibleMonthsProperty);
        Assert.True(calendar.ShowYearPicker);
        Assert.True(picker.ShowYearPicker);
        Assert.True(rangePicker.ShowYearPicker);
        Assert.Equal(1, calendar.VisibleMonths);
        Assert.Equal(1, picker.VisibleMonths);
        Assert.Equal(1, rangePicker.VisibleMonths);
        calendar.VisibleMonths = 0;
        picker.VisibleMonths = 4;
        rangePicker.VisibleMonths = 4;
        Assert.Equal(1, calendar.VisibleMonths);
        Assert.Equal(3, picker.VisibleMonths);
        Assert.Equal(3, rangePicker.VisibleMonths);
    }

    [AvaloniaFact]
    public void CalendarYearPickerRecyclesRowsAndRestoresHeadingFocus()
    {
        var date = new DateOnly(2026, 11, 23);
        var calendar = new AlderCalendar
        {
            Culture = CultureInfo.GetCultureInfo("en-US"),
            DisplayDate = date,
            Minimum = new DateOnly(2018, 1, 1),
            Maximum = new DateOnly(2030, 12, 31)
        };
        AlderMotion.SetReduced(calendar, true);
        var window = new AlderTestWindow { Content = calendar, Width = 420, Height = 420 };
        window.Show();
        try
        {
            var heading = calendar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Heading");
            var years = calendar.GetVisualDescendants().OfType<ListBox>().Single(list => list.Name == "PART_Years");
            var arrows = calendar.GetVisualDescendants().OfType<Button>().Where(button => button.Name is "PART_Previous" or "PART_Next").ToArray();
            var chevron = heading.GetVisualDescendants().OfType<PathIcon>().Single();
            Assert.Equal(2, arrows.Length);
            Assert.All(arrows, arrow => Assert.Equal(new Size(24, 24), arrow.Bounds.Size));
            Assert.Equal(new Size(8, 8), chevron.Bounds.Size);
            Assert.NotEmpty(chevron.GetVisualChildren());

            Click(heading);
            Assert.True(years.IsVisible);
            Assert.NotEmpty(years.GetRealizedContainers());
            Assert.Contains("expanded", chevron.Classes);
            Assert.All(arrows, arrow =>
            {
                Assert.Equal(0, arrow.Opacity);
                Assert.False(arrow.IsHitTestVisible);
                Assert.False(arrow.IsTabStop);
            });

            Click(heading);
            Assert.False(years.IsVisible);
            Assert.Same(heading, window.FocusManager?.GetFocusedElement());
            Assert.DoesNotContain("expanded", chevron.Classes);
            Assert.All(arrows, arrow =>
            {
                Assert.Equal(1, arrow.Opacity);
                Assert.True(arrow.IsHitTestVisible);
                Assert.True(arrow.IsTabStop);
            });

            Click(heading);
            var year = years.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Tag, 2027));
            Click(year);
            Assert.Equal(date.AddYears(1), calendar.DisplayDate);
            Assert.False(years.IsVisible);
            Assert.Same(heading, window.FocusManager?.GetFocusedElement());

            Click(heading);
            Press(window, PhysicalKey.Escape);
            Assert.False(years.IsVisible);
            Assert.Same(heading, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }

        void Click(Control control)
        {
            var center = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void RangeCalendarKeepsDraftSeparateAndChecksUnavailableDates()
    {
        var first = new DateOnly(2026, 11, 27);
        var unavailable = first.AddDays(1);
        var last = first.AddDays(7);
        var committed = new AlderDateRange(first.AddDays(-5), first.AddDays(-1));
        var calendar = new AlderRangeCalendar
        {
            Culture = CultureInfo.GetCultureInfo("en-US"),
            Value = committed,
            VisibleMonths = 2,
            IsDateUnavailable = date => date == unavailable
        };
        var window = Show(calendar);
        Pick(first);
        Pick(last);
        Assert.Equal(committed, calendar.Value);
        Press(window, PhysicalKey.Escape);
        Assert.Equal(committed, calendar.Value);

        calendar.AllowNonContiguousRanges = true;
        Pick(last);
        Pick(first);
        Assert.Equal(new AlderDateRange(first, last), calendar.Value);
        Assert.Contains("start", Day(first).Classes);
        Assert.Contains("end", Day(last).Classes);
        Assert.DoesNotContain("range", Day(unavailable).Classes);
        calendar.IsReadOnly = true;
        Pick(last);
        Pick(last.AddDays(1));
        Assert.Equal(new AlderDateRange(first, last), calendar.Value);
        window.Close();

        Button Day(DateOnly date) => calendar.GetVisualDescendants().OfType<AlderCalendarDayButton>()
            .Single(x => Equals(x.Tag, date) && !x.Classes.Contains("outside"));
        void Pick(DateOnly date)
        {
            Day(date).Focus();
            Press(window, PhysicalKey.Enter);
        }
    }

    [AvaloniaFact]
    public void DateRangePickerDoesNotPublishInvertedTypedDraft()
    {
        var start = new DateOnly(2026, 11, 22);
        var end = start.AddDays(5);
        var picker = new AlderDateRangePicker
        {
            Culture = CultureInfo.GetCultureInfo("en-US"), Value = new AlderDateRange(start, end), Variant = InputVariant.Secondary
        };
        var window = Show(picker);
        Assert.False(picker.ClipToBounds);
        var fields = picker.GetVisualDescendants().OfType<AlderDateField>().ToArray();
        Assert.All(fields, field =>
        {
            var surface = field.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "FieldSurface");
            Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color.A);
        });
        Assert.Equal(start, fields[0].Value);
        Assert.Equal(end, fields[1].Value);
        fields[1].Value = start.AddDays(-1);
        Assert.True(picker.HasInvalidInput);
        Assert.Equal(new AlderDateRange(start, end), picker.Value);
        fields[1].Value = end.AddDays(1);
        Assert.False(picker.HasInvalidInput);
        Assert.Equal(new AlderDateRange(start, end.AddDays(1)), picker.Value);
        window.Close();
    }

    [AvaloniaFact]
    public void InputUsesNativeEditingAndPasswordPresentation()
    {
        var input = new AlderInput { PlaceholderText = "Name", MaxLength = 4 };
        var window = Show(new StackPanel { Children = { input } });
        var presenter = input.GetVisualDescendants().OfType<TextPresenter>().Single();
        var placeholder = input.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Placeholder");
        Assert.True(placeholder.IsVisible);
        Assert.Null(input.InnerRightContent);
        Assert.True(input.Focus());
        window.KeyTextInput("ABCDE");
        Assert.Equal("ABCD", input.Text);
        Assert.False(placeholder.IsVisible);

        input.SelectAll();
        window.KeyTextInput("Hi");
        Assert.Equal("Hi", input.Text);
        input.IsReadOnly = true;
        input.SelectAll();
        window.KeyTextInput("Changed");
        Assert.Equal("Hi", input.Text);
        Assert.Equal("Hi", input.SelectedText);

        input.Type = InputType.Password;
        Assert.Equal('•', presenter.PasswordChar);
        Assert.True(TextInputOptions.GetIsSensitive(input));
        input.Type = InputType.Email;
        Assert.Equal(TextInputContentType.Email, TextInputOptions.GetContentType(input));
        Assert.Equal('\0', presenter.PasswordChar);
        window.Close();
    }

    [AvaloniaFact]
    public void InputMatchesWidthAndInvalidFocusAndNumericInputSteps()
    {
        var input = new AlderInput { FullWidth = true, Variant = InputVariant.Secondary, IsRequired = true };
        Assert.True(Application.Current!.TryFindResource("AlderNumericInputTheme", out var resource));
        var number = new NumericUpDown { Theme = (ControlTheme)resource!, Minimum = 0, Maximum = 10, Increment = 2, Value = 4 };
        var window = Show(new StackPanel { Children = { input, number } });
        window.RequestedThemeVariant = ThemeVariant.Light;
        AlderMotion.SetReduced(window, true);

        Assert.Equal(new Size(window.ClientSize.Width, 36), input.Bounds.Size);
        Assert.True(AutomationProperties.GetIsRequiredForForm(input));
        var surface = input.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "FieldSurface");
        var ring = input.GetVisualDescendants().OfType<Control>().Single(b => b.Name == "FieldRing");
        BoxShadow Shadow() => ring.GetValue(Border.BoxShadowProperty)[0];
        Assert.Equal(Color.Parse("#EBEBEC"), Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color);
        Assert.Equal(0, surface.BoxShadow.Count);
        Assert.Null(ring.Transitions);
        Assert.Equal(0, Shadow().Spread);
        Assert.Equal(0, Shadow().Color.A);
        input.IsInvalid = true;
        Assert.Equal(1, Shadow().Spread);
        Assert.True(input.Focus());
        Assert.Equal(2, Shadow().Spread);
        Assert.Equal(Color.Parse("#FF383C"), Shadow().Color);
        input.IsInvalid = false;
        DataValidationErrors.SetErrors(input, ["A value is required."]);
        Assert.Equal(Color.Parse("#FF383C"), Shadow().Color);
        DataValidationErrors.ClearErrors(input);
        Assert.Equal(Color.Parse("#0485F7"), Shadow().Color);

        var editor = number.GetVisualDescendants().OfType<AlderInput>().Single();
        Assert.False(number.ClipToBounds);
        Assert.False(number.GetVisualDescendants().OfType<AlderInputSpinner>().Single().ClipToBounds);
        Assert.True(editor.Focus());
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal(6, number.Value);
        Press(window, PhysicalKey.ArrowDown);
        Assert.Equal(4, number.Value);
        window.Close();
    }

    [AvaloniaFact]
    public void InputGroupSharesFieldChromeAndKeepsSuffixActionsFocusable()
    {
        var action = new AlderButton { Content = "Show" };
        var group = new AlderInputGroup { Prefix = "$", Suffix = action, Type = InputType.Password, FullWidth = true };
        var window = Show(group);
        AlderMotion.SetReduced(window, true);
        var prefix = group.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "FieldPrefix");
        var suffix = group.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "FieldSuffix");
        var ring = group.GetVisualDescendants().OfType<Control>().Single(b => b.Name == "FieldRing");
        var viewer = group.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.Name == "PART_ScrollViewer");
        int clicks = 0;
        action.Click += (_, _) => { clicks++; group.RevealPassword = !group.RevealPassword; };

        Assert.Equal("$", prefix.Content);
        Assert.Same(action, suffix.Content);
        Assert.Equal(new Thickness(0, 8), viewer.Margin);
        Assert.True(group.Focus());
        window.KeyTextInput("Secret");
        Assert.Equal(2, ring.GetValue(Border.BoxShadowProperty)[0].Spread);
        Assert.True(action.Focus());
        Press(window, PhysicalKey.Space);
        Assert.Equal(1, clicks);
        Assert.Equal("Secret", group.Text);
        Assert.True(group.RevealPassword);
        Assert.Equal(0, ring.GetValue(Border.BoxShadowProperty)[0].Spread);

        group.Type = InputType.Text;
        group.AcceptsReturn = true;
        Assert.Equal(VerticalAlignment.Top, suffix.VerticalAlignment);
        Assert.True(group.Focus());
        group.CaretIndex = group.Text!.Length;
        Press(window, PhysicalKey.Enter);
        window.KeyTextInput("Line two");
        Assert.Contains("\nLine two", group.Text);
        group.Prefix = null;
        Assert.Equal(new Thickness(12, 8, 0, 8), viewer.Margin);
        group.IsEnabled = false;
        Assert.False(action.IsEffectivelyEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void PendingKeepsFocusAndBlocksKeyboardActivation()
    {
        var button = new AlderButton { Content = "保存" };
        var w = Show(button);
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        button.Focus();
        button.IsPending = true;
        Press(w, PhysicalKey.Space);
        Assert.True(button.IsFocused);
        Assert.Equal(0, clicks);
        button.IsPending = false;
        Press(w, PhysicalKey.Space);
        Assert.Equal(1, clicks);
        w.Close();
    }

    [AvaloniaFact]
    public void PressChangesTheCompositedFaceAndKeepsItsHitArea()
    {
        var button = new AlderButton { Content = "按住", Size = AlderSize.Large, Variant = ButtonVariant.Ghost };
        var window = Show(button);
        var face = button.GetVisualDescendants().OfType<ButtonFeedback>().Single();
        var tint = button.GetVisualDescendants().OfType<ButtonTint>().Single();
        var visual = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(face)!;
        var bounds = button.Bounds;
        var center = button.TranslatePoint(new Rect(bounds.Size).Center, window)!.Value;
        window.MouseMove(center);
        Assert.Equal(1, tint.Opacity);
        window.MouseDown(center, MouseButton.Left);
        Assert.Equal(.96, face.PressScale);
        Assert.Equal(.96, visual.Scale.X, 4);
        Assert.Equal(face.Bounds.Width / 2, visual.CenterPoint.X, 3);
        Assert.NotNull(visual.ImplicitAnimations);
        Assert.Equal(bounds, button.Bounds);
        AlderMotion.SetReduced(button, true);
        Assert.Equal(1, visual.Scale.X);
        Assert.Null(visual.ImplicitAnimations);
        window.MouseUp(center, MouseButton.Left);
        window.Close();
    }

    [AvaloniaFact]
    public void PendingShowsSpinnerAndSuppressesRippleWhileKeepingFocus()
    {
        var button = new AlderButton { Content = "Upload File", Icon = Geometry.Parse("M0 0H8V8H0Z"), IsRippleEnabled = true };
        var window = Show(button);
        var clip = button.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "RippleClip");
        var spinner = button.GetVisualDescendants().OfType<PathIcon>().Single(x => x.Name == "Spinner");
        var icon = button.GetVisualDescendants().OfType<AlderIconPresenter>().Single(x => x.Name == "LeadingIcon");
        Assert.Same(button.Icon, icon.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        Assert.True(icon.IsVisible);
        Assert.False(spinner.IsVisible);
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Assert.NotNull(clip.Child);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        button.IsPending = true;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(spinner.IsVisible);
        Assert.Equal(0.5, button.Opacity);
        Assert.False(icon.IsVisible);
        Assert.True(button.IsFocused);
        Press(window, PhysicalKey.Space);
        Assert.Null(clip.Child);
        button.IsPending = false;
        AlderMotion.SetReduced(button, true);
        Press(window, PhysicalKey.Space);
        Assert.False(spinner.IsVisible);
        Assert.Equal(1, button.Opacity);
        Assert.True(icon.IsVisible);
        Assert.Null(clip.Child);
        window.Close();
    }

    [AvaloniaFact]
    public void IconOnlyTracksSizeAndFullWidthUsesAvailableSpace()
    {
        var button = new AlderButton { IsIconOnly = true, Size = AlderSize.Small, Icon = Geometry.Parse("M0 0H8V8H0Z") };
        var window = Show(button);
        Assert.Equal(32, button.Bounds.Width);
        var icon = button.GetVisualDescendants().OfType<AlderIconPresenter>().Single(x => x.Name == "LeadingIcon");
        Assert.Equal(new Size(14, 14), icon.Bounds.Size);
        var iconCenter = icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, button)!.Value;
        Assert.Equal(new Rect(button.Bounds.Size).Center, iconCenter);
        button.Size = AlderSize.Large;
        window.UpdateLayout();
        Assert.Equal(40, button.Bounds.Width);
        Assert.Equal(new Size(20, 20), icon.Bounds.Size);
        button.IsIconOnly = false;
        button.FullWidth = true;
        window.UpdateLayout();
        Assert.Equal(window.ClientSize.Width, button.Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void IconControlScalesWithTheButtonThemeAndCanBeReplacedWithGeometry()
    {
        var glyph = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        var button = new AlderButton { IsIconOnly = true, Size = AlderSize.Small, Icon = glyph };
        var window = Show(button);
        var slot = button.GetVisualDescendants().OfType<AlderIconPresenter>().Single(x => x.Name == "LeadingIcon");

        CheckSize(14);
        button.Size = AlderSize.Medium;
        CheckSize(16);
        button.Size = AlderSize.Large;
        CheckSize(20);

        button.IsPending = true;
        Assert.False(slot.IsVisible);
        button.IsPending = false;
        button.Icon = "M0 0H16V16H0Z";
        window.UpdateLayout();
        Assert.Null(glyph.GetVisualParent());
        var vector = slot.GetVisualDescendants().OfType<PathIcon>().Single();
        Assert.Equal(new Rect(0, 0, 16, 16), vector.Data!.Bounds);

        button.Icon = null;
        window.UpdateLayout();
        Assert.False(slot.IsVisible);
        window.Close();

        void CheckSize(double side)
        {
            window.UpdateLayout();
            Assert.Equal(new Size(side, side), slot.Bounds.Size);
            Assert.Contains(glyph, slot.GetVisualDescendants());
            var topLeft = glyph.TranslatePoint(default, slot)!.Value;
            var bottomRight = glyph.TranslatePoint(new Point(glyph.Bounds.Width, glyph.Bounds.Height), slot)!.Value;
            Assert.Equal(side, bottomRight.X - topLeft.X, 4);
            Assert.Equal(side / 2, bottomRight.Y - topLeft.Y, 4);
            Assert.Equal(40, glyph.Width);
            Assert.Equal(20, glyph.Height);
        }
    }

    [AvaloniaFact]
    public void ToggleButtonUsesNativeSelectionAndDisabledBehavior()
    {
        var button = new AlderToggleButton { Content = "Like", Variant = ToggleButtonVariant.Ghost };
        var disabled = new AlderToggleButton { Content = "Saved", IsChecked = true, IsEnabled = false };
        var window = Show(new StackPanel { Children = { button, disabled } });
        window.RequestedThemeVariant = ThemeVariant.Light;
        var peer = Assert.IsAssignableFrom<IToggleProvider>(ControlAutomationPeer.CreatePeerForElement(button));
        int changes = 0;
        button.IsCheckedChanged += (_, _) => changes++;
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
        Assert.Equal(ToggleState.Off, peer.ToggleState);

        Assert.True(button.Focus());
        Press(window, PhysicalKey.Space);
        Assert.True(button.IsChecked);
        Assert.Equal(ToggleState.On, peer.ToggleState);
        Assert.Equal(Color.Parse("#260485F7"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);

        window.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Equal(Color.Parse("#1F0485F7"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);
        Press(window, PhysicalKey.Enter);
        Assert.False(button.IsChecked);
        Assert.Equal(2, changes);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color);

        var disabledCenter = disabled.TranslatePoint(new Rect(disabled.Bounds.Size).Center, window)!.Value;
        window.MouseDown(disabledCenter, MouseButton.Left);
        window.MouseUp(disabledCenter, MouseButton.Left);
        Assert.True(disabled.IsChecked);
        Assert.False(disabled.Focus());
        Assert.Equal(.5, disabled.Opacity);
        window.Close();
    }

    [AvaloniaFact]
    public void ToggleButtonSizesIconAndRespectsReducedMotion()
    {
        var button = new AlderToggleButton
        {
            IsIconOnly = true,
            Size = AlderSize.Small,
            Icon = Geometry.Parse("M0 0H16V16H0Z"),
        };
        var window = Show(new StackPanel { Children = { button } });
        var icon = button.GetVisualDescendants().OfType<AlderIconPresenter>().Single(x => x.Name == "LeadingIcon");
        var face = button.GetVisualDescendants().OfType<ButtonFeedback>().Single();
        var visual = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(face)!;
        Assert.Equal(new Size(32, 32), button.Bounds.Size);
        Assert.Same(button.Icon, icon.GetVisualDescendants().OfType<PathIcon>().Single().Data);
        Assert.Equal(new Rect(button.Bounds.Size).Center, icon.TranslatePoint(new Rect(icon.Bounds.Size).Center, button));
        var fillTransition = Assert.IsType<global::Avalonia.Animation.BrushTransition>(Assert.Single(face.Transitions!));
        Assert.Equal(TimeSpan.FromMilliseconds(100), fillTransition.Duration);

        var center = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        Assert.Equal(.98, face.PressScale);
        AlderMotion.SetReduced(window, true);
        Assert.Null(face.Transitions);
        Assert.Equal(1, visual.Scale.X);
        window.MouseUp(center, MouseButton.Left);
        Assert.True(button.IsChecked);

        button.Size = AlderSize.Large;
        window.UpdateLayout();
        Assert.Equal(new Size(40, 40), button.Bounds.Size);
        button.FullWidth = true;
        window.UpdateLayout();
        Assert.Equal(window.ClientSize.Width, button.Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void CheckBoxSupportsReadOnlyAutomationAndMixedSelection()
    {
        var check = new AlderCheckBox { Content = "Select all", IsChecked = null, IsRequired = true };
        var window = Show(new StackPanel { Children = { check } });
        var peer = ControlAutomationPeer.CreatePeerForElement(check);
        var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
        Assert.Equal(AutomationControlType.CheckBox, peer.GetAutomationControlType());
        Assert.True(AutomationProperties.GetIsRequiredForForm(check));
        Assert.Equal(ToggleState.Indeterminate, toggle.ToggleState);
        int clicks = 0;
        check.Click += (_, _) => clicks++;
        Assert.True(check.Focus());
        Press(window, PhysicalKey.Space);
        Assert.Equal(ToggleState.On, toggle.ToggleState);

        check.IsReadOnly = true;
        Assert.True(check.IsEffectivelyEnabled);
        Press(window, PhysicalKey.Space);
        toggle.Toggle();
        Assert.True(check.IsChecked);
        Assert.Equal(1, clicks);

        check.IsReadOnly = false;
        check.IsThreeState = true;
        Press(window, PhysicalKey.Space);
        Assert.Null(check.IsChecked);
        Press(window, PhysicalKey.Space);
        Assert.False(check.IsChecked);
        window.Close();
    }

    [AvaloniaFact]
    public void CheckBoxShowsValidationAndKeepsDescriptionOutsideTheHitArea()
    {
        var check = new AlderCheckBox
        {
            Content = "Accept",
            Description = "Review the terms first.",
            ErrorMessage = "Selection is required.",
            Variant = CheckBoxVariant.Secondary,
            IndicatorSize = 20,
            IndicatorCornerRadius = new CornerRadius(10),
        };
        var window = Show(new StackPanel { Children = { check } });
        window.RequestedThemeVariant = ThemeVariant.Light;
        AlderMotion.SetReduced(window, true);
        var indicator = check.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "Indicator");
        var fill = check.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "SelectionFill");
        var row = check.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "Row");
        var description = check.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "Description");
        var error = check.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "FieldError");
        Assert.True(description.IsVisible);
        Assert.False(error.IsVisible);
        Assert.Equal(new Size(20, 20), indicator.Bounds.Size);
        Assert.Equal(new CornerRadius(10), fill.CornerRadius);
        Assert.Equal(Color.Parse("#EBEBEC"), Assert.IsAssignableFrom<ISolidColorBrush>(indicator.Background).Color);
        Assert.Null(fill.Transitions);

        var descriptionCenter = description.TranslatePoint(new Rect(description.Bounds.Size).Center, window)!.Value;
        window.MouseDown(descriptionCenter, MouseButton.Left);
        window.MouseUp(descriptionCenter, MouseButton.Left);
        Assert.False(check.IsChecked);

        check.IsInvalid = true;
        Assert.True(error.IsVisible);
        Assert.Equal(new Thickness(1), indicator.BorderThickness);
        var rowCenter = row.TranslatePoint(new Rect(row.Bounds.Size).Center, window)!.Value;
        window.MouseDown(rowCenter, MouseButton.Left);
        window.MouseUp(rowCenter, MouseButton.Left);
        Assert.True(check.IsChecked);
        Assert.Equal(1, fill.Opacity);
        Assert.Equal(Color.Parse("#FF383C"), Assert.IsAssignableFrom<ISolidColorBrush>(fill.Background).Color);
        check.IsInvalid = false;
        Assert.False(error.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void CheckBoxStrokeSnapsWithReducedMotionAndSupportsCustomIcons()
    {
        var check = new AlderCheckBox { Content = "Accept" };
        var window = Show(new StackPanel { Children = { check } });
        var defaultMark = check.GetVisualDescendants().OfType<Viewbox>().Single(x => x.Name == "DefaultCheckMark");
        var stroke = defaultMark.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().Single();
        var customMark = check.GetVisualDescendants().OfType<PathIcon>().Single(x => x.Name == "CheckMark");

        Assert.True(defaultMark.IsVisible);
        Assert.False(customMark.IsVisible);
        Assert.Single(stroke.Transitions!);
        var hiddenOffset = stroke.StrokeDashOffset;
        Assert.True(hiddenOffset > 0);

        AlderMotion.SetReduced(window, true);
        Assert.Null(stroke.Transitions);
        check.IsChecked = true;
        Assert.Equal(0, stroke.StrokeDashOffset);
        check.IsChecked = false;
        Assert.Equal(hiddenOffset, stroke.StrokeDashOffset);

        check.CheckedIcon = Geometry.Parse("M0,0 H10 V10 H0 Z");
        check.IsChecked = true;
        Assert.False(defaultMark.IsVisible);
        Assert.True(customMark.IsVisible);
        Assert.Equal(1, customMark.Opacity);
        Assert.Same(check.CheckedIcon, customMark.Data);
        check.CheckedIcon = null;
        Assert.True(defaultMark.IsVisible);
        Assert.False(customMark.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void GroupEdgesFollowOrientationVisibilityAndRemoval()
    {
        var first = new AlderButton();
        var last = new AlderButton();
        var group = new AlderButtonGroup { Children = { first, last } };
        var w = Show(group);
        Assert.Contains("alder-leading", first.Classes);
        Assert.DoesNotContain("alder-divider-x", first.Classes);
        Assert.Contains("alder-divider-x", last.Classes);
        group.ShowSeparators = false;
        Assert.DoesNotContain("alder-divider-x", last.Classes);
        AlderButtonGroup.SetShowSeparator(last, true);
        Assert.Contains("alder-divider-x", last.Classes);
        group.Orientation = Orientation.Vertical;
        Assert.Contains("alder-bottom", last.Classes);
        Assert.DoesNotContain("alder-divider-x", last.Classes);
        Assert.Contains("alder-divider-y", last.Classes);
        first.IsVisible = false;
        Assert.DoesNotContain("alder-bottom", last.Classes);
        Assert.DoesNotContain("alder-divider-y", last.Classes);
        w.UpdateLayout();
        var face = last.GetVisualDescendants().OfType<ButtonFeedback>().Single();
        var center = last.TranslatePoint(new Rect(last.Bounds.Size).Center, w)!.Value;
        w.MouseDown(center, MouseButton.Left);
        Assert.Equal(1, face.PressScale);
        w.MouseUp(center, MouseButton.Left);

        group.Joined = false;
        w.MouseDown(center, MouseButton.Left);
        Assert.Equal(.97, face.PressScale);
        w.MouseUp(center, MouseButton.Left);
        group.Children.Remove(last);
        Assert.DoesNotContain("alder-trailing", last.Classes);
        w.Close();
    }

    [AvaloniaFact]
    public void FullWidthGroupDividesAvailableSpaceBetweenVisibleButtons()
    {
        var first = new AlderButton { Content = "A longer label" };
        var middle = new AlderButton { Content = "Short" };
        var icon = new AlderButton { IsIconOnly = true };
        var group = new AlderButtonGroup
        {
            FullWidth = true,
            Width = 300,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { first, middle, icon },
        };
        var window = Show(group);
        Assert.Equal(new Rect(0, 0, 100, 36), first.Bounds);
        Assert.Equal(new Rect(100, 0, 100, 36), middle.Bounds);
        Assert.Equal(new Rect(200, 0, 100, 36), icon.Bounds);

        group.Width = 400;
        window.UpdateLayout();
        Assert.Equal(first.Bounds.Right, middle.Bounds.Left);
        Assert.Equal(middle.Bounds.Right, icon.Bounds.Left);
        Assert.Equal(group.Bounds.Width, icon.Bounds.Right);

        group.FlowDirection = FlowDirection.RightToLeft;
        window.UpdateLayout();
        Assert.True(first.TranslatePoint(new Point(), window)!.Value.X >
                    icon.TranslatePoint(new Point(), window)!.Value.X);
        group.FlowDirection = FlowDirection.LeftToRight;

        group.Width = 300;
        middle.IsVisible = false;
        window.UpdateLayout();
        Assert.Equal(new Rect(150, 0, 150, 36), icon.Bounds);

        group.Orientation = Orientation.Vertical;
        window.UpdateLayout();
        Assert.Equal(new Rect(0, 36, 300, 36), icon.Bounds);

        group.FullWidth = false;
        window.UpdateLayout();
        Assert.Equal(36, icon.Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public void GroupDisabledDefaultAllowsLocalOverrideAndRestoresRemovedButtons()
    {
        var normal = new AlderButton();
        var enabled = new AlderButton { IsEnabled = true };
        var disabled = new AlderButton { IsEnabled = false };
        var nested = new AlderButton();
        var group = new AlderButtonGroup
        {
            IsDisabled = true,
            Children = { normal, enabled, disabled, new Border { Child = nested } },
        };
        var window = Show(group);
        int clicks = 0;
        normal.Click += (_, _) => clicks++;
        enabled.Click += (_, _) => clicks++;
        Assert.Equal(AutomationControlType.Group, ControlAutomationPeer.CreatePeerForElement(group).GetAutomationControlType());
        Assert.False(normal.IsEffectivelyEnabled);
        Assert.True(nested.IsEffectivelyEnabled);
        Assert.False(normal.Focus());
        Assert.True(enabled.Focus());
        Press(window, PhysicalKey.Space);
        Assert.Equal(1, clicks);

        group.IsDisabled = false;
        Assert.True(normal.IsEffectivelyEnabled);
        Assert.False(disabled.IsEffectivelyEnabled);
        group.IsDisabled = true;
        group.Children.Clear();
        Assert.True(normal.IsEnabled);
        Assert.True(enabled.IsEnabled);
        Assert.False(disabled.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void RadioKeysReplaceOnceAndStayInsideTheirGroup()
    {
        static RadioButton Option(string key)
        {
            var radio = new RadioButton { Content = key };
            AlderChoiceGroup.SetKey(radio, key);
            return radio;
        }
        var a = Option("a"); var b = Option("b"); var c = Option("c");
        var left = new AlderRadioGroup { Children = { a, b }, SelectedKeys = ["a"] };
        var right = new AlderRadioGroup { Children = { c }, SelectedKeys = ["c"] };
        var w = Show(new StackPanel { Children = { left, right } });
        int changes = 0;
        left.PropertyChanged += (_, e) => { if (e.Property == AlderChoiceGroup.SelectedKeysProperty) changes++; };
        a.Focus();
        Press(w, PhysicalKey.ArrowDown);
        Assert.Equal(["b"], left.SelectedKeys);
        Assert.Equal(1, changes);
        Assert.True(b.IsFocused);
        Assert.True(c.IsChecked);
        w.Close();
    }

    [AvaloniaFact]
    public void ReadOnlyRadioGroupMovesFocusWithoutChangingSelection()
    {
        Assert.True(Application.Current!.TryFindResource("AlderRadioButtonTheme", out var theme));
        var initial = new AlderRadioButton { Content = "First" };
        var disabled = new AlderRadioButton { Content = "Disabled", IsEnabled = false };
        var other = new AlderRadioButton { Content = "Last", Theme = Assert.IsType<ControlTheme>(theme) };
        AlderChoiceGroup.SetKey(initial, "first");
        AlderChoiceGroup.SetKey(other, "last");
        other.Description = "Accessible information";
        var group = new AlderRadioGroup
        {
            IsReadOnly = true, IsRequired = true, Variant = ButtonVariant.Secondary,
            Children = { initial, disabled, other }, SelectedKeys = ["first"]
        };
        var window = Show(new StackPanel { Margin = new Thickness(8), Children = { group } });
        group.Focus();
        Assert.True(initial.IsFocused);
        Assert.True(other.IsSecondary);
        Assert.Equal("Accessible information", AutomationProperties.GetHelpText(other));
        AlderMotion.SetReduced(group, true);
        var dot = other.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "DefaultDot");
        Assert.Equal(1, dot.RenderTransform!.Value.M11);
        Press(window, PhysicalKey.ArrowDown);
        Assert.True(other.IsFocused);
        Press(window, PhysicalKey.Space);
        var point = other.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.Equal(["first"], group.SelectedKeys);
        Assert.True(initial.IsChecked);
        Assert.False(other.IsChecked);

        other.IsChecked = true;
        Assert.Equal(["first"], group.SelectedKeys);
        Assert.True(initial.IsChecked);
        Assert.False(other.IsChecked);

        group.SelectedKeys = ["last"];
        Assert.True(other.IsChecked);
        Assert.Equal(0.4286, dot.RenderTransform!.Value.M11);
        group.SelectedKeys = [];
        Assert.True(group.IsInvalid);
        Assert.True(AlderLabel.GetIsInvalid(group));
        window.Close();
    }

    [AvaloniaFact]
    public void RadioGroupWrapsWithoutChangingSelectionOrder()
    {
        var first = new AlderRadioButton { Content = "First", Width = 90 };
        var second = new AlderRadioButton { Content = "Second", Width = 90 };
        var third = new AlderRadioButton { Content = "Third", Width = 90 };
        AlderChoiceGroup.SetKey(first, "first");
        AlderChoiceGroup.SetKey(second, "second");
        AlderChoiceGroup.SetKey(third, "third");
        second.Description = "Description";
        var group = new AlderRadioGroup
        {
            Width = 210, Orientation = Orientation.Horizontal,
            Children = { first, second, third }, SelectedKeys = ["first"]
        };
        var window = Show(group);

        Assert.Equal(first.Bounds.Y, second.Bounds.Y);
        Assert.True(third.Bounds.Y > first.Bounds.Y);
        var description = second.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "Description");
        var point = description.TranslatePoint(new Point(8, 8), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.Equal(["first"], group.SelectedKeys);

        second.Focus();
        Press(window, PhysicalKey.ArrowRight);
        Assert.True(third.IsFocused);
        Assert.Equal(["third"], group.SelectedKeys);
        window.Close();
    }

    [AvaloniaFact]
    public void RadioTilePresentsTrailingIndicatorFooterAndFieldError()
    {
        var option = new AlderRadioButton { Content = "Express", Classes = { "tile", "trailing" } };
        option.Footer = "$16.00";
        option.FieldError = "Choose a different option";
        var group = new AlderRadioGroup { Children = { option }, SelectedKeys = ["express"] };
        var window = Show(group);
        AlderChoiceGroup.SetKey(option, "express");
        Assert.True(option.IsChecked);
        var indicator = option.GetVisualDescendants().OfType<Grid>().Single(x => x.Name == "IndicatorHost");
        var presenter = option.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
        var footer = option.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "Footer");
        var error = option.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "FieldError");
        var ring = option.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "InvalidRing");

        Assert.Equal(new Size(20, 20), indicator.Bounds.Size);
        Assert.True(indicator.Bounds.X > presenter.Bounds.X);
        Assert.True(footer.IsVisible);
        Assert.Equal("$16.00", footer.Content);
        Assert.True(error.IsVisible);
        Assert.True(ring.IsVisible);
        Assert.Equal(error.Text, AutomationProperties.GetHelpText(option));
        option.FieldError = null;
        Assert.False(error.IsVisible);
        Assert.False(ring.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void RemovingCheckedOptionsUpdatesRequiredValidation()
    {
        var check = new CheckBox();
        AlderChoiceGroup.SetKey(check, "mail");
        var group = new AlderCheckboxGroup { IsRequired = true, Children = { check }, SelectedKeys = ["mail"] };
        Assert.True(check.IsChecked);
        group.Children.Remove(check);
        Assert.Empty(group.SelectedKeys);
        Assert.True(group.IsInvalid);
    }

    [AvaloniaFact]
    public void ToolbarSkipsDisabledButtonsAndLeavesTextCaretKeysAlone()
    {
        var a = new AlderButton(); var b = new AlderButton(); var edit = new AlderInput { Text = "abc" };
        var toolbar = new AlderToolbar { Children = { a, new AlderButton { IsEnabled = false }, b, edit } };
        var w = Show(toolbar);
        a.Focus();
        Press(w, PhysicalKey.ArrowRight);
        Assert.True(b.IsFocused);
        edit.Focus(); edit.CaretIndex = 1;
        Press(w, PhysicalKey.ArrowRight);
        Assert.True(edit.IsFocused);
        Assert.Equal(2, edit.CaretIndex);
        Assert.Equal(AutomationControlType.ToolBar, ControlAutomationPeer.CreatePeerForElement(toolbar)!.GetAutomationControlType());
        w.Close();
    }

    [AvaloniaFact]
    public void RangeHasTwoKeyboardStopsAndRespondsToPointerAndBounds()
    {
        var range = new AlderRangeSlider { LowerValue = 20, UpperValue = 70, Step = 5 };
        var w = Show(range);
        var handles = range.GetVisualDescendants().OfType<Slider>().ToArray();
        Assert.Equal(2, handles.Length);
        handles[0].Focus(); Press(w, PhysicalKey.ArrowRight);
        Assert.Equal(25, range.LowerValue);
        Assert.Equal(70, range.UpperValue);
        var point = range.TranslatePoint(new Point(390, range.Bounds.Height / 2), w)!.Value;
        w.MouseDown(point, MouseButton.Left); w.MouseUp(point, MouseButton.Left);
        Assert.True(range.UpperValue > 70);
        range.LowerValue = 200;
        Assert.Equal(range.UpperValue, range.LowerValue);
        range.Maximum = 30;
        Assert.Equal(30, range.UpperValue);
        Assert.Equal(30, range.LowerValue);
        range.Orientation = Orientation.Vertical;
        handles[0].Focus(); Press(w, PhysicalKey.ArrowDown);
        Assert.Equal(25, range.LowerValue);
        w.Close();
    }

    [AvaloniaFact]
    public void GroupDefaultsRespectLocalSizeAndAreRemovedWithTheChild()
    {
        var child = new AlderButton { Size = AlderSize.Large };
        var toggle = new AlderToggleButton();
        var group = new AlderButtonGroup { Size = AlderSize.Small, Variant = ButtonVariant.Danger, Children = { child, toggle } };
        Assert.Equal(AlderSize.Large, child.Size);
        Assert.Equal(ButtonVariant.Danger, child.Variant);
        Assert.Equal(AlderSize.Small, toggle.Size);
        group.Children.Remove(child);
        group.Children.Remove(toggle);
        Assert.Equal(ButtonVariant.Primary, child.Variant);
        Assert.Equal(AlderSize.Large, child.Size);
        Assert.Equal(AlderSize.Medium, toggle.Size);
    }

    [AvaloniaFact]
    public void SwitchTogglesWithSpaceAndInheritsReducedMotion()
    {
        var app = Application.Current!;
        Assert.True(app.TryFindResource("AlderSwitchTheme", out var theme));
        var toggle = new AlderSwitch { Theme = (ControlTheme)theme! };
        var panel = new StackPanel { Children = { toggle } };
        var window = Show(panel);
        toggle.Focus();
        Press(window, PhysicalKey.Space);
        Assert.True(toggle.IsChecked);
        AlderMotion.SetReduced(panel, true);
        Assert.True(AlderMotion.GetReduced(toggle));
        Assert.Null(toggle.KnobTransitions);
        window.Close();
    }

    [AvaloniaFact]
    public void SwitchHighlightsItsTrackOnPointerAndKeyboardFocus()
    {
        Assert.True(Application.Current!.TryFindResource("AlderSwitchTheme", out var theme));
        var toggle = new AlderSwitch { Theme = (ControlTheme)theme!, IsChecked = true };
        var selected = new SolidColorBrush(Color.Parse("#CC00C950"));
        var hovered = new SolidColorBrush(Color.Parse("#CC2B7FFF"));
        toggle.Size = AlderSize.Large;
        toggle.CheckedTrackBrush = selected;
        toggle.CheckedHoverBrush = hovered;
        AlderMotion.SetReduced(toggle, true);
        var panel = new StackPanel { Margin = new Thickness(20), Children = { toggle } };
        var window = Show(panel);
        var parts = toggle.GetVisualDescendants().OfType<Border>().ToArray();
        var track = parts.Single(part => part.Name == "Track");
        var outline = parts.Single(part => part.Name == "TrackOutline");
        var knob = parts.Single(part => part.Name == "Knob");

        Assert.Same(selected, track.Background);
        Assert.True(track.ClipToBounds);
        Assert.Equal(new CornerRadius(12), track.CornerRadius);
        Assert.Equal(new Size(27.5, 20), knob.Bounds.Size);
        Assert.Equal(new CornerRadius(10), knob.CornerRadius);
        Assert.Equal(default, track.BorderThickness);
        Assert.Equal(0, outline.BoxShadow.Count);

        window.MouseMove(track.TranslatePoint(new Rect(track.Bounds.Size).Center, window)!.Value);
        Assert.Same(hovered, track.Background);
        window.MouseMove(new Point(10, 10));
        Assert.Same(selected, track.Background);
        Assert.True(toggle.Focus(NavigationMethod.Tab));
        Assert.Equal(0, outline.BoxShadow.Count);
        Assert.Same(hovered, track.Background);
        window.Close();
    }

    [AvaloniaFact]
    public void ToolbarIsOneTabStopAndNavigatesInBothOrientations()
    {
        var before = new Button();
        var after = new Button();
        var first = new Button();
        var second = new Button();
        var toolbar = new AlderToolbar { Children = { first, second } };
        var w = Show(new StackPanel { Children = { before, toolbar, after } });
        before.Focus();
        Press(w, PhysicalKey.Tab);
        Assert.True(first.IsFocused);
        Press(w, PhysicalKey.ArrowRight);
        Assert.True(second.IsFocused);
        Press(w, PhysicalKey.Tab);
        Assert.True(after.IsFocused);
        toolbar.Orientation = Orientation.Vertical;
        first.Focus();
        Press(w, PhysicalKey.ArrowDown);
        Assert.True(second.IsFocused);
        w.Close();
    }

    [AvaloniaFact]
    public void ToolbarArrowsCrossFromFormatGroupToCommandGroup()
    {
        var bold = new ToggleButton();
        var italic = new ToggleButton();
        AlderChoiceGroup.SetKey(bold, "bold");
        AlderChoiceGroup.SetKey(italic, "italic");
        var styles = new AlderToggleButtonGroup { Mode = ChoiceMode.Multiple, Children = { bold, italic } };
        var copy = new AlderButton();
        var actions = new AlderButtonGroup { Children = { copy } };
        var toolbar = new AlderToolbar { Children = { styles, new Border { Width = 1 }, actions } };
        var window = Show(toolbar);
        italic.Focus();
        Press(window, PhysicalKey.ArrowRight);
        Assert.True(copy.IsFocused);
        Press(window, PhysicalKey.ArrowLeft);
        Assert.True(italic.IsFocused);
        toolbar.Orientation = Orientation.Vertical;
        styles.Orientation = Orientation.Vertical;
        Press(window, PhysicalKey.ArrowDown);
        Assert.True(copy.IsFocused);
        window.Close();
    }

    [AvaloniaFact]
    public void SliderThemeKeepsNativeThumbAndSharesItsValueWithTheRail()
    {
        Assert.True(Application.Current!.TryFindResource("AlderSliderTheme", out var theme));
        var slider = new AlderSlider { Theme = (ControlTheme)theme!, Value = 40, TickPlacement = TickPlacement.BottomRight };
        var window = Show(slider);
        var rail = slider.GetVisualDescendants().OfType<SliderSurface>().Single();
        var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
        Assert.True(rail.ShowSteps);
        Assert.Equal(rail.Value, slider.Value);
        Assert.Equal(new Size(28, 20), thumb.Bounds.Size);
        slider.Focus();
        Press(window, PhysicalKey.ArrowRight);
        Assert.True(slider.Value > 40);
        Assert.Equal(slider.Value, rail.Value);
        slider.Orientation = Orientation.Vertical;
        window.UpdateLayout();
        Assert.Equal(Orientation.Vertical, rail.Orientation);
        Assert.Equal(new Size(20, 28), thumb.Bounds.Size);
        window.Close();
    }

    [AvaloniaFact]
    public void SliderMarksFollowLiveItemsAndIgnoreOutOfRangeLabels()
    {
        Assert.True(Application.Current!.TryFindResource("AlderSliderTheme", out var theme));
        var slider = new AlderSlider { Theme = (ControlTheme)theme!, Minimum = 0, Maximum = 100, Value = 40 };
        var quiet = new SliderMark { Value = 20, Label = "Quiet" };
        var marks = new SliderMarkCollection { quiet, new() { Value = -10 }, new() { Value = double.NaN } };
        slider.Marks = marks;
        var window = Show(slider);
        var panel = slider.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "Marks");
        var label = Assert.IsType<AlderTextBlock>(Assert.Single(panel.Children));
        Assert.Contains("filled", label.Classes);

        quiet.Value = 60;
        marks.Add(new SliderMark { Value = 100, Label = "Max" });
        window.UpdateLayout();
        Assert.Equal(2, panel.Children.Count);
        Assert.DoesNotContain("filled", panel.Children[0].Classes);
        slider.Value = 70;
        Assert.Contains("filled", panel.Children[0].Classes);
        Assert.DoesNotContain("filled", panel.Children[1].Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void MultipleToggleChoicesRetainOtherKeysAndUpdateOncePerToggle()
    {
        var first = new ToggleButton(); AlderChoiceGroup.SetKey(first, "one");
        var second = new ToggleButton(); AlderChoiceGroup.SetKey(second, "two");
        var group = new AlderToggleButtonGroup { Mode = ChoiceMode.Multiple, Children = { first, second } };
        first.IsChecked = true;
        second.IsChecked = true;
        Assert.Equal(["one", "two"], group.SelectedKeys);
        first.IsChecked = false;
        Assert.Equal(["two"], group.SelectedKeys);
        group.SelectedKeys = ["one"];
        Assert.True(first.IsChecked);
        Assert.False(second.IsChecked);
    }

    [AvaloniaFact]
    public void TwoCoincidentThumbsCanSeparateInEitherDirection()
    {
        var range = new AlderRangeSlider { LowerValue = 50, UpperValue = 50 };
        var w = Show(range);
        var midpoint = range.TranslatePoint(new Point(range.Bounds.Width / 2, range.Bounds.Height / 2), w)!.Value;
        w.MouseDown(midpoint, MouseButton.Left);
        w.MouseMove(midpoint - new Point(80, 0));
        w.MouseUp(midpoint - new Point(80, 0), MouseButton.Left);
        Assert.True(range.LowerValue < 50);
        Assert.Equal(50, range.UpperValue);
        w.Close();
    }

    [AvaloniaFact]
    public void SecondaryButtonChangesBrushWithTheme()
    {
        var button = new AlderButton { Variant = ButtonVariant.Secondary };
        var w = Show(button);
        w.RequestedThemeVariant = ThemeVariant.Light;
        var light = ((ISolidColorBrush)button.Background!).Color;
        w.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.NotEqual(light, ((ISolidColorBrush)button.Background!).Color);
        w.Close();
    }
}
