using System.Collections.ObjectModel;
using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NavigationBindingViewModel : ReactiveObject
{
    private readonly Func<string, string, string> translate;
    private string? noticeKey;
    private string? noticeFallback;

    public NavigationBindingViewModel() : this((_, fallback) => fallback) { }

    public NavigationBindingViewModel(Func<string, string, string> translate)
    {
        this.translate = translate;
        Home = new(() => translate("NavDemoHome", "Home"),
            Geometry.Parse("M1,7 L8,1 L15,7 H13 V14 H9 V10 H7 V14 H3 V7 Z"),
            () => translate("NavigationBindingHomeDescription", "Choose a project from the navigation to get started."));
        Projects = new(() => translate("NavDemoProjects", "Projects"),
            Geometry.Parse("F0 M1,3 H6 L8,5 H15 V14 H1 Z M3,6 V12 H13 V7 H7 L5,5 H3 Z"));
        Overview = new(() => translate("NavDemoOverview", "Overview"),
            description: () => translate("NavigationBindingOverviewDescription", "Review the status of your projects."));
        Settings = new(() => translate("NavDemoSettings", "Settings"),
            description: () => translate("NavigationBindingSettingsDescription", "Manage project preferences."));
        Projects.Children.Add(Overview);
        Projects.Children.Add(Settings);
        Projects.IsExpanded = true;
        Navigation = [Home, Projects];
        SelectedPage = Home;

        OpenProfileCommand = ReactiveCommand.Create(() => ShowNotice("NavDemoProfile", "Profile"));
        OpenSettingsCommand = ReactiveCommand.Create(() =>
        {
            Projects.IsExpanded = true;
            SelectedPage = Settings;
            ShowNotice("NavDemoSettings", "Settings");
        });
        LogOutCommand = ReactiveCommand.Create(() => ShowNotice("NavigationBindingSignedOut", "Signed out (demo)"));
        AccountActions =
        [
            new(() => ProfileLabel, OpenProfileCommand),
            new(() => SettingsLabel, OpenSettingsCommand)
        ];
    }

    public string WorkspaceTitle => translate("NavDemoWorkspace", "Workspace");
    public Geometry WorkspaceIcon { get; } = Geometry.Parse("M2,2 H7 V7 H2 Z M9,2 H14 V7 H9 Z M2,9 H7 V14 H2 Z M9,9 H14 V14 H9 Z");
    public string ProfileLabel => translate("NavDemoProfile", "Profile");
    public string SettingsLabel => translate("NavDemoSettings", "Settings");
    public string AccountName => "Avery Chen";
    public string AccountEmail => "avery@example.com";
    public string AccountInitials => "AC";
    public string Notice => noticeKey is null ? "" : translate(noticeKey, noticeFallback!);

    public ObservableCollection<NavigationBindingPage> Navigation { get; }
    public ObservableCollection<NavigationBindingAction> AccountActions { get; }
    public NavigationBindingPage Home { get; }
    public NavigationBindingPage Projects { get; }
    public NavigationBindingPage Overview { get; }
    public NavigationBindingPage Settings { get; }
    public NavigationBindingPage? SelectedPage
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
    public bool IsSidebarOpen { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
    public bool IsSearchEnabled { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
    public AlderUserThemeMode ThemeMode
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(PreviewTheme));
        }
    }
    public ThemeVariant PreviewTheme => ThemeMode switch
    {
        AlderUserThemeMode.Light => ThemeVariant.Light,
        AlderUserThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
    public ICommand OpenProfileCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand LogOutCommand { get; }

    public void RefreshLanguage()
    {
        this.RaisePropertyChanged(string.Empty);
        foreach (var page in new[] { Home, Projects, Overview, Settings }) page.RefreshText();
        foreach (var action in AccountActions) action.RefreshTitle();
    }

    private void ShowNotice(string key, string fallback)
    {
        noticeKey = key;
        noticeFallback = fallback;
        this.RaisePropertyChanged(nameof(Notice));
    }
}

public sealed class NavigationBindingPage(Func<string> title, Geometry? icon = null, Func<string>? description = null)
    : ReactiveObject
{
    public string Title => title();
    public string? Description => description?.Invoke();
    public Geometry? Icon { get; } = icon;
    public ObservableCollection<NavigationBindingPage> Children { get; } = [];
    public bool IsExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    internal void RefreshText()
    {
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(Description));
    }
}

public sealed class NavigationBindingAction(Func<string> title, ICommand command) : ReactiveObject
{
    public string Title => title();
    public ICommand Command { get; } = command;
    internal void RefreshTitle() => this.RaisePropertyChanged(nameof(Title));
}
