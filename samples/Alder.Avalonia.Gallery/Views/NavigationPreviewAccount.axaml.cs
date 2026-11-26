using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Localization;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.Views;

public partial class NavigationPreviewAccount : UserControl
{
    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<NavigationPreviewAccount, bool>(nameof(ShowLabel));
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }

    public static readonly StyledProperty<bool> ShowThemeSwitcherProperty =
        AvaloniaProperty.Register<NavigationPreviewAccount, bool>(nameof(ShowThemeSwitcher));
    public bool ShowThemeSwitcher { get => GetValue(ShowThemeSwitcherProperty); set => SetValue(ShowThemeSwitcherProperty, value); }

    private NavigationPreview? preparedModel;
    private bool inSidebar;

    public NavigationPreviewAccount()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (this.IsAttachedToVisualTree()) PrepareMenu();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        bool sidebar = this.FindAncestorOfType<AlderSidebar>() is not null;
        if (inSidebar != sidebar) preparedModel = null;
        inSidebar = sidebar;
        AccountTrigger.ShowChevron = sidebar;
        AccountTrigger.LogOutIsDestructive = sidebar;
        AccountTrigger.MinHeight = sidebar ? 36 : 32;
        AccountTrigger.MenuPlacement = sidebar ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedRight;
        PrepareMenu();
    }

    private void PrepareMenu()
    {
        var model = DataContext as NavigationPreview;
        if (ReferenceEquals(preparedModel, model)) return;
        preparedModel = model;
        AccountTrigger.Items.Clear();
        if (model is null)
        {
            AccountTrigger.LogOutCommand = null;
            return;
        }
        AccountTrigger[!AlderUserMenu.LogOutTextProperty] = new DynamicResourceExtension(
            inSidebar ? "GalleryText.NavDemoAccountLogOut" : "GalleryText.NavDemoSignOut");
        AccountTrigger.LogOutCommand = ReactiveCommand.CreateFromTask(() => ShowAction(model, "logout"));

        if (model.AccountInNavbarOnly)
        {
            Add("NavDemoMyAccount", "AlderIconAvatarPerson", () => SelectPage("NavDemoMyAccount", model));
            AddSeparator();
            Add("NavDemoPrivacyPolicy", "AlderIconInfo", () => SelectPage("NavDemoPrivacyPolicy", model));
            Add("NavDemoShareFeedback", "NavPreviewIconChat", () => SelectPage("NavDemoShareFeedback", model));
            AddSeparator();
        }
        else if (model.IsMeetingNotes)
        {
            Add("NavDemoInviteTeammates", "NavPreviewIconPeople", () => ShowAction(model, "invite"));
            Add("NavDemoAddWorkspace", "AlderIconPlus", () => ShowAction(model, "workspace"));
            Add("NavDemoManageTemplates", "NavPreviewIconBook", () => SelectPage("NavDemoManageTemplates", model));
            Add("NavDemoHelpCenter", "AlderIconInfo", () => SelectPage("NavDemoHelpCenter", model));
            Add("NavDemoSettings", "AlderIconGear", () => SelectPage("NavDemoSettings", model));
        }
        else
        {
            if (!inSidebar || !model.ShowNewChat)
                Add("NavDemoDashboard", "NavPreviewIconHome", () => SelectPage("NavDemoDashboard", model));
            Add("NavDemoProfile", null, () => SelectPage("NavDemoProfile", model));
            Add("NavDemoSettings", "AlderIconGear", () => SelectPage("NavDemoSettings", model));
            if (inSidebar)
                Add("NavDemoCreateTeam", "NavPreviewIconPeople", () => ShowAction(model, "workspace"));
        }
    }

    private void Add(string title, string? icon, Func<Task> action)
    {
        var item = new AlderMenuItem();
        item[!MenuItem.HeaderProperty] = new DynamicResourceExtension("GalleryText." + title);
        if (icon is not null)
        {
            var glyph = new AlderPathIcon { Width = 14, Height = 14 };
            glyph[!PathIcon.DataProperty] = new DynamicResourceExtension(icon);
            item.Icon = glyph;
        }
        item.Classes.Set("leading-icon", preparedModel?.AccountInNavbarOnly == true);
        item.Command = ReactiveCommand.CreateFromTask(action);
        AccountTrigger.Items.Add(item);
    }

    private void AddSeparator() => AccountTrigger.Items.Add(new AlderMenuItem { Header = "-", Focusable = false });

    private Task ShowAction(NavigationPreview model, string action) =>
        NavigationPreviewSidebar.ShowActionAsync(this, model, action);

    private Task SelectPage(string key, NavigationPreview model)
    {
        string title = GalleryLocalization.Current.Text(key);
        if (model.SearchItems().FirstOrDefault(item => item.Title == title) is { } entry)
        {
            model.SelectedItem = entry;
            return Task.CompletedTask;
        }
        return Dialog.ShowModalAsync(model.AccountDisplayName,
            new DialogOptions { Title = title, Buttons = DialogButtons.OK }, owner: this);
    }
}
