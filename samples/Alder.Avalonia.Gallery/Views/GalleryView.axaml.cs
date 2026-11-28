using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;

namespace Alder.Avalonia.Gallery.Views;

public partial class GalleryView : UserControl
{
    private ScrollViewer? homePage;
    private readonly Dictionary<string, Lazy<ScrollViewer>> pages = new(StringComparer.Ordinal)
    {
        ["Button"] = new(() => CreateGroupedPage(
            () => CreateExamples(new ButtonExample(), new ButtonViewModel(), "Button"),
            () => CreateExamples(new ButtonGroupExample(), new ButtonGroupViewModel(), "ButtonGroup"))),
        ["ToggleButton"] = new(() => CreatePage(CreateExamples(new ToggleButtonExample(), new ToggleButtonViewModel()))),
        ["NumberValue"] = new(() => CreatePage(new NumberValueExample())),
        ["PressableFeedback"] = new(() => CreatePage(new PressableFeedbackExample())),
        ["Rating"] = new(() => CreatePage(new RatingExample())),
        ["TrendChip"] = new(() => CreatePage(new TrendChipExample())),
        ["EmojiReactionButton"] = new(() => CreatePage(CreateExamples(new EmojiReactionButtonExample(), new EmojiReactionButtonViewModel()))),
        ["ApprovalCard"] = new(() => CreatePage(CreateExamples(new ApprovalCardExample(), new AiExampleViewModel()))),
        ["ChainOfThought"] = new(() => CreatePage(CreateExamples(new ChainOfThoughtExample(), new AiExampleViewModel()))),
        ["ChatAttachment"] = new(() => CreatePage(CreateExamples(new ChatAttachmentExample(), new ChatAttachmentViewModel()))),
        ["ChatConversation"] = new(() => CreatePage(CreateExamples(new ChatConversationExample(), new AiExampleViewModel()))),
        ["ChatListView"] = new(() => CreatePage(CreateExamples(new ChatListViewExample(), new AiExampleViewModel()))),
        ["ChatLoader"] = new(() => CreatePage(CreateExamples(new ChatLoaderExample(), new AiExampleViewModel()))),
        ["ChatMessage"] = new(() => CreatePage(CreateExamples(new ChatMessageExample(), new AiExampleViewModel()))),
        ["ChatMessageActions"] = new(() => CreatePage(CreateExamples(new ChatMessageActionsExample(), new AiExampleViewModel()))),
        ["ChatSource"] = new(() => CreatePage(CreateExamples(new ChatSourceExample(), new AiExampleViewModel()))),
        ["ChatTool"] = new(() => CreatePage(CreateExamples(new ChatToolExample(), new AiExampleViewModel()))),
        ["CodeBlock"] = new(() => CreatePage(CreateExamples(new CodeBlockExample(), new AiExampleViewModel()))),
        ["Loader"] = new(() => CreatePage(CreateExamples(new LoaderExample(), new AiExampleViewModel()))),
        ["Markdown"] = new(() => CreatePage(CreateExamples(new MarkdownExample(), new AiExampleViewModel()))),
        ["ModelSelector"] = new(() => CreatePage(CreateExamples(new ModelSelectorExample(), new AiExampleViewModel()))),
        ["PromptInput"] = new(() => CreatePage(CreateExamples(new PromptInputExample(), new AiExampleViewModel()))),
        ["PromptSuggestion"] = new(() => CreatePage(CreateExamples(new PromptSuggestionExample(), new AiExampleViewModel()))),
        ["Reasoning"] = new(() => CreatePage(CreateExamples(new ReasoningExample(), new AiExampleViewModel()))),
        ["TextShimmer"] = new(() => CreatePage(CreateExamples(new TextShimmerExample(), new AiExampleViewModel()))),
        ["EmojiPicker"] = new(() => CreatePage(CreateExamples(new EmojiPickerExample(), new EmojiPickerViewModel()))),
        ["Checkbox"] = new(() => CreatePage(CreateExamples(new CheckBoxExample(), new CheckBoxViewModel()))),
        ["RadioButtonGroup"] = new(() => CreatePage(CreateExamples(new RadioButtonGroupExample(), new RadioButtonGroupViewModel()))),
        ["SalashWindow"] = new(() => CreatePage(new SalashWindowExample())),
        ["DropZone"] = new(() => CreatePage(CreateExamples(new DropZoneExample(), new DropZoneViewModel()))),
        ["NumberStepper"] = new(() => CreatePage(CreateExamples(new NumberStepperExample(), new NumberStepperViewModel()))),
        ["InlineSelect"] = new(() => CreatePage(new InlineSelectExample())),
        ["NativeSelect"] = new(() => CreatePage(CreateExamples(new NativeSelectExample(), new FormSelectViewModel()))),
        ["TextField"] = new(() => CreatePage(CreateExamples(new TextFieldExample(), new FormDemoViewModel()))),
        ["Fieldset"] = new(() => CreatePage(CreateExamples(new FieldsetExample(), new FormDemoViewModel()))),
        ["Form"] = new(() => CreatePage(CreateExamples(new FormExample(), new FormDemoViewModel()))),
        ["FieldMessages"] = new(() => CreatePage(CreateExamples(new FieldMessagesExample(), new FormDemoViewModel()))),
        ["RichTextEditor"] = new(() => CreatePage(CreateExamples(new RichTextEditorExample(), new RichTextEditorViewModel()))),
        ["CheckboxButtonGroup"] = new(() => CreatePage(CreateExamples(new CheckboxButtonGroupExample(), new CheckboxButtonGroupViewModel()))),
        ["Input"] = new(() => CreateGroupedPage(
            () => CreateExamples(new InputExample(), new InputViewModel(), "Input"),
            () => CreateExamples(new InputGroupExample(), new InputGroupViewModel(), "InputGroup"),
            () => CreateExamples(new InputOtpExample(), new InputOtpViewModel(), "InputOTP"),
            () => CreateExamples(new NumberFieldExample(), new NumberFieldViewModel(), "NumberField"),
            () => CreateExamples(new SearchFieldExample(), new SearchFieldViewModel(), "SearchField"),
            () => CreateExamples(new TextAreaExample(), new TextAreaViewModel(), "TextArea"))),
        ["Selection"] = new(() => CreateGroupedPage(
            () => CreateExamples(new ChoiceExample(), new ChoiceViewModel(), "Selection"),
            () => CreateExamples(new RadioGroupExample(), new RadioGroupViewModel(), "RadioGroup"))),
        ["Switch"] = new(() => CreatePage(CreateExamples(new NotificationExample(), new NotificationViewModel()))),
        ["CellSwitch"] = new(() => CreatePage(new CellSwitchExample())),
        ["Resizable"] = new(() => CreatePage(new ResizableExample())),
        ["Toolbar"] = new(() => CreatePage(CreateExamples(new ToolbarExample(), new ToolbarViewModel()))),
        ["Slider"] = new(() => CreatePage(CreateExamples(new SliderExample(), new SliderViewModel()))),
        ["CellSlider"] = new(() => CreatePage(new CellSliderExample())),
        ["ActionBar"] = new(() => CreatePage(new ActionBarExample())),
        ["ExpandableActionBar"] = new(() => CreatePage(CreateExamples(new ExpandableActionBarExample(), new ExpandableActionBarViewModel()))),
        ["OverflowActions"] = new(() => CreatePage(CreateExamples(new OverflowActionsExample(), new OverflowActionsViewModel()))),
        ["EmptyState"] = new(() => CreatePage(new EmptyStateExample())),
        ["FileTree"] = new(() => CreatePage(new FileTreeExample())),
        ["FloatingToc"] = new(() => CreatePage(new FloatingTocExample())),
        ["HoloCard"] = new(() => CreatePage(CreateExamples(new HoloCardExample(), new HoloCardViewModel()))),
        ["HoverCard"] = new(() => CreatePage(CreateExamples(new HoverCardExample(), new HoverCardViewModel()))),
        ["ImageView"] = new(() => CreatePage(new ImageViewExample())),
        ["Kanban"] = new(() => CreatePage(CreateExamples(new KanbanExample(), new KanbanViewModel()))),
        ["ItemCard"] = new(() => CreatePage(CreateExamples(new ItemCardExample(), new ProDataDisplayViewModel()))),
        ["Kpi"] = new(() => CreatePage(CreateExamples(new KpiExample(), new ProDataDisplayViewModel()))),
        ["ListView"] = new(() => CreatePage(CreateExamples(new ListViewExample(), new ProDataDisplayViewModel()))),
        ["Map"] = new(() => CreatePage(CreateExamples(new MapExample(), new MapViewModel()))),
        ["Timeline"] = new(() => CreatePage(new TimelineExample())),
        ["Widget"] = new(() => CreatePage(CreateExamples(new WidgetExample(), new ProDataDisplayViewModel()))),
        ["Agenda"] = new(() => CreatePage(new AgendaExample())),
        ["Carousel"] = new(() => CreatePage(new CarouselExample())),
        ["DateAndTime"] = new(() => CreateSectionPage(
            ("Calendar", new CalendarExample(), new CalendarViewModel()),
            ("DateField", new DateFieldExample(), new DateFieldViewModel()),
            ("DatePicker", new DatePickerExample(), new DatePickerViewModel()),
            ("DateRangePicker", new DateRangePickerExample(), new DateRangePickerViewModel()),
            ("RangeCalendar", new RangeCalendarExample(), new RangeCalendarViewModel()),
            ("TimeField", new TimeFieldExample(), new TimeFieldViewModel()))),
        ["Accordion"] = new(() => CreatePage(CreateExamples(new AccordionExample(), new AccordionViewModel()))),
        ["AppLayout"] = new(() => CreateDeferredPage(() => [CreateExamples(new AppLayoutExample(), new AppLayoutViewModel(), completeNavigationSources: true)])),
        ["Breadcrumbs"] = new(() => CreatePage(CreateExamples(new BreadcrumbsExample(), new BreadcrumbsViewModel()))),
        ["Command"] = new(() => CreatePage(CreateExamples(new CommandExample(), new CommandViewModel()))),
        ["ContextMenu"] = new(() => CreatePage(CreateExamples(new ContextMenuExample(), new ContextMenuViewModel()))),
        ["Menu"] = new(() => CreatePage(CreateExamples(new MenuExample(), new MenuViewModel()))),
        ["TreeView"] = new(() => CreatePage(CreateExamples(new TreeViewExample(), new TreeViewExampleViewModel()))),
        ["TimePicker"] = new(() => CreatePage(CreateExamples(new TimePickerExample(), new TimePickerViewModel()))),
        ["Disclosure"] = new(() => CreateGroupedPage(
            () => CreateExamples(new DisclosureExample(), new DisclosureViewModel(), "Disclosure"),
            () => CreateExamples(new DisclosureGroupExample(), new DisclosureGroupViewModel(), "DisclosureGroup"))),
        ["Link"] = new(() => CreatePage(CreateExamples(new LinkExample(), new LinkViewModel()))),
        ["Segment"] = new(() => CreatePage(CreateExamples(new SegmentExample(), new SegmentViewModel()))),
        ["Navbar"] = new(() => CreatePage(CreateExamples(new NavbarExample(), new NavbarViewModel()))),
        ["Stepper"] = new(() => CreatePage(CreateExamples(new StepperExample(), new StepperViewModel()))),
        ["Sidebar"] = new(() => CreateDeferredPage(() => [CreateExamples(new SidebarExample(), new SidebarViewModel(), completeNavigationSources: true)])),
        ["Pagination"] = new(() => CreatePage(CreateExamples(new PaginationExample(), new PaginationViewModel()))),
        ["Tabs"] = new(() => CreateGroupedPage(
            () => CreateExamples(new ExpandableTabsExample(), new ExpandableTabsViewModel()),
            () => CreateExamples(new MorphingTabsExample(), new MorphingTabsViewModel()),
            () => CreateExamples(new TabsExample(), new TabsViewModel(), "Tabs"))),
        ["Dialog"] = new(() => CreatePage(CreateExamples(new DialogExample(), new DialogViewModel()))),
        ["Drawer"] = new(() => CreatePage(CreateExamples(new DrawerExample(), new DrawerViewModel()))),
        ["MessageBox"] = new(() => CreatePage(CreateExamples(new MessageBoxExample(), new MessageBoxViewModel()))),
        ["Popover"] = new(() => CreatePage(CreateExamples(new PopoverExample(), new PopoverViewModel()))),
        ["BloomMenu"] = new(() => CreatePage(CreateExamples(new BloomMenuExample(), new BloomMenuViewModel()))),
        ["Sheet"] = new(() => CreatePage(CreateExamples(new SheetExample(), new SheetViewModel()))),
        ["Toast"] = new(() => CreatePage(CreateExamples(new ToastExample(), new ToastViewModel()))),
        ["ToolTip"] = new(() => CreatePage(CreateExamples(new ToolTipExample(), new ToolTipViewModel()))),
        ["Alert"] = new(() => CreatePage(CreateExamples(new AlertExample(), new FeedbackViewModel()))),
        ["Meter"] = new(() => CreatePage(CreateExamples(new MeterExample(), new FeedbackViewModel()))),
        ["Progress"] = new(() => CreateGroupedPage(
            () => CreateExamples(new ProgressBarExample(), new FeedbackViewModel(), "ProgressBar"),
            () => CreateExamples(new ProgressCircleExample(), new FeedbackViewModel(), "ProgressCircle"))),
        ["Skeleton"] = new(() => CreatePage(CreateExamples(new SkeletonExample(), new FeedbackViewModel()))),
        ["Spinner"] = new(() => CreatePage(CreateExamples(new SpinnerExample(), new FeedbackViewModel()))),
        ["Card"] = new(() => CreatePage(CreateExamples(new CardExample(), new CardViewModel()))),
        ["Separator"] = new(() => CreatePage(CreateExamples(new SeparatorExample(), new LayoutViewModel()))),
        ["Surface"] = new(() => CreatePage(CreateExamples(new SurfaceExample(), new LayoutViewModel()))),
        ["TransitioningContent"] = new(() => CreatePage(CreateExamples(new TransitioningContentExample(), new TransitioningContentViewModel()))),
        ["Avatar"] = new(() =>
        {
            var viewModel = new AvatarViewModel();
            return CreateGroupedPage(() => CreateExamples(new AvatarExample(), viewModel, "Avatar"),
                () => CreateExamples(new AvatarGroupExample(), viewModel, "AvatarGroup"));
        }),
        ["Autocomplete"] = new(() => CreatePage(CreateExamples(new AutocompleteExample(), new AutocompleteViewModel()))),
        ["ComboBox"] = new(() => CreatePage(CreateExamples(new ComboBoxExample(), new ComboBoxViewModel()))),
        ["Select"] = new(() => CreatePage(CreateExamples(new SelectExample(), new SelectViewModel()))),
        ["CellSelect"] = new(() => CreatePage(new CellSelectExample())),
        ["Kbd"] = new(() => CreatePage(CreateExamples(new KbdExample(), new KbdViewModel()))),
        ["Typography"] = new(() => CreatePage(CreateExamples(new TypographyExample(), new TypographyViewModel()))),
        ["ColorArea"] = new(() => CreatePage(CreateExamples(new ColorAreaExample(), new ColorsViewModel()))),
        ["ColorField"] = new(() => CreatePage(CreateExamples(new ColorFieldExample(), new ColorsViewModel()))),
        ["ColorPicker"] = new(() => CreatePage(CreateExamples(new ColorPickerExample(), new ColorsViewModel()))),
        ["CellColorPicker"] = new(() => CreatePage(new CellColorPickerExample())),
        ["ColorSlider"] = new(() => CreatePage(CreateExamples(new ColorSliderExample(), new ColorsViewModel()))),
        ["ColorSwatch"] = new(() => CreatePage(CreateExamples(new ColorSwatchExample(), new ColorsViewModel()))),
        ["ColorSwatchPicker"] = new(() => CreatePage(CreateExamples(new ColorSwatchPickerExample(), new ColorsViewModel()))),
        ["Badge"] = new(() => CreatePage(CreateExamples(new BadgeExample(), new StatusIndicatorViewModel()))),
        ["Chip"] = new(() => CreatePage(CreateExamples(new ChipExample(), new StatusIndicatorViewModel()))),
        ["TagGroup"] = new(() => CreatePage(CreateExamples(new TagExample(), new TagViewModel()))),
        ["AreaChart"] = new(() => CreatePage(CreateExamples(new AreaChartExample(), new ChartViewModel()))),
        ["BarChart"] = new(() => CreatePage(CreateExamples(new BarChartExample(), new ChartViewModel()))),
        ["ChartTooltip"] = new(() => CreatePage(CreateExamples(new ChartTooltipExample(), new ChartsViewModel()))),
        ["ComposedChart"] = new(() => CreatePage(CreateExamples(new ComposedChartExample(), new ChartsViewModel()))),
        ["LineChart"] = new(() => CreatePage(CreateExamples(new LineChartExample(), new ChartsViewModel()))),
        ["PieChart"] = new(() => CreatePage(CreateExamples(new PieChartExample(), new ChartsViewModel()))),
        ["RadarChart"] = new(() => CreatePage(CreateExamples(new RadarChartExample(), new ChartsViewModel()))),
        ["RadialChart"] = new(() => CreatePage(CreateExamples(new RadialChartExample(), new ChartsViewModel()))),
        ["DataGrid"] = new(() => CreatePage(CreateExamples(new DataGridExample(), new DataGridViewModel()))),
        ["Table"] = new(() => CreatePage(CreateExamples(new TableExample(), new TableViewModel()))),
        ["ScrollShadow"] = new(() => CreatePage(CreateExamples(new ScrollShadowExample(), new ScrollShadowViewModel()))),
        ["WorkflowEditor"] = new(() => CreatePage(CreateExamples(new WorkflowEditorExample(), new WorkflowEditorViewModel())))
    };

    public GalleryView()
    {
        InitializeComponent();

        PageContent.ContentTemplate = new FuncDataTemplate<GalleryNavigationItem>(BuildPage);
        var languageMenu = (AlderMenuFlyout)LanguageButton.Flyout!;
        languageMenu.Popup.ShouldUseOverlayLayer = true;
        languageMenu.Popup.WindowManagerAddShadowHint = false;
        DataContext = new GalleryWindowViewModel();
    }

    private ScrollViewer? BuildPage(GalleryNavigationItem? item, INameScope _)
    {
        if (item?.PageKey == "Home")
        {
            homePage ??= new GalleryPageScrollViewer
            {
                Content = new GalleryHomeView(OpenHomeExample),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            return OpenPageAtTop(homePage);
        }

        if (item?.PageKey is not string key || !pages.TryGetValue(key, out var page))
            return null;

        bool firstVisit = !page.IsValueCreated;
        var scrollViewer = page.Value;
        if (firstVisit)
        {
            var stack = (StackPanel)scrollViewer.Content!;
            var title = new AlderTextBlock { FontSize = 30, FontWeight = FontWeight.SemiBold, DataContext = item };
            title[!TextBlock.TextProperty] = CompiledBinding.Create((GalleryNavigationItem selected) => selected.Title);
            title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
            stack.Children.Insert(0, title);
        }

        return OpenPageAtTop(scrollViewer);
    }

    private ScrollViewer OpenPageAtTop(ScrollViewer page)
    {
        page.Offset = default;
        // A cached scroll presenter can restore its old offset while it is reattached and laid out.
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(PageContent.Presenter?.Child, page))
                page.Offset = default;
        }, DispatcherPriority.Loaded);
        return page;
    }

    private void OpenHomeExample(GalleryHomeLink link)
    {
        if (DataContext is not GalleryWindowViewModel model ||
            model.NavigationItems.FirstOrDefault(item => item.PageKey == link.PageKey) is not { } item ||
            !pages.TryGetValue(link.PageKey, out var page)) return;

        model.SelectedItem = item;
        // Resolve the link after deferred example groups have been built and laid out.
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(model.SelectedItem, item)) return;
            var root = page.Value;
            if (link.SectionName is { } sectionName && root.Content is GallerySectionStack groups &&
                groups.LoadThrough(sectionName))
                TopLevel.GetTopLevel(root)?.UpdateLayout();

            if (link.SectionName is { } name && ((StackPanel)root.Content!).Children.OfType<GalleryExampleList>()
                .FirstOrDefault()?.RevealSection(name, link.ExampleName, link.ShowCode) == true)
                return;

            Control section = link.SectionName is null ? root :
                root.GetLogicalDescendants().OfType<StackPanel>().FirstOrDefault(group => group.Name == link.SectionName)
                ?? (Control)root;
            var descendants = section.GetLogicalDescendants();
            var (list, card) = descendants.OfType<GalleryExampleList>()
                .Select(group => (group, group.FindExample(link.ExampleName)))
                .FirstOrDefault(match => match.Item2 is not null);
            var example = card ?? descendants.OfType<ExampleCard>().FirstOrDefault(exampleCard => exampleCard.Name == link.ExampleName);
            if (example is null)
            {
                section.BringIntoView(new Rect(0, 0, section.Bounds.Width, 48));
                return;
            }

            if (link.ShowCode) example.ShowAxaml();
            if (list is not null) list.Reveal(example);
            else example.BringIntoView(new Rect(0, 0, example.Bounds.Width, 48));
        }, DispatcherPriority.ApplicationIdle);
    }

    private static ScrollViewer CreateDeferredPage(Func<Control[]> createExamples)
    {
        var page = CreatePage();
        var stack = (StackPanel)page.Content!;
        var loading = new AlderTextBlock();
        loading[!TextBlock.TextProperty] = new DynamicResourceExtension("GalleryText.Loading");
        loading[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        stack.Children.Add(loading);
        stack.AttachedToVisualTree += LoadExamples;
        return page;

        void LoadExamples(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Let the new navigation selection and page header render before building the previews.
            Dispatcher.UIThread.Post(() =>
            {
                if (!stack.IsAttachedToVisualTree() || !stack.Children.Contains(loading)) return;
                var examples = createExamples();
                stack.AttachedToVisualTree -= LoadExamples;
                stack.Children.Remove(loading);
                foreach (var example in examples)
                {
                    VirtualizeExamples(example);
                    stack.Children.Add(example);
                }
            }, DispatcherPriority.Background);
        }
    }

    private static ScrollViewer CreatePage(params Control[] groups)
    {
        var stack = new StackPanel { Spacing = 20, Classes = { "galleryPage" } };
        foreach (var group in groups)
        {
            VirtualizeExamples(group);
            stack.Children.Add(group);
        }
        return new GalleryPageScrollViewer { Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static ScrollViewer CreateGroupedPage(params Func<Control>[] groups)
    {
        var stack = new GallerySectionStack { Spacing = 20, Classes = { "galleryPage" } };
        stack.AddSections(groups.Select(group => (Func<Control>)(() =>
        {
            var control = group();
            VirtualizeExamples(control);
            return control;
        })));
        return new GalleryPageScrollViewer { Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static ScrollViewer CreateSectionPage(params (string Title, GalleryExampleSection Section, object Model)[] groups)
    {
        var sections = new GalleryExampleList();
        foreach (var (title, section, model) in groups)
        {
            section.DataContext = model;
            ExampleSource.Attach(section);
            sections.AddSection(title, CreateSectionHeading(title), section);
        }
        return CreatePage(sections);
    }

    private static void VirtualizeExamples(Control group)
    {
        if (group is StackPanel section)
        {
            foreach (var child in section.Children)
                VirtualizeExamples(child);
        }
        else if (group is UserControl { Content: StackPanel stack } &&
                 stack.Children.Count > 1 && stack.Children.All(child => child is ExampleCard))
        {
            // Retain the AXAML panel as the resource, styling, and container-query scope.
            var cards = stack.Children.ToArray();
            var list = new GalleryExampleList { Spacing = stack.Spacing };
            stack.Children.Clear();
            foreach (var card in cards)
                list.Add(card);
            stack.Children.Add(list);
        }
    }

    private static Control CreateExamples(UserControl view, object model, string? title = null, bool completeNavigationSources = false)
    {
        view.DataContext = model;
        if (completeNavigationSources) ExampleSource.AttachNavigation(view);
        else ExampleSource.Attach(view);

        if (title is null)
            return view;

        return new StackPanel { Name = title, Spacing = 12, Children = { CreateSectionHeading(title), view } };
    }

    private static AlderTextBlock CreateSectionHeading(string title)
    {
        var heading = new AlderTextBlock { FontSize = 20, FontWeight = FontWeight.SemiBold };
        heading[!TextBlock.TextProperty] = new DynamicResourceExtension("GalleryText." + title);
        heading[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderInk");
        return heading;
    }
}
