using System.Windows.Input;
using Avalonia;
using Alder.Avalonia.Gallery.Localization;
using Avalonia.Styling;
using ReactiveUI;

namespace Alder.Avalonia.Gallery.ViewModels;

public sealed class GalleryWindowViewModel : GalleryViewModel
{
    public GalleryWindowViewModel()
    {
        Language = GalleryLocalization.Current.Language;
        var chooseLanguage = ReactiveCommand.Create<GalleryLanguage>(ApplyLanguage);
        Languages = GalleryLocalization.Languages
            .Select(language => new GalleryLanguageChoice(language, chooseLanguage) { IsSelected = language == Language })
            .ToArray();

        Groups = CreateGroups();
        var home = new GalleryNavigationItem("Home", "Home", "GalleryNavHomeIcon");
        NavigationItems = Groups.SelectMany((group, index) => group.Items.Prepend(group.CreateHeading(index == 0)))
            .Prepend(home).ToArray();
        SelectedItem = home;
        WindowTheme = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Light;
    }

    public IReadOnlyList<GalleryNavigationGroup> Groups { get; }
    public IReadOnlyList<GalleryNavigationItem> NavigationItems { get; }
    public Func<object, string?> NavigationSearchText { get; } =
        item => item is GalleryNavigationItem { RowKind: "item" } page ? page.Title : null;
    public IReadOnlyList<GalleryLanguageChoice> Languages { get; }

    public GalleryNavigationItem? SelectedItem
    {
        get => field;
        set
        {
            if (value is null || ReferenceEquals(field, value)) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ShowBreadcrumbParents));
        }
    }

    public GalleryLanguage Language { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
    public bool IsSidebarOpen { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;

    public bool ShowBreadcrumbParents => SelectedItem?.PageKey is not (null or "Home");

    public ThemeVariant WindowTheme
    {
        get => field;
        set
        {
            if (field == value) return;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ThemeKey));
        }
    } = ThemeVariant.Light;

    public object ThemeKey
    {
        get => WindowTheme == ThemeVariant.Dark ? "dark" : "light";
        set
        {
            if (value is "light") WindowTheme = ThemeVariant.Light;
            else if (value is "dark") WindowTheme = ThemeVariant.Dark;
        }
    }

    private void ApplyLanguage(GalleryLanguage language)
    {
        if (Application.Current is not App app || language == Language) return;
        app.Localization.Apply(language, app.Resources);
        Language = language;
        foreach (var choice in Languages)
            choice.IsSelected = choice.Language == language;
    }

    private static IReadOnlyList<GalleryNavigationGroup> CreateGroups() =>
    [
        new("NavGroupButtons",
            ("Button", "GalleryNavButtonIcon"),
            ("ToggleButton", "GalleryNavToggleIcon")),
        new("NavGroupAI",
            ("ApprovalCard", "AlderIconStarOutline"),
            ("ChainOfThought", "AlderIconStarOutline"),
            ("ChatAttachment", "AlderIconStarOutline"),
            ("ChatConversation", "AlderIconStarOutline"),
            ("ChatListView", "AlderIconStarOutline"),
            ("ChatLoader", "AlderIconStarOutline"),
            ("ChatMessage", "AlderIconStarOutline"),
            ("ChatMessageActions", "AlderIconStarOutline"),
            ("ChatSource", "AlderIconStarOutline"),
            ("ChatTool", "AlderIconStarOutline"),
            ("CodeBlock", "AlderIconStarOutline"),
            ("Loader", "AlderIconStarOutline"),
            ("Markdown", "AlderIconStarOutline"),
            ("ModelSelector", "AlderIconStarOutline"),
            ("PromptInput", "AlderIconStarOutline"),
            ("PromptSuggestion", "AlderIconStarOutline"),
            ("Reasoning", "AlderIconStarOutline"),
            ("TextShimmer", "AlderIconStarOutline")),
        new("NavGroupForms",
            ("Checkbox", "GalleryNavCheckboxIcon"),
            ("RichTextEditor", "GalleryNavInputIcon"),
            ("CheckboxButtonGroup", "GalleryNavCheckboxIcon"),
            ("RadioButtonGroup", "AlderIconCheckCircle"),
            ("DropZone", "AlderIconArrowUp"),
            ("NumberStepper", "AlderIconPlus"),
            ("Input", "GalleryNavInputIcon"),
            ("Selection", "AlderIconCheckCircle")),
        new("NavGroupDateAndTime",
            ("DateAndTime", "AlderIconCalendar"),
            ("Agenda", "AlderIconCalendar")),
        new("NavGroupNavigation",
            ("Accordion", "GalleryNavAccordionIcon"),
            ("AppLayout", "AlderAppLayoutAsideIcon"),
            ("Breadcrumbs", "GalleryNavBreadcrumbsIcon"),
            ("Command", "GalleryNavKeyboardIcon"),
            ("ContextMenu", "AlderIconEllipsis"),
            ("Disclosure", "GalleryNavDisclosureIcon"),
            ("Link", "AlderIconLinkExternal"),
            ("Navbar", "GalleryNavNavbarIcon"),
            ("Pagination", "GalleryNavPaginationIcon"),
            ("Segment", "GalleryNavSegmentIcon"),
            ("Sidebar", "AlderAppLayoutSidebarIcon"),
            ("Stepper", "GalleryNavStepperIcon"),
            ("Tabs", "GalleryNavTabsIcon")),
        new("NavGroupOverlays",
            ("BloomMenu", "AlderIconPlus"),
            ("EmojiPicker", "AlderIconHeart"),
            ("MessageBox", "AlderIconEnvelope"),
            ("Drawer", "AlderAppLayoutSidebarIcon"),
            ("Dialog", "GalleryNavDialogIcon"),
            ("Popover", "NavPreviewIconChat"),
            ("Sheet", "AlderAppLayoutAsideIcon"),
            ("Toast", "AlderIconBell"),
            ("SalashWindow", "AlderIconEye"),
            ("ToolTip", "AlderIconInfo")),
        new("NavGroupControls",
            ("CellSlider", "GalleryNavSliderIcon"),
            ("CellSwitch", "GalleryNavToggleIcon"),
            ("Slider", "GalleryNavSliderIcon"),
            ("Switch", "GalleryNavToggleIcon")),
        new("NavGroupFeedback",
            ("Alert", "AlderIconWarning"),
            ("EmojiReactionButton", "AlderIconHeartFill"),
            ("NumberValue", "GalleryNavInputIcon"),
            ("PressableFeedback", "GalleryNavButtonIcon"),
            ("Rating", "AlderIconStarOutline"),
            ("TrendChip", "AlderTrendUp"),
            ("Meter", "AlderIconChart"),
            ("Progress", "GalleryNavProgressIcon"),
            ("Skeleton", "GalleryNavSkeletonIcon"),
            ("Spinner", "AlderIconCircleDashed")),
        new("NavGroupLayout",
            ("Card", "GalleryNavCardIcon"),
            ("Resizable", "GalleryNavSeparatorIcon"),
            ("Separator", "GalleryNavSeparatorIcon"),
            ("Surface", "AlderIconCopy"),
            ("Toolbar", "GalleryNavNavbarIcon"),
            ("TransitioningContent", "AlderIconCopy")),
        new("NavGroupMedia",
            ("Avatar", "AlderIconAvatarPerson")),
        new("NavGroupPickers",
            ("Autocomplete", "AlderIconMagnifier"),
            ("CellSelect", "GalleryNavSelectIcon"),
            ("InlineSelect", "GalleryNavSelectIcon"),
            ("NativeSelect", "GalleryNavSelectIcon"),
            ("ComboBox", "GalleryNavInputIcon"),
            ("Select", "GalleryNavSelectIcon")),
        new("NavGroupTypography",
            ("Kbd", "GalleryNavKeyboardIcon"),
            ("Typography", "AlderIconBold")),
        new("NavGroupCharts",
            ("AreaChart", "GalleryNavAreaChartIcon"),
            ("BarChart", "GalleryNavBarChartIcon"),
            ("ChartTooltip", "AlderIconInfo"),
            ("ComposedChart", "GalleryNavBarChartIcon"),
            ("LineChart", "GalleryNavLineChartIcon"),
            ("PieChart", "GalleryNavPieChartIcon"),
            ("RadarChart", "GalleryNavRadarChartIcon"),
            ("RadialChart", "GalleryNavRadialChartIcon")),
        new("NavGroupDataDisplay",
            ("ActionBar", "GalleryNavNavbarIcon"),
            ("ExpandableActionBar", "GalleryNavNavbarIcon"),
            ("OverflowActions", "GalleryNavNavbarIcon"),
            ("Badge", "AlderIconBookmark"),
            ("Carousel", "GalleryNavCardIcon"),
            ("Chip", "GalleryNavSegmentIcon"),
            ("DataGrid", "GalleryNavDataGridIcon"),
            ("EmptyState", "AlderIconArchive"),
            ("FileTree", "AlderFileTreeFolderIcon"),
            ("FloatingToc", "GalleryNavTableIcon"),
            ("HoloCard", "AlderIconStarOutline"),
            ("HoverCard", "GalleryNavCardIcon"),
            ("ImageView", "AlderImageViewExpandIcon"),
            ("ItemCard", "GalleryNavCardIcon"),
            ("Kanban", "GalleryNavTableIcon"),
            ("Kpi", "GalleryNavBarChartIcon"),
            ("ListView", "GalleryNavTableIcon"),
            ("Map", "AlderIconGlobe"),
            ("Table", "GalleryNavTableIcon"),
            ("Timeline", "AlderIconCalendar"),
            ("Widget", "GalleryNavCardIcon")),
        new("NavGroupColors",
            ("CellColorPicker", "GalleryNavColorPickerIcon"),
            ("ColorArea", "GalleryNavigationColorIcon"),
            ("ColorField", "AlderIconPencil"),
            ("ColorPicker", "GalleryNavColorPickerIcon"),
            ("ColorSlider", "GalleryNavSliderIcon"),
            ("ColorSwatch", "GalleryNavSwatchIcon"),
            ("ColorSwatchPicker", "GalleryNavSwatchesIcon")),
        new("NavGroupUtilities",
            ("ScrollShadow", "AlderIconChevronsExpandVertical"),
            ("WorkflowEditor", "AlderAppLayoutAsideIcon")),
    ];
}

public sealed class GalleryNavigationGroup(string titleKey, params (string PageKey, string IconKey)[] pages) : GalleryViewModel
{
    public string Title => Text(titleKey);
    public IReadOnlyList<GalleryNavigationItem> Items { get; } =
        pages.Select(page => new GalleryNavigationItem(page.PageKey, titleKey, page.IconKey)).ToArray();

    internal GalleryNavigationItem CreateHeading(bool first) => new("", titleKey, "", first ? 6 : 16);
}

public sealed class GalleryNavigationItem(string pageKey, string groupTitleKey, string iconKey, int? headingInset = null) : GalleryViewModel
{
    public string Title => headingInset.HasValue ? GroupTitle : Text(PageKey switch
    {
        "DateAndTime" => "NavCalendarsAndFields",
        "Command" => "CommandPalette",
        _ => PageKey
    });
    public string GroupTitle => Text(groupTitleKey);
    public string PageKey { get; } = pageKey;
    public string IconKey { get; } = iconKey;
    public string RowKind => headingInset.HasValue ? "heading" : "item";
    public Thickness HeadingMargin => new(8, headingInset ?? 0, 8, 4);
    public override string ToString() => Title;
}

public sealed class GalleryLanguageChoice(GalleryLanguage language, ICommand command) : ReactiveObject
{
    public GalleryLanguage Language { get; } = language;
    public ICommand Command { get; } = command;
    public bool IsSelected { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
}
