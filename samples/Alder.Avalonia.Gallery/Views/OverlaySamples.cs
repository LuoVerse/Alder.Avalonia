using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;

namespace Alder.Avalonia.Gallery.Views;

internal static class OverlaySamples
{
    public static DialogAnimation Motion { get; } = new()
    {
        EnterDuration = TimeSpan.FromMilliseconds(350), ExitDuration = TimeSpan.FromMilliseconds(180),
        EnterOffset = new Vector(0, 30), ExitOffset = new Vector(0, 14),
        EnterScale = .88, ExitScale = .95
    };

    public static IBrush Gradient() => new LinearGradientBrush
    {
        StartPoint = RelativePoint.TopLeft, EndPoint = RelativePoint.BottomRight,
        GradientStops = { new GradientStop(Color.Parse("#A9262757"), 0), new GradientStop(Color.Parse("#A75A3267"), 1) }
    };

    public static StackPanel Activity(GalleryLocalization text, int count = 28, double rowMargin = 6)
    {
        var panel = new StackPanel { Spacing = 12 };
        for (int i = 1; i <= count; i++)
            panel.Children.Add(new AlderTextBlock
            {
                Text = text.Render(new GalleryMessage("SheetActivityItem", i)),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, rowMargin)
            });
        return panel;
    }

    public static StackPanel SnapDetails(GalleryLocalization text)
    {
        var panel = Activity(text, count: 14, rowMargin: 2);
        panel.Children.Insert(0, new AlderTextBlock { Text = text.Text("SheetDragDescription"), TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    public static StackPanel NestedSheet(GalleryLocalization text, ICommand openDetails) => new()
    {
        Spacing = 12,
        Children =
        {
            new AlderTextBlock { Text = text.Text("SheetDescription"), TextWrapping = TextWrapping.Wrap },
            new AlderButton
            {
                Content = text.Text("SheetOpenDetails"), Variant = ButtonVariant.Secondary, Command = openDetails
            }
        }
    };

    public static StackPanel Navigation(GalleryLocalization text)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var (label, iconKey) in new[] { ("TabOverview", "AlderIconEye"), ("TabActivity", "AlderIconClock"), ("TabSettings", "AlderIconGear") })
        {
            var item = CloseButton(text.Text(label));
            item.Variant = ButtonVariant.Ghost;
            item.FullWidth = true;
            item.HorizontalAlignment = HorizontalAlignment.Stretch;
            item.HorizontalContentAlignment = HorizontalAlignment.Left;
            item.Icon = Application.Current?.FindResource(iconKey) as Geometry;
            panel.Children.Add(item);
        }
        return panel;
    }

    public static WrapPanel ConfirmationFooter(string cancelText, string confirmText)
    {
        var cancel = CloseButton(cancelText, DialogResult.Cancel);
        var confirm = CloseButton(confirmText, DialogResult.OK);
        cancel.Variant = confirm.Variant = ButtonVariant.Secondary;
        return new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right, ItemSpacing = 8, LineSpacing = 8,
            Children = { cancel, confirm }
        };
    }

    public static StackPanel ProfessionPicker(GalleryLocalization text, Action<string> select)
    {
        var keys = new[] { "OverlayProfessionDesigner", "OverlayProfessionDeveloper", "OverlayProfessionWriter",
            "OverlayProfessionAnalyst", "OverlayProfessionManager", "OverlayProfessionResearcher", "OverlayProfessionEngineer" };
        var search = new AlderSearchField { PlaceholderText = text.Text("OverlaySearchProfessions"), FullWidth = true };
        AutomationProperties.SetName(search, text.Text("OverlaySearchProfessions"));
        var items = new StackPanel { Spacing = 4 };

        void Filter()
        {
            items.Children.Clear();
            string query = search.Text?.Trim() ?? "";
            foreach (var key in keys)
            {
                string caption = text.Text(key);
                if (text.Culture.CompareInfo.IndexOf(caption, query, CompareOptions.IgnoreCase) < 0)
                    continue;
                var choice = CloseButton(caption);
                choice.FullWidth = true;
                choice.Variant = ButtonVariant.Ghost;
                choice.HorizontalContentAlignment = HorizontalAlignment.Left;
                choice.Click += (_, _) => select(key);
                items.Children.Add(choice);
            }
            if (items.Children.Count == 0)
                items.Children.Add(new AlderTextBlock { Text = text.Text("OverlayNoProfessions"), TextWrapping = TextWrapping.Wrap });
        }

        search.TextChanged += (_, _) => Filter();
        Filter();
        return new StackPanel { Spacing = 10, Children = { search, items } };
    }

    public static AlderButton CloseButton(string caption, DialogResult result = DialogResult.OK)
    {
        var button = new AlderButton { Content = caption, HorizontalAlignment = HorizontalAlignment.Right };
        button.Click += (_, _) => Dialog.Close(button, result);
        return button;
    }
}
