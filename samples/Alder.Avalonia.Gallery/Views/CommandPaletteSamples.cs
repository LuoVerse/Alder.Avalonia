using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

internal static class CommandPaletteSamples
{
    internal static AlderCommand Create(Control trigger, CommandViewModel model)
    {
        var demo = trigger.Tag is CommandDemo mode ? mode : CommandDemo.Usage;
        var size = trigger.Tag is AlderSize selectedSize ? selectedSize : AlderSize.Medium;
        var palette = new AlderCommand { Size = size, FlowDirection = trigger.FlowDirection };
        switch (demo)
        {
            case CommandDemo.Launcher:
                palette.Size = AlderSize.Large;
                palette.Groups = model.LauncherGroups;
                palette.Placeholder = Text("CommandFindApps");
                palette.ShowDismissButton = false;
                palette.Footer = CreateLauncherFooter(palette, model);
                break;
            case CommandDemo.Toolbar:
                palette.Groups = model.ToolbarGroups;
                palette.Placeholder = Text("CommandWhatDoYouNeed");
                break;
            case CommandDemo.Clean:
                palette.Size = AlderSize.Large;
                palette.Groups = model.CleanGroups;
                palette.Header = CreateRootLabel("CommandAllActions");
                palette.Placeholder = Text("CommandWhatDoYouNeed");
                palette.ShowDismissButton = false;
                break;
            case CommandDemo.Minimal:
                palette.Size = AlderSize.Large;
                palette.Groups = model.MinimalGroups;
                palette.Header = CreateRootLabel("CommandIssuePreview");
                palette.ShowDismissButton = false;
                break;
            case CommandDemo.Split:
                palette.Size = AlderSize.Large;
                palette.Groups = model.SplitGroups;
                palette.Placeholder = Text("CommandFindComponents");
                palette.PreviewTemplate = new FuncDataTemplate<AlderCommandItem>(
                    (item, _) => item is null ? null : CreateComponentPreview(item, trigger));
                palette.ShowDismissButton = false;
                break;
            case CommandDemo.SearchTerms:
                palette.Size = AlderSize.Large;
                palette.Placeholder = Text("CommandSearchOrJump");
                palette.ShowDismissButton = false;
                ConfigureSearchTerms(palette, model, trigger);
                break;
            default:
                palette.Groups = model.UsageGroups;
                palette.Header = CreateRootLabel("CommandAllActions");
                if (trigger.Tag is not DialogBackdrop) palette.Footer = CreateKeyboardHints(trigger);
                break;
        }
        return palette;
    }

    private static Control CreateKeyboardHints(Control trigger) => new AlderContentControl
    {
        Content = "",
        ContentTemplate = trigger.FindResource("CommandKeyboardHintsTemplate") as IDataTemplate
    };

    private static Border CreateRootLabel(string key)
    {
        var label = new AlderTextBlock { FontSize = 12, LineHeight = 16, Text = Text(key) };
        var badge = new Border
        {
            Child = label, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        badge[!Border.BackgroundProperty] = new DynamicResourceExtension("AlderSurfaceSecondary");
        return badge;
    }

    private static Control CreateLauncherFooter(AlderCommand palette, CommandViewModel model)
    {
        var launchGroups = palette.Groups;
        string? previousQuery = null;
        var headline = new AlderTextBlock { Text = Text("CommandOpenActive"), VerticalAlignment = VerticalAlignment.Center };
        var actions = new AlderButton { Content = Text("CommandActions"), Variant = ButtonVariant.Ghost, Size = AlderSize.Small };
        actions.Click += (_, _) =>
        {
            bool showingApps = ReferenceEquals(palette.Groups, launchGroups);
            if (showingApps)
            {
                previousQuery = palette.Query;
                palette.Groups = model.LauncherActions(palette.ActiveItem);
                palette.Query = null;
            }
            else
            {
                palette.Groups = launchGroups;
                palette.Query = previousQuery;
            }
            headline.Text = Text(showingApps ? "CommandActionMenu" : "CommandOpenActive");
            actions.Content = Text(showingApps ? "CommandBackToApps" : "CommandActions");
            FocusSearch(palette);
        };
        DockPanel.SetDock(actions, Dock.Right);
        var enter = new AlderKbd { FontSize = 12, MinHeight = 20, Padding = new Thickness(6, 0) };
        enter.Items.Add(new AlderKbdKey { KeyValue = KbdKey.Enter });
        return new DockPanel { Children = { actions, new WrapPanel { ItemSpacing = 6, Children = { headline, enter } } } };
    }

    private static void ConfigureSearchTerms(AlderCommand palette, CommandViewModel model, Control trigger)
    {
        var categories = CommandViewModel.SearchCategories.ToHashSet();
        var tags = new WrapPanel { ItemSpacing = 6, LineSpacing = 4 };
        var showAll = new AlderButton { Content = Text("CommandShowAll"), Variant = ButtonVariant.Ghost, Size = AlderSize.Small };
        showAll.Click += (_, _) =>
        {
            categories.UnionWith(CommandViewModel.SearchCategories);
            palette.Query = null;
            RefreshFilters();
        };
        DockPanel.SetDock(showAll, Dock.Right);
        palette.SearchFooter = tags;
        palette.Footer = new DockPanel { Children = { showAll, CreateKeyboardHints(trigger) } };
        RefreshFilters(focus: false);

        void RefreshFilters(bool focus = true)
        {
            palette.Groups = model.SearchGroups(categories);
            tags.Children.Clear();
            foreach (string key in CommandViewModel.SearchCategories.Where(categories.Contains))
            {
                var label = Text(key);
                var remove = new AlderButton
                {
                    Theme = trigger.FindResource("AlderSearchClearButtonTheme") as ControlTheme,
                    Width = 20, Height = 20, MinHeight = 20, VerticalAlignment = VerticalAlignment.Center,
                    Content = new AlderPathIcon
                    {
                        Data = trigger.FindResource("AlderIconXmark") as Geometry,
                        Width = 8, Height = 8, IsHitTestVisible = false
                    }
                };
                AutomationProperties.SetName(remove, string.Format(Text("CommandRemoveFilter"), label));
                remove.Click += (_, _) =>
                {
                    categories.Remove(key);
                    RefreshFilters();
                };
                tags.Children.Add(new AlderChip
                {
                    Size = AlderSize.Small, Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 4,
                        Children = { new AlderTextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, remove }
                    }
                });
            }
            if (focus) FocusSearch(palette);
        }
    }

    private static Control CreateComponentPreview(AlderCommandItem item, Control trigger)
    {
        Control control = item.CommandParameter switch
        {
            "Input" => new AlderInput { MaxWidth = 144, PlaceholderText = Text("CommandPreviewInputText") },
            "CommandRadio" => new AlderRadioButton
            {
                Content = Text("CommandRadioOption"), IsChecked = true,
                Theme = trigger.FindResource("AlderRadioButtonTheme") as ControlTheme
            },
            "Chip" => new AlderChip { Content = Text("CommandPreviewChip") },
            "Slider" => new AlderSlider
            {
                Width = 136, Maximum = 100, Value = 60,
                Theme = trigger.FindResource("AlderSliderTheme") as ControlTheme
            },
            "Avatar" => new AlderAvatar { Fallback = "AL" },
            "Switch" => new AlderSwitch { IsChecked = true, Theme = trigger.FindResource("AlderSwitchTheme") as ControlTheme },
            _ => new AlderButton { Content = Text("Primary") }
        };
        return new StackPanel
        {
            Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new AlderTextBlock { Text = item.Label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                control
            }
        };
    }

    private static void FocusSearch(AlderCommand palette) =>
        palette.GetVisualDescendants().OfType<AlderSearchField>().FirstOrDefault()?.Focus();

    private static string Text(string key) => GalleryLocalization.Current.Text(key);
}
