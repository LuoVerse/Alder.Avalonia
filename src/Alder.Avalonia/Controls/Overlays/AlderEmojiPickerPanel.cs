using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Alder.Avalonia.Controls;

// Rows are the virtualization unit. A row creates at most one screen-width of emoji buttons.
internal sealed class AlderEmojiPickerPanel : TemplatedControl
{
    private const string RecentKey = "recent";
    private const int CellHeight = 38;
    private readonly AlderEmojiPicker owner;
    private readonly List<EmojiPickerCell> cells = [];
    private readonly Dictionary<string, int> categoryRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> categoryButtons = new(StringComparer.Ordinal);
    private AlderSearchField? search;
    private ListBox? grid;
    private UniformGrid? categories;
    private Button? tone;
    private ContentPresenter? toneEmoji;
    private AlderPopover? tonePopup;
    private INotifyCollectionChanged? observedRecents;
    private EmojiPickerCell? active;
    private int columns;
    private int cellWidth;
    private int updateVersion;

    private static readonly (string Glyph, string Name)[] Tones =
    [
        ("✋", "Default"), ("✋🏻", "Light"), ("✋🏼", "MediumLight"),
        ("✋🏽", "Medium"), ("✋🏾", "MediumDark"), ("✋🏿", "Dark")
    ];

    internal AlderEmojiPickerPanel(AlderEmojiPicker owner, bool isInline)
    {
        this.owner = owner;
        Classes.Set("inline", isInline);
        AddHandler(KeyDownEvent, HandleNavigation, RoutingStrategies.Tunnel);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (search is not null) search.PropertyChanged -= OnSearchPropertyChanged;
        if (grid is not null)
        {
            grid.RemoveHandler(Button.ClickEvent, OnCellClick);
            grid.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScroll);
        }
        if (tone is not null) tone.Click -= OnToneClick;
        tonePopup?.HideImmediately();
        tonePopup = null;
        base.OnApplyTemplate(e);
        search = e.NameScope.Find<AlderSearchField>("PART_Search");
        grid = e.NameScope.Find<ListBox>("PART_Grid");
        categories = e.NameScope.Find<UniformGrid>("PART_Categories");
        tone = e.NameScope.Find<Button>("PART_Tone");
        toneEmoji = e.NameScope.Find<ContentPresenter>("PART_ToneGlyph");
        if (search is not null) search.PropertyChanged += OnSearchPropertyChanged;
        if (grid is not null)
        {
            grid.AddHandler(Button.ClickEvent, OnCellClick);
            grid.AddHandler(ScrollViewer.ScrollChangedEvent, OnScroll);
        }
        if (tone is not null) tone.Click += OnToneClick;
        UpdateTone();
        CreateCategories();
        Rebuild(resetOffset: true, revealSelection: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveRecents();
        Rebuild(resetOffset: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (observedRecents is not null) observedRecents.CollectionChanged -= OnRecentsChanged;
        observedRecents = null;
        tonePopup?.HideImmediately();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (CellMetrics() != (columns, cellWidth)) Rebuild(resetOffset: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // An inline picker prefers its preset width and yields to a narrower parent.
        var width = Math.Min(availableSize.Width, owner.Metrics.Width);
        var content = base.MeasureOverride(new Size(width, availableSize.Height));
        return new Size(width, content.Height);
    }

    internal void Refresh(AvaloniaProperty property)
    {
        if (property == AlderEmojiPicker.RecentEmojisProperty) ObserveRecents();
        if (property == AlderEmojiPicker.SkinToneProperty) UpdateTone();
        if (property == AlderEmojiPicker.CategoriesProperty || property == AlderEmojiPicker.ShowRecentProperty ||
            property == AlderEmojiPicker.SizeProperty)
            CreateCategories();
        bool reset = property == AlderEmojiPicker.CategoriesProperty || property == AlderEmojiPicker.FilterProperty;
        Rebuild(reset);
    }

    internal void ResetSearch()
    {
        if (!string.IsNullOrEmpty(search?.Text)) search.SetCurrentValue(TextBox.TextProperty, "");
        Rebuild(resetOffset: true, revealSelection: true);
    }

    private IReadOnlyList<AlderEmojiCategory> Sections => owner.Categories ?? AlderEmojiPicker.DefaultCategories;

    private void ObserveRecents()
    {
        if (observedRecents is not null) observedRecents.CollectionChanged -= OnRecentsChanged;
        observedRecents = this.IsAttachedToVisualTree() ? owner.RecentItems as INotifyCollectionChanged : null;
        if (observedRecents is not null) observedRecents.CollectionChanged += OnRecentsChanged;
    }

    private void OnRecentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!owner.IsCommitting) Rebuild(resetOffset: false);
    }

    private (int Columns, int Width) CellMetrics()
    {
        var preset = owner.Metrics;
        var width = Math.Max(1, (int)(Bounds.Width > 0 ? Bounds.Width : preset.Width) - 16);
        var count = Math.Clamp((width - 8) / 40, 1, preset.Columns);
        return (count, Math.Max(1, (width - 10) / count));
    }

    private void Rebuild(bool resetOffset, bool revealSelection = false)
    {
        if (grid is null) return;
        (columns, cellWidth) = CellMetrics();
        var selected = owner.SelectedEmoji;
        var query = search?.Text?.Trim() ?? "";
        var terms = query.Trim(':').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var glyphSize = owner.Metrics.GlyphSize;
        var previousOffset = (grid.Scroll as ScrollViewer)?.Offset.Y ?? 0;
        var rows = new List<EmojiPickerRow>();
        cells.Clear();
        categoryRows.Clear();
        active = null;

        if (query.Length == 0 && owner.ShowRecent && owner.RecentItems.Count > 0)
        {
            var catalog = Sections.SelectMany(section => section.Items).ToArray();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var recent = new List<(AlderEmoji Item, string Glyph)>();
            foreach (var glyph in owner.RecentItems.Take(24))
            {
                if (!unique.Add(glyph)) continue;
                var item = Array.Find(catalog, emoji => emoji.HasGlyph(glyph));
                if (item is not null) recent.Add((item, glyph));
            }
            AddSection(RecentKey, recent);
        }

        if (query.Length == 0)
            foreach (var section in Sections)
                AddSection(section.Key, section.Items.Select(item => (item, item.GetGlyph(owner.SkinTone))));
        else
        {
            var matches = Sections.SelectMany(section => section.Items).Where(item =>
                owner.Filter?.Invoke(item, query) ?? (terms.Length == 0 ? item.Matches(query) : terms.All(item.Matches)));
            AddSection("", matches.Select(item => (item, item.GetGlyph(owner.SkinTone))));
        }

        grid.ItemsSource = rows;
        Classes.Set("empty", cells.Count == 0);
        SetActiveCategory(null);
        if (categoryButtons.TryGetValue(RecentKey, out var recentButton))
            recentButton.IsEnabled = owner.RecentItems.Count > 0;
        var version = ++updateVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != updateVersion || grid is null || !this.IsAttachedToVisualTree()) return;
            if (grid.Scroll is ScrollViewer scroll)
                scroll.Offset = new Vector(0, resetOffset ? 0 : previousOffset);
            if (revealSelection && cells.Find(cell => cell.IsSelected) is { Row: > 0 } cell)
                grid.ScrollIntoView(cell.Row);
        }, DispatcherPriority.Loaded);

        void AddSection(string key, IEnumerable<(AlderEmoji Item, string Glyph)> entries)
        {
            var row = new List<EmojiPickerCell>(columns);
            foreach (var (item, glyph) in entries)
            {
                if (row.Count == 0 && key.Length != 0) categoryRows.TryAdd(key, rows.Count);
                var cell = new EmojiPickerCell(item, glyph, cells.Count, rows.Count, cellWidth, glyphSize, glyph == selected);
                row.Add(cell);
                cells.Add(cell);
                if (row.Count == columns)
                {
                    rows.Add(new EmojiPickerRow(row));
                    row = new List<EmojiPickerCell>(columns);
                }
            }
            if (row.Count > 0) rows.Add(new EmojiPickerRow(row));
        }
    }

    private void CreateCategories()
    {
        if (categories is null) return;
        categories.Children.Clear();
        categoryButtons.Clear();
        if (owner.ShowRecent) Add(RecentKey, "🕐", "AlderText.EmojiRecent", "Recently used");
        foreach (var section in Sections)
            if (!categoryButtons.ContainsKey(section.Key))
                Add(section.Key, section.Icon, section.ResourceKey, section.Title, section.IconData);

        void Add(string key, object? icon, string? labelKey, string title, Geometry? iconData = null)
        {
            var button = GlyphButton(icon as string ?? "", "AlderEmojiCategoryButtonTheme", 16);
            if (icon is Control or Geometry || icon is string && AlderIconPresenter.AsGeometry(icon) is not null)
                {
                double extent = owner.Size switch { AlderSize.Small => 14, AlderSize.Large => 18, _ => 16 };
                button.Content = new AlderIconPresenter { Content = icon, Width = extent, Height = extent };
            }
            if (iconData is not null)
            {
                var outline = new ShapePath
                {
                    Data = iconData, Width = 14, Height = 14, Stretch = Stretch.Uniform,
                    StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round
                };
                outline[!Shape.StrokeProperty] = new DynamicResourceExtension("AlderMuted");
                button.Content = outline;
            }
            if (labelKey is null) AutomationProperties.SetName(button, title);
            else button[!AutomationProperties.NameProperty] = new DynamicResourceExtension(labelKey);
            button.Click += (_, _) => ShowCategory(key);
            categoryButtons.Add(key, button);
            categories.Children.Add(button);
        }
    }

    private void ShowCategory(string category)
    {
        if (grid is null) return;
        if (!string.IsNullOrEmpty(search?.Text)) search.SetCurrentValue(TextBox.TextProperty, "");
        if (!categoryRows.TryGetValue(category, out var row)) return;
        int version = ++updateVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != updateVersion || grid?.Scroll is not ScrollViewer scroll) return;
            scroll.Offset = new Vector(0, row * CellHeight);
            SetActiveCategory(category);
        }, DispatcherPriority.Loaded);
    }

    private void OnScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (grid?.Scroll is not ScrollViewer scroll || !string.IsNullOrWhiteSpace(search?.Text)) return;
        var firstRow = (int)Math.Floor(scroll.Offset.Y / CellHeight);
        var category = categoryRows.LastOrDefault(entry => entry.Value <= firstRow).Key;
        SetActiveCategory(category);
    }

    private void SetActiveCategory(string? category)
    {
        foreach (var (key, button) in categoryButtons)
            button.Classes.Set("active", key == category);
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty) Rebuild(resetOffset: true);
    }

    private void OnCellClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: EmojiPickerCell cell })
        {
            e.Handled = true;
            owner.Pick(cell.Item, cell.Glyph);
        }
    }

    private void HandleNavigation(object? sender, KeyEventArgs e)
    {
        if (grid is null || cells.Count == 0 || e.KeyModifiers != KeyModifiers.None ||
            e.Source is not Visual origin) return;
        bool inGrid = ReferenceEquals(grid, origin) || grid.IsVisualAncestorOf(origin);
        bool inSearch = ReferenceEquals(search, origin);
        if (!inGrid && !inSearch) return;

        var focusCell = (origin as Control)?.DataContext as EmojiPickerCell;
        var current = focusCell?.Index ?? active?.Index ?? -1;
        if (e.Key == Key.Enter)
        {
            if (current < 0 && inSearch && string.IsNullOrWhiteSpace(search?.Text)) return;
            var selected = cells[Math.Max(0, current)];
            owner.Pick(selected.Item, selected.Glyph);
        }
        else
        {
            int? next = e.Key switch
            {
                Key.Down => current < 0 ? 0 : current + columns,
                Key.Up => current < 0 ? 0 : current - columns,
                Key.Right when inGrid => current + 1,
                Key.Left when inGrid => current - 1,
                Key.Home when inGrid => 0,
                Key.End when inGrid => cells.Count - 1,
                Key.PageDown when inGrid => current + columns * 5,
                Key.PageUp when inGrid => current - columns * 5,
                _ => null
            };
            if (next is null) return;
            if (active is not null) active.IsActive = false;
            active = cells[Math.Clamp(next.Value, 0, cells.Count - 1)];
            setActive();
        }
        e.Handled = true;

        void setActive()
        {
            var target = active!;
            target.IsActive = true;
            grid.ScrollIntoView(target.Row);
            if (inGrid)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!ReferenceEquals(active, target)) return;
                    grid.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => ReferenceEquals(b.DataContext, target))
                        ?.Focus(NavigationMethod.Directional);
                }, DispatcherPriority.Loaded);
        }
    }

    private void UpdateTone()
    {
        if (toneEmoji is not null) toneEmoji.Content = Tones[Math.Clamp((int)owner.SkinTone, 0, Tones.Length - 1)].Glyph;
    }

    private void OnToneClick(object? sender, RoutedEventArgs e)
    {
        if (tone is null) return;
        if (tonePopup?.IsOpen == true && !tonePopup.IsClosing) { tonePopup.Hide(); return; }
        tonePopup?.FinishExit();
        var options = new UniformGrid { Rows = 1 };
        for (int i = 0; i < Tones.Length; i++)
        {
            var toneValue = (AlderEmojiSkinTone)i;
            var (glyph, name) = Tones[i];
            var option = GlyphButton(glyph, "AlderEmojiToneButtonTheme", 20);
            option[!AutomationProperties.NameProperty] = new DynamicResourceExtension($"AlderText.EmojiTone{name}");
            option.Classes.Set("active", owner.SkinTone == toneValue);
            option.Click += (_, _) =>
            {
                owner.SetCurrentValue(AlderEmojiPicker.SkinToneProperty, toneValue);
                tonePopup?.Hide();
                search?.Focus();
            };
            options.Children.Add(option);
        }
        tonePopup = new AlderPopover
        {
            Content = options, ContentPadding = new Thickness(6, 3), SurfaceCornerRadius = new CornerRadius(18),
            Placement = PlacementMode.BottomEdgeAlignedRight, Offset = 4, FocusReturnTarget = tone,
            Motion = AnchoredSurfaceMotion.ListBox
        };
        tonePopup.ShowAt(tone);
    }

    private static Button GlyphButton(string glyph, string theme, int fontSize)
    {
        var text = new AlderTextBlock { Text = glyph, FontSize = fontSize, FlowDirection = FlowDirection.LeftToRight };
        text[!TextBlock.FontFamilyProperty] = new DynamicResourceExtension("AlderEmojiFont");
        var button = new AlderButton { Content = text };
        button[!ThemeProperty] = new DynamicResourceExtension(theme);
        return button;
    }
}

internal sealed record EmojiPickerRow(IReadOnlyList<EmojiPickerCell> Cells);

internal sealed class EmojiPickerCell(AlderEmoji item, string glyph, int index, int row, int width, int fontSize, bool selected)
    : INotifyPropertyChanged
{
    public AlderEmoji Item { get; } = item;
    public string Name => Item.GetDisplayName();
    public string Glyph { get; } = glyph;
    public int Index { get; } = index;
    public int Row { get; } = row;
    public int Width { get; } = width;
    public int FontSize { get; } = fontSize;
    public bool IsSelected { get; } = selected;
    public bool IsActive
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
