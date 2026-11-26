using System.Reflection;
using Alder.Avalonia.Controls;
using Alder.Avalonia.Gallery.Controls;
using Alder.Avalonia.Gallery.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Gallery.Views;

internal sealed class GalleryHomeView : UserControl
{
    private readonly GalleryHomeViewModel model = new();
    private readonly Action<GalleryHomeLink> openExample;
    private readonly GalleryExampleList sections = new() { Spacing = 28 };

    public GalleryHomeView(Action<GalleryHomeLink> openExample)
    {
        this.openExample = openExample;
        DataContext = model;
        AddHandler(RequestBringIntoViewEvent, KeepPreviewScrollLocal, RoutingStrategies.Bubble);

        var basics = Section("HomeBasics",
            Columns("*,*,*",
                Tile("HomeTileButtons", Buttons(), new("Button", "SampleVariants", "Button"),
                    secondary: ("ButtonGroup", new("Button", "SampleSplitAction", "ButtonGroup"))),
                Tile("HomeTileInputs", Stack(6,
                    Label("HomeProjectName", 13),
                    new AlderInput
                    {
                        Text = "Alder Gallery", Variant = InputVariant.Secondary, Width = double.NaN,
                        HorizontalAlignment = HorizontalAlignment.Stretch, [!AutomationProperties.NameProperty] = Resource("HomeProjectName")
                    },
                    new AlderSearchField
                    {
                        Variant = InputVariant.Secondary, Width = double.NaN, HorizontalAlignment = HorizontalAlignment.Stretch,
                        [!TextBox.PlaceholderTextProperty] = Resource("HomeSearchPlaceholder")
                    }), new("Input", "SampleUsage", "Input"), secondary: ("SearchField", new("Input", SectionName: "SearchField"))),
                Tile("Checkbox", Stack(6, Check("HomeCheckLayout", true), Check("HomeCheckSync", false), Check("HomeCheckPartial", null)),
                    new("Checkbox", "SampleUsage"))),
            Columns("*,*,*",
                Tile("HomeTileProgress", Progress(), new("Progress", "SampleBasic", "ProgressBar"),
                    secondary: ("Slider", new("Slider", "SampleVolume"))),
                Tile("HomeTileIdentity", Identity(), new("Avatar", SectionName: "Avatar"),
                    secondary: ("Chip", new("Chip", "SampleUsage"))),
                Tile("Alert", Alert(), new("Alert", "SampleBasic"))));

        var navigation = Section("HomeFormsAndNavigation",
            Columns("*,*,*",
                Stack(14,
                    Tile("Segment", Segment(), new("Segment", "SampleBasic"), minHeight: 112),
                    Tile("Tabs", Inline(new TabsExample { DataContext = new TabsViewModel() }, "SampleUsage"),
                        new("Tabs", "SampleUsage", "Tabs"), minHeight: 190)),
                Stack(14,
                    Tile("Autocomplete", Inline(new AutocompleteExample { DataContext = new AutocompleteViewModel() }, "SampleUsage"),
                        new("Autocomplete", "SampleUsage"), minHeight: 168),
                    Tile("DatePicker", DatePicker(), new("DateAndTime", "SampleScheduleAnAppointment", "DatePicker"), minHeight: 134)),
                Tile("HomeTileColors", Stack(10,
                        Inline(new ColorAreaExample { DataContext = new ColorsViewModel() }, "SampleUsage"),
                        Inline(new ColorPickerExample { DataContext = new ColorsViewModel() }, "SampleColorPickerFields")),
                    new("ColorArea", "SampleUsage"), secondary: ("ColorPicker", new("ColorPicker", "SampleColorPickerFields")))));

        var actionBar = new ActionBarExample();
        var employees = ((ActionBarViewModel)actionBar.DataContext!).Employees;
        employees.SetInitialSelection(employees.Rows[1], employees.Rows[3]);
        var data = Section("NavGroupDataDisplay",
            Columns("1.7*,*",
                Tile("HomeTileGridActions", Inline(actionBar, "DataGridExample"), new("ActionBar", "DataGridExample"),
                    secondary: ("DataGrid", new("DataGrid", "SampleUsers"))),
                Stack(14,
                    Tile("FileTree", Inline(new FileTreeExample(), "Icons"), new("FileTree", "Icons")),
                    Tile("EmptyState", Inline(new EmptyStateExample(), "Minimal"), new("EmptyState", "Minimal")))),
            Columns("*,*",
                Tile("AreaChart", Inline(new AreaChartExample { DataContext = new ChartViewModel() }, "SampleUsage"), new("AreaChart", "SampleUsage")),
                Tile("BarChart", Inline(new BarChartExample { DataContext = new ChartViewModel() }, "SampleUsage"), new("BarChart", "SampleUsage"))));

        var advanced = Section("HomeAdvanced",
            Tile("Agenda", Inline(new AgendaExample(), "SampleAgenda"), new("Agenda", "SampleAgenda")),
            Columns("*,*",
                Tile("Carousel", Inline(new CarouselExample(), "ProductExample"), new("Carousel", "ProductExample")),
                Stack(14,
                    Tile("HoloCard", Inline(new HoloCardExample { DataContext = new HoloCardViewModel() }, "Usage", 245), new("HoloCard", "Usage")),
                    Tile("HoverCard", Inline(new HoverCardExample { DataContext = new HoverCardViewModel() }, "Usage", 143), new("HoverCard", "Usage")))),
            Tile("FloatingToc", Inline(new FloatingTocExample(), "InPage", 300), new("FloatingToc", "InPage")));

        foreach (var section in new Control[] { Intro(basics), QuickStart(), basics, navigation, data, advanced })
            sections.Add(section);
        var page = Stack(0, sections);
        page.Classes.Add("galleryPage");
        Content = page;
    }

    private void KeepPreviewScrollLocal(object? sender, RequestBringIntoViewEventArgs e)
    {
        // Embedded selections can scroll their own controls without jumping the outer home page.
        // When keyboard focus moves into a preview, reveal it in the outer page too.
        // The overview list's own requests implement the Browse shortcut.
        if (e.TargetObject is Control container && ReferenceEquals(container.Parent, sections)) return;
        if (e.TargetObject is not Visual target ||
            TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused ||
            (target != focused && !target.IsVisualAncestorOf(focused)))
            e.Handled = true;
    }

    private Grid Intro(Control basics)
    {
        var browse = Button("HomeBrowse", ButtonVariant.Primary);
        browse.Click += (_, _) =>
        {
            sections.Reveal(basics);
            if (this.FindAncestorOfType<ScrollViewer>() is { } scroll && basics.TranslatePoint(default, scroll) is { } position)
                scroll.Offset = new Vector(0, Math.Max(0, scroll.Offset.Y + position.Y - 24));
        };
        var code = Button("HomeViewCode", ButtonVariant.Outline);
        code.Click += (_, _) => openExample(new("Button", "SampleVariants", "Button", ShowCode: true));

        var title = new AlderTextBlock
        {
            Text = "Alder.Avalonia", FontSize = 40, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetHeadingLevel(title, 1);
        var about = new AlderCard
        {
            Padding = new Thickness(26), Spacing = 13,
            Header = title,
            Content = Stack(9, Label("HomeSubtitle", 19, FontWeight.SemiBold), Label("HomeDescription", 14, muted: true),
                Wrap(new AlderChip { Content = "AXAML" }, new AlderChip { Content = "C#" },
                    new AlderChip { [!ContentControl.ContentProperty] = Resource("HomeThemes") })),
            Footer = Wrap(browse, code)
        };
        var guide = new AlderCard
        {
            Padding = new Thickness(24), Spacing = 12,
            Header = Label("HomeGuide", 19, FontWeight.SemiBold),
            Content = Stack(13, GuideStep("Preview", "HomeGuidePreview", localizedLabel: true),
                GuideStep("AXAML", "HomeGuideAxaml"), GuideStep("C#", "HomeGuideCSharp")),
            Footer = Label("HomeGuideSearch", 13, muted: true)
        };
        return Columns("1.55*,*", about, guide);
    }

    private static Grid GuideStep(string label, string description, bool localizedLabel = false)
    {
        var chip = new AlderChip { HorizontalAlignment = HorizontalAlignment.Left };
        if (localizedLabel) chip[!ContentControl.ContentProperty] = Resource(label);
        else chip.Content = label;
        var text = Label(description, 13, muted: true);
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { ColumnDefinitions = new("78,*"), ColumnSpacing = 12, Children = { chip, text } };
        Grid.SetColumn(text, 1);
        return row;
    }

    private static Control QuickStart()
    {
        var version = typeof(AlderButton).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+', 2)[0];
        var install = string.IsNullOrEmpty(version) ? "dotnet package add Alder.Avalonia" :
            $"dotnet package add Alder.Avalonia --version {version}";
        const string theme = """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:alderThemes="using:Alder.Avalonia.Themes">
              <Application.Styles>
                <FluentTheme />
                <alderThemes:AlderTheme />
              </Application.Styles>
            </Application>
            """;
        const string button = """
            <StackPanel xmlns="https://github.com/avaloniaui"
                        xmlns:alder="using:Alder.Avalonia.Controls">
              <alder:AlderButton Variant="Primary" Content="Save" />
            </StackPanel>
            """;

        return Section("HomeQuickStart", new AlderCard
        {
            Padding = new Thickness(24),
            Content = Stack(18,
                SetupStep("HomeInstallAlder", "HomeInstallAlderHint", install, "shell"),
                Columns("*,*",
                    SetupStep("HomeRegisterTheme", "HomeRegisterThemeHint", theme, "xml"),
                    SetupStep("HomeAddControl", "HomeAddControlHint", button, "xml")))
        });
    }

    private static Control SetupStep(string title, string hint, string code, string language)
    {
        var heading = Label(title, 16, FontWeight.SemiBold);
        AutomationProperties.SetHeadingLevel(heading, 3);
        return Stack(6, heading, Label(hint, 13, muted: true), new AlderCodeBlock
        {
            Code = code,
            Language = language,
            [!AutomationProperties.NameProperty] = Resource(title)
        });
    }

    private Control Buttons()
    {
        var confirm = Button("HomeConfirm", ButtonVariant.Primary);
        confirm.Command = model.Save;
        var cancel = Button("HomeCancel", ButtonVariant.Secondary);
        cancel.Command = model.Cancel;
        var sample = Label("HomeAlignmentPreview", 13, muted: true);
        sample.Bind(Layoutable.HorizontalAlignmentProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.Alignment));
        var group = new AlderButtonGroup();
        foreach (var (key, alignment) in new[]
                 {
                     ("HomeLeft", HorizontalAlignment.Left), ("HomeCenter", HorizontalAlignment.Center), ("HomeRight", HorizontalAlignment.Right)
                 })
        {
            var button = Button(key, ButtonVariant.Outline);
            button.Command = model.Align;
            button.CommandParameter = alignment;
            group.Children.Add(button);
        }
        return Stack(10, Wrap(confirm, cancel), group, sample);
    }

    private static AlderCheckBox Check(string text, bool? value) => new()
    {
        [!ContentControl.ContentProperty] = Resource(text), Variant = CheckBoxVariant.Secondary,
        IsChecked = value, IsThreeState = value is null
    };

    private static Control Progress()
    {
        var progress = new AlderProgressBar { [!AlderProgressBar.LabelProperty] = Resource("HomeUpload"), Color = FeedbackColor.Accent };
        var slider = new AlderSlider
        {
            Maximum = 100,
            [!Control.ThemeProperty] = new DynamicResourceExtension("AlderSliderTheme"),
            [!AutomationProperties.NameProperty] = Resource("HomeUpload")
        };
        progress.Bind(AlderProgressBar.ValueProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.Progress));
        slider.Bind(Slider.ValueProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.Progress, mode: BindingMode.TwoWay));
        return Stack(11, progress, slider);
    }

    private static Control Identity() => Stack(12,
        Wrap(new AlderAvatar { Fallback = "AL" }, new AlderAvatar { Fallback = "MJ" }, new AlderAvatar { Fallback = "KS" }),
        Wrap(Status("HomePublished", AlderStatusColor.Success), Status("HomeDraft", AlderStatusColor.Default),
            Status("HomeReview", AlderStatusColor.Warning)));

    private static AlderChip Status(string key, AlderStatusColor color) =>
        new() { [!ContentControl.ContentProperty] = Resource(key), Color = color };

    private static AlderAlert Alert()
    {
        var alert = new AlderAlert();
        alert.Bind(AlderAlert.TitleProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.FeedbackTitle));
        alert.Bind(AlderAlert.DescriptionProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.FeedbackDescription));
        alert.Bind(AlderAlert.ColorProperty, CompiledBinding.Create((GalleryHomeViewModel vm) => vm.FeedbackColor));
        return alert;
    }

    private static AlderSegment Segment() => new()
    {
        SelectedKey = "overview", HorizontalAlignment = HorizontalAlignment.Left,
        [!AutomationProperties.NameProperty] = Resource("Segment"),
        Items =
        {
            new AlderSegmentItem { Key = "overview", [!ContentControl.ContentProperty] = Resource("HomeOverview") },
            new AlderSegmentItem { Key = "activity", [!ContentControl.ContentProperty] = Resource("HomeActivity") },
            new AlderSegmentItem { Key = "settings", [!ContentControl.ContentProperty] = Resource("HomeSettings") }
        }
    };

    private static Control DatePicker() => Stack(7, Label("HomeProjectDate", 13),
        new AlderDatePicker
        {
            HorizontalAlignment = HorizontalAlignment.Left, Value = DateOnly.FromDateTime(DateTime.Today),
            [!AlderDatePicker.CultureProperty] = new DynamicResourceExtension("GalleryCulture"),
            [!AutomationProperties.NameProperty] = Resource("HomeProjectDate")
        }, Label("HomeDateHint", 12, muted: true));

    private static Control Inline(UserControl view, string name, double? maxHeight = null)
    {
        var featured = view.FindControl<ExampleCard>(name) ?? throw new InvalidOperationException($"Missing home preview {name}.");
        foreach (var card in view.GetLogicalDescendants().OfType<ExampleCard>())
            if (!ReferenceEquals(card, featured)) card.IsVisible = false;
        featured.Classes.Add("inline-preview");
        if (maxHeight is { } limit && featured.Preview is Control preview)
        {
            preview.MaxHeight = limit;
            if (preview.MinHeight > limit) preview.MinHeight = limit;
            if (preview.Height > limit) preview.Height = limit;
        }
        return view;
    }

    private AlderCard Tile(string title, Control preview, GalleryHomeLink link, double minHeight = 0,
        (string Label, GalleryHomeLink Link)? secondary = null)
    {
        var button = new AlderButton
        {
            Size = AlderSize.Small, Variant = ButtonVariant.Ghost, IsIconOnly = true,
            [!AlderButton.IconProperty] = new DynamicResourceExtension("AlderIconArrowRight"),
            [!AutomationProperties.NameProperty] = Resource(title),
            [!AlderToolTip.TipProperty] = Resource("HomeOpenExample")
        };
        button.Click += (_, _) => openExample(link);
        var heading = Label(title, 16, FontWeight.SemiBold);
        heading.VerticalAlignment = VerticalAlignment.Center;
        var header = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8, Children = { heading, button } };
        Grid.SetColumn(button, 1);
        var card = new AlderCard { Header = header, Content = preview, Padding = new Thickness(18), Spacing = 10, MinHeight = minHeight };
        if (secondary is { } entry)
        {
            var more = Button(entry.Label, ButtonVariant.Ghost);
            more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Click += (_, _) => openExample(entry.Link);
            card.Footer = more;
        }
        return card;
    }

    private static StackPanel Section(string key, params Control[] rows)
    {
        var heading = Label(key, 24, FontWeight.SemiBold);
        AutomationProperties.SetHeadingLevel(heading, 2);
        return Stack(14, [heading, .. rows]);
    }

    private static Grid Columns(string wideWidths, params Control[] children)
    {
        var grid = new Grid { ColumnSpacing = 14, RowSpacing = 14 };
        grid.Children.AddRange(children);
        int activeColumns = 0;
        Layout(children.Length);
        grid.SizeChanged += (_, e) =>
        {
            var width = e.NewSize.Width;
            Layout(width < 610 ? 1 : children.Length == 3 && width < 890 ? 2 : children.Length);
        };
        return grid;

        void Layout(int count)
        {
            if (count == activeColumns) return;
            activeColumns = count;
            grid.ColumnDefinitions = count == children.Length ? new(wideWidths) : new(count == 1 ? "*" : "*,*");
            grid.RowDefinitions.Clear();
            for (int i = 0; i < (children.Length + count - 1) / count; i++)
                grid.RowDefinitions.Add(new(GridLength.Auto));
            for (int i = 0; i < children.Length; i++)
            {
                Grid.SetRow(children[i], i / count);
                Grid.SetColumn(children[i], i % count);
            }
        }
    }

    private static StackPanel Stack(double spacing, params Control[] children)
    {
        var stack = new StackPanel { Spacing = spacing };
        stack.Children.AddRange(children);
        return stack;
    }

    private static WrapPanel Wrap(params Control[] children)
    {
        var panel = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        panel.Children.AddRange(children);
        return panel;
    }

    private static TextBlock Label(string key, double size, FontWeight? weight = null, bool muted = false) =>
        new()
        {
            FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = Resource(key),
            [!TextBlock.ForegroundProperty] = new DynamicResourceExtension(muted ? "AlderMuted" : "AlderInk")
        };

    private static AlderButton Button(string key, ButtonVariant variant) =>
        new() { Size = AlderSize.Small, Variant = variant, [!ContentControl.ContentProperty] = Resource(key) };

    private static DynamicResourceExtension Resource(string key) => new("GalleryText." + key);
}
