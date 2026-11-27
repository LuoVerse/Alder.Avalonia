using System.Buffers;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>A named group of actions on a command palette page.</summary>
public sealed class AlderCommandGroup
{
    /// <summary>The optional label identifying this group in the palette.</summary>
    public string? Heading { get; init; }
    /// <summary>The entries offered by this group.</summary>
    public IReadOnlyList<AlderCommandItem> Items { get; init; } = [];
}

/// <summary>An action or entry point to a nested command palette page.</summary>
public sealed class AlderCommandItem
{
    /// <summary>The text that identifies the control or value.</summary>
    public string Label { get; init; } = "";
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get; init; }
    /// <summary>Additional search terms used to match the item.</summary>
    public string? Keywords { get; init; }
    /// <summary>The optional geometry displayed with the content.</summary>
    public object? Icon { get; init; }
    /// <summary>Keyboard hint displayed beside the action.</summary>
    public string? Shortcut { get; init; }
    /// <summary>Whether the item or action is available for interaction.</summary>
    public bool IsEnabled { get; init; } = true;
    /// <summary>The command run when the item or action is invoked.</summary>
    public ICommand? Command { get; init; }
    /// <summary>The argument passed to the command when it is invoked.</summary>
    public object? CommandParameter { get; init; }
    /// <summary>The optional subgroups opened by invoking this item.</summary>
    public IReadOnlyList<AlderCommandGroup>? Groups { get; init; }

    private string? searchText;

    internal string SearchText => searchText ??= Keywords is null && Description is null
        ? Label : string.Concat(Label, " ", Keywords, " ", Description);
    internal bool CanInvoke => IsEnabled && (Groups is not null || Command?.CanExecute(CommandParameter) != false);
}

/// <summary>A searchable command palette that can be embedded or shown in a DialogHost.</summary>
public sealed class AlderCommand : TemplatedControl
{
    /// <summary>Identifies the <see cref="Groups"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderCommandGroup>> GroupsProperty =
        AvaloniaProperty.Register<AlderCommand, IReadOnlyList<AlderCommandGroup>>(nameof(Groups), []);
    /// <summary>Identifies the <see cref="Query"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<AlderCommand, string?>(nameof(Query), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="Placeholder"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AlderCommand, string?>(nameof(Placeholder));
    /// <summary>Identifies the <see cref="Header"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<AlderCommand, object?>(nameof(Header));
    /// <summary>Identifies the <see cref="Footer"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderCommand, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="SearchFooter"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SearchFooterProperty =
        AvaloniaProperty.Register<AlderCommand, object?>(nameof(SearchFooter));
    /// <summary>Identifies the <see cref="PreviewTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PreviewTemplateProperty =
        AvaloniaProperty.Register<AlderCommand, IDataTemplate?>(nameof(PreviewTemplate));
    /// <summary>Identifies the <see cref="ShowDismissButton"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowDismissButtonProperty =
        AvaloniaProperty.Register<AlderCommand, bool>(nameof(ShowDismissButton), true);
    /// <summary>Identifies the <see cref="EmptyContent"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<AlderCommand, object?>(nameof(EmptyContent));
    /// <summary>Identifies the <see cref="Size"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderSize> SizeProperty =
        AvaloniaProperty.Register<AlderCommand, AlderSize>(nameof(Size), AlderSize.Medium);
    /// <summary>Identifies the <see cref="Filter"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<AlderCommandItem, string, bool>?> FilterProperty =
        AvaloniaProperty.Register<AlderCommand, Func<AlderCommandItem, string, bool>?>(nameof(Filter));
    /// <summary>Identifies the <see cref="ActiveItem"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderCommand, AlderCommandItem?> ActiveItemProperty =
        AvaloniaProperty.RegisterDirect<AlderCommand, AlderCommandItem?>(nameof(ActiveItem), control => control.ActiveItem);

    internal static readonly DirectProperty<AlderCommand, string?> PageTitleProperty =
        AvaloniaProperty.RegisterDirect<AlderCommand, string?>(nameof(PageTitle), control => control.PageTitle);
    internal static readonly DirectProperty<AlderCommand, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<AlderCommand, bool>(nameof(IsEmpty), control => control.IsEmpty);
    internal static readonly DirectProperty<AlderCommand, bool> IsModalProperty =
        AvaloniaProperty.RegisterDirect<AlderCommand, bool>(nameof(IsModal), control => control.IsModal);

    /// <summary>The root groups and items shown by the command palette.</summary>
    public IReadOnlyList<AlderCommandGroup> Groups { get => GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }
    /// <summary>The search text used to filter the current level of command items.</summary>
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }
    /// <summary>The text displayed when no user input has been entered.</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    /// <summary>The optional content displayed in the header.</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    /// <summary>The optional content displayed in the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>Optional content between the search row and the results, such as search filters.</summary>
    public object? SearchFooter { get => GetValue(SearchFooterProperty); set => SetValue(SearchFooterProperty, value); }
    /// <summary>Displays a preview of the active action next to the results.</summary>
    public IDataTemplate? PreviewTemplate { get => GetValue(PreviewTemplateProperty); set => SetValue(PreviewTemplateProperty, value); }
    /// <summary>Whether to show the palette's close button.</summary>
    public bool ShowDismissButton { get => GetValue(ShowDismissButtonProperty); set => SetValue(ShowDismissButtonProperty, value); }
    /// <summary>The enabled item targeted by keyboard navigation.</summary>
    public AlderCommandItem? ActiveItem { get => field; private set => SetAndRaise(ActiveItemProperty, ref field, value); }
    /// <summary>The content displayed when no command matches the current query.</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    /// <summary>The size preset used for the control.</summary>
    public AlderSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>A predicate that decides whether a command item matches the current query.</summary>
    public Func<AlderCommandItem, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    internal string? PageTitle { get => field; private set => SetAndRaise(PageTitleProperty, ref field, value); }
    internal bool IsEmpty { get => field; private set => SetAndRaise(IsEmptyProperty, ref field, value); }
    internal bool IsModal { get => field; private set => SetAndRaise(IsModalProperty, ref field, value); }

    /// <summary>Raised when a command item is invoked.</summary>
    public event EventHandler<AlderCommandItem>? ItemInvoked;

    private readonly Stack<(AlderCommandItem Item, string? Query)> pages = new();
    private readonly HashSet<INotifyCollectionChanged> observedCollections = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ICommand> observedCommands = new(ReferenceEqualityComparer.Instance);
    private List<object> rows = [];
    private AlderSearchField? search;
    private AlderCommandList? list;
    private Grid? body;
    private Button? back;
    private Button? dismiss;
    private AlderCommandItem? pendingItem;
    private IReadOnlyList<AlderCommandGroup> CurrentGroups => pages.TryPeek(out var page) ? page.Item.Groups! : Groups;

    /// <summary>Initializes a new instance of <see cref="AlderCommand"/>.</summary>
    public AlderCommand() => AddHandler(KeyDownEvent, OnPaletteKeyDown, RoutingStrategies.Tunnel);

    /// <summary>Shows an unattached palette and invokes the chosen action after the modal closes.</summary>
    public async Task<DialogResult> ShowAsync(Control owner, DialogBackdrop backdrop = DialogBackdrop.Opaque,
        bool canLightDismiss = true)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();
        if (IsModal || Parent is not null || this.IsAttachedToVisualTree() || this.GetVisualParent() is not null)
            throw new InvalidOperationException("The command palette must be unattached before it can open.");

        IBrush? backdropBrush = backdrop != DialogBackdrop.Transparent &&
            owner.TryFindResource("AlderCommandBackdrop", out var brush) ? brush as IBrush : null;
        IsModal = true;
        DialogResult result;
        AlderCommandItem? chosen;
        try
        {
            result = await Dialog.ShowModalAsync(this, new DialogOptions
            {
                MaxWidth = Size switch { AlderSize.Small => 384, AlderSize.Large => 576, _ => 512 },
                IsContentFullBleed = true,
                TopOffsetRatio = .15,
                ShowCloseButton = false,
                CanEscapeDismiss = false,
                CanLightDismiss = canLightDismiss,
                Backdrop = backdrop,
                BackdropBrush = backdropBrush,
                Animation = new()
                {
                    EnterDuration = TimeSpan.FromMilliseconds(200), BackdropEnterDuration = TimeSpan.FromMilliseconds(150),
                    EnterScale = .95, EnterOffset = new Vector(0, -8)
                }
            }, owner);
            chosen = pendingItem;
        }
        finally
        {
            pendingItem = null;
            IsModal = false;
            if (pages.Count > 0)
            {
                var rootQuery = pages.Last().Query;
                pages.Clear();
                PageTitle = null;
                SetPageQuery(rootQuery);
            }
        }
        if (result == DialogResult.OK && chosen is { CanInvoke: true }) Execute(chosen);
        return result;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (list is not null)
        {
            list.Invoke = null;
            list.SelectionChanged -= ActiveRowChanged;
        }
        if (back is not null) back.Click -= BackClicked;
        if (dismiss is not null) dismiss.Click -= DismissClicked;
        base.OnApplyTemplate(e);
        search = e.NameScope.Find<AlderSearchField>("PART_Search");
        list = e.NameScope.Find<AlderCommandList>("PART_Results");
        body = e.NameScope.Find<Grid>("PART_Body");
        back = e.NameScope.Find<Button>("PART_Back");
        dismiss = e.NameScope.Find<Button>("PART_Dismiss");
        if (list is not null)
        {
            list.Invoke = Activate;
            list.SelectionChanged += ActiveRowChanged;
        }
        if (back is not null) back.Click += BackClicked;
        if (dismiss is not null) dismiss.Click += DismissClicked;
        UpdateBodyColumns();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObservePage();
        Refresh();
        if (IsModal)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsModal && this.IsAttachedToVisualTree()) search?.Focus();
            }, DispatcherPriority.Loaded);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopObserving();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupsProperty)
        {
            pages.Clear();
            PageTitle = null;
            ObservePage();
            Refresh();
        }
        else if (change.Property == QueryProperty || change.Property == FilterProperty)
            Refresh();
        else if (change.Property == PreviewTemplateProperty)
            UpdateBodyColumns();
    }

    private void UpdateBodyColumns()
    {
        if (body is not null)
            body.ColumnDefinitions = PreviewTemplate is null ? new("*,Auto") : new("2*,3*");
    }

    private void ActiveRowChanged(object? sender, SelectionChangedEventArgs e) =>
        ActiveItem = list?.SelectedItem as AlderCommandItem;

    private void Refresh(AlderCommandItem? selection = null)
    {
        if (list is null) return;
        var query = Query?.Trim() ?? "";
        var filter = Filter;
        var terms = filter is null
            ? query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(term => (Text: term, Offsets: StringInfo.ParseCombiningCharacters(term))).ToArray()
            : [];
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var groups = CurrentGroups;
        int count = filter is null && query.Length != 0 ? groups.Sum(group => group.Items.Count) : 0;
        var matches = SearchWork.CanPartition(count) ? MatchGroups(groups, count, terms, compare) : null;
        // Keep the original action references and section headings. Realized containers build their own binding data.
        var results = new List<object>();
        int index = 0;
        foreach (var group in groups)
        {
            int start = results.Count;
            foreach (var item in group.Items)
            {
                if (matches is not null ? matches[index++] : query.Length == 0 ||
                    (filter?.Invoke(item, query) ?? Matches(item.SearchText, terms, compare)))
                    results.Add(item);
            }
            if (results.Count > start && !string.IsNullOrWhiteSpace(group.Heading))
                results.Insert(start, new AlderCommandHeading(group.Heading, start > 0));
        }

        rows = results;
        list.ItemsSource = rows;
        IsEmpty = rows.Count == 0;
        int activeIndex = selection is { CanInvoke: true } ? results.IndexOf(selection) : -1;
        SetActive(activeIndex >= 0 ? activeIndex : results.FindIndex(row => row is AlderCommandItem { CanInvoke: true }));
    }

    private static bool[] MatchGroups(IReadOnlyList<AlderCommandGroup> groups, int count,
        (string Text, int[] Offsets)[] terms, CompareInfo compare)
    {
        var text = ArrayPool<string>.Shared.Rent(count);
        try
        {
            int index = 0;
            foreach (var group in groups)
                foreach (var item in group.Items) text[index++] = item.SearchText;
            return SearchWork.Match(count, i => Matches(text[i], terms, compare));
        }
        finally { ArrayPool<string>.Shared.Return(text, clearArray: true); }
    }

    private static bool Matches(string searchable, (string Text, int[] Offsets)[] terms, CompareInfo compare)
    {
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth;
        foreach (var (term, offsets) in terms)
        {
            if (compare.IndexOf(searchable, term, options) >= 0) continue;
            int position = 0;
            for (int letter = 0; letter < offsets.Length; letter++)
            {
                int end = letter + 1 < offsets.Length ? offsets[letter + 1] : term.Length;
                var element = term.AsSpan(offsets[letter], end - offsets[letter]);
                int index = compare.IndexOf(searchable.AsSpan(position), element, options);
                if (index < 0) return false;
                position += index + element.Length;
                if (position > searchable.Length) return false;
            }
        }
        return true;
    }

    private void ObservePage()
    {
        StopObserving();
        if (!this.IsAttachedToVisualTree()) return;
        ObserveGroups(Groups);
        foreach (var page in pages) ObserveGroups(page.Item.Groups!);
        foreach (var item in CurrentGroups.SelectMany(group => group.Items))
            if (item.Command is { } command && observedCommands.Add(command))
                command.CanExecuteChanged += CommandChanged;
    }

    private void ObserveGroups(IReadOnlyList<AlderCommandGroup> groups)
    {
        ObserveCollection(groups);
        foreach (var group in groups) ObserveCollection(group.Items);
    }

    private void ObserveCollection(object source)
    {
        if (source is INotifyCollectionChanged collection && observedCollections.Add(collection))
            collection.CollectionChanged += CollectionChanged;
    }

    private void StopObserving()
    {
        foreach (var collection in observedCollections) collection.CollectionChanged -= CollectionChanged;
        foreach (var command in observedCommands) command.CanExecuteChanged -= CommandChanged;
        observedCollections.Clear();
        observedCommands.Clear();
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRefresh(true);
    private void CommandChanged(object? sender, EventArgs e) => ScheduleRefresh(false);

    private void ScheduleRefresh(bool collectionChanged)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ScheduleRefresh(collectionChanged));
            return;
        }
        if (!this.IsAttachedToVisualTree()) return;
        if (collectionChanged)
        {
            if (!PagePathStillExists())
            {
                var rootQuery = pages.Last().Query;
                pages.Clear();
                PageTitle = null;
                SetPageQuery(rootQuery);
            }
            ObservePage();
        }
        Refresh(ActiveItem);
    }

    private bool PagePathStillExists()
    {
        var groups = Groups;
        foreach (var page in pages.Reverse())
        {
            if (!groups.Any(group => group.Items.Contains(page.Item))) return false;
            groups = page.Item.Groups!;
        }
        return true;
    }

    private void OnPaletteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || search is null || list is null) return;
        var presenter = search.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (search.IsFocused && !string.IsNullOrEmpty(presenter?.PreeditText)) return;

        if (e.Key == Key.Escape)
        {
            e.Handled = DismissOrBack();
            return;
        }

        if (!search.IsFocused && (e.Source is not Visual source ||
            (source != list && !list.IsVisualAncestorOf(source)))) return;

        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
                MoveActive(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Enter:
                Activate(list.SelectedIndex);
                e.Handled = true;
                break;
            case Key.Back when pages.Count > 0 && string.IsNullOrEmpty(Query):
                GoBack();
                e.Handled = true;
                break;
        }
    }

    private void MoveActive(int step)
    {
        if (list is null || rows.Count == 0) return;
        int index = list.SelectedIndex < 0 ? (step > 0 ? -1 : 0) : list.SelectedIndex;
        for (int count = 0; count < rows.Count; count++)
        {
            index = (index + step + rows.Count) % rows.Count;
            if (rows[index] is not AlderCommandItem { CanInvoke: true }) continue;
            SetActive(index);
            break;
        }
    }

    private void SetActive(int index)
    {
        if (list is null) return;
        list.SelectedIndex = index;
        if (index >= 0) list.ScrollIntoView(index);
    }

    private void Activate(int index)
    {
        if (pendingItem is not null || (uint)index >= (uint)rows.Count || rows[index] is not AlderCommandItem { CanInvoke: true } item) return;
        if (item.Groups is not null)
        {
            pages.Push((item, Query));
            PageTitle = item.Label;
            ObservePage();
            SetPageQuery(null);
            search?.Focus();
        }
        else if (IsModal)
        {
            pendingItem = item;
            if (!Dialog.Close(this, DialogResult.OK)) pendingItem = null;
        }
        else
        {
            search?.Focus();
            Execute(item);
        }
    }

    private void Execute(AlderCommandItem item)
    {
        item.Command?.Execute(item.CommandParameter);
        ItemInvoked?.Invoke(this, item);
    }

    private void BackClicked(object? sender, RoutedEventArgs e) => GoBack();

    private void DismissClicked(object? sender, RoutedEventArgs e) => DismissOrBack();

    private bool DismissOrBack()
    {
        if (pages.Count == 0) return IsModal && Dialog.Close(this);
        GoBack();
        return true;
    }

    private void GoBack()
    {
        if (!pages.TryPop(out var page)) return;
        PageTitle = pages.TryPeek(out var parent) ? parent.Item.Label : null;
        ObservePage();
        SetPageQuery(page.Query);
        int index = page.Item.CanInvoke ? rows.IndexOf(page.Item) : -1;
        if (index >= 0) SetActive(index);
        search?.Focus();
    }

    private void SetPageQuery(string? query)
    {
        if (Query == query) Refresh();
        else SetCurrentValue(QueryProperty, query);
    }
}

internal sealed record AlderCommandHeading(string Label, bool AfterGroup);

internal sealed record AlderCommandRow(string Label, AlderCommandItem? Item, bool IsEnabled, bool AfterGroup = false)
{
    public bool IsHeading => Item is null;
    public bool IsAction => Item is not null;
    public bool IsPage => Item?.Groups is not null;
    public bool IsDisabledAction => Item is not null && !IsEnabled;
    public string? Description => Item?.Description;
    public string? Shortcut => Item?.Shortcut;
    public object? Icon => Item?.Icon;
}

internal sealed class AlderCommandList : AlderListBox
{
    internal Action<int>? Invoke { get; set; }
    protected override Type StyleKeyOverride => typeof(AlderListBox);

    public AlderCommandList() => AddHandler(TappedEvent, OnTappedRow, RoutingStrategies.Bubble, handledEventsToo: true);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new AlderCommandListItem(this);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        var row = item switch
        {
            AlderCommandItem action => new AlderCommandRow(action.Label, action, action.CanInvoke),
            AlderCommandHeading heading => new AlderCommandRow(heading.Label, null, false, heading.AfterGroup),
            _ => null
        };
        base.PrepareContainerForItemOverride(container, row ?? item, index);
        if (row is null) return;
        container.DataContext = row;
        container.IsEnabled = row.IsEnabled;
        AutomationProperties.SetName(container, row.Label);
        AutomationProperties.SetHeadingLevel(container, row.IsHeading ? 2 : 0);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        container.ClearValue(DataContextProperty);
    }

    private void OnTappedRow(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual visual ||
            visual.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { IsEffectivelyEnabled: true } container) return;
        int index = IndexFromContainer(container);
        if (index < 0) return;
        Invoke?.Invoke(index);
        e.Handled = true;
    }
}

internal sealed class AlderCommandListItem(AlderCommandList list) : ListBoxItem
{
    protected override Type StyleKeyOverride => typeof(ListBoxItem);
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this, list);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type != PointerType.Mouse || !IsEffectivelyEnabled) return;
        int index = list.IndexFromContainer(this);
        if (index >= 0) list.SelectedIndex = index;
    }

    private sealed class ItemPeer(AlderCommandListItem owner, AlderCommandList list) : ListItemAutomationPeer(owner), IInvokeProvider
    {
        public void Invoke()
        {
            EnsureEnabled();
            list.Invoke?.Invoke(list.IndexFromContainer(owner));
        }
    }
}
