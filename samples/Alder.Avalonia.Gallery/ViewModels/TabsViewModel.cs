using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class TabExampleItem(string titleKey, string bodyKey, string? headingKey = null, Uri? documentation = null) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public string Body => Text(bodyKey);
    public string? Heading => headingKey is null ? null : Text(headingKey);
    public Uri? Documentation => documentation;
}

public sealed class TabsViewModel : GalleryViewModel
{
    public int SelectedIndex
    {
        get => field;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IReadOnlyList<TabExampleItem> Sections { get; } =
    [
        new("TabOverview", "TabOverviewBody"),
        new("TabAnalytics", "TabAnalyticsBody"),
        new("TabReports", "TabReportsBody")
    ];

    public IReadOnlyList<TabExampleItem> OverflowSections { get; } =
    [
        new("TabOverview", "TabOverviewBody"),
        new("TabAnalytics", "TabAnalyticsBody"),
        new("TabReports", "TabReportsBody"),
        new("TabPerformance", "TabAnalyticsBody"),
        new("TabEngagement", "TabActivityBody"),
        new("TabAudience", "TabTeamBody"),
        new("TabAcquisition", "TabAnalyticsBody"),
        new("TabRetention", "TabReportsBody"),
        new("TabSettings", "TabSettingsBody")
    ];

    public IReadOnlyList<TabExampleItem> AccountSections { get; } =
    [
        new("TabAccount", "TabAccountBody", "TabAccountSettings"),
        new("AccordionSecurity", "TabSecurityBody", "AccordionSecurity"),
        new("AccordionAlerts", "TabAlertsBody", "AccordionAlerts"),
        new("DisclosureBilling", "TabBillingBody", "DisclosureBilling")
    ];

    public IReadOnlyList<TabExampleItem> Preferences { get; } =
    [
        new("TabGeneral", "TabAccountBody", "TabGeneral"),
        new("TabSubscription", "TabBillingBody", "TabSubscription"),
        new("AccordionAppearance", "TabAppearanceBody", "AccordionAppearance"),
        new("AccordionAlerts", "TabAlertsBody", "AccordionAlerts"),
        new("TabPrivacy", "TabPrivacyBody", "TabPrivacy")
    ];

    public IReadOnlyList<TabExampleItem> Documentation { get; } =
    [
        new("TabGettingStarted", "TabGettingStartedBody", documentation: new("https://docs.avaloniaui.net/docs/get-started/")),
        new("Components", "TabComponentsBody", documentation: new("https://docs.avaloniaui.net/docs/reference/controls/")),
        new("TabReleases", "TabReleasesBody", documentation: new("https://github.com/AvaloniaUI/Avalonia/releases"))
    ];
}
