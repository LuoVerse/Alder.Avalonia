using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Alder.Avalonia.Controls;

/// <summary>A page button, or an ellipsis when PageNumber is null.</summary>
public readonly record struct AlderPaginationItem(int? PageNumber, string? Label = null, bool IsEnabled = true);

/// <summary>One-based page navigation. CurrentPage is zero when PageCount is zero.</summary>
public sealed class AlderPagination : TemplatedControl
{
    /// <summary>Identifies the <see cref="PageCount"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<AlderPagination, int>(nameof(PageCount), 1, validate: value => value >= 0);
    /// <summary>Identifies the <see cref="CurrentPage"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<AlderPagination, int>(nameof(CurrentPage), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: (owner, page) => ((AlderPagination)owner).ClampPage(page));
    /// <summary>Identifies the <see cref="SiblingCount"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> SiblingCountProperty =
        AvaloniaProperty.Register<AlderPagination, int>(nameof(SiblingCount), 1, validate: value => value is >= 0 and <= 5);
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderPagination, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="ShowPageNumbers"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowPageNumbersProperty =
        AvaloniaProperty.Register<AlderPagination, bool>(nameof(ShowPageNumbers), true);
    /// <summary>Identifies the <see cref="ShowNavigationLabels"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowNavigationLabelsProperty =
        AvaloniaProperty.Register<AlderPagination, bool>(nameof(ShowNavigationLabels), true);
    /// <summary>Identifies the <see cref="Summary"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SummaryProperty =
        AvaloniaProperty.Register<AlderPagination, object?>(nameof(Summary));
    /// <summary>Identifies the <see cref="SummaryTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> SummaryTemplateProperty =
        AvaloniaProperty.Register<AlderPagination, IDataTemplate?>(nameof(SummaryTemplate));
    /// <summary>Identifies the <see cref="PageItems"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderPaginationItem>?> PageItemsProperty =
        AvaloniaProperty.Register<AlderPagination, IReadOnlyList<AlderPaginationItem>?>(nameof(PageItems));
    /// <summary>Identifies the <see cref="IsPreviousDisabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsPreviousDisabledProperty =
        AvaloniaProperty.Register<AlderPagination, bool>(nameof(IsPreviousDisabled));
    /// <summary>Identifies the <see cref="IsNextDisabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsNextDisabledProperty =
        AvaloniaProperty.Register<AlderPagination, bool>(nameof(IsNextDisabled));
    /// <summary>Identifies the <see cref="ActivePageVariant"/> Avalonia property.</summary>
    public static readonly StyledProperty<ButtonVariant> ActivePageVariantProperty =
        AvaloniaProperty.Register<AlderPagination, ButtonVariant>(nameof(ActivePageVariant), ButtonVariant.Tertiary);
    /// <summary>Identifies the <see cref="NavigationAlignment"/> Avalonia property.</summary>
    public static readonly StyledProperty<HorizontalAlignment> NavigationAlignmentProperty =
        AvaloniaProperty.Register<AlderPagination, HorizontalAlignment>(nameof(NavigationAlignment), HorizontalAlignment.Left);
    /// <summary>Identifies the <see cref="NavigationIconSize"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> NavigationIconSizeProperty =
        AvaloniaProperty.Register<AlderPagination, double>(nameof(NavigationIconSize), 16,
            validate: size => double.IsFinite(size) && size > 0);
    /// <summary>Identifies the <see cref="PreviousText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> PreviousTextProperty =
        AvaloniaProperty.Register<AlderPagination, string>(nameof(PreviousText), "Previous");
    /// <summary>Identifies the <see cref="NextText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> NextTextProperty =
        AvaloniaProperty.Register<AlderPagination, string>(nameof(NextText), "Next");
    /// <summary>Identifies the <see cref="PageNameFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> PageNameFormatProperty =
        AvaloniaProperty.Register<AlderPagination, string>(nameof(PageNameFormat), "Page {0}");
    /// <summary>Identifies the <see cref="CurrentPageNameFormat"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> CurrentPageNameFormatProperty =
        AvaloniaProperty.Register<AlderPagination, string>(nameof(CurrentPageNameFormat), "Page {0}, current page");
    /// <summary>Identifies the <see cref="PreviousIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> PreviousIconProperty =
        AvaloniaProperty.Register<AlderPagination, Geometry?>(nameof(PreviousIcon));
    /// <summary>Identifies the <see cref="NextIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> NextIconProperty =
        AvaloniaProperty.Register<AlderPagination, Geometry?>(nameof(NextIcon));
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<AlderPagination, CultureInfo?>(nameof(Culture));

    private Panel? pages;
    private Button? previous;
    private Button? next;
    private INotifyCollectionChanged? observedPageItems;

    /// <summary>The number of available pages.</summary>
    public int PageCount { get => GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }
    /// <summary>The selected one-based page number.</summary>
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    /// <summary>The number of neighboring page buttons to show on either side of the current page.</summary>
    public int SiblingCount { get => GetValue(SiblingCountProperty); set => SetValue(SiblingCountProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Whether to show the page numbers.</summary>
    public bool ShowPageNumbers { get => GetValue(ShowPageNumbersProperty); set => SetValue(ShowPageNumbersProperty, value); }
    /// <summary>Whether to show the navigation labels.</summary>
    public bool ShowNavigationLabels { get => GetValue(ShowNavigationLabelsProperty); set => SetValue(ShowNavigationLabelsProperty, value); }
    /// <summary>Custom content summarizing the currently visible range of items.</summary>
    public object? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    /// <summary>The data template for rendering the summary content.</summary>
    public IDataTemplate? SummaryTemplate { get => GetValue(SummaryTemplateProperty); set => SetValue(SummaryTemplateProperty, value); }
    /// <summary>The generated pagination entries, including ellipses where pages are collapsed.</summary>
    public IReadOnlyList<AlderPaginationItem>? PageItems { get => GetValue(PageItemsProperty); set => SetValue(PageItemsProperty, value); }
    /// <summary>Whether navigation to the previous page is unavailable.</summary>
    public bool IsPreviousDisabled { get => GetValue(IsPreviousDisabledProperty); set => SetValue(IsPreviousDisabledProperty, value); }
    /// <summary>Whether navigation to the next page is unavailable.</summary>
    public bool IsNextDisabled { get => GetValue(IsNextDisabledProperty); set => SetValue(IsNextDisabledProperty, value); }
    /// <summary>The button variant used for the current page.</summary>
    public ButtonVariant ActivePageVariant { get => GetValue(ActivePageVariantProperty); set => SetValue(ActivePageVariantProperty, value); }
    /// <summary>The alignment of the previous and next page buttons.</summary>
    public HorizontalAlignment NavigationAlignment { get => GetValue(NavigationAlignmentProperty); set => SetValue(NavigationAlignmentProperty, value); }
    /// <summary>The size of the previous and next icons in device-independent pixels.</summary>
    public double NavigationIconSize { get => GetValue(NavigationIconSizeProperty); set => SetValue(NavigationIconSizeProperty, value); }
    /// <summary>The label for navigating to the previous page.</summary>
    public string PreviousText { get => GetValue(PreviousTextProperty); set => SetValue(PreviousTextProperty, value); }
    /// <summary>The label for navigating to the next page.</summary>
    public string NextText { get => GetValue(NextTextProperty); set => SetValue(NextTextProperty, value); }
    /// <summary>The format string used for a page button's accessible name.</summary>
    public string PageNameFormat { get => GetValue(PageNameFormatProperty); set => SetValue(PageNameFormatProperty, value); }
    /// <summary>The format string used for the current page button's accessible name.</summary>
    public string CurrentPageNameFormat { get => GetValue(CurrentPageNameFormatProperty); set => SetValue(CurrentPageNameFormatProperty, value); }
    /// <summary>The geometry used for the previous icon.</summary>
    public Geometry? PreviousIcon { get => GetValue(PreviousIconProperty); set => SetValue(PreviousIconProperty, value); }
    /// <summary>The geometry used for the next icon.</summary>
    public Geometry? NextIcon { get => GetValue(NextIconProperty); set => SetValue(NextIconProperty, value); }
    /// <summary>The culture used to format or interpret displayed values.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (previous is not null) previous.Click -= PreviousClicked;
        if (next is not null) next.Click -= NextClicked;
        if (pages is not null)
        {
            foreach (var button in pages.Children.OfType<AlderButton>())
                button.Click -= PageClicked;
        }

        pages = e.NameScope.Find<Panel>("PART_Pages");
        previous = e.NameScope.Find<Button>("PART_Previous");
        next = e.NameScope.Find<Button>("PART_Next");
        if (previous is not null) previous.Click += PreviousClicked;
        if (next is not null) next.Click += NextClicked;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ObservePageItems(PageItems as INotifyCollectionChanged);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        ObservePageItems(null);
        base.OnDetachedFromLogicalTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageCountProperty) CoerceValue(CurrentPageProperty);
        if (change.Property == PageItemsProperty && ((ILogical)this).IsAttachedToLogicalTree)
            ObservePageItems(PageItems as INotifyCollectionChanged);
        if (change.Property == BoundsProperty || change.Property == SummaryProperty)
            PseudoClasses.Set(":wide-summary", Summary is not null && Bounds.Width >= 600);
        if (change.Property == BoundsProperty || change.Property == ShowPageNumbersProperty || change.Property == PageCountProperty)
            PseudoClasses.Set(":compact-navigation", ShowPageNumbers && PageCount > 3 && Bounds.Width is > 0 and < 376);
        if (change.Property == PageCountProperty || change.Property == CurrentPageProperty ||
            change.Property == SiblingCountProperty || change.Property == ShowPageNumbersProperty ||
            change.Property == SizeProperty || change.Property == CultureProperty || change.Property == FontSizeProperty ||
            change.Property == PageItemsProperty || change.Property == ActivePageVariantProperty ||
            change.Property == IsPreviousDisabledProperty || change.Property == IsNextDisabledProperty ||
            change.Property == PageNameFormatProperty || change.Property == CurrentPageNameFormatProperty)
            Refresh();
    }

    private void ObservePageItems(INotifyCollectionChanged? source)
    {
        if (observedPageItems == source) return;
        if (observedPageItems is not null) observedPageItems.CollectionChanged -= OnPageItemsChanged;
        observedPageItems = source;
        if (source is not null) source.CollectionChanged += OnPageItemsChanged;
    }

    private void OnPageItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private int ClampPage(int page) => PageCount == 0 ? 0 : Math.Clamp(page, 1, PageCount);

    private void PreviousClicked(object? sender, RoutedEventArgs e)
    {
        if (!IsPreviousDisabled) MoveTo(CurrentPage - 1);
    }

    private void NextClicked(object? sender, RoutedEventArgs e)
    {
        if (!IsNextDisabled && CurrentPage < PageCount) MoveTo(CurrentPage + 1);
    }

    private void PageClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is AlderButton { Tag: int page, IsEffectivelyEnabled: true }) MoveTo(page);
    }

    private void MoveTo(int page)
    {
        if (!IsEffectivelyEnabled || page < 1 || page > PageCount || page == CurrentPage) return;
        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        SetCurrentValue(CurrentPageProperty, page);
        if ((ReferenceEquals(focus, previous) || ReferenceEquals(focus, next)) &&
            focus is Control { IsEffectivelyEnabled: false })
        {
            var current = pages?.Children.OfType<AlderButton>().FirstOrDefault(button =>
                Equals(button.Tag, CurrentPage) && button.IsEffectivelyEnabled);
            (current as Button ?? (next?.IsEffectivelyEnabled == true ? next : previous))?.Focus();
        }
    }

    private void Refresh()
    {
        previous?.SetCurrentValue(IsEnabledProperty, !IsPreviousDisabled && CurrentPage > 1);
        next?.SetCurrentValue(IsEnabledProperty, !IsNextDisabled && CurrentPage < PageCount);
        if (pages is null) return;

        var children = pages.Children;
        var existing = children.OfType<AlderButton>().ToDictionary(button => (int)button.Tag!);
        var gaps = new Queue<TextBlock>(children.OfType<TextBlock>());
        var desired = new List<Control>();
        AlderButton? currentButton = null;
        int width = Size switch { AlderSize.Small => 28, AlderSize.Large => 36, _ => 32 };
        var culture = Culture ?? CultureInfo.CurrentCulture;
        foreach (var item in VisibleItems())
        {
            if (item.PageNumber is not int page)
            {
                var ellipsis = gaps.TryDequeue(out var gap) ? gap : CreateEllipsis();
                ellipsis.Width = width;
                ellipsis.FontSize = FontSize;
                ellipsis.Text = item.Label ?? "…";
                desired.Add(ellipsis);
                continue;
            }
            if (!existing.TryGetValue(page, out var button))
            {
                button = new AlderButton { Tag = page, Padding = new Thickness(0), MinHeight = 0, Cursor = new Cursor(StandardCursorType.Hand) };
                button[!ThemeProperty] = new DynamicResourceExtension("AlderPaginationButtonTheme");
                button.Click += PageClicked;
            }

            string label = page.ToString(culture);
            bool current = page == CurrentPage;
            button.Size = Size;
            button.Width = button.Height = width;
            button.FontSize = FontSize;
            button.IsEnabled = item.IsEnabled;
            button.Content = item.Label ?? label;
            button.Variant = current ? ActivePageVariant : ButtonVariant.Ghost;
            AutomationProperties.SetName(button, string.Format(culture, current ? CurrentPageNameFormat : PageNameFormat, label));
            if (current && item.IsEnabled) currentButton = button;
            desired.Add(button);
        }

        bool restoreFocus = false;
        for (int index = children.Count - 1; index >= 0; index--)
        {
            if (desired.Contains(children[index])) continue;
            if (children[index] is AlderButton button) button.Click -= PageClicked;
            restoreFocus |= children[index].IsKeyboardFocusWithin;
            children.RemoveAt(index);
        }
        for (int index = 0; index < desired.Count; index++)
        {
            int previousIndex = children.IndexOf(desired[index]);
            if (previousIndex < 0) children.Insert(index, desired[index]);
            else if (previousIndex != index) children.Move(previousIndex, index);
        }

        if (restoreFocus)
            (currentButton as Button ?? (next?.IsEnabled == true ? next : previous))?.Focus();
    }

    private static TextBlock CreateEllipsis()
    {
        var ellipsis = new AlderTextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        AutomationProperties.SetAccessibilityView(ellipsis, AccessibilityView.Raw);
        ellipsis[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("AlderMuted");
        return ellipsis;
    }

    private IEnumerable<AlderPaginationItem> VisibleItems()
    {
        if (!ShowPageNumbers || PageCount == 0) yield break;
        if (PageItems is { } items)
        {
            var seen = new HashSet<int>();
            foreach (var item in items)
                if (item.PageNumber is null || (item.PageNumber >= 1 && item.PageNumber <= PageCount && seen.Add(item.PageNumber.Value)))
                    yield return item;
            yield break;
        }
        foreach (int page in VisiblePages())
            yield return new AlderPaginationItem(page == 0 ? null : page);
    }

    private IEnumerable<int> VisiblePages()
    {
        long budget = 2L * SiblingCount + 5;
        if (PageCount <= budget)
        {
            for (int page = 1; page <= PageCount; page++) yield return page;
            yield break;
        }

        // Keep both endpoints. Show a single skipped page in place of an ellipsis.
        long first = Math.Max(2, (long)CurrentPage - SiblingCount);
        long last = Math.Min(PageCount - 1L, (long)CurrentPage + SiblingCount);
        if (first == 3) first = 2;
        if (last == PageCount - 2L) last++;

        yield return 1;
        if (first > 2) yield return 0;
        for (long page = first; page <= last; page++) yield return (int)page;
        if (last < PageCount - 1L) yield return 0;
        yield return PageCount;
    }
}
