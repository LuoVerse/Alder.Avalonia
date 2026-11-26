namespace Alder.Avalonia.Gallery.ViewModels;

internal sealed class NavigationPreviewCatalog
{
    private NavigationPreviewItem? initialSelection;

    public static NavigationPreview Create(string scenario) => new NavigationPreviewCatalog().CreatePreview(scenario);

    private NavigationPreview CreatePreview(string scenario) => scenario switch
    {
        nameof(SidebarComplex) => SidebarComplex(),
        nameof(SidebarAccount) => SidebarAccount(),
        nameof(SidebarInset) => SidebarInset(),
        nameof(SidebarFloating) => SidebarFloating(),
        nameof(SidebarCollapsible) => SidebarCollapsible(),
        nameof(SidebarIconOnly) => SidebarIconOnly(),
        nameof(SidebarGroups) => SidebarGroups(),
        nameof(SidebarDocumentation) => SidebarDocumentation(),
        nameof(SidebarAvatar) => SidebarAvatar(),
        nameof(SidebarPages) => SidebarPages(),
        nameof(SidebarMeetingNotes) => SidebarMeetingNotes(),
        nameof(SidebarAgentHub) => SidebarAgentHub(),
        nameof(SidebarAgentWorkspace) => SidebarAgentWorkspace(),
        nameof(SidebarReducedMotion) => SidebarReducedMotion(),
        nameof(LayoutDashboard) => LayoutDashboard(),
        nameof(LayoutDocs) => LayoutDocs(),
        nameof(LayoutScroll) => LayoutScroll(),
        nameof(LayoutAside) => LayoutAside(),
        nameof(LayoutBreadcrumbs) => LayoutBreadcrumbs(),
        nameof(LayoutToolbar) => LayoutToolbar(),
        nameof(LayoutComplex) => LayoutComplex(),
        nameof(LayoutResizableAside) => LayoutResizableAside(),
        "LayoutDefault" => DashboardSidebar("NavDemoLayoutDefaultSummary", hasConversions: true, showLogout: true, navbarActions: true),
        "LayoutOffcanvas" => DashboardSidebar("NavDemoLayoutOffcanvasSummary", navbarActions: true),
        "LayoutInset" => DashboardSidebar("NavDemoLayoutInsetSummary", navbarActions: true),
        "LayoutFloating" => DashboardSidebar("NavDemoLayoutFloatingSummary", navbarActions: true),
        "LayoutCollapsible" => DashboardSidebar("NavDemoLayoutCollapsibleSummary", hasConversions: true, navbarActions: true),
        "LayoutResizableSidebar" => DashboardSidebar("NavDemoLayoutResizableSidebarSummary", showLogout: true, hasSubmenu: false, navbarActions: true),
        "SidebarDefault" => DashboardSidebar("NavDemoSidebarDefaultSummary", hasConversions: true, showLogout: true, navbarActions: false),
        "SidebarRight" => DashboardSidebar("NavDemoSidebarRightSummary", hasConversions: true, showLogout: true, navbarActions: false),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };

    private NavigationPreview SidebarComplex() => Finish(new NavigationPreview
    {
        ShowNewChat = true,
        BrandKey = "NavDemoAcmeLabs",
        BrandMark = "A",
        SummaryKey = "NavDemoSidebarComplexSummary",
        Compact = true,
        Header =
        [
            Group(null,
            [
                Item("NavDemoHome", "NavPreviewIconHome"),
                Item("NavDemoMeetings", "AlderIconCalendar"),
                Item("NavDemoInbox", "NavPreviewIconInbox")
            ]),
        ],
        Groups =
        [
            Group("NavDemoRecents",
            [
                Item("NavDemoUserSettings", "AlderIconGear"),
                Item("NavDemoOnboardingFlow", "NavPreviewIconChat"),
                Item("NavDemoAPIGateway", "NavPreviewIconChat"),
                Item("NavDemoThemeBuilder", "AlderIconGear"),
                Item("NavDemoNavigation", "NavPreviewIconChat")
            ]),
            Group("NavDemoFavorites",
            [
                Item("NavDemoTutorials", "NavPreviewIconBook"),
                Item("NavDemoMyTasks", "NavPreviewIconChecklist")
            ]),
            Group("NavDemoAgents",
            [
                Item("NavDemoPersonal", "NavPreviewIconFolder"),
                Item("NavDemoAddNew", "NavPreviewIconBasket", action: "workspace")
            ]),
            Group("NavDemoTeamspaces",
            [
                Item("NavDemoAcmeHQ", "NavPreviewIconPeople", expanded: true, children:
                [
                    Item("NavDemoHome", "NavPreviewIconHome"),
                    Item("NavDemoMyTasks", "NavPreviewIconChecklist"),
                    Item("NavDemoProjects", "NavPreviewIconFolder"),
                    Item("NavDemoEpics", "NavPreviewIconChat"),
                    Item("NavDemoRoadmap", "NavPreviewIconChat", selected: true),
                    Item("NavDemoSprintBoard", "NavPreviewIconChecklist"),
                    Item("NavDemoEngBoard", "NavPreviewIconChat"),
                    Item("NavDemoDesignBoard", "NavPreviewIconChat"),
                    Item("NavDemoSprints", "NavPreviewIconChecklist"),
                    Item("NavDemoInitiatives", "NavPreviewIconChat"),
                    Item("NavDemoVault", "AlderIconArchive"),
                    Item("NavDemoArchive", "AlderIconArchive"),
                    Item("NavDemoWiki", "NavPreviewIconBook"),
                    Item("NavDemoBrainstorm", "NavPreviewIconChat"),
                    Item("NavDemoStandup", "AlderIconCalendar"),
                    Item("NavDemoLaunchV3", "NavPreviewIconChat")
                ]),
                Item("NavDemoEngineering", "NavPreviewIconChat"),
                Item("NavDemoMetrics", "AlderIconChart"),
                Item("NavDemoTracker", "NavPreviewIconChecklist"),
                Item("NavDemoReports", "AlderIconChart")
            ]),
            Group("NavDemoShared",
            [
                Item("NavDemoSprints", "NavPreviewIconChecklist")
            ]),
            Group("NavDemoApps",
            [
                Item("NavDemoAcmeMail", "AlderIconEnvelope"),
                Item("NavDemoAcmeCalendar", "AlderIconCalendar")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoLibrary", "AlderIconArchive"),
                Item("NavDemoMyTasks", "NavPreviewIconChecklist"),
                Item("NavDemoMarketplace", "NavPreviewIconBasket"),
                Item("NavDemoHelp", "AlderIconInfo"),
                Item("NavDemoTrash", "AlderIconTrash")
            ]),
        ],
    });

    private NavigationPreview SidebarAccount() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoAlderLabs",
        BrandMark = "AL",
        AccountKey = "NavDemoJunior",
        AccountMark = "JG",
        SummaryKey = "NavDemoSidebarAccountSummary",
        Compact = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoSearch", "AlderIconMagnifier", action: "search", badge: "NavDemoBadgeK"),
                Item("NavDemoHome", "NavPreviewIconHome", selected: true),
                Item("NavDemoActivity", "NavPreviewIconChecklist"),
                Item("NavDemoBrowse", "AlderIconGlobe")
            ]),
            Group("NavDemoSpaces",
            [
                Item("NavDemoMyLibrary", "AlderIconArchive", expanded: true, children:
                [
                    Item("NavDemoDesignTokens", "NavPreviewIconBook"),
                    Item("NavDemoColorPalette", "NavPreviewIconBook"),
                    Item("NavDemoTypography", "NavPreviewIconBook")
                ]),
                Item("NavDemoDesignTeam", "NavPreviewIconPeople"),
                Item("NavDemoAddSpace", "AlderIconPlus", action: "workspace")
            ]),
        ],
    });

    private NavigationPreview SidebarInset() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoAlderInc",
        BrandHintKey = "NavDemoEnterprise",
        BrandMark = "A",
        TitleKey = "NavDemoSidebarInsetTitle",
        SummaryKey = "NavDemoSidebarInsetSummary",
        Groups =
        [
            Group("NavDemoPlatform",
            [
                Item("NavDemoPlayground", "NavPreviewIconHome"),
                Item("NavDemoModels", "NavPreviewIconChat"),
                Item("NavDemoDocumentation", "NavPreviewIconBook"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
            Group("NavDemoProjects",
            [
                Item("NavDemoDesignEngineering", "NavPreviewIconFolder"),
                Item("NavDemoSalesMarketing", "NavPreviewIconFolder"),
                Item("NavDemoTravel", "NavPreviewIconFolder")
            ]),
        ],
    });

    private NavigationPreview SidebarFloating() => Finish(new NavigationPreview
    {
        SummaryKey = "NavDemoSidebarFloatingSummary",
        ShowRail = false,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoDashboard", "NavPreviewIconHome"),
                Item("NavDemoOrders", "NavPreviewIconBasket"),
                Item("NavDemoTracker", "NavPreviewIconChecklist"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoTeam", "NavPreviewIconPeople"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
        ],
    });

    private NavigationPreview SidebarCollapsible() => Finish(new NavigationPreview
    {
        SummaryKey = "NavDemoSidebarCollapsibleSummary",
        Groups =
        [
            Group("NavDemoNavigation",
            [
                Item("NavDemoDashboard", "NavPreviewIconHome", selected: true),
                Item("NavDemoOrders", "NavPreviewIconBasket"),
                Item("NavDemoTracker", "NavPreviewIconChecklist", badge: "NavDemoBadgeNew"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoTeam", "NavPreviewIconPeople"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
        ],
    });

    private NavigationPreview SidebarIconOnly() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoKateMoore",
        BrandMark = "KM",
        BrandIsAvatar = true,
        SummaryKey = "NavDemoSidebarIconOnlySummary",
        ShowRail = false,
        IconOnly = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoDashboard", "NavPreviewIconHome"),
                Item("NavDemoOrders", "NavPreviewIconBasket"),
                Item("NavDemoTracker", "NavPreviewIconChecklist"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoTeam", "NavPreviewIconPeople"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoHelpSupport", "AlderIconInfo")
            ]),
        ],
    });

    private NavigationPreview SidebarGroups() => Finish(new NavigationPreview
    {
        SummaryKey = "NavDemoSidebarGroupsSummary",
        ShowRail = false,
        Groups =
        [
            Group("NavDemoPlatform",
            [
                Item("NavDemoDashboard", "NavPreviewIconHome"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoOrders", "NavPreviewIconBasket")
            ]),
            Group("NavDemoSettings",
            [
                Item("NavDemoGeneral", "AlderIconGear"),
                Item("NavDemoTeam", "NavPreviewIconPeople"),
                Item("NavDemoNotifications", "AlderIconBell")
            ]),
        ],
    });

    private NavigationPreview SidebarDocumentation() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoDocumentation",
        BrandHintKey = "NavDemoV100",
        BrandMark = "D",
        SummaryKey = "NavDemoSidebarDocumentationSummary",
        Groups =
        [
            Group(null,
            [
                Item("NavDemoGettingStarted", null, children:
                [
                    Item("NavDemoInstallation"),
                    Item("NavDemoProjectStructure")
                ]),
                Item("NavDemoBuildYourApplication", null, expanded: true, children:
                [
                    Item("NavDemoRouting"),
                    Item("NavDemoDataFetching", selected: true),
                    Item("NavDemoRendering"),
                    Item("NavDemoCaching"),
                    Item("NavDemoStyling"),
                    Item("NavDemoTesting")
                ]),
                Item("NavDemoAPIReference", null, children:
                [
                    Item("NavDemoComponents"),
                    Item("NavDemoFunctions"),
                    Item("NavDemoHooks"),
                    Item("NavDemoConfig")
                ])
            ]),
        ],
    });

    private NavigationPreview SidebarAvatar() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoKateMoore",
        BrandHintKey = "NavDemoKateAcmeCom",
        BrandMark = "KM",
        BrandIsAvatar = true,
        SummaryKey = "NavDemoSidebarAvatarSummary",
        Groups =
        [
            Group(null,
            [
                Item("NavDemoDashboard", "NavPreviewIconHome"),
                Item("NavDemoOrders", "NavPreviewIconBasket"),
                Item("NavDemoTracker", "NavPreviewIconChecklist", badge: "NavDemoBadgeNew"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoTeam", "NavPreviewIconPeople", selected: true),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoHelpInformation", "AlderIconInfo"),
                Item("NavDemoLogOut", "AlderIconPower", action: "logout")
            ]),
        ],
    });

    private NavigationPreview SidebarPages() => Finish(new NavigationPreview
    {
        SummaryKey = "NavDemoSidebarPagesSummary",
        Pages =
        [
            Group(null,
            [
                Item("NavDemoOverview", "NavPreviewIconHome", action: "page1"),
                Item("NavDemoAnalytics", "AlderIconChart", action: "page2"),
                Item("NavDemoSettings", "AlderIconGear", action: "page3")
            ]),
            Group(null,
            [
                Item("NavDemoBack", "AlderIconArrowLeft", action: "page0"),
                Item("NavDemoSummary", "NavPreviewIconChat"),
                Item("NavDemoActivity", "NavPreviewIconChecklist")
            ]),
            Group(null,
            [
                Item("NavDemoBack", "AlderIconArrowLeft", action: "page0"),
                Item("NavDemoReports", "AlderIconChart"),
                Item("NavDemoTraffic", "NavPreviewIconChat")
            ]),
            Group(null,
            [
                Item("NavDemoBack", "AlderIconArrowLeft", action: "page0"),
                Item("NavDemoGeneral", "AlderIconGear"),
                Item("NavDemoTeam", "NavPreviewIconPeople"),
                Item("NavDemoBilling", "NavPreviewIconChat")
            ]),
        ],
    });

    private NavigationPreview SidebarMeetingNotes() => Finish(new NavigationPreview
    {
        BrandKey = "",
        BrandMark = "",
        AccountKey = "NavDemoSarah",
        AccountMark = "SP",
        SummaryKey = "NavDemoSidebarMeetingNotesSummary",
        Compact = true,
        IsMeetingNotes = true,
        IsSearchEnabled = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoHome", "NavPreviewIconHome", selected: true),
                Item("NavDemoSharedWithMe", "NavPreviewIconInbox"),
                Item("NavDemoChat", "NavPreviewIconChat", expanded: true, children:
                [
                    Item("NavDemoCallPrepNotesForUpco", null, badge: "NavDemoBadge4m"),
                    Item("NavDemoQ2MarketingStrategy", null, badge: "NavDemoBadge12m"),
                    Item("NavDemoDesignSystemMigration", null, badge: "NavDemoBadge1h"),
                    Item("NavDemoSprintRetroActionItems", null, badge: "NavDemoBadge3h")
                ])
            ]),
            Group("NavDemoSpaces",
            [
                Item("NavDemoMyNotes", "NavPreviewIconFolder", expanded: true, children:
                [
                    Item("NavDemoPersonal", "NavPreviewIconFolder")
                ]),
                Item("NavDemoDesignTeam", "NavPreviewIconPeople"),
                Item("NavDemoAddFolder", "NavPreviewIconFolder", action: "workspace")
            ]),
        ],
    });

    private NavigationPreview SidebarAgentHub() => Finish(new NavigationPreview
    {
        ShowNewChat = true,
        BrandKey = "NavDemoAgentHub",
        BrandMark = "AH",
        AccountKey = "NavDemoJunior",
        AccountMark = "JG",
        SummaryKey = "NavDemoSidebarAgentHubSummary",
        Compact = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoNewAgent", "AlderIconPlus", action: "new-agent"),
                Item("NavDemoSearch", "AlderIconMagnifier", action: "search", badge: "NavDemoBadgeK"),
                Item("NavDemoHome", "NavPreviewIconHome"),
                Item("NavDemoActivity", "NavPreviewIconChecklist"),
                Item("NavDemoMarketplace", "NavPreviewIconBasket")
            ]),
            Group("NavDemoRecentChats",
            [
                Item("NavDemoRefactorAuthModule", "NavPreviewIconChat"),
                Item("NavDemoDebugPaymentFlow", "NavPreviewIconChat"),
                Item("NavDemoWriteAPIDocs", "NavPreviewIconChat"),
                Item("NavDemoReviewPR482", "NavPreviewIconChat"),
                Item("NavDemoPlanV3Migration", "NavPreviewIconChat")
            ]),
            Group("NavDemoAgents",
            [
                Item("NavDemoCoder", "AlderIconAvatarPerson"),
                Item("NavDemoReviewer", "AlderIconAvatarPerson"),
                Item("NavDemoWriter", "AlderIconAvatarPerson"),
                Item("NavDemoAddAgent", "AlderIconPlus", action: "new-agent")
            ]),
            Group("NavDemoWorkspaces",
            [
                Item("NavDemoPersonal", "NavPreviewIconFolder", expanded: true, children:
                [
                    Item("NavDemoBackendAPI", "NavPreviewIconChat", selected: true),
                    Item("NavDemoMobileApp", "NavPreviewIconChat"),
                    Item("NavDemoTracker", "NavPreviewIconChecklist")
                ]),
                Item("NavDemoTeam", "NavPreviewIconPeople")
            ]),
            Group("NavDemoApps",
            [
                Item("NavDemoDocs", "NavPreviewIconBook"),
                Item("NavDemoCalendar", "AlderIconCalendar")
            ]),
        ],
    });

    private NavigationPreview SidebarAgentWorkspace() => Finish(new NavigationPreview
    {
        BrandKey = "",
        BrandMark = "",
        AccountKey = "NavDemoAlexChen",
        AccountMark = "AC",
        TitleKey = "NavDemoSidebarAgentWorkspaceTitle",
        SummaryKey = "NavDemoSidebarAgentWorkspaceSummary",
        Compact = true,
        Header =
        [
            Group(null,
            [
                Item("NavDemoNewAgent", "AlderIconPlus", action: "new-agent", badge: "NavDemoBadgeN"),
                Item("NavDemoMarketplace", "NavPreviewIconBasket", selected: true)
            ]),
        ],
        Groups =
        [
            Group("NavDemoAcmePlatform",
            [
                Item("NavDemoUserNamingPreferenceRefactor", "NavPreviewIconChat", badge: "NavDemoBadgeNow"),
                Item("NavDemoAgentsSidebarKeyboardExpe", "NavPreviewIconChat", badge: "NavDemoBadge12m"),
                Item("NavDemoGlassSidebarGroupByNoneOp", "NavPreviewIconChat", badge: "NavDemoBadge33m"),
                Item("NavDemoCloudAgentErrorMessageRe", "NavPreviewIconChat", badge: "NavDemoBadge1h"),
                Item("NavDemoUseApplicationPropertyMigrati", "NavPreviewIconChat"),
                Item("NavDemoGlassTestTimingStabilization", "NavPreviewIconChat"),
                Item("NavDemoFlakyTestsRootCause", "NavPreviewIconChat", badge: "NavDemoBadge10h")
            ]),
            Group("NavDemoAcmeLanding",
            [
                Item("NavDemoMarketingPagesResponsiven", "NavPreviewIconChat", badge: "NavDemoBadge2h"),
                Item("NavDemoGitCheckpointsAutomaticC", "NavPreviewIconChat", badge: "NavDemoBadge3h"),
                Item("NavDemoLocalServerUpdateProcess", "NavPreviewIconChat"),
                Item("NavDemoPerformanceAuditForRedesigne", "NavPreviewIconChat", badge: "NavDemoBadge1d"),
                Item("NavDemoLocalizedPageSEO", "NavPreviewIconChat")
            ]),
            Group("NavDemoAcmeBackoffice",
            [
                Item("NavDemoNewPageForNaturalLanguageF", "NavPreviewIconChat", badge: "NavDemoBadge5h"),
                Item("NavDemoBackgroundWorkerRetryHandling", "NavPreviewIconChat"),
                Item("NavDemoAdminOnlyMetricStripForInco", "NavPreviewIconChat")
            ]),
            Group("NavDemoToolsPlugins",
            [
                Item("NavDemoPluginSchemaVerification", "NavPreviewIconChat", badge: "NavDemoBadge2d")
            ]),
            Group("NavDemoCompass",
            [
                Item("NavDemoLightModeSettings", "NavPreviewIconChat"),
                Item("NavDemoMCPServerSetup", "NavPreviewIconChat", badge: "NavDemoBadge4h")
            ]),
            Group(null,
            [
                Item("NavDemoMore", "AlderIconEllipsis"),
                Item("NavDemoOpenWorkspace", "NavPreviewIconFolder", action: "workspace")
            ]),
        ],
    });

    private NavigationPreview SidebarReducedMotion() => Finish(new NavigationPreview
    {
        SummaryKey = "NavDemoSidebarReducedMotionSummary",
        Groups =
        [
            Group("NavDemoNavigation",
            [
                Item("NavDemoDashboard", "NavPreviewIconHome"),
                Item("NavDemoAnalytics", "AlderIconChart", expanded: true, children:
                [
                    Item("NavDemoOverview"),
                    Item("NavDemoReports", selected: true),
                    Item("NavDemoExports")
                ]),
                Item("NavDemoTracker", "NavPreviewIconChecklist", badge: "NavDemoBadgeNew"),
                Item("NavDemoSettings", "AlderIconGear", children:
                [
                    Item("NavDemoGeneral"),
                    Item("NavDemoTeam"),
                    Item("NavDemoBilling")
                ])
            ]),
        ],
    });

    private NavigationPreview LayoutDashboard() => Finish(new NavigationPreview
    {
        AccountInNavbarOnly = true,
        BrandKey = "NavDemoSoundwaveStudio",
        BrandMark = "S",
        AccountKey = "NavDemoNoraKim",
        AccountMark = "NK",
        SummaryKey = "NavDemoLayoutDashboardSummary",
        Compact = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoDrafts", "AlderIconArchive", badge: "NavDemoBadge4"),
                Item("NavDemoOverview", "NavPreviewIconHome", selected: true),
                Item("NavDemoEpisodes", "AlderIconCalendar"),
                Item("NavDemoGuests", "NavPreviewIconPeople"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoCampaigns", "AlderIconGlobe"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
            Group("NavDemoUpcomingReleases",
            [
                Item("NavDemoDesigningForAttentionSpans"),
                Item("NavDemoThePricingConversationNobodyWants"),
                Item("NavDemoBuildingACreativeStudioFromZero"),
                Item("NavDemoLateNightDebuggingWithAda")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoHelpCenter", "AlderIconInfo"),
                Item("NavDemoWhatSNew", "AlderIconBell")
            ]),
        ],
    });

    private NavigationPreview LayoutDocs() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoAlderDocs",
        BrandHintKey = "NavDemoV30Beta",
        BrandMark = "A",
        SummaryKey = "NavDemoLayoutDocsSummary",
        Compact = true,
        ShowNavbarThemeSwitcher = true,
        Groups =
        [
            Group("NavDemoGettingStarted",
            [
                Item("NavDemoIntroduction", selected: true),
                Item("NavDemoInstallation"),
                Item("NavDemoProjectStructure"),
                Item("NavDemoEditorSetup"),
                Item("NavDemoQuickStart")
            ]),
            Group("NavDemoFoundations",
            [
                Item("NavDemoDesignTokens"),
                Item("NavDemoColors"),
                Item("NavDemoTypography"),
                Item("NavDemoSpacing"),
                Item("NavDemoDarkMode"),
                Item("NavDemoAccessibility")
            ]),
            Group("NavDemoComponents",
            [
                Item("NavDemoActions", null, children:
                [
                    Item("NavDemoButton"),
                    Item("NavDemoButtonGroup"),
                    Item("NavDemoIconButton"),
                    Item("NavDemoLink")
                ]),
                Item("NavDemoInputs", null, expanded: true, children:
                [
                    Item("NavDemoTextField"),
                    Item("NavDemoSelect"),
                    Item("NavDemoCombobox", null, badge: "NavDemoBadgeBeta"),
                    Item("NavDemoCheckbox"),
                    Item("NavDemoRadioGroup"),
                    Item("NavDemoSwitch"),
                    Item("NavDemoSlider")
                ]),
                Item("NavDemoDataDisplay", null, children:
                [
                    Item("NavDemoAvatar"),
                    Item("NavDemoBadge"),
                    Item("NavDemoCard"),
                    Item("NavDemoChip"),
                    Item("NavDemoSkeleton"),
                    Item("NavDemoTag")
                ]),
                Item("NavDemoFeedback", null, children:
                [
                    Item("NavDemoAlert"),
                    Item("NavDemoProgress"),
                    Item("NavDemoSpinner"),
                    Item("NavDemoToast")
                ]),
                Item("NavDemoNavigation", null, children:
                [
                    Item("NavDemoBreadcrumbs"),
                    Item("NavDemoMenu"),
                    Item("NavDemoPagination"),
                    Item("NavDemoTabs")
                ]),
                Item("NavDemoOverlays", null, children:
                [
                    Item("NavDemoDialog"),
                    Item("NavDemoDrawer"),
                    Item("NavDemoPopover"),
                    Item("NavDemoTooltip")
                ])
            ]),
            Group("NavDemoHooks",
            [
                Item("NavDemoUseTheme"),
                Item("NavDemoUseDisclosure"),
                Item("NavDemoUseMediaQuery"),
                Item("NavDemoUseControlledState"),
                Item("NavDemoUseInterval")
            ]),
            Group("NavDemoGuides",
            [
                Item("NavDemoTheming"),
                Item("NavDemoCustomizingComponents"),
                Item("NavDemoServerComponents"),
                Item("NavDemoMigratingFromV2"),
                Item("NavDemoTesting")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoChangelog", "NavPreviewIconBook", badge: "NavDemoBadgeNew"),
                Item("NavDemoGitHub", "AlderIconGlobe"),
                Item("NavDemoHelpSupport", "AlderIconInfo")
            ]),
        ],
    });

    private NavigationPreview LayoutScroll() => Finish(new NavigationPreview
    {
        InitialTitleKey = "NavDemoLayoutScrollTitle",
        SummaryKey = "NavDemoLayoutScrollSummary",
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoHome", "NavPreviewIconHome"),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoAlerts", "AlderIconBell")
            ]),
        ],
    });

    private NavigationPreview LayoutAside() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoInbox",
        BrandMark = "I",
        InitialTitleKey = "NavDemoLayoutAsideTitle",
        SummaryKey = "NavDemoLayoutAsideSummary",
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoMyInbox", "NavPreviewIconInbox", badge: "NavDemoBadge3"),
                Item("NavDemoAllMessages", "NavPreviewIconInbox", badge: "NavDemoBadge13", selected: true),
                Item("NavDemoCreatedByMe", "AlderIconPencil"),
                Item("NavDemoUnassigned", "NavPreviewIconInbox", badge: "NavDemoBadge10")
            ]),
            Group("NavDemoTeamInboxes",
            [
                Item("NavDemoCustomerSuccess", "NavPreviewIconPeople"),
                Item("NavDemoSales", "NavPreviewIconPeople"),
                Item("NavDemoDevelopers", "NavPreviewIconPeople")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoHelpCenter", "AlderIconInfo")
            ]),
        ],
    });

    private NavigationPreview LayoutBreadcrumbs() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoDesignSystem",
        BrandMark = "DS",
        SummaryKey = "NavDemoLayoutBreadcrumbsSummary",
        ShowRail = false,
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoOverview", "NavPreviewIconHome"),
                Item("NavDemoComponents", "NavPreviewIconChat", selected: true),
                Item("NavDemoTokens", "NavPreviewIconChat"),
                Item("NavDemoTypography", "NavPreviewIconBook"),
                Item("NavDemoChangelog", "NavPreviewIconBook"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
            Group("NavDemoWorkspaces",
            [
                Item("NavDemoFrontend", "NavPreviewIconFolder"),
                Item("NavDemoBackend", "NavPreviewIconFolder")
            ]),
        ],
        Footer =
        [
            Group(null,
            [
                Item("NavDemoHelp", "AlderIconInfo")
            ]),
        ],
    });

    private NavigationPreview LayoutToolbar() => Finish(new NavigationPreview
    {
        InitialTitleKey = "NavDemoLayoutToolbarTitle",
        SummaryKey = "NavDemoLayoutToolbarSummary",
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoDashboard", "NavPreviewIconHome", selected: true),
                Item("NavDemoAnalytics", "AlderIconChart"),
                Item("NavDemoSettings", "AlderIconGear")
            ]),
        ],
    });

    private NavigationPreview LayoutComplex() => Finish(new NavigationPreview
    {
        ShowNewChat = true,
        BrandKey = "NavDemoAcmeLabs",
        BrandMark = "A",
        AccountKey = "NavDemoJuniorGarcia",
        AccountMark = "JG",
        SummaryKey = "NavDemoLayoutComplexSummary",
        Compact = true,
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoNewAgent", "AlderIconPlus", action: "new-agent"),
                Item("NavDemoHome", "NavPreviewIconHome"),
                Item("NavDemoInbox", "NavPreviewIconInbox", badge: "NavDemoBadge3"),
                Item("NavDemoMeetings", "AlderIconCalendar")
            ]),
            Group("NavDemoRecentChats",
            [
                Item("NavDemoRefactorAuthModule", "NavPreviewIconChat"),
                Item("NavDemoDebugPaymentFlow", "NavPreviewIconChat"),
                Item("NavDemoWriteAPIDocs", "NavPreviewIconChat"),
                Item("NavDemoReviewPR482", "NavPreviewIconChat")
            ]),
            Group("NavDemoAgents",
            [
                Item("NavDemoCoder", "AlderIconAvatarPerson"),
                Item("NavDemoReviewer", "AlderIconAvatarPerson"),
                Item("NavDemoWriter", "AlderIconAvatarPerson"),
                Item("NavDemoAddAgent", "AlderIconPlus", action: "new-agent")
            ]),
            Group("NavDemoTeamspaces",
            [
                Item("NavDemoAcmeHQ", "NavPreviewIconPeople", expanded: true, children:
                [
                    Item("NavDemoHome", "NavPreviewIconHome"),
                    Item("NavDemoMyTasks", "NavPreviewIconChecklist"),
                    Item("NavDemoProjects", "NavPreviewIconFolder"),
                    Item("NavDemoEpics", "NavPreviewIconChat"),
                    Item("NavDemoRoadmap", "NavPreviewIconChat", selected: true),
                    Item("NavDemoEngBoard", "NavPreviewIconChat")
                ])
            ]),
            Group("NavDemoApps",
            [
                Item("NavDemoDocs", "NavPreviewIconBook"),
                Item("NavDemoCalendar", "AlderIconCalendar")
            ]),
        ],
    });

    private NavigationPreview LayoutResizableAside() => Finish(new NavigationPreview
    {
        BrandKey = "NavDemoInbox",
        BrandMark = "I",
        InitialTitleKey = "NavDemoLayoutResizableAsideTitle",
        SummaryKey = "NavDemoLayoutResizableAsideSummary",
        ShowNavbarActions = true,
        Groups =
        [
            Group(null,
            [
                Item("NavDemoInbox", "NavPreviewIconInbox", selected: true),
                Item("NavDemoHome", "NavPreviewIconHome")
            ]),
        ],
    });

    private NavigationPreview DashboardSidebar(string summary, bool hasConversions = false, bool showLogout = false,
        bool hasSubmenu = true, bool navbarActions = false)
    {
        NavigationPreviewItem[] analytics = hasConversions
            ? [Item("NavDemoOverview"), Item("NavDemoReports"), Item("NavDemoConversions")]
            : [Item("NavDemoOverview"), Item("NavDemoReports")];
        return Finish(new NavigationPreview
        {
            SummaryKey = summary,
            ShowNavbarActions = navbarActions,
            Groups =
            [
                Group(null,
                [
                    Item("NavDemoDashboard", "NavPreviewIconHome", selected: true),
                    Item("NavDemoAnalytics", "AlderIconChart", expanded: hasSubmenu, children: hasSubmenu ? analytics : []),
                    Item("NavDemoTracker", "NavPreviewIconChecklist", badge: "NavDemoBadgeNew"),
                    Item("NavDemoSettings", "AlderIconGear", children:
                        hasSubmenu ? [Item("NavDemoGeneral"), Item("NavDemoTeam"), Item("NavDemoBilling")] : [])
                ])
            ],
            Footer = [Group(null, showLogout
                ? [Item("NavDemoHelpInformation", "AlderIconInfo"), Item("NavDemoLogOut", "AlderIconPower", action: "logout")]
                : [Item("NavDemoHelp", "AlderIconInfo")])]
        });
    }

    private NavigationPreview Finish(NavigationPreview preview)
    {
        preview.SetInitialSelection(initialSelection);
        return preview;
    }

    private static NavigationPreviewGroup Group(string? title, IReadOnlyList<NavigationPreviewItem> items) => new(title, items);

    private NavigationPreviewItem Item(string title, string? icon = null, string? badge = null,
        bool expanded = false, string? action = null, bool selected = false, IReadOnlyList<NavigationPreviewItem>? children = null)
    {
        var item = new NavigationPreviewItem(title, icon, badge, expanded, action, children ?? []);
        if (selected) initialSelection = item;
        return item;
    }
}
