using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Styling;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class NavigationPreviewItem(
    string titleKey, string? icon, string? badge, bool expanded, string? action,
    IReadOnlyList<NavigationPreviewItem> children) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public string? Icon { get; } = icon;
    public string? Badge => badge is null ? null : Text(badge);
    public NavigationPreviewItem? BadgeContent => badge is null ? null : this;
    public bool HasNewBadge => badge == "NavDemoBadgeNew";
    public string? Action { get; } = action;
    public bool SelectOnInvoke => Action is null;
    public ObservableCollection<NavigationPreviewItem> Children { get; } = new(children);
    public bool IsExpanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = expanded;
}

public sealed class NavigationPreviewGroup(string? titleKey, IReadOnlyList<NavigationPreviewItem> items) : GalleryViewModel
{
    public string? Key => titleKey;
    public string? Title => titleKey is null ? null : Text(titleKey);
    public ObservableCollection<NavigationPreviewItem> Items { get; } = new(items);
}

public sealed class NavigationPreview : GalleryViewModel
{
    public IReadOnlyList<NavigationPreviewGroup> Header { get; init; } = [];
    public IReadOnlyList<NavigationPreviewGroup> Groups { get; init; } = [];
    public IReadOnlyList<NavigationPreviewGroup> Footer { get; init; } = [];
    public IReadOnlyList<NavigationPreviewGroup> Pages { get; init; } = [];
    public string BrandKey { get; init; } = "NavDemoAlder";
    public string? BrandHintKey { get; init; }
    public string BrandMark { get; init; } = "A";
    public string? AccountKey { get; init; }
    public string AccountMark { get; init; } = "JG";
    public string? TitleKey { get; init; }
    public string? InitialTitleKey { get; init; }
    public string SummaryKey { get; init; } = "NavDemoMain";
    public bool Compact { get; init; }
    public bool ShowRail { get; init; } = true;
    public bool IconOnly { get; init; }
    public bool BrandIsAvatar { get; init; }
    public bool ShowNavbarThemeSwitcher { get; init; }
    public bool IsMeetingNotes { get; init; }
    public bool IsSearchEnabled { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool ShowNavbarActions { get; init; }
    public bool AccountInNavbarOnly { get; init; }
    public bool ShowNewChat { get; init; }
    public bool HasPages => Pages.Count > 0;
    public bool HasSidebarAccount => AccountKey is not null && !AccountInNavbarOnly;
    public bool HasBrand => BrandKey.Length > 0;
    public bool HasPinnedNavigation => Header.Count > 0;
    public NavigationPreview? HeaderContent => HasBrand || HasPinnedNavigation ? this : null;
    public NavigationPreview? HeaderIconContent => HasBrand && !HasPinnedNavigation ? this : null;
    public NavigationPreview? FooterContent => Footer.Count > 0 || HasSidebarAccount || ShowNewChat ? this : null;
    public string Brand => Text(BrandKey);
    public string? BrandHint => BrandHintKey is null ? null : Text(BrandHintKey);
    public string? Account => AccountKey is null ? null : Text(AccountKey);
    public string AccountDisplayName => AccountKey is null or "NavDemoJunior" ? Text("NavDemoJuniorGarcia") : Text(AccountKey);
    public bool AccountUsesGradient => AccountMark == "JG";
    public double AccountMenuWidth => AccountInNavbarOnly ? 240 : 220;
    public string AccountEmail => AccountMark switch
    {
        "SP" => "sarah@example.com",
        "NK" => "nora@example.com",
        "AC" => "alex@example.com",
        _ => "junior@example.com"
    };
    public string Title => TitleKey is { } title ? Text(title)
        : IsInitialSelection && InitialTitleKey is { } initialTitle ? Text(initialTitle)
        : SelectedItem?.Title ?? Text("NavDemoDashboard");
    public bool IsInitialSelection => ReferenceEquals(SelectedItem, initialSelection);
    public string Summary => Text(SummaryKey);
    public string? SelectedIcon => SelectedItem?.Icon;
    public bool IsNarrow { get => field; set { this.RaiseAndSetIfChanged(ref field, value); this.RaisePropertyChanged(nameof(PreviewWidth)); } }
    public double PreviewWidth => IsNarrow ? 390 : 1080;
    private string? widthFile;
    private double sidebarWidth = 240;
    private double asideWidth = 320;
    public double SidebarWidth
    {
        get => sidebarWidth;
        set
        {
            if (sidebarWidth == value) return;
            this.RaiseAndSetIfChanged(ref sidebarWidth, value);
            SaveWidths();
        }
    }
    public double AsideWidth
    {
        get => asideWidth;
        set
        {
            if (asideWidth == value) return;
            this.RaiseAndSetIfChanged(ref asideWidth, value);
            SaveWidths();
        }
    }
    public object? ToolbarKey { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = "list";
    public object? ThemeKey
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Theme));
        }
    } = "system";
    public ThemeVariant Theme => ThemeKey switch
    {
        "light" => ThemeVariant.Light,
        "dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
    public bool ConversationClosed { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool ConversationSnoozed { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    public void ToggleConversation() => ConversationClosed = !ConversationClosed;
    public void ToggleSnooze() => ConversationSnoozed = !ConversationSnoozed;

    public void ResetConversation()
    {
        ConversationClosed = false;
        ConversationSnoozed = false;
    }

    internal NavigationPreview RestoreWidths(string scenario)
    {
        string dataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (dataRoot.Length == 0) return this;
        widthFile = Path.Combine(dataRoot, "Alder.Avalonia.Gallery", scenario + ".widths");
        try
        {
            if (File.Exists(widthFile) && File.ReadAllLines(widthFile) is [var navigation, var details])
            {
                sidebarWidth = ReadWidth(navigation, sidebarWidth);
                asideWidth = ReadWidth(details, asideWidth);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return this;
    }

    private static double ReadWidth(string value, double fallback) =>
        double.TryParse(value, CultureInfo.InvariantCulture, out double width) && double.IsFinite(width) && width > 0
            ? width : fallback;

    private void SaveWidths()
    {
        if (widthFile is not { } path) return;
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            try
            {
                File.WriteAllLines(temporaryPath,
                    [sidebarWidth.ToString(CultureInfo.InvariantCulture), asideWidth.ToString(CultureInfo.InvariantCulture)]);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally { File.Delete(temporaryPath); }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private NavigationPreviewItem? initialSelection;

    internal void SetInitialSelection(NavigationPreviewItem? item)
    {
        initialSelection = item;
        SelectedItem = item;
    }

    public NavigationPreviewItem? SelectedItem
    {
        get => field;
        set
        {
            if (ReferenceEquals(value, field)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(IsInitialSelection));
            this.RaisePropertyChanged(nameof(SelectedIcon));
        }
    }

    public int PageIndex { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }

    private readonly Dictionary<int, NavigationPreviewItem?> pageSelections = [];

    public void OpenPage(int index)
    {
        pageSelections[PageIndex] = SelectedItem;
        PageIndex = index;
        SelectedItem = pageSelections.GetValueOrDefault(index) ??
            (index > 0 ? Pages[index].Items.FirstOrDefault(item => item.SelectOnInvoke) : null);
    }

    public IEnumerable<NavigationPreviewItem> SearchItems() =>
        Header.Concat(Groups).Concat(Footer).SelectMany(group => Descendants(group.Items))
            .Where(item => item.SelectOnInvoke && item.Children.Count == 0);

    public void OpenItem(NavigationPreviewItem selection)
    {
        foreach (var group in Header.Concat(Groups).Concat(Footer))
            if (Reveal(group.Items, selection)) break;
        SelectedItem = selection;
    }

    private static bool Reveal(IEnumerable<NavigationPreviewItem> items, NavigationPreviewItem selection)
    {
        foreach (var item in items)
        {
            if (item == selection) return true;
            if (!Reveal(item.Children, selection)) continue;
            item.IsExpanded = true;
            return true;
        }
        return false;
    }

    public void AddItem(string name, string group)
    {
        var target = Groups.FirstOrDefault(section => section.Key == group) ?? Groups.FirstOrDefault();
        if (target is null) return;
        var item = new NavigationPreviewItem(name, "NavPreviewIconChat", null, false, null, []);
        target.Items.Add(item);
        SelectedItem = item;
    }

    private static IEnumerable<NavigationPreviewItem> Descendants(IEnumerable<NavigationPreviewItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Descendants(item.Children))
                yield return child;
        }
    }
}
