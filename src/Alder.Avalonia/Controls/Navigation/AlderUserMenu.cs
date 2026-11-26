using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
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

namespace Alder.Avalonia.Controls;

/// <summary>Represents the theme preference selected in the user menu.</summary>
public enum AlderUserThemeMode
{
    /// <summary>Follows the system theme.</summary>
    System,
    /// <summary>Requests a light theme.</summary>
    Light,
    /// <summary>Requests a dark theme.</summary>
    Dark
}

/// <summary>Carries the previous and requested theme mode for a user menu change.</summary>
/// <param name="routedEvent">The registered event being raised.</param>
/// <param name="oldMode">The previous theme mode.</param>
/// <param name="newMode">The newly selected theme mode.</param>
public sealed class AlderUserThemeChangedEventArgs(
    RoutedEvent routedEvent, AlderUserThemeMode oldMode, AlderUserThemeMode newMode) : RoutedEventArgs(routedEvent)
{
    /// <summary>The selected theme mode before the change.</summary>
    public AlderUserThemeMode OldMode { get; } = oldMode;
    /// <summary>The selected theme mode after the change.</summary>
    public AlderUserThemeMode NewMode { get; } = newMode;
}

/// <summary>An account button with an anchored, command-bindable menu and optional account actions.</summary>
public sealed class AlderUserMenu : AlderButton
{
    /// <summary>Identifies the <see cref="Title"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AlderUserMenu, string?>(nameof(Title));
    /// <summary>Identifies the <see cref="Description"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<AlderUserMenu, string?>(nameof(Description));
    /// <summary>Identifies the <see cref="TriggerTitle"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> TriggerTitleProperty = AvaloniaProperty.Register<AlderUserMenu, string?>(nameof(TriggerTitle));
    /// <summary>Identifies the <see cref="Initials"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> InitialsProperty = AvaloniaProperty.Register<AlderUserMenu, string?>(nameof(Initials));
    /// <summary>Identifies the <see cref="AvatarData"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> AvatarDataProperty = AvaloniaProperty.Register<AlderUserMenu, object?>(nameof(AvatarData));
    /// <summary>Identifies the <see cref="AvatarTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> AvatarTemplateProperty =
        AvaloniaProperty.Register<AlderUserMenu, IDataTemplate?>(nameof(AvatarTemplate));
    /// <summary>Identifies the <see cref="ShowLabel"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowLabelProperty = AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowLabel), true);
    /// <summary>Identifies the <see cref="ShowChevron"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowChevronProperty = AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowChevron), true);
    /// <summary>Identifies the <see cref="ShowAvatarInMenu"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowAvatarInMenuProperty =
        AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowAvatarInMenu), true);

    /// <summary>Identifies the <see cref="ItemsSource"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = MenuFlyout.ItemsSourceProperty.AddOwner<AlderUserMenu>();
    /// <summary>Identifies the <see cref="ItemTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = MenuFlyout.ItemTemplateProperty.AddOwner<AlderUserMenu>();
    /// <summary>Identifies the <see cref="ItemContainerTheme"/> Avalonia property.</summary>
    public static readonly StyledProperty<ControlTheme?> ItemContainerThemeProperty = MenuFlyout.ItemContainerThemeProperty.AddOwner<AlderUserMenu>();
    /// <summary>Identifies the <see cref="MenuTheme"/> Avalonia property.</summary>
    public static readonly StyledProperty<ControlTheme?> MenuThemeProperty = AvaloniaProperty.Register<AlderUserMenu, ControlTheme?>(nameof(MenuTheme));
    /// <summary>Identifies the <see cref="MenuWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MenuWidthProperty =
        AvaloniaProperty.Register<AlderUserMenu, double>(nameof(MenuWidth), 220, validate: value => double.IsFinite(value) && value > 0);
    /// <summary>Identifies the <see cref="MenuPlacement"/> Avalonia property.</summary>
    public static readonly StyledProperty<PlacementMode> MenuPlacementProperty =
        AvaloniaProperty.Register<AlderUserMenu, PlacementMode>(nameof(MenuPlacement), PlacementMode.BottomEdgeAlignedRight);
    /// <summary>Identifies the <see cref="MenuGap"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> MenuGapProperty =
        AvaloniaProperty.Register<AlderUserMenu, double>(nameof(MenuGap), 8, validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Identifies the <see cref="ShowThemeSwitcher"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowThemeSwitcherProperty =
        AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowThemeSwitcher));
    /// <summary>Identifies the <see cref="AutoApplyTheme"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AutoApplyThemeProperty =
        AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(AutoApplyTheme), true);
    /// <summary>Identifies the <see cref="ThemeMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderUserThemeMode> ThemeModeProperty =
        AvaloniaProperty.Register<AlderUserMenu, AlderUserThemeMode>(nameof(ThemeMode), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="ShowLogOut"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowLogOutProperty = AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowLogOut));
    /// <summary>Identifies the <see cref="ShowLogOutSeparator"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowLogOutSeparatorProperty =
        AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(ShowLogOutSeparator));
    /// <summary>Identifies the <see cref="LogOutIsDestructive"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> LogOutIsDestructiveProperty =
        AvaloniaProperty.Register<AlderUserMenu, bool>(nameof(LogOutIsDestructive), true);
    /// <summary>Identifies the <see cref="LogOutText"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> LogOutTextProperty = AvaloniaProperty.Register<AlderUserMenu, string?>(nameof(LogOutText));
    /// <summary>Identifies the <see cref="LogOutIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> LogOutIconProperty = AvaloniaProperty.Register<AlderUserMenu, Geometry?>(nameof(LogOutIcon));
    /// <summary>Identifies the <see cref="LogOutCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> LogOutCommandProperty = AvaloniaProperty.Register<AlderUserMenu, ICommand?>(nameof(LogOutCommand));
    /// <summary>Identifies the <see cref="LogOutCommandParameter"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> LogOutCommandParameterProperty = AvaloniaProperty.Register<AlderUserMenu, object?>(nameof(LogOutCommandParameter));

    /// <summary>Identifies the <see cref="ThemeModeChanged"/> routed event.</summary>
    public static readonly RoutedEvent<AlderUserThemeChangedEventArgs> ThemeModeChangedEvent =
        RoutedEvent.Register<AlderUserMenu, AlderUserThemeChangedEventArgs>(nameof(ThemeModeChanged), RoutingStrategies.Bubble);
    /// <summary>Identifies the <see cref="LogOutRequested"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> LogOutRequestedEvent =
        RoutedEvent.Register<AlderUserMenu, RoutedEventArgs>(nameof(LogOutRequested), RoutingStrategies.Bubble);

    /// <summary>The primary text displayed for the content.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The optional supporting text displayed with the content.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    /// <summary>An optional title to display in the menu's trigger.</summary>
    public string? TriggerTitle { get => GetValue(TriggerTitleProperty); set => SetValue(TriggerTitleProperty, value); }
    /// <summary>The fallback initials displayed when there is no avatar image.</summary>
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    /// <summary>Data passed to AvatarTemplate each time an avatar is presented.</summary>
    public object? AvatarData { get => GetValue(AvatarDataProperty); set => SetValue(AvatarDataProperty, value); }
    /// <summary>An optional template used to present the avatar.</summary>
    public IDataTemplate? AvatarTemplate { get => GetValue(AvatarTemplateProperty); set => SetValue(AvatarTemplateProperty, value); }
    /// <summary>Whether the trigger shows its text label.</summary>
    public bool ShowLabel { get => GetValue(ShowLabelProperty); set => SetValue(ShowLabelProperty, value); }
    /// <summary>Whether the trigger displays the menu chevron.</summary>
    public bool ShowChevron { get => GetValue(ShowChevronProperty); set => SetValue(ShowChevronProperty, value); }
    /// <summary>Whether the menu header repeats the avatar.</summary>
    public bool ShowAvatarInMenu { get => GetValue(ShowAvatarInMenuProperty); set => SetValue(ShowAvatarInMenuProperty, value); }

    /// <summary>Inline menu entries. MenuItem entries support the standard Command binding.</summary>
    public AvaloniaList<object?> Items { get; } = [];
    /// <summary>The custom entries to display in the user menu.</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>The data template used to present the item.</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    /// <summary>The optional theme applied to containers for custom menu items.</summary>
    public ControlTheme? ItemContainerTheme { get => GetValue(ItemContainerThemeProperty); set => SetValue(ItemContainerThemeProperty, value); }
    /// <summary>An optional control theme for the menu surface.</summary>
    public ControlTheme? MenuTheme { get => GetValue(MenuThemeProperty); set => SetValue(MenuThemeProperty, value); }
    /// <summary>The preferred width of the menu in device-independent pixels.</summary>
    public double MenuWidth { get => GetValue(MenuWidthProperty); set => SetValue(MenuWidthProperty, value); }
    /// <summary>The placement requested for the menu relative to its trigger.</summary>
    public PlacementMode MenuPlacement { get => GetValue(MenuPlacementProperty); set => SetValue(MenuPlacementProperty, value); }
    /// <summary>The distance between the trigger and menu surface.</summary>
    public double MenuGap { get => GetValue(MenuGapProperty); set => SetValue(MenuGapProperty, value); }

    /// <summary>Whether the menu includes its built-in theme chooser.</summary>
    public bool ShowThemeSwitcher { get => GetValue(ShowThemeSwitcherProperty); set => SetValue(ShowThemeSwitcherProperty, value); }
    /// <summary>Applies the chosen theme to the application and current window. Disable to handle ThemeModeChanged yourself.</summary>
    public bool AutoApplyTheme { get => GetValue(AutoApplyThemeProperty); set => SetValue(AutoApplyThemeProperty, value); }
    /// <summary>The theme preference represented by the menu's theme chooser.</summary>
    public AlderUserThemeMode ThemeMode { get => GetValue(ThemeModeProperty); set => SetValue(ThemeModeProperty, value); }
    /// <summary>Whether the menu includes its built-in logout action.</summary>
    public bool ShowLogOut { get => GetValue(ShowLogOutProperty); set => SetValue(ShowLogOutProperty, value); }
    /// <summary>Separates the built-in log out action from preceding menu items.</summary>
    public bool ShowLogOutSeparator { get => GetValue(ShowLogOutSeparatorProperty); set => SetValue(ShowLogOutSeparatorProperty, value); }
    /// <summary>Whether the logout action uses destructive styling.</summary>
    public bool LogOutIsDestructive { get => GetValue(LogOutIsDestructiveProperty); set => SetValue(LogOutIsDestructiveProperty, value); }
    /// <summary>An optional replacement for the logout label.</summary>
    public string? LogOutText { get => GetValue(LogOutTextProperty); set => SetValue(LogOutTextProperty, value); }
    /// <summary>The geometry displayed beside the logout label.</summary>
    public Geometry? LogOutIcon { get => GetValue(LogOutIconProperty); set => SetValue(LogOutIconProperty, value); }
    /// <summary>The command executed when logout is requested.</summary>
    public ICommand? LogOutCommand { get => GetValue(LogOutCommandProperty); set => SetValue(LogOutCommandProperty, value); }
    /// <summary>The argument passed to the log out command.</summary>
    public object? LogOutCommandParameter { get => GetValue(LogOutCommandParameterProperty); set => SetValue(LogOutCommandParameterProperty, value); }
    /// <summary>Raised when the selected theme mode changes.</summary>
    public event EventHandler<AlderUserThemeChangedEventArgs>? ThemeModeChanged
    {
        add => AddHandler(ThemeModeChangedEvent, value);
        remove => RemoveHandler(ThemeModeChangedEvent, value);
    }
    /// <summary>Raised when the built-in logout action is invoked.</summary>
    public event EventHandler<RoutedEventArgs>? LogOutRequested
    {
        add => AddHandler(LogOutRequestedEvent, value);
        remove => RemoveHandler(LogOutRequestedEvent, value);
    }

    private readonly AlderMenuFlyout menu = new();
    private readonly AvaloniaList<object?> entries = [];
    private readonly AlderMenuItem logOutSeparator = new() { Header = "-", Focusable = false };
    private readonly AlderMenuItem logOutItem = new();
    private readonly LogOutAction logOutAction;
    private INotifyCollectionChanged? observedItems;
    private TopLevel? menuFocusRoot;
    private bool initializingTheme;
    private bool followsHostTheme;

    /// <summary>Initializes a new instance of <see cref="AlderUserMenu"/>.</summary>
    public AlderUserMenu()
    {
        logOutAction = new(this);
        logOutItem.Classes.Add("danger");
        logOutItem.Command = logOutAction;
        logOutItem.Bind(MenuItem.HeaderProperty, this.GetObservable(LogOutTextProperty));
        var icon = new AlderPathIcon { Width = 14, Height = 14 };
        icon.Bind(PathIcon.DataProperty, this.GetObservable(LogOutIconProperty));
        logOutItem.Icon = icon;
        menu.ItemsSource = entries;
        menu.Opened += (_, _) => ObserveMenuFocus(TopLevel.GetTopLevel(this));
        menu.Closed += (_, _) => ObserveMenuFocus(null);
        menu.Opening += (_, _) =>
        {
            if (AutoApplyTheme && ShowThemeSwitcher && (followsHostTheme || !IsSet(ThemeModeProperty)))
                ReadHostTheme();
            if (menu.Popup.Child is MenuFlyoutPresenter presenter && presenter.DataContext != this)
                presenter.DataContext = this;
        };
        Items.CollectionChanged += OnItemsChanged;
        Flyout = menu;
        ApplyPlacement();
    }

    /// <summary>Opens the user menu.</summary>
    public void Open()
    {
        if (!IsEffectivelyEnabled || !this.IsAttachedToVisualTree()) return;
        menu.PreparePresenter(this, this);
        menu.ShowAt(this);
    }
    /// <summary>Closes the user menu if it is open.</summary>
    public void Close() => menu.Hide();

    internal override void PrepareFlyout() => menu.PreparePresenter(this, this);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveItems();
        RefreshEntries();
        logOutAction.Observe(LogOutCommand);
        Dispatcher.UIThread.Post(() =>
        {
            if (this.IsAttachedToVisualTree() && IsEffectivelyVisible && IsEffectivelyEnabled)
                menu.PreparePresenter(this, this);
        }, DispatcherPriority.Background);

        if (AutoApplyTheme && ShowThemeSwitcher)
        {
            if (IsSet(ThemeModeProperty) && !followsHostTheme) ApplyTheme();
            else ReadHostTheme();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ObserveItems(attach: false);
        ObserveMenuFocus(null);
        logOutAction.Observe(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void ObserveMenuFocus(TopLevel? root)
    {
        menuFocusRoot?.RemoveHandler(GotFocusEvent, OnMenuFocusChanged);
        menuFocusRoot = root;
        menuFocusRoot?.AddHandler(GotFocusEvent, OnMenuFocusChanged, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnMenuFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        if (!menu.Popup.IsHitTestVisible || e.Source is not Visual focused || focused == this || this.IsVisualAncestorOf(focused))
            return;

        if (menu.Popup.Child is { } content && focused != content && !content.IsVisualAncestorOf(focused))
            Close();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveItems(this.IsAttachedToVisualTree());
            RefreshEntries();
        }
        else if (change.Property == ShowLogOutProperty || change.Property == ShowLogOutSeparatorProperty) RefreshEntries();
        else if (change.Property == LogOutIsDestructiveProperty) logOutItem.Classes.Set("danger", LogOutIsDestructive);
        else if (change.Property == LogOutCommandProperty) logOutAction.Observe(this.IsAttachedToVisualTree() ? LogOutCommand : null);
        else if (change.Property == LogOutCommandParameterProperty) logOutAction.Refresh();
        else if (change.Property == ItemTemplateProperty) menu.ItemTemplate = ItemTemplate;
        else if (change.Property == ItemContainerThemeProperty) menu.ItemContainerTheme = ItemContainerTheme;
        else if (change.Property == MenuThemeProperty) menu.FlyoutPresenterTheme = MenuTheme;
        else if (change.Property == MenuPlacementProperty || change.Property == MenuGapProperty) ApplyPlacement();
        else if (change.Property == ThemeModeProperty && !initializingTheme)
        {
            if (AutoApplyTheme && this.IsAttachedToVisualTree()) ApplyTheme();
            RaiseEvent(new AlderUserThemeChangedEventArgs(ThemeModeChangedEvent, change.GetOldValue<AlderUserThemeMode>(), ThemeMode));
        }
    }

    private void ObserveItems(bool attach = true)
    {
        if (observedItems is not null) observedItems.CollectionChanged -= OnItemsChanged;
        observedItems = attach ? ItemsSource as INotifyCollectionChanged : null;
        if (observedItems is not null) observedItems.CollectionChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshEntries();

    private void RefreshEntries()
    {
        var next = (ItemsSource ?? Items).Cast<object?>().ToList();
        if (ShowLogOut)
        {
            if (ShowLogOutSeparator && next.Count > 0 && next[^1] is not MenuItem { Header: "-" })
                next.Add(logOutSeparator);
            next.Add(logOutItem);
        }
        if (entries.SequenceEqual(next)) return;
        entries.Clear();
        entries.AddRange(next);
    }

    private void ApplyPlacement()
    {
        bool useVerticalPlacement = AnchoredPopupPlacement.IsVertical(MenuPlacement);
        menu.MotionPlacement = MenuPlacement;
        menu.CustomPopupPlacementCallback = useVerticalPlacement
            ? placement => AnchoredPopupPlacement.PlaceVertical(placement, MenuPlacement, MenuGap)
            : null;
        menu.Placement = useVerticalPlacement ? PlacementMode.Custom : MenuPlacement;
        menu.VerticalOffset = MenuPlacement switch
        {
            PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => -MenuGap,
            PlacementMode.Bottom or PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight => MenuGap,
            _ => 0
        };
        menu.HorizontalOffset = MenuPlacement switch
        {
            PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => -MenuGap,
            PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => MenuGap,
            _ => 0
        };
    }

    private void ReadHostTheme()
    {
        followsHostTheme = true;
        var requested = TopLevel.GetTopLevel(this)?.RequestedThemeVariant;
        if (requested is null || requested == ThemeVariant.Default) requested = Application.Current?.RequestedThemeVariant;
        var mode = requested == ThemeVariant.Dark ? AlderUserThemeMode.Dark :
            requested == ThemeVariant.Light ? AlderUserThemeMode.Light : AlderUserThemeMode.System;
        initializingTheme = true;
        try { SetCurrentValue(ThemeModeProperty, mode); }
        finally { initializingTheme = false; }
    }

    private void ApplyTheme()
    {
        var variant = ThemeMode switch
        {
            AlderUserThemeMode.Dark => ThemeVariant.Dark,
            AlderUserThemeMode.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
        Application.Current?.SetCurrentValue(Application.RequestedThemeVariantProperty, variant);
        TopLevel.GetTopLevel(this)?.SetCurrentValue(TopLevel.RequestedThemeVariantProperty, variant);
    }

    private sealed class LogOutAction(AlderUserMenu owner) : ICommand
    {
        private ICommand? observed;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => owner.LogOutCommand?.CanExecute(owner.LogOutCommandParameter) ?? true;
        public void Execute(object? parameter)
        {
            if (!owner.ShowLogOut || !CanExecute(parameter)) return;
            owner.menu.Hide();
            owner.LogOutCommand?.Execute(owner.LogOutCommandParameter);
            owner.RaiseEvent(new RoutedEventArgs(LogOutRequestedEvent));
        }

        public void Observe(ICommand? command)
        {
            if (observed is not null) observed.CanExecuteChanged -= Forward;
            observed = command;
            if (observed is not null) observed.CanExecuteChanged += Forward;
            Refresh();
        }
        public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        private void Forward(object? sender, EventArgs args) => Refresh();
    }
}
