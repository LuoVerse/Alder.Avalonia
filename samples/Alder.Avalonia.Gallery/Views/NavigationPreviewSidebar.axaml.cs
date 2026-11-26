using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.Views;

public partial class NavigationPreviewSidebar : UserControl
{
    public NavigationPreviewSidebar()
    {
        InitializeComponent();
        Navigation.AddHandler(AlderSidebarItem.InvokedEvent, OnItemInvoked);
    }

    private async void OnNewChat(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NavigationPreview model) await ShowActionAsync(this, model, "new-chat");
    }

    private async void OnItemInvoked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not AlderSidebarItem { DataContext: NavigationPreviewItem { Action: { } action } } item ||
            DataContext is not NavigationPreview model) return;
        AlderSidebar.SetCloseMobileOnAction(item, false);
        if (action.StartsWith("page", StringComparison.Ordinal) && int.TryParse(action.AsSpan(4), out int index))
        {
            model.OpenPage(index);
        }
        else if (action == "search")
        {
            await SearchAsync(this, model);
        }
        else
        {
            await ShowActionAsync(this, model, action);
        }
    }

    private static async Task SearchAsync(Control owner, NavigationPreview model)
    {
        var localization = GalleryLocalization.Current;
        var converter = new IconResourceConverter();
        var command = new AlderCommand
        {
            FlowDirection = owner.FlowDirection,
            Placeholder = localization.Text("NavDemoSearchNavigation"),
            Groups = [new AlderCommandGroup
            {
                Items = model.SearchItems().Select(item => new AlderCommandItem
                {
                    Label = item.Title,
                    Icon = converter.Convert(item.Icon, typeof(Geometry), null, localization.Culture) as Geometry,
                    Command = ReactiveCommand.Create(() => model.OpenItem(item))
                }).ToArray()
            }]
        };
        await command.ShowAsync(owner);
    }

    internal static async Task ShowActionAsync(Control owner, NavigationPreview model, string action)
    {
        var localization = GalleryLocalization.Current;
        if (action == "logout")
        {
            await Dialog.ShowModalAsync(localization.Text("NavDemoSignOutQuestion"), new DialogOptions
            {
                Title = localization.Text("NavDemoSignOut"),
                Buttons = DialogButtons.OKCancel,
                IsDestructive = true,
                PrimaryText = localization.Text("NavDemoSignOut")
            }, owner: owner);
            return;
        }

        string key = action switch
        {
            "new-agent" => "NavDemoNewAgent",
            "new-chat" => "NavDemoNewChat",
            "invite" => "NavDemoInviteTeammates",
            _ => "NavDemoAddWorkspace"
        };
        var name = new AlderInput
        {
            PlaceholderText = localization.Text(action == "invite" ? "NavDemoEmailAddress" : "NavDemoName")
        };
        var result = await Dialog.ShowModalAsync(name, new DialogOptions
        {
            Title = localization.Text(key),
            Buttons = DialogButtons.OKCancel,
            Size = DialogSize.Small,
            PrimaryText = localization.Text(action == "invite" ? "NavDemoInvite" : "NavDemoCreate")
        }, owner: owner);
        if (result != DialogResult.OK || name.Text?.Trim() is not { Length: > 0 } value) return;
        if (action == "invite")
        {
            await Dialog.ShowModalAsync(localization.Text("NavDemoInvitePreview"), new DialogOptions
            {
                Title = localization.Text(key), Buttons = DialogButtons.OK, Size = DialogSize.Small
            }, owner: owner);
            return;
        }
        model.AddItem(value, action switch
        {
            "new-agent" => "NavDemoAgents",
            "new-chat" => "NavDemoRecentChats",
            _ => "NavDemoWorkspaces"
        });
    }
}
