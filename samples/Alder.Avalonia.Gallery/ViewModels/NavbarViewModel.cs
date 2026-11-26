using System.Windows.Input;
using Alder.Avalonia.Controls;
using Avalonia.Styling;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NavbarViewModel : GalleryViewModel
{
    private string themeKey = "system";
    private bool isMenuOpen;
    private string? workspace;
    private bool hasSearched;
    private string? docQuery;
    private string? destination;
    private int workspaceIndex;
    private int projectIndex;

    public NavbarViewModel()
    {
        ChangeWorkspace = ReactiveCommand.Create<string>(key =>
        {
            workspace = key;
            this.RaisePropertyChanged(nameof(Workspace));
            this.RaisePropertyChanged(nameof(WorkspaceMark));
        });
        SearchDocs = ReactiveCommand.Create<string>(query =>
        {
            DocQuery = query;
            hasSearched = true;
            this.RaisePropertyChanged(nameof(SearchStatus));
        });
        Navigate = ReactiveCommand.Create<string>(route =>
        {
            destination = route;
            this.RaisePropertyChanged(nameof(NavigationStatus));
            this.RaisePropertyChanged(nameof(HasNavigationStatus));
        });
        ShowMenuActionCommand = ReactiveCommand.Create<string>(message => Toast.Show(new ToastOptions(message)));
    }

    public ICommand ChangeWorkspace { get; }
    public ICommand SearchDocs { get; }
    public ICommand Navigate { get; }
    public ICommand ShowMenuActionCommand { get; }
    public string Workspace => Text(workspace ?? "NavbarStudio");
    public string WorkspaceMark => workspace switch { "NavbarPersonal" => "PS", "NavbarResearch" => "RS", _ => "AL" };
    public KbdKey SearchModifier => OperatingSystem.IsMacOS() ? KbdKey.Command : KbdKey.Ctrl;
    public string NavigationStatus => destination is null ? "" : Format("NavbarNavigationStatus", Text(destination));
    public bool HasNavigationStatus => destination is not null;
    public IReadOnlyList<NavbarOption> Workspaces =>
        [new(Text("NavbarStudio"), "AL"), new(Text("NavbarPersonal"), "PS"), new(Text("NavbarResearch"), "RS")];
    public IReadOnlyList<string> Projects =>
        [Text("NavbarContentHub"), Text("NavbarMarketingSite"), Text("NavbarApiGateway"), Text("NavbarDesignTokens")];
    public IReadOnlyList<NavbarOption> Timezones =>
        [new(Text("NavbarUTC"), "UTC+00:00"), new(Text("NavbarPST"), "UTC−08:00"), new(Text("NavbarEST"), "UTC−05:00"),
         new(Text("NavbarCET"), "UTC+01:00"), new(Text("NavbarJST"), "UTC+09:00")];
    public int WorkspaceIndex { get => workspaceIndex; set => this.RaiseAndSetIfChanged(ref workspaceIndex, value); }
    public int ProjectIndex { get => projectIndex; set => this.RaiseAndSetIfChanged(ref projectIndex, value); }

    public bool IsMenuOpen { get => isMenuOpen; set => this.RaiseAndSetIfChanged(ref isMenuOpen, value); }
    public string? DocQuery { get => docQuery; set => this.RaiseAndSetIfChanged(ref docQuery, value); }
    public string SearchStatus => hasSearched && !string.IsNullOrWhiteSpace(DocQuery) ? Format("NavbarSearchStatus", DocQuery) : "";

    public string ThemeKey
    {
        get => themeKey;
        set
        {
            if (themeKey == value) return;
            this.RaiseAndSetIfChanged(ref themeKey, value);
            this.RaisePropertyChanged(nameof(DocsTheme));
        }
    }

    public ThemeVariant DocsTheme => ThemeKey switch
    {
        "light" => ThemeVariant.Light,
        "dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
}


public sealed record NavbarOption(string Name, string Detail)
{
    public override string ToString() => Name;
}
