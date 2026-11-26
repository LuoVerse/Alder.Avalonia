using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Alder.Avalonia.Controls;

/// <summary>Provides the sidebar, optional compact sidebar, and the adjacent content area.</summary>
public sealed class AlderSidebarHost : AlderAppLayout
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(AlderAppLayout);
}

/// <summary>A sidebar surface with pinned header and footer around independently scrolling navigation.</summary>
public sealed class AlderSidebar : HeaderedContentControl
{
    /// <summary>Identifies the <see cref="HeaderIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> HeaderIconProperty =
        AvaloniaProperty.Register<AlderSidebar, object?>(nameof(HeaderIcon));
    /// <summary>Identifies the <see cref="HeaderIconTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderIconTemplateProperty =
        AvaloniaProperty.Register<AlderSidebar, IDataTemplate?>(nameof(HeaderIconTemplate));
    /// <summary>Identifies the <see cref="Footer"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<AlderSidebar, object?>(nameof(Footer));
    /// <summary>Identifies the <see cref="FooterTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<AlderSidebar, IDataTemplate?>(nameof(FooterTemplate));
    /// <summary>Identifies the <see cref="Variant"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutSidebarVariant> VariantProperty =
        AvaloniaProperty.Register<AlderSidebar, AppLayoutSidebarVariant>(nameof(Variant));
    /// <summary>Identifies the <see cref="Placement"/> Avalonia property.</summary>
    public static readonly StyledProperty<AppLayoutSidebarPlacement> PlacementProperty =
        AvaloniaProperty.Register<AlderSidebar, AppLayoutSidebarPlacement>(nameof(Placement));
    /// <summary>Identifies the <see cref="ShowRail"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowRailProperty =
        AvaloniaProperty.Register<AlderSidebar, bool>(nameof(ShowRail));
    /// <summary>Identifies the <see cref="IsSearchEnabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsSearchEnabledProperty =
        AvaloniaProperty.Register<AlderSidebar, bool>(nameof(IsSearchEnabled));
    /// <summary>Identifies the <see cref="SearchPlaceholder"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<AlderSidebar, string?>(nameof(SearchPlaceholder));
    /// <summary>Identifies the <see cref="SearchGesture"/> Avalonia property.</summary>
    public static readonly StyledProperty<KeyGesture?> SearchGestureProperty =
        AvaloniaProperty.Register<AlderSidebar, KeyGesture?>(nameof(SearchGesture),
            new KeyGesture(Key.K, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control));
    /// <summary>Identifies the <see cref="SearchGroups"/> Avalonia property.</summary>
    public static readonly StyledProperty<IReadOnlyList<AlderCommandGroup>?> SearchGroupsProperty =
        AvaloniaProperty.Register<AlderSidebar, IReadOnlyList<AlderCommandGroup>?>(nameof(SearchGroups));
    /// <summary>Identifies the <see cref="SelectedItem"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<AlderSidebar, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="GetRail"/> attached property.</summary>
    public static readonly AttachedProperty<bool> RailProperty =
        AvaloniaProperty.RegisterAttached<AlderSidebar, StyledElement, bool>("Rail", inherits: true);
    /// <summary>Identifies the <see cref="CompactRows"/> Avalonia property.</summary>
    public static readonly AttachedProperty<bool> CompactRowsProperty =
        AvaloniaProperty.RegisterAttached<AlderSidebar, StyledElement, bool>("CompactRows", inherits: true);
    /// <summary>Identifies the <see cref="CloseMobileOnAction"/> Avalonia property.</summary>
    public static readonly AttachedProperty<bool> CloseMobileOnActionProperty =
        AvaloniaProperty.RegisterAttached<AlderSidebar, StyledElement, bool>("CloseMobileOnAction", true, inherits: true);

    /// <summary>The icon shown beside Header when expanded and on its own in the icon rail. Accepts a geometry, a control, or data with a template.</summary>
    public object? HeaderIcon { get => GetValue(HeaderIconProperty); set => SetValue(HeaderIconProperty, value); }
    /// <summary>The header template used for the collapsed rail.</summary>
    public IDataTemplate? HeaderIconTemplate { get => GetValue(HeaderIconTemplateProperty); set => SetValue(HeaderIconTemplateProperty, value); }
    /// <summary>The optional content displayed in the footer.</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    /// <summary>The data template used to present the footer.</summary>
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    /// <summary>The visual treatment applied to the control.</summary>
    public AppLayoutSidebarVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    /// <summary>The edge used when presenting the sidebar as a mobile overlay.</summary>
    public AppLayoutSidebarPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>Whether to expose the affordance for switching to the sidebar rail.</summary>
    public bool ShowRail { get => GetValue(ShowRailProperty); set => SetValue(ShowRailProperty, value); }
    /// <summary>Shows a search trigger for the sidebar navigation.</summary>
    /// <remarks>The trigger remains available in the icon rail and uses the same selection as the menus.</remarks>
    public bool IsSearchEnabled { get => GetValue(IsSearchEnabledProperty); set => SetValue(IsSearchEnabledProperty, value); }
    /// <summary>The placeholder shown for sidebar search.</summary>
    public string? SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }
    /// <summary>The shortcut to open navigation search when focus is within the sidebar or its layout.</summary>
    public KeyGesture? SearchGesture { get => GetValue(SearchGestureProperty); set => SetValue(SearchGestureProperty, value); }
    /// <summary>Optional command groups to search in place of the rendered navigation items.</summary>
    public IReadOnlyList<AlderCommandGroup>? SearchGroups { get => GetValue(SearchGroupsProperty); set => SetValue(SearchGroupsProperty, value); }
    /// <summary>The selected navigation item across all menus in this sidebar.</summary>
    /// <remarks>Bind this property two-way to drive adjacent content from both navigation rows and search results.</remarks>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    /// <summary>Whether the sidebar presents its compact, icon-oriented rail.</summary>
    public bool IsRail { get => GetRail(this); set => SetRail(this, value); }
    /// <summary>Whether descendant navigation rows use the compact height.</summary>
    public bool CompactRows { get => GetCompactRows(this); set => SetCompactRows(this, value); }
    /// <summary>Whether invoking a sidebar action dismisses its mobile overlay.</summary>
    public bool CloseMobileOnAction { get => GetCloseMobileOnAction(this); set => SetCloseMobileOnAction(this, value); }

    /// <summary>Gets the inherited rail state used by descendant sidebar controls.</summary>
    public static bool GetRail(StyledElement control) => control.GetValue(RailProperty);
    /// <summary>Sets the inherited rail state used by descendant sidebar controls.</summary>
    public static void SetRail(StyledElement control, bool value) => control.SetValue(RailProperty, value);
    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>Whether descendant navigation rows use the compact height.</remarks>
    public static bool GetCompactRows(StyledElement control) => control.GetValue(CompactRowsProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>Whether descendant navigation rows use the compact height.</remarks>
    public static void SetCompactRows(StyledElement control, bool value) => control.SetValue(CompactRowsProperty, value);
    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>Whether invoking a sidebar action dismisses its mobile overlay.</remarks>
    public static bool GetCloseMobileOnAction(StyledElement control) => control.GetValue(CloseMobileOnActionProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>Whether invoking a sidebar action dismisses its mobile overlay.</remarks>
    public static void SetCloseMobileOnAction(StyledElement control, bool value) => control.SetValue(CloseMobileOnActionProperty, value);

    private AlderAppLayout? host;
    private IDisposable? hostRail;
    private Button? railButton;
    private Button? searchButton;
    private AlderPopover? searchFlyout;
    private bool isSearchOpen;
    private readonly List<AlderSidebarMenu> menus = [];
    private AlderSidebarMenu? selectedMenu;
    private bool synchronizingSelection;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (railButton is not null) railButton.Click -= OnRailClick;
        if (searchButton is not null) searchButton.Click -= OnSearchClick;
        base.OnApplyTemplate(e);
        railButton = e.NameScope.Find<Button>("PART_Rail");
        if (railButton is not null) railButton.Click += OnRailClick;
        searchButton = e.NameScope.Find<Button>("PART_Search");
        if (searchButton is not null) searchButton.Click += OnSearchClick;
        ReadHostState();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host = this.GetVisualAncestors().OfType<AlderAppLayout>().FirstOrDefault(layout =>
            ReferenceEquals(layout.Sidebar, this) || ReferenceEquals(layout.MobileSidebar, this) ||
            this.GetVisualAncestors().TakeWhile(parent => parent != layout).OfType<Border>().Any(b => b.Name == "PART_Sidebar"));
        if (host is { } owner)
        {
            hostRail = Bind(RailProperty, owner.GetObservable(AlderAppLayout.IsSidebarCollapsedProperty), BindingPriority.Template);
            owner.PropertyChanged += OnHostChanged;
        }
        ((Control?)host ?? this).AddHandler(KeyDownEvent, OnSearchGesture);
        AddHandler(AlderSidebarItem.InvokedEvent, OnItemInvoked);
        ReadHostState();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        RemoveHandler(AlderSidebarItem.InvokedEvent, OnItemInvoked);
        ((Control?)host ?? this).RemoveHandler(KeyDownEvent, OnSearchGesture);
        searchFlyout?.HideImmediately();
        if (host is not null) host.PropertyChanged -= OnHostChanged;
        hostRail?.Dispose();
        hostRail = null;
        host = null;
        ReadHostState();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderIconProperty)
            PseudoClasses.Set(":has-header-icon", HeaderIcon is not null);
        if (change.Property == ShowRailProperty || change.Property == PlacementProperty)
            ReadHostState();
        if (change.Property == SelectedItemProperty)
            SynchronizeSelection();
    }

    internal void RegisterMenu(AlderSidebarMenu menu)
    {
        menus.Add(menu);
        if (SelectedItem is null && menu.SelectedItem is not null)
            OnMenuSelectionChanged(menu);
        else
            SynchronizeSelection();
    }

    internal void UnregisterMenu(AlderSidebarMenu menu)
    {
        menus.Remove(menu);
        if (selectedMenu == menu) selectedMenu = null;
    }

    internal void OnMenuSelectionChanged(AlderSidebarMenu menu)
    {
        if (synchronizingSelection || (menu.SelectedItem is null && selectedMenu != menu)) return;
        selectedMenu = menu.SelectedItem is null ? null : menu;
        SetCurrentValue(SelectedItemProperty, menu.SelectedItem);
        SynchronizeSelection();
    }

    internal void SynchronizeSelection()
    {
        if (synchronizingSelection || menus.Count == 0) return;
        synchronizingSelection = true;
        try
        {
            var item = SelectedItem;
            if (item is null)
                selectedMenu = null;
            else if (selectedMenu is null ||
                (!Equals(selectedMenu.SelectedItem, item) && !selectedMenu.ContainsItem(item)))
                selectedMenu = menus.FirstOrDefault(menu => menu.ContainsItem(item));

            foreach (var menu in menus.ToArray())
            {
                var selection = menu == selectedMenu ? item : null;
                if (!Equals(menu.SelectedItem, selection)) menu.SelectedItem = selection;
            }
        }
        finally { synchronizingSelection = false; }
    }

    private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AlderAppLayout.IsCompactProperty ||
            e.Property == AlderAppLayout.SidebarCollapseProperty || e.Property == AlderAppLayout.SidebarPlacementProperty)
            ReadHostState();
    }

    private void ReadHostState()
    {
        PseudoClasses.Set(":hosted", host is not null);
        PseudoClasses.Set(":drawer", host?.IsCompact == true);
        PseudoClasses.Set(":end", (host?.SidebarPlacement ?? Placement) == AppLayoutSidebarPlacement.End);
        if (railButton is not null)
            railButton.IsVisible = ShowRail && (host is null || !host.IsCompact && host.SidebarCollapse != AppLayoutSidebarCollapse.None);
    }

    private void OnRailClick(object? sender, RoutedEventArgs args) => ToggleRail();

    private void ToggleRail()
    {
        if (host is { } owner) owner.ToggleSidebar();
        else SetCurrentValue(RailProperty, !IsRail);
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button trigger) return;
        args.Handled = true;
        await OpenSearch(trigger);
    }

    private async void OnSearchGesture(object? sender, KeyEventArgs args)
    {
        if (!IsSearchEnabled || searchButton is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } trigger ||
            SearchGesture?.Matches(args) != true) return;
        args.Handled = true;
        await OpenSearch(trigger);
    }

    private async Task OpenSearch(Button trigger)
    {
        if (!IsSearchEnabled || isSearchOpen || searchFlyout is not null) return;
        var palette = new AlderCommand
        {
            FlowDirection = FlowDirection,
            Placeholder = SearchPlaceholder,
            Groups = SearchGroups ?? CollectSearchGroups()
        };
        AutomationProperties.SetName(palette, SearchPlaceholder);
        if (this.TryFindResource("AlderText.SidebarSearchEmpty", out var emptyContent))
            palette.EmptyContent = emptyContent;

        if (this.FindAncestorOfType<DialogHost>() is not null)
        {
            isSearchOpen = true;
            try { await palette.ShowAsync(this); }
            finally { isSearchOpen = false; }
        }
        else
        {
            ShowSearchFlyout(trigger, palette);
        }
    }

    private void ShowSearchFlyout(Button trigger, AlderCommand palette)
    {
        palette.Width = 344;
        var flyout = new AlderPopover
        {
            Content = palette,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            Offset = 6,
            ContentPadding = new Thickness(8),
            SurfaceCornerRadius = new CornerRadius(16),
            SurfaceBorderThickness = new Thickness(1),
            FocusReturnTarget = trigger,
            FocusContentOnOpen = false
        };
        if (this.TryFindResource("AlderSurface", out var background)) flyout.SurfaceBackground = background as IBrush;
        if (this.TryFindResource("AlderBorder", out var border)) flyout.SurfaceBorderBrush = border as IBrush;
        if (this.TryFindResource("AlderToolbarShadow", out var shadow) && shadow is BoxShadows boxShadow)
            flyout.SurfaceShadow = boxShadow;
        flyout.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (flyout.IsOpen)
                palette.GetVisualDescendants().OfType<AlderSearchField>().FirstOrDefault()?.Focus();
        }, DispatcherPriority.Loaded);
        flyout.Closed += (_, _) => searchFlyout = null;
        palette.ItemInvoked += (_, _) => flyout.Hide();
        palette.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            flyout.Hide();
            args.Handled = true;
        };
        searchFlyout = flyout;
        flyout.ShowAt(trigger);
        if (!flyout.IsOpen) searchFlyout = null;
    }

    private IReadOnlyList<AlderCommandGroup> CollectSearchGroups()
    {
        var groups = new List<AlderCommandGroup>();
        foreach (var menu in menus)
        {
            if (!menu.IsEffectivelyVisible || !menu.IsEffectivelyEnabled) continue;
            var heading = menu.FindAncestorOfType<AlderSidebarGroup>()?.Header as string;
            var items = new List<AlderCommandItem>();
            foreach (var path in menu.LeafPaths())
            {
                var data = path[^1];
                var item = data as AlderSidebarItem ?? menu.TreeContainerFromItem(data) as AlderSidebarItem;
                string? label = menu.SearchTextSelector is { } selectText ? selectText(data) :
                    item is null ? data.ToString() : GetSearchLabel(item);
                if (string.IsNullOrWhiteSpace(label) ||
                    item is { SelectOnInvoke: false } or { IsVisible: false } or { IsEnabled: false } ||
                    path.OfType<AlderSidebarItem>().Any(row => !row.IsEnabled || !row.IsVisible) ||
                    item?.Command?.CanExecute(item.CommandParameter) == false) continue;

                items.Add(new AlderCommandItem
                {
                    Label = label,
                    Icon = item?.Icon,
                    Keywords = string.Join(" ", path.Take(path.Length - 1)
                        .Select(parent => parent is AlderSidebarItem row ? GetSearchLabel(row) : parent.ToString())
                        .Append(heading).Append(item?.SearchKeywords)),
                    Command = new SearchNavigationAction(this, menu, path)
                });
            }
            if (items.Count > 0) groups.Add(new AlderCommandGroup { Heading = heading, Items = items });
        }
        return groups;
    }

    private static string? GetSearchLabel(AlderSidebarItem item) =>
        item.Header as string ?? AutomationProperties.GetName(item) ?? item.RailToolTip as string ??
        item.GetTemplateDescendants().FirstOrDefault(child => child.Name == "Label")?
            .GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text;

    private sealed class SearchNavigationAction(AlderSidebar sidebar, AlderSidebarMenu menu, object[] path) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => menu.IsAttachedToVisualTree() && menu.IsEffectivelyEnabled;

        public void Execute(object? parameter)
        {
            if (path.Length > 1 && sidebar.IsRail) sidebar.ToggleRail();
            menu.RevealPath(path)?.Invoke();
        }
    }

    private void OnItemInvoked(object? sender, RoutedEventArgs args)
    {
        if (host?.ActiveOverlay == AppLayoutOverlay.Sidebar && args.Source is AlderSidebarItem item && GetCloseMobileOnAction(item))
            host.CloseOverlay();
    }
}

/// <summary>Groups related sidebar content under a header.</summary>
public sealed class AlderSidebarGroup : HeaderedContentControl;

/// <summary>Controls when nested sidebar rows show indentation guides.</summary>
public enum AlderSidebarGuideLines
{
    /// <summary>Always shows the guides.</summary>
    Always,
    /// <summary>Shows the guides on pointer hover.</summary>
    Hover,
    /// <summary>Hides the guides.</summary>
    Hidden
}

/// <summary>Hosts hierarchical sidebar navigation with tree and keyboard behavior.</summary>
public sealed class AlderSidebarMenu : TreeView, ICustomKeyboardNavigation
{
    /// <summary>Identifies the <see cref="GuideLines"/> Avalonia property.</summary>
    public static readonly AttachedProperty<AlderSidebarGuideLines> GuideLinesProperty =
        AvaloniaProperty.RegisterAttached<AlderSidebarMenu, StyledElement, AlderSidebarGuideLines>("GuideLines", inherits: true);
    /// <summary>Identifies the <see cref="SearchTextSelector"/> Avalonia property.</summary>
    public static readonly StyledProperty<Func<object, string?>?> SearchTextSelectorProperty =
        AvaloniaProperty.Register<AlderSidebarMenu, Func<object, string?>?>(nameof(SearchTextSelector));

    /// <summary>When nested sidebar items show their indentation guides.</summary>
    public AlderSidebarGuideLines GuideLines { get => GetGuideLines(this); set => SetGuideLines(this, value); }
    /// <summary>Provides the label for a data item in sidebar search, including items outside the viewport. Return null to exclude an item.</summary>
    public Func<object, string?>? SearchTextSelector { get => GetValue(SearchTextSelectorProperty); set => SetValue(SearchTextSelectorProperty, value); }
    /// <summary>Gets the attached setting for the target.</summary>
    /// <remarks>When nested sidebar items show their indentation guides.</remarks>
    public static AlderSidebarGuideLines GetGuideLines(StyledElement control) => control.GetValue(GuideLinesProperty);
    /// <summary>Sets the attached setting for the target.</summary>
    /// <remarks>When nested sidebar items show their indentation guides.</remarks>
    public static void SetGuideLines(StyledElement control, AlderSidebarGuideLines value) => control.SetValue(GuideLinesProperty, value);

    private AlderSidebar? sidebar;
    private bool reconcilingSelection;
    private bool refreshPending;
    private readonly ConditionalWeakTable<object, object> expandedItems = new();
    private static readonly object ExpandedMarker = new();

    static AlderSidebarMenu() => SelectionModeProperty.OverrideMetadata<AlderSidebarMenu>(
        new StyledPropertyMetadata<SelectionMode>(SelectionMode.Single, coerce: (_, _) => SelectionMode.Single));

    /// <summary>Initializes a new instance of <see cref="AlderSidebarMenu"/>.</summary>
    public AlderSidebarMenu() => SelectionChanged += OnSelectionChanged;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        sidebar = this.FindAncestorOfType<AlderSidebar>();
        sidebar?.RegisterMenu(this);
        QueueSelectionRefresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        sidebar?.UnregisterMenu(this);
        sidebar = null;
        base.OnDetachedFromVisualTree(e);
    }

    internal void QueueSelectionRefresh()
    {
        if (refreshPending) return;
        refreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            refreshPending = false;
            if (!this.IsAttachedToVisualTree()) return;
            if (SelectedItem is null && sidebar?.SelectedItem is null &&
                this.GetVisualDescendants().OfType<AlderSidebarItem>().FirstOrDefault(item => item.IsCurrent &&
                    item.FindAncestorOfType<AlderSidebarMenu>() == this) is { } current)
                SelectContainer(current);

            sidebar?.SynchronizeSelection();
        }, DispatcherPriority.Loaded);
    }

    internal void SelectContainer(AlderSidebarItem item) => UpdateSelectionFromContainer(item);

    internal void RememberExpansion(AlderSidebarItem row, object? item)
    {
        if (item is null) return;
        if (row.IsExpanded) expandedItems.TryAdd(item, ExpandedMarker);
        else expandedItems.Remove(item);
    }

    internal void RestoreExpansion(AlderSidebarItem row, object? item)
    {
        if (item is not null && expandedItems.TryGetValue(item, out _) && !row.IsSet(TreeViewItem.IsExpandedProperty))
            row.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
    }

    internal bool ContainsItem(object item) => ItemsView.Contains(item) || TreeContainerFromItem(item) is not null ||
        Paths(ItemsView, [], leavesOnly: false).Any(path => Equals(path[^1], item));

    internal IEnumerable<object[]> LeafPaths() => Paths(ItemsView, [], leavesOnly: true);

    private IEnumerable<object[]> Paths(IEnumerable source, object[] parents, bool leavesOnly)
    {
        foreach (var item in source.OfType<object>())
        {
            if (parents.Any(parent => ReferenceEquals(parent, item))) continue;
            object[] path = [.. parents, item];
            if (!leavesOnly) yield return path;
            var children = ReadChildren(item);
            if (children.Length == 0)
            {
                if (leavesOnly) yield return path;
            }
            else
                foreach (var child in Paths(children, path, leavesOnly)) yield return child;
        }
    }

    private object[] ReadChildren(object item)
    {
        if (item is AlderSidebarItem row) return row.ItemsView.OfType<object>().ToArray();
        if (this.FindDataTemplate(item, ItemTemplate) is not ITreeDataTemplate tree) return [];
        var source = new ChildSource();
        using var binding = tree.BindChildren(source, ChildSource.ItemsProperty, item);
        return source.GetValue(ChildSource.ItemsProperty)?.OfType<object>().ToArray() ?? [];
    }

    private sealed class ChildSource : StyledElement
    {
        public static readonly StyledProperty<IEnumerable?> ItemsProperty =
            AvaloniaProperty.Register<ChildSource, IEnumerable?>("Items");
    }

    internal AlderSidebarItem? RevealPath(IReadOnlyList<object> path) => RevealPath(path, expand: true);

    private AlderSidebarItem? RevealPath(IReadOnlyList<object> path, bool expand)
    {
        ItemsControl owner = this;
        for (int i = 0; i < path.Count; i++)
        {
            int index = owner.ItemsView.IndexOf(path[i]);
            if (index < 0) return null;
            if (!expand && i < path.Count - 1 && owner.ContainerFromIndex(index) is null &&
                path[i] is not AlderSidebarItem { IsSubmenuOpen: true } && !expandedItems.TryGetValue(path[i], out _)) return null;
            if (Realize(owner, index) is not { } row) return null;
            if (i == path.Count - 1)
            {
                row.BringIntoView();
                return row;
            }
            if (!row.IsSubmenuOpen)
            {
                if (!expand) return null;
                row.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                TopLevel.GetTopLevel(this)?.UpdateLayout();
            }
            owner = row;
        }
        return null;
    }

    private AlderSidebarItem? RevealSelection(object item)
    {
        if (TreeContainerFromItem(item) is AlderSidebarItem { IsEffectivelyVisible: true } row) return row;
        int index = ItemsView.IndexOf(item);
        if (index >= 0) return Realize(this, index);
        var path = Paths(ItemsView, [], leavesOnly: false).FirstOrDefault(path => Equals(path[^1], item));
        return path is null ? null : RevealPath(path, expand: false);
    }

    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        if (direction is not (NavigationDirection.Next or NavigationDirection.Previous)) return (false, null);
        if (element is Visual visual && this.IsVisualAncestorOf(visual)) return (true, null);
        var row = SelectedItem is { } item ? RevealSelection(item) : ContainerFromIndex(0);
        return (row is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true }, row);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Key is not (Key.Up or Key.Down or Key.Right) ||
            GetContainerFromEventSource(e.Source!) is not AlderSidebarItem from)
        {
            base.OnKeyDown(e);
            return;
        }

        bool backward = e.Key == Key.Up;
        while (FindNeighbor(from, backward) is { } next)
        {
            if (next.Focus(NavigationMethod.Directional))
            {
                e.Handled = true;
                break;
            }
            from = next;
        }
    }

    private static AlderSidebarItem? FindNeighbor(AlderSidebarItem row, bool backward)
    {
        if (!backward && row.IsEffectivelyEnabled && row.IsSubmenuOpen && row.ItemCount > 0)
            return Realize(row, 0);

        while (row.Parent is ItemsControl owner)
        {
            int index = owner.IndexFromContainer(row);
            if (index < 0) return null;
            int neighbor = index + (backward ? -1 : 1);
            if ((uint)neighbor < (uint)owner.ItemCount)
            {
                var next = Realize(owner, neighbor);
                while (backward && next is { IsEffectivelyEnabled: true, IsSubmenuOpen: true, ItemCount: > 0 })
                    next = Realize(next, next.ItemCount - 1);
                return next;
            }
            if (owner is not AlderSidebarItem parent) return null;
            if (backward) return parent;
            row = parent;
        }
        return null;
    }

    private static AlderSidebarItem? Realize(ItemsControl owner, int index)
    {
        if (owner.ContainerFromIndex(index) is not AlderSidebarItem)
            owner.ScrollIntoView(index);
        return owner.ContainerFromIndex(index) as AlderSidebarItem;
    }

    internal void ClearContainer(AlderSidebarItem item)
    {
        if (Equals(SelectedItem, TreeItemFromContainer(item))) SelectedItem = null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source != this || reconcilingSelection) return;
        // Avalonia's container IsSelected changes can add to SelectedItems directly.
        if (SelectedItems.Count > 1)
        {
            reconcilingSelection = true;
            try { SelectedItem = e.AddedItems.OfType<object>().LastOrDefault() ?? SelectedItem; }
            finally { reconcilingSelection = false; }
        }
        sidebar?.OnMenuSelectionChanged(this);
        if (AutoScrollToSelectedItem && SelectedItem is { } item && TreeContainerFromItem(item) is null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.IsAttachedToVisualTree() && Equals(SelectedItem, item))
                    RevealSelection(item)?.BringIntoView();
            }, DispatcherPriority.Loaded);
        }
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderSidebarItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderSidebarItem();

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        // A reattached item can already reflect the current selection. Letting TreeView interpret
        // that value as a new toggle would remove the selection.
        if (container is AlderSidebarItem { IsSelected: true } && SelectedItems.Contains(item)) return;
        base.ContainerForItemPreparedOverride(container, item, index);
    }
}

/// <summary>Presents an actionable sidebar row with optional navigation content, badges, and row actions.</summary>
public sealed class AlderSidebarItem : TreeViewItem
{
    /// <summary>Identifies the <see cref="Icon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<AlderSidebarItem, Geometry?>(nameof(Icon));
    /// <summary>Identifies the <see cref="Badge"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> BadgeProperty = AvaloniaProperty.Register<AlderSidebarItem, object?>(nameof(Badge));
    /// <summary>Identifies the <see cref="BadgeTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> BadgeTemplateProperty =
        AvaloniaProperty.Register<AlderSidebarItem, IDataTemplate?>(nameof(BadgeTemplate));
    /// <summary>Identifies the <see cref="RailToolTip"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> RailToolTipProperty =
        AvaloniaProperty.Register<AlderSidebarItem, object?>(nameof(RailToolTip));
    /// <summary>Identifies the <see cref="SearchKeywords"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> SearchKeywordsProperty =
        AvaloniaProperty.Register<AlderSidebarItem, string?>(nameof(SearchKeywords));
    /// <summary>Identifies the <see cref="Actions"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<AlderSidebarItem, object?>(nameof(Actions));
    /// <summary>Identifies the <see cref="IsCurrent"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty = AvaloniaProperty.Register<AlderSidebarItem, bool>(nameof(IsCurrent));
    /// <summary>Identifies the <see cref="SelectOnInvoke"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> SelectOnInvokeProperty =
        AvaloniaProperty.Register<AlderSidebarItem, bool>(nameof(SelectOnInvoke), true);
    /// <summary>Identifies the <see cref="Command"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty = Button.CommandProperty.AddOwner<AlderSidebarItem>();
    /// <summary>Identifies the <see cref="CommandParameter"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty = Button.CommandParameterProperty.AddOwner<AlderSidebarItem>();
    /// <summary>Identifies the <see cref="Invoked"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> InvokedEvent =
        RoutedEvent.Register<AlderSidebarItem, RoutedEventArgs>(nameof(Invoked), RoutingStrategies.Bubble);

    /// <summary>The optional geometry displayed with the content.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    /// <summary>Optional supplementary content displayed beside the navigation label.</summary>
    public object? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    /// <summary>The data template used to present the badge.</summary>
    public IDataTemplate? BadgeTemplate { get => GetValue(BadgeTemplateProperty); set => SetValue(BadgeTemplateProperty, value); }
    /// <summary>The hint shown for an item in the rail presentation.</summary>
    public object? RailToolTip { get => GetValue(RailToolTipProperty); set => SetValue(RailToolTipProperty, value); }
    /// <summary>Additional terms that match this navigation item in sidebar search.</summary>
    public string? SearchKeywords { get => GetValue(SearchKeywordsProperty); set => SetValue(SearchKeywordsProperty, value); }
    /// <summary>Optional controls displayed in the row's action area.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    /// <summary>Marks this item as current. Bind the menu or sidebar SelectedItem to track the current selection.</summary>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    /// <summary>Whether invoking this row also changes the current selection.</summary>
    public bool SelectOnInvoke { get => GetValue(SelectOnInvokeProperty); set => SetValue(SelectOnInvokeProperty, value); }
    /// <summary>The command run when the item or action is invoked.</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    /// <summary>The argument passed to the command when it is invoked.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Raised when the sidebar row is invoked.</summary>
    public event EventHandler<RoutedEventArgs>? Invoked { add => AddHandler(InvokedEvent, value); remove => RemoveHandler(InvokedEvent, value); }

    private Button? row;
    private AlderAccordionPanel? submenu;
    internal bool IsSubmenuOpen => IsExpanded && !AlderSidebar.GetRail(this);
    internal bool AreChildrenVisible => IsSubmenuOpen || submenu?.Progress > 0;
    internal bool IsSubmenuAnimating => submenu?.Progress is > 0 and < 1 || IsSubmenuOpen && submenu?.Progress == 0;

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderSidebarItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderSidebarItem();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        this.FindAncestorOfType<AlderSidebarMenu>()?.QueueSelectionRefresh();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (row is not null) row.Click -= OnRowClick;
        base.OnApplyTemplate(e);
        row = e.NameScope.Find<Button>("PART_Row");
        submenu = e.NameScope.Find<AlderAccordionPanel>("Submenu");
        if (row is not null) row.Click += OnRowClick;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && IsFocused)
        {
            if (AlderSidebar.GetRail(this) && e.Key is Key.Left or Key.Right)
                e.Handled = true;
            else if (e.Key is Key.Enter or Key.Space && Invoke())
            {
                if (IsFocused && row is AlderButton button)
                    button.Feedback?.BeginSpringKey(e.Key);
                e.Handled = true;
            }
        }
        if (!e.Handled) base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (row is AlderButton button) button.Feedback?.EndSpringKey(e.Key);
        base.OnKeyUp(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (row is AlderButton button) button.Feedback?.CancelSpringKey();
        base.OnLostFocus(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (submenu?.IsKeyboardFocusWithin == true &&
            (change.Property == IsExpandedProperty && !IsExpanded ||
             change.Property == AlderSidebar.RailProperty && AlderSidebar.GetRail(this)))
            Focus(NavigationMethod.Tab);
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) PseudoClasses.Set(":has-icon", Icon is not null);
        if (change.Property == IsExpandedProperty)
            ItemsPanelRoot?.InvalidateMeasure();
        if (change.Property == IsCurrentProperty && this.FindAncestorOfType<AlderSidebarMenu>() is { } menu)
        {
            if (IsCurrent) menu.SelectContainer(this);
            else menu.ClearContainer(this);
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source != row) return;
        Focus(NavigationMethod.Pointer);
        e.Handled = Invoke();
    }

    /// <summary>Attempts to invoke the row, applying its selection and command behavior.</summary>
    public bool Invoke()
    {
        if (!IsEffectivelyEnabled) return false;
        if (ItemCount > 0)
        {
            if (AlderSidebar.GetRail(this)) return false;
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
            return true;
        }

        var command = Command;
        var parameter = CommandParameter;
        if (command is not null && !command.CanExecute(parameter)) return false;
        if (SelectOnInvoke)
        {
            if (this.FindAncestorOfType<AlderSidebarMenu>() is { } menu)
                menu.SelectContainer(this);
            else
                SetCurrentValue(IsSelectedProperty, true);
        }
        command?.Execute(parameter);
        RaiseEvent(new RoutedEventArgs(InvokedEvent));
        return true;
    }
}

/// <summary>Hosts the contents of one page in a paged sidebar.</summary>
public sealed class AlderSidebarPage : ContentControl;

/// <summary>Switches between pages within a sidebar using carousel selection.</summary>
public sealed class AlderSidebarPages : Carousel
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Carousel);

    private static readonly TimeSpan PageDuration = TimeSpan.FromMilliseconds(300);
    private static readonly SplineEasing PageEasing = new(.22, .8, .28, 1);
    private readonly CompositePageTransition slide = new()
    {
        PageTransitions =
        {
            new PageSlide(PageDuration, PageSlide.SlideAxis.Horizontal) { SlideInEasing = PageEasing, SlideOutEasing = PageEasing },
            new CrossFade(PageDuration) { FadeInEasing = PageEasing, FadeOutEasing = PageEasing }
        }
    };

    /// <summary>Initializes a new instance of <see cref="AlderSidebarPages"/>.</summary>
    public AlderSidebarPages()
    {
        SetCurrentValue(PageTransitionProperty, slide);
        ClipToBounds = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlderMotion.ReducedProperty)
            SetCurrentValue(PageTransitionProperty, AlderMotion.GetReduced(this) ? null : slide);
    }
}
