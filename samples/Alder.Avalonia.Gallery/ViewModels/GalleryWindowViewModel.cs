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
        new("NavGroupActionsAndMenus",
            ("Button", "GalleryNavButtonIcon"),
            ("ToggleButton", "GalleryNavToggleIcon"),
            ("Toolbar", "GalleryNavNavbarIcon"),
            ("ActionBar", "GalleryNavNavbarIcon"),
            ("ExpandableActionBar", "GalleryNavNavbarIcon"),
            ("OverflowActions", "GalleryNavNavbarIcon"),
            ("Command", "GalleryNavKeyboardIcon"),
            ("Menu", "GalleryNavNavbarIcon"),
            ("ContextMenu", "AlderIconEllipsis"),
            ("BloomMenu", "AlderIconPlus"),
            ("EmojiReactionButton", "AlderIconHeartFill")),
        new("NavGroupInputAndEditing",
            ("Input", "GalleryNavInputIcon"),
            ("TextField", "GalleryNavInputIcon"),
            ("Fieldset", "GalleryNavCardIcon"),
            ("Form", "GalleryNavInputIcon"),
            ("FieldMessages", "AlderIconInfo"),
            ("RichTextEditor", "GalleryNavInputIcon"),
            ("NumberStepper", "AlderIconPlus"),
            ("Slider", "GalleryNavSliderIcon"),
            ("CellSlider", "GalleryNavSliderIcon"),
            ("DropZone", "AlderIconArrowUp"),
            ("DateAndTime", "AlderIconCalendar"),
            ("TimePicker", "AlderIconClock"),
            ("Agenda", "AlderIconCalendar")),
        new("NavGroupOptionsAndToggles",
            ("Checkbox", "GalleryNavCheckboxIcon"),
            ("CheckboxButtonGroup", "GalleryNavCheckboxIcon"),
            ("RadioButtonGroup", "AlderIconCheckCircle"),
            ("Selection", "AlderIconCheckCircle"),
            ("Switch", "GalleryNavToggleIcon"),
            ("CellSwitch", "GalleryNavToggleIcon"),
            ("Select", "GalleryNavSelectIcon"),
            ("Autocomplete", "AlderIconMagnifier"),
            ("ComboBox", "GalleryNavInputIcon"),
            ("NativeSelect", "GalleryNavSelectIcon"),
            ("InlineSelect", "GalleryNavSelectIcon"),
            ("CellSelect", "GalleryNavSelectIcon"),
            ("EmojiPicker", "AlderIconHeart"),
            ("Rating", "AlderIconStarOutline")),
        new("NavGroupNavigationAndFlow",
            ("Sidebar", "AlderAppLayoutSidebarIcon"),
            ("Navbar", "GalleryNavNavbarIcon"),
            ("Tabs", "GalleryNavTabsIcon"),
            ("Segment", "GalleryNavSegmentIcon"),
            ("Breadcrumbs", "GalleryNavBreadcrumbsIcon"),
            ("Link", "AlderIconLinkExternal"),
            ("Pagination", "GalleryNavPaginationIcon"),
            ("Stepper", "GalleryNavStepperIcon"),
            ("FloatingToc", "GalleryNavTableIcon")),
        new("NavGroupLayoutAndWindows",
            ("AppLayout", "AlderAppLayoutAsideIcon"),
            ("SalashWindow", "AlderIconEye"),
            ("Accordion", "GalleryNavAccordionIcon"),
            ("Disclosure", "GalleryNavDisclosureIcon"),
            ("Card", "GalleryNavCardIcon"),
            ("HoloCard", "AlderIconStarOutline"),
            ("ItemCard", "GalleryNavCardIcon"),
            ("Surface", "AlderIconCopy"),
            ("Separator", "GalleryNavSeparatorIcon"),
            ("Resizable", "GalleryNavSeparatorIcon"),
            ("ScrollShadow", "AlderIconChevronsExpandVertical"),
            ("TransitioningContent", "AlderIconCopy")),
        new("NavGroupOverlaysAndDialogs",
            ("Dialog", "GalleryNavDialogIcon"),
            ("MessageBox", "AlderIconEnvelope"),
            ("Drawer", "AlderAppLayoutSidebarIcon"),
            ("Sheet", "AlderAppLayoutAsideIcon"),
            ("Popover", "NavPreviewIconChat"),
            ("HoverCard", "GalleryNavCardIcon"),
            ("ToolTip", "AlderIconInfo")),
        new("NavGroupStatusAndFeedback",
            ("Alert", "AlderIconWarning"),
            ("Toast", "AlderIconBell"),
            ("EmptyState", "AlderIconArchive"),
            ("Progress", "GalleryNavProgressIcon"),
            ("Spinner", "AlderIconCircleDashed"),
            ("Skeleton", "GalleryNavSkeletonIcon"),
            ("Loader", "AlderIconStarOutline"),
            ("TextShimmer", "AlderIconStarOutline"),
            ("PressableFeedback", "GalleryNavButtonIcon")),
        new("NavGroupContentAndLabels",
            ("Typography", "AlderIconBold"),
            ("Kbd", "GalleryNavKeyboardIcon"),
            ("Markdown", "AlderIconStarOutline"),
            ("CodeBlock", "AlderIconStarOutline"),
            ("ImageView", "AlderImageViewExpandIcon"),
            ("Carousel", "GalleryNavCardIcon"),
            ("Avatar", "AlderIconAvatarPerson"),
            ("Badge", "AlderIconBookmark"),
            ("Chip", "GalleryNavSegmentIcon"),
            ("TagGroup", "GalleryNavSegmentIcon")),
        new("NavGroupDataViews",
            ("Table", "GalleryNavTableIcon"),
            ("DataGrid", "GalleryNavDataGridIcon"),
            ("ListView", "GalleryNavTableIcon"),
            ("FileTree", "AlderFileTreeFolderIcon"),
            ("TreeView", "AlderFileTreeFolderIcon"),
            ("Kanban", "GalleryNavTableIcon"),
            ("Timeline", "AlderIconCalendar"),
            ("Map", "AlderIconGlobe"),
            ("Widget", "GalleryNavCardIcon"),
            ("WorkflowEditor", "AlderAppLayoutAsideIcon")),
        new("NavGroupChartsAndMetrics",
            ("AreaChart", "GalleryNavAreaChartIcon"),
            ("BarChart", "GalleryNavBarChartIcon"),
            ("LineChart", "GalleryNavLineChartIcon"),
            ("PieChart", "GalleryNavPieChartIcon"),
            ("RadarChart", "GalleryNavRadarChartIcon"),
            ("RadialChart", "GalleryNavRadialChartIcon"),
            ("ComposedChart", "GalleryNavBarChartIcon"),
            ("ChartTooltip", "AlderIconInfo"),
            ("Kpi", "GalleryNavBarChartIcon"),
            ("Meter", "AlderIconChart"),
            ("TrendChip", "AlderTrendUp"),
            ("NumberValue", "GalleryNavInputIcon")),
        new("NavGroupColors",
            ("CellColorPicker", "GalleryNavColorPickerIcon"),
            ("ColorArea", "GalleryNavigationColorIcon"),
            ("ColorField", "AlderIconPencil"),
            ("ColorPicker", "GalleryNavColorPickerIcon"),
            ("ColorSlider", "GalleryNavSliderIcon"),
            ("ColorSwatch", "GalleryNavSwatchIcon"),
            ("ColorSwatchPicker", "GalleryNavSwatchesIcon")),
        new("NavGroupAiChat",
            ("ChatConversation", "AlderIconStarOutline"),
            ("ChatListView", "AlderIconStarOutline"),
            ("ChatMessage", "AlderIconStarOutline"),
            ("ChatMessageActions", "AlderIconStarOutline"),
            ("ChatAttachment", "AlderIconStarOutline"),
            ("ChatSource", "AlderIconStarOutline"),
            ("ChatTool", "AlderIconStarOutline"),
            ("ChatLoader", "AlderIconStarOutline"),
            ("PromptInput", "AlderIconStarOutline"),
            ("PromptSuggestion", "AlderIconStarOutline"),
            ("ModelSelector", "AlderIconStarOutline"),
            ("Reasoning", "AlderIconStarOutline"),
            ("ChainOfThought", "AlderIconStarOutline"),
            ("ApprovalCard", "AlderIconStarOutline")),
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
