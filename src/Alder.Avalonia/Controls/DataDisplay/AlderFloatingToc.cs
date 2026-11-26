using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Sets the physical edge used for the floating table of contents.</summary>
public enum FloatingTocPlacement
{
    /// <summary>Places the table of contents on the left edge.</summary>
    Left,
    /// <summary>Places the table of contents on the right edge.</summary>
    Right
}
/// <summary>Determines the interaction that opens the table of contents.</summary>
public enum FloatingTocTriggerMode
{
    /// <summary>Opens through pointer hover.</summary>
    Hover,
    /// <summary>Opens through explicit activation.</summary>
    Press
}

/// <summary>Identifies a scroll anchor and provides the text and heading level displayed for it.</summary>
public sealed record AlderTocEntry
{
    /// <summary>Creates an entry whose key and title can be supplied through property initializers.</summary>
    public AlderTocEntry() { }
    /// <summary>Creates a heading entry for the supplied anchor key.</summary>
    /// <param name="key">The key shared with the scroll anchor.</param>
    /// <param name="title">The display label.</param>
    /// <param name="level">The heading level used to represent the entry.</param>
    public AlderTocEntry(string key, string title, int level = 1) => (Key, Title, Level) = (key, title, level);
    /// <summary>The key shared with the target's attached anchor key.</summary>
    public string Key { get; init; } = "";
    /// <summary>The text shown when this heading is listed.</summary>
    public string Title { get; init; } = "";
    /// <summary>The heading level used to determine indentation and marker size.</summary>
    public int Level { get; init; } = 1;
}

/// <summary>A compact section navigator. Attach AnchorKey to headings in its ScrollHost, or handle NavigateCommand in the view model.</summary>
public sealed class AlderFloatingToc : TemplatedControl
{
    /// <summary>Identifies the <see cref="GetAnchorKey"/> attached property.</summary>
    public static readonly AttachedProperty<string?> AnchorKeyProperty =
        AvaloniaProperty.RegisterAttached<AlderFloatingToc, Control, string?>("AnchorKey");
    /// <summary>Gets the key that associates a scroll target with a table-of-contents entry.</summary>
    public static string? GetAnchorKey(Control control) => control.GetValue(AnchorKeyProperty);
    /// <summary>Sets the key that associates a scroll target with a table-of-contents entry.</summary>
    public static void SetAnchorKey(Control control, string? key) => control.SetValue(AnchorKeyProperty, key);

    /// <summary>Identifies the <see cref="ItemsSource"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable<AlderTocEntry>?> ItemsSourceProperty =
        AvaloniaProperty.Register<AlderFloatingToc, IEnumerable<AlderTocEntry>?>(nameof(ItemsSource));
    /// <summary>Identifies the <see cref="ActiveKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> ActiveKeyProperty =
        AvaloniaProperty.Register<AlderFloatingToc, string?>(nameof(ActiveKey), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="IsOpen"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AlderFloatingToc, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="Placement"/> Avalonia property.</summary>
    public static readonly StyledProperty<FloatingTocPlacement> PlacementProperty =
        AvaloniaProperty.Register<AlderFloatingToc, FloatingTocPlacement>(nameof(Placement), FloatingTocPlacement.Right);
    /// <summary>Identifies the <see cref="TriggerMode"/> Avalonia property.</summary>
    public static readonly StyledProperty<FloatingTocTriggerMode> TriggerModeProperty =
        AvaloniaProperty.Register<AlderFloatingToc, FloatingTocTriggerMode>(nameof(TriggerMode));
    /// <summary>Identifies the <see cref="OpenDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> OpenDelayProperty =
        AvaloniaProperty.Register<AlderFloatingToc, TimeSpan>(nameof(OpenDelay), TimeSpan.FromMilliseconds(200), validate: value => value >= TimeSpan.Zero);
    /// <summary>Identifies the <see cref="CloseDelay"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> CloseDelayProperty =
        AvaloniaProperty.Register<AlderFloatingToc, TimeSpan>(nameof(CloseDelay), TimeSpan.FromMilliseconds(300), validate: value => value >= TimeSpan.Zero);
    /// <summary>Identifies the <see cref="AlignBarsLeft"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> AlignBarsLeftProperty =
        AvaloniaProperty.Register<AlderFloatingToc, bool>(nameof(AlignBarsLeft));
    /// <summary>Identifies the <see cref="Header"/> Avalonia property.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<AlderFloatingToc, string?>(nameof(Header));
    /// <summary>Identifies the <see cref="ScrollHost"/> Avalonia property.</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollHostProperty =
        AvaloniaProperty.Register<AlderFloatingToc, ScrollViewer?>(nameof(ScrollHost));
    /// <summary>Identifies the <see cref="NavigateCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<AlderFloatingToc, ICommand?>(nameof(NavigateCommand));
    /// <summary>Identifies the <see cref="IsLightDismissEnabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<AlderFloatingToc, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>The inline collection of heading entries.</summary>
    public AvaloniaList<AlderTocEntry> Items { get; } = [];
    /// <summary>The external source of heading entries used in place of the inline collection.</summary>
    public IEnumerable<AlderTocEntry>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    /// <summary>The key of the heading associated with the current scroll position or selection.</summary>
    public string? ActiveKey { get => GetValue(ActiveKeyProperty); set => SetValue(ActiveKeyProperty, value); }
    /// <summary>Whether the surface is open.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    /// <summary>The left or right viewport edge used for the table of contents.</summary>
    public FloatingTocPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>Whether hovering or pressing the trigger opens the table of contents.</summary>
    public FloatingTocTriggerMode TriggerMode { get => GetValue(TriggerModeProperty); set => SetValue(TriggerModeProperty, value); }
    /// <summary>The delay before the hover trigger opens the surface.</summary>
    public TimeSpan OpenDelay { get => GetValue(OpenDelayProperty); set => SetValue(OpenDelayProperty, value); }
    /// <summary>The delay before the hover surface closes.</summary>
    public TimeSpan CloseDelay { get => GetValue(CloseDelayProperty); set => SetValue(CloseDelayProperty, value); }
    /// <summary>Whether the rail markers align to its left edge.</summary>
    public bool AlignBarsLeft { get => GetValue(AlignBarsLeftProperty); set => SetValue(AlignBarsLeftProperty, value); }
    /// <summary>The optional content displayed in the header.</summary>
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    /// <summary>The scroll viewer that contains the tracked anchors.</summary>
    public ScrollViewer? ScrollHost { get => GetValue(ScrollHostProperty); set => SetValue(ScrollHostProperty, value); }
    /// <summary>The optional command used to navigate to a heading.</summary>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
    /// <summary>Whether an interaction outside the surface may dismiss it.</summary>
    public bool IsLightDismissEnabled { get => GetValue(IsLightDismissEnabledProperty); set => SetValue(IsLightDismissEnabledProperty, value); }

    private TocRow[] rows = [];
    private INotifyCollectionChanged? observedCollection;
    private ScrollViewer? observedHost;
    private Button? trigger;
    private Popup? popup;
    private Control? surface;
    private ListBox? list;
    private ItemsControl? bars;
    private IDisposable? timer;
    private bool focusOnOpen;
    private bool settingPopup;
    private bool switching;
    private double? navigatedOffset;
    private readonly MenuPopupMotion motion = new();

    /// <summary>Initializes a new instance of <see cref="AlderFloatingToc"/>.</summary>
    public AlderFloatingToc() => Items.CollectionChanged += ItemsChanged;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveItems(ItemsSource as INotifyCollectionChanged);
        ObserveHost(ScrollHost);
        RefreshItems();
        UpdatePopup();
        Dispatcher.UIThread.Post(UpdateScrollPosition, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopDelay();
        ObserveHost(null);
        ObserveItems(null);
        ClosePopupImmediately();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnwireTemplate();
        base.OnApplyTemplate(e);
        trigger = e.NameScope.Find<Button>("PART_Trigger");
        popup = e.NameScope.Find<Popup>("PART_Popup");
        surface = e.NameScope.Find<Control>("PART_Surface");
        list = e.NameScope.Find<ListBox>("PART_List");
        bars = e.NameScope.Find<ItemsControl>("PART_Bars");
        if (trigger is not null)
        {
            trigger.Click += TriggerClicked;
            trigger.PointerEntered += PointerArrived;
            trigger.PointerExited += PointerLeft;
            trigger.KeyDown += OnTriggerKey;
        }
        if (surface is not null)
        {
            surface.PointerEntered += PointerArrived;
            surface.PointerExited += PointerLeft;
        }
        if (list is not null)
        {
            list.AddHandler(InputElement.TappedEvent, ActivateTapped);
            list.KeyDown += OnListKey;
            list.LostFocus += OnListLostFocus;
        }
        if (popup is not null)
        {
            popup.Opened += PopupOpened;
            popup.Closed += PopupClosed;
        }
        RefreshItems();
        UpdatePlacement();
        UpdatePopup();
    }

    private void UnwireTemplate()
    {
        ClosePopupImmediately();
        if (trigger is not null)
        {
            trigger.Click -= TriggerClicked;
            trigger.PointerEntered -= PointerArrived;
            trigger.PointerExited -= PointerLeft;
            trigger.KeyDown -= OnTriggerKey;
        }
        if (surface is not null)
        {
            surface.PointerEntered -= PointerArrived;
            surface.PointerExited -= PointerLeft;
        }
        if (list is not null)
        {
            list.RemoveHandler(InputElement.TappedEvent, ActivateTapped);
            list.KeyDown -= OnListKey;
            list.LostFocus -= OnListLostFocus;
        }
        if (popup is not null)
        {
            popup.Opened -= PopupOpened;
            popup.Closed -= PopupClosed;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveItems(this.IsAttachedToVisualTree() ? ItemsSource as INotifyCollectionChanged : null);
            RefreshItems();
        }
        else if (change.Property == ActiveKeyProperty) UpdateActive();
        else if (change.Property == IsOpenProperty) UpdatePopup();
        else if (change.Property == PlacementProperty || change.Property == AlignBarsLeftProperty) UpdatePlacement();
        else if (change.Property == TriggerModeProperty) StopDelay();
        else if (change.Property == ScrollHostProperty && this.IsAttachedToVisualTree())
        {
            ObserveHost(ScrollHost);
            UpdateScrollPosition();
        }
    }

    /// <summary>Activates an entry, invokes the caller's navigation command and scrolls to its anchor if available.</summary>
    public bool NavigateTo(string key)
    {
        if (rows.All(row => row.Entry.Key != key) || NavigateCommand is { } command && !command.CanExecute(key))
            return false;
        SetCurrentValue(ActiveKeyProperty, key);
        NavigateCommand?.Execute(key);
        if (ScrollHost is { } host && FindAnchors(host).FirstOrDefault(anchor => GetAnchorKey(anchor) == key) is { } anchor)
            ScrollToAnchor(host, anchor);
        SetCurrentValue(IsOpenProperty, false);
        return true;
    }

    private void ObserveItems(INotifyCollectionChanged? source)
    {
        if (ReferenceEquals(source, Items)) source = null;
        if (observedCollection is not null) observedCollection.CollectionChanged -= ItemsChanged;
        observedCollection = source;
        if (observedCollection is not null) observedCollection.CollectionChanged += ItemsChanged;
    }

    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        rows = (ItemsSource ?? Items).Select(entry => new TocRow(entry)).ToArray();
        if (list is not null) list.ItemsSource = rows;
        PseudoClasses.Set(":empty", rows.Length == 0);
        UpdateActive();
        UpdatePlacement();
        UpdatePopup();
    }

    private void UpdateActive()
    {
        string? active = ActiveKey ?? rows.FirstOrDefault()?.Entry.Key;
        foreach (var row in rows) row.IsActive = row.Entry.Key == active;
        if (bars is null) return;
        const int maximumBars = 20;
        if (rows.Length <= maximumBars)
        {
            bars.ItemsSource = rows;
            return;
        }

        var condensed = new TocRow[maximumBars];
        for (int index = 0; index < maximumBars; index++)
            condensed[index] = rows[(int)Math.Round(index * (rows.Length - 1.0) / (maximumBars - 1))];
        int activeIndex = Array.FindIndex(rows, row => row.IsActive);
        if (activeIndex >= 0)
            condensed[(int)Math.Round(activeIndex * (maximumBars - 1.0) / (rows.Length - 1))] = rows[activeIndex];
        bars.ItemsSource = condensed;
    }

    private void UpdatePlacement()
    {
        bool left = Placement == FloatingTocPlacement.Left;
        if (popup is not null)
        {
            popup.Placement = left ? PlacementMode.Right : PlacementMode.Left;
            popup.HorizontalOffset = left ? 8 : -8;
        }
        foreach (var row in rows) row.BarAlignment = AlignBarsLeft || left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void TriggerClicked(object? sender, RoutedEventArgs e)
    {
        StopDelay();
        SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    private void PointerArrived(object? sender, PointerEventArgs e)
    {
        if (TriggerMode != FloatingTocTriggerMode.Hover || e.Pointer.Type != PointerType.Mouse) return;
        StopDelay();
        if (sender == trigger && !IsOpen) Schedule(true, OpenDelay);
    }

    private void PointerLeft(object? sender, PointerEventArgs e)
    {
        if (TriggerMode == FloatingTocTriggerMode.Hover && e.Pointer.Type == PointerType.Mouse) Schedule(false, CloseDelay);
    }

    private void Schedule(bool open, TimeSpan delay)
    {
        StopDelay();
        void Apply()
        {
            timer = null;
            if (open || trigger?.IsPointerOver != true && surface?.IsPointerOver != true && list?.IsKeyboardFocusWithin != true)
                SetCurrentValue(IsOpenProperty, open);
        }
        if (delay == TimeSpan.Zero) Apply();
        else timer = DispatcherTimer.RunOnce(Apply, delay);
    }

    private void StopDelay()
    {
        timer?.Dispose();
        timer = null;
    }

    private void UpdatePopup()
    {
        if (popup is null || !this.IsAttachedToVisualTree()) return;
        bool open = IsOpen && rows.Length > 0;
        settingPopup = true;
        try
        {
            if (open && motion.IsClosing) motion.FinishClose();
            if (!open && popup.IsOpen)
            {
                var closing = new CancelEventArgs();
                motion.Closing(closing);
                if (closing.Cancel) return;
            }
            popup.IsOpen = open;
        }
        finally { settingPopup = false; }
    }

    private void ClosePopupImmediately()
    {
        settingPopup = true;
        try
        {
            if (popup is not null)
            {
                if (motion.IsClosing) motion.FinishClose();
                popup.IsOpen = false;
                if (motion.IsClosing) motion.FinishClose();
            }
        }
        finally { settingPopup = false; }
    }

    private void PopupOpened(object? sender, EventArgs e)
    {
        if (popup is not null && surface is not null && trigger is not null)
            motion.Opened(popup, surface, trigger);
        if (focusOnOpen) FocusList();
        focusOnOpen = false;
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        motion.Closed();
        if (!settingPopup && popup?.IsOpen != true) SetCurrentValue(IsOpenProperty, false);
    }

    private void OnTriggerKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter or Key.Space)
        {
            if (IsOpen) FocusList();
            else
            {
                focusOnOpen = true;
                SetCurrentValue(IsOpenProperty, true);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
            e.Handled = true;
        }
    }

    private void FocusList()
    {
        if (list is null || rows.Length == 0) return;
        int index = Math.Max(0, Array.FindIndex(rows, row => row.IsActive));
        list.SelectedIndex = index;
        list.ScrollIntoView(index);
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && list is { } navigation) (navigation.ContainerFromIndex(index) ?? navigation).Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    private void ActivateTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.Content is TocRow row)
        {
            NavigateTo(row.Entry.Key);
            e.Handled = true;
        }
    }

    private void OnListKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && list?.SelectedItem is TocRow row)
        {
            NavigateTo(row.Entry.Key);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SetCurrentValue(IsOpenProperty, false);
            trigger?.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private void OnListLostFocus(object? sender, RoutedEventArgs e)
    {
        if (TriggerMode == FloatingTocTriggerMode.Hover) Schedule(false, CloseDelay);
    }

    private void ObserveHost(ScrollViewer? host)
    {
        if (observedHost is not null)
        {
            observedHost.ScrollChanged -= OnScrollChanged;
            observedHost.PointerWheelChanged -= StopScrolling;
            observedHost.PointerPressed -= StopScrolling;
        }
        observedHost = host;
        scrollVersion++;
        switching = false;
        navigatedOffset = null;
        if (host is null) return;
        host.ScrollChanged += OnScrollChanged;
        host.PointerWheelChanged += StopScrolling;
        host.PointerPressed += StopScrolling;
    }

    private static IEnumerable<Control> FindAnchors(ScrollViewer host) =>
        host.Content is Control root ? root.GetVisualDescendants().Prepend(root).OfType<Control>().Where(c =>
            c.IsEffectivelyVisible && GetAnchorKey(c) is not null) : Enumerable.Empty<Control>();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateScrollPosition();

    private void UpdateScrollPosition()
    {
        if (switching || observedHost is not { } host) return;
        if (navigatedOffset is { } target && Math.Abs(target - host.Offset.Y) < .5) return;
        navigatedOffset = null;
        string? current = null;
        double best = double.NegativeInfinity;
        double first = double.PositiveInfinity;
        string? firstKey = null;
        bool atEnd = host.Extent.Height > host.Viewport.Height && host.Offset.Y >= host.Extent.Height - host.Viewport.Height - .5;
        foreach (var anchor in FindAnchors(host))
        {
            string? key = GetAnchorKey(anchor);
            if (anchor.TranslatePoint(default, host) is not { } point || rows.All(row => row.Entry.Key != key)) continue;
            if (point.Y < first) (first, firstKey) = (point.Y, key);
            if ((atEnd || point.Y <= 36) && point.Y > best) (best, current) = (point.Y, key);
        }
        if ((current ?? firstKey) is { } active) SetCurrentValue(ActiveKeyProperty, active);
    }

    private int scrollVersion;

    private void StopScrolling(object? sender, PointerEventArgs e)
    {
        scrollVersion++;
        switching = false;
        navigatedOffset = null;
    }

    private void ScrollToAnchor(ScrollViewer host, Control anchor)
    {
        if (anchor.TranslatePoint(default, host) is not { } point) return;
        Vector from = host.Offset;
        double extent = Math.Max(0, host.Extent.Height - host.Viewport.Height);
        Vector to = new(from.X, Math.Clamp(from.Y + point.Y - 12, 0, extent));
        int version = ++scrollVersion;
        navigatedOffset = to.Y;
        switching = true;
        if (AlderMotion.GetReduced(this) || TopLevel.GetTopLevel(this) is not { } root || to == from)
        {
            host.SetCurrentValue(ScrollViewer.OffsetProperty, to);
            switching = false;
            return;
        }
        long start = Stopwatch.GetTimestamp();
        void Render(TimeSpan _)
        {
            if (version != scrollVersion || !this.IsAttachedToVisualTree() || observedHost != host) return;
            double progress = Math.Min(Stopwatch.GetElapsedTime(start).TotalMilliseconds / 240, 1);
            double eased = 1 - Math.Pow(1 - progress, 3);
            host.SetCurrentValue(ScrollViewer.OffsetProperty, from + (to - from) * eased);
            if (progress < 1) root.RequestAnimationFrame(Render);
            else switching = false;
        }
        root.RequestAnimationFrame(Render);
    }
}

internal sealed class TocRow(AlderTocEntry entry) : AvaloniaObject
{
    public static readonly DirectProperty<TocRow, bool> IsActiveProperty =
        AvaloniaProperty.RegisterDirect<TocRow, bool>(nameof(IsActive), row => row.IsActive);
    public static readonly DirectProperty<TocRow, HorizontalAlignment> BarAlignmentProperty =
        AvaloniaProperty.RegisterDirect<TocRow, HorizontalAlignment>(nameof(BarAlignment), row => row.BarAlignment);

    public AlderTocEntry Entry { get; } = entry;
    private int Depth => Math.Clamp(Entry.Level - 1, 0, 7);
    public double BarWidth => Math.Max(4, (IsActive ? 24 : 16) - Depth * 3);
    public Thickness Indent => new(8 + Depth * 12, 0, 8, 0);
    public bool IsActive
    {
        get => field;
        set
        {
            if (!SetAndRaise(IsActiveProperty, ref field, value)) return;
            RaisePropertyChanged(BarWidthProperty, Math.Max(4, (value ? 16 : 24) - Depth * 3), BarWidth);
        }
    }
    public static readonly DirectProperty<TocRow, double> BarWidthProperty =
        AvaloniaProperty.RegisterDirect<TocRow, double>(nameof(BarWidth), row => row.BarWidth);
    public HorizontalAlignment BarAlignment { get => field; set => SetAndRaise(BarAlignmentProperty, ref field, value); }
}
