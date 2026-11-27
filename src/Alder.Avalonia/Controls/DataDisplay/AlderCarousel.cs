using System.Diagnostics;
using System.Globalization;
using Avalonia;
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

namespace Alder.Avalonia.Controls;

/// <summary>Selects the presentation used for the slides and their navigation.</summary>
public enum AlderCarouselLayout
{
    /// <summary>Shows slide content inline.</summary>
    InPlace,
    /// <summary>Uses the presentation intended for a modal slide viewer.</summary>
    Modal,
    /// <summary>Uses the thumbnail-oriented presentation.</summary>
    Miniatures
}

/// <summary>Displays a collection of slides with drag, keyboard, and optional automatic navigation.</summary>
public sealed class AlderCarousel : ItemsControl
{
    /// <summary>Identifies the <see cref="SelectedIndex"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<AlderCarousel, int>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="SelectedItem"/> Avalonia property.</summary>
    public static readonly DirectProperty<AlderCarousel, object?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<AlderCarousel, object?>(nameof(SelectedItem), owner => owner.SelectedItem);
    /// <summary>Identifies the <see cref="Layout"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderCarouselLayout> LayoutProperty =
        AvaloniaProperty.Register<AlderCarousel, AlderCarouselLayout>(nameof(Layout));
    /// <summary>Identifies the <see cref="SlidesPerView"/> Avalonia property.</summary>
    public static readonly StyledProperty<int> SlidesPerViewProperty =
        AvaloniaProperty.Register<AlderCarousel, int>(nameof(SlidesPerView), 1, validate: value => value >= 1);
    /// <summary>Identifies the <see cref="SlideSpacing"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> SlideSpacingProperty =
        AvaloniaProperty.Register<AlderCarousel, double>(nameof(SlideSpacing), 10, validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="StageMaxWidth"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> StageMaxWidthProperty =
        AvaloniaProperty.Register<AlderCarousel, double>(nameof(StageMaxWidth), double.PositiveInfinity, validate: value => value >= 0);
    /// <summary>Identifies the <see cref="ViewportAspectRatio"/> Avalonia property.</summary>
    public static readonly StyledProperty<double> ViewportAspectRatioProperty =
        AvaloniaProperty.Register<AlderCarousel, double>(nameof(ViewportAspectRatio), validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>Identifies the <see cref="Loop"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(Loop));
    /// <summary>Identifies the <see cref="IsDragEnabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsDragEnabledProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(IsDragEnabled), true);
    /// <summary>Identifies the <see cref="ShowNavigation"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowNavigationProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(ShowNavigation), true);
    /// <summary>Identifies the <see cref="ShowDots"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowDotsProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(ShowDots), true);
    /// <summary>Identifies the <see cref="ShowThumbnails"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowThumbnailsProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(ShowThumbnails));
    /// <summary>Identifies the <see cref="ThumbnailTemplate"/> Avalonia property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ThumbnailTemplateProperty =
        AvaloniaProperty.Register<AlderCarousel, IDataTemplate?>(nameof(ThumbnailTemplate));
    /// <summary>Identifies the <see cref="IsAutoPlayEnabled"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> IsAutoPlayEnabledProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(IsAutoPlayEnabled), defaultBindingMode: BindingMode.TwoWay);
    /// <summary>Identifies the <see cref="AutoPlayInterval"/> Avalonia property.</summary>
    public static readonly StyledProperty<TimeSpan> AutoPlayIntervalProperty =
        AvaloniaProperty.Register<AlderCarousel, TimeSpan>(nameof(AutoPlayInterval), TimeSpan.FromSeconds(4),
            validate: value => value >= TimeSpan.FromMilliseconds(100) && value <= TimeSpan.FromHours(1));
    /// <summary>Identifies the <see cref="PauseOnHover"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> PauseOnHoverProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(PauseOnHover), true);
    /// <summary>Identifies the <see cref="ShowPlayPause"/> Avalonia property.</summary>
    public static readonly StyledProperty<bool> ShowPlayPauseProperty =
        AvaloniaProperty.Register<AlderCarousel, bool>(nameof(ShowPlayPause));
    /// <summary>Identifies the <see cref="PreviousIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> PreviousIconProperty =
        AvaloniaProperty.Register<AlderCarousel, Geometry?>(nameof(PreviousIcon));
    /// <summary>Identifies the <see cref="NextIcon"/> Avalonia property.</summary>
    public static readonly StyledProperty<Geometry?> NextIconProperty =
        AvaloniaProperty.Register<AlderCarousel, Geometry?>(nameof(NextIcon));
    /// <summary>Identifies the <see cref="Culture"/> Avalonia property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<AlderCarousel, CultureInfo?>(nameof(Culture));

    /// <summary>The zero-based index of the current snap position.</summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    /// <summary>The item represented by the current slide.</summary>
    public object? SelectedItem => selectedItem;
    /// <summary>The visual presentation of the slides and their navigation.</summary>
    public AlderCarouselLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    /// <summary>The number of slides to fit in the carousel viewport.</summary>
    public int SlidesPerView { get => GetValue(SlidesPerViewProperty); set => SetValue(SlidesPerViewProperty, value); }
    /// <summary>The gap between slides in device-independent pixels.</summary>
    public double SlideSpacing { get => GetValue(SlideSpacingProperty); set => SetValue(SlideSpacingProperty, value); }
    /// <summary>Limits the width of the slides and their side arrows independently of the thumbnail row.</summary>
    public double StageMaxWidth { get => GetValue(StageMaxWidthProperty); set => SetValue(StageMaxWidthProperty, value); }
    /// <summary>The slide viewport's width divided by its height. Zero uses the content's preferred height.</summary>
    public double ViewportAspectRatio { get => GetValue(ViewportAspectRatioProperty); set => SetValue(ViewportAspectRatioProperty, value); }
    /// <summary>Whether navigation can wrap between the first and last slides.</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }
    /// <summary>Whether dragging can change the selected slide.</summary>
    public bool IsDragEnabled { get => GetValue(IsDragEnabledProperty); set => SetValue(IsDragEnabledProperty, value); }
    /// <summary>Whether to show the previous and next controls.</summary>
    public bool ShowNavigation { get => GetValue(ShowNavigationProperty); set => SetValue(ShowNavigationProperty, value); }
    /// <summary>Whether to show buttons for navigating between snap positions.</summary>
    public bool ShowDots { get => GetValue(ShowDotsProperty); set => SetValue(ShowDotsProperty, value); }
    /// <summary>Whether to show the thumbnail navigation strip.</summary>
    public bool ShowThumbnails { get => GetValue(ShowThumbnailsProperty); set => SetValue(ShowThumbnailsProperty, value); }
    /// <summary>The template used to present slide thumbnails.</summary>
    public IDataTemplate? ThumbnailTemplate { get => GetValue(ThumbnailTemplateProperty); set => SetValue(ThumbnailTemplateProperty, value); }
    /// <summary>Whether the carousel initially advances without user navigation.</summary>
    public bool IsAutoPlayEnabled { get => GetValue(IsAutoPlayEnabledProperty); set => SetValue(IsAutoPlayEnabledProperty, value); }
    /// <summary>The delay between automatic advances.</summary>
    public TimeSpan AutoPlayInterval { get => GetValue(AutoPlayIntervalProperty); set => SetValue(AutoPlayIntervalProperty, value); }
    /// <summary>Whether automatic advances pause when the pointer is over the carousel.</summary>
    public bool PauseOnHover { get => GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }
    /// <summary>Whether to show the control for stopping and restarting automatic advances.</summary>
    public bool ShowPlayPause { get => GetValue(ShowPlayPauseProperty); set => SetValue(ShowPlayPauseProperty, value); }
    /// <summary>The geometry used for the previous icon.</summary>
    public Geometry? PreviousIcon { get => GetValue(PreviousIconProperty); set => SetValue(PreviousIconProperty, value); }
    /// <summary>The geometry used for the next icon.</summary>
    public Geometry? NextIcon { get => GetValue(NextIconProperty); set => SetValue(NextIconProperty, value); }
    /// <summary>The culture used to format or interpret displayed values.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>The number of positions to which the slide track can snap.</summary>
    public int SnapCount => ItemCount == 0 ? 0 : CanLoop ? ItemCount : ItemCount - VisibleSlides + 1;
    /// <summary>Whether the track can currently move to a previous position.</summary>
    public bool CanMovePrevious => SnapCount > 1 && (CanLoop || SelectedIndex > 0);
    /// <summary>Whether the track can currently move to a following position.</summary>
    public bool CanMoveNext => SnapCount > 1 && (CanLoop || SelectedIndex < SnapCount - 1);
    internal int VisibleSlides => Math.Max(1, Math.Min(SlidesPerView, ItemCount));
    internal bool CanLoop => Loop && ItemCount > VisibleSlides;
    internal double Position { get; private set; }
    internal AlderCarouselTrack? Track { get; set; }

    private Control? viewport;
    private Grid? stage, miniatureBar;
    private Button? previous, next, playPause;
    private StackPanel? dots, thumbnails;
    private AlderFilmstrip? virtualDots, virtualThumbnails;
    private ScrollViewer? thumbnailStrip, dotStrip;
    private object? selectedItem;
    private bool settingIndex;
    private bool inViewport = true;
    private bool itemsPrepared;
    private bool allowPlayButtonFocus;
    private bool indicatorRevealQueued;
    private int markedIndex = -1;
    private Drag? drag;
    private Animation? animation;
    private Action<TimeSpan>? onFrame;
    private bool framePending;
    private readonly DispatcherTimer autoPlay;
    private CultureInfo DisplayCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>Initializes a new instance of <see cref="AlderCarousel"/>.</summary>
    public AlderCarousel()
    {
        autoPlay = new DispatcherTimer { Interval = AutoPlayInterval };
        autoPlay.Tick += (_, _) =>
        {
            if (CanAutoPlay()) MoveNext();
            else autoPlay.Stop();
        };
        EffectiveViewportChanged += (_, e) =>
        {
            bool visible = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
            if (visible == inViewport) return;
            inViewport = visible;
            RefreshAutoPlay();
        };
        ItemsView.CollectionChanged += (_, change) =>
        {
            if (itemsPrepared) RefreshItems(preserveSelection: true, resetIndicators: !AlderFilmstrip.IsTailChange(change, ItemCount));
        };
        AddHandler(PointerPressedEvent, HandlePressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, HandleMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, HandleReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, HandleKey, RoutingStrategies.Tunnel);
    }

    /// <summary>Moves the carousel to the previous snap position when one is available.</summary>
    public void MovePrevious()
    {
        if (CanMovePrevious) Select(SelectedIndex - 1, -1, animate: true);
    }

    /// <summary>Moves the carousel to the next snap position when one is available.</summary>
    public void MoveNext()
    {
        if (CanMoveNext) Select(SelectedIndex + 1, 1, animate: true);
    }

    /// <summary>Moves to the requested snap position, clamped to the available positions.</summary>
    /// <param name="index">The zero-based snap position requested.</param>
    /// <param name="animate">Whether to animate the move when motion is enabled.</param>
    public void GoTo(int index, bool animate = true) => Select(Math.Clamp(index, 0, Math.Max(0, SnapCount - 1)), 0, animate);

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<AlderCarouselItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new AlderCarouselItem();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (previous is not null) previous.Click -= PreviousClick;
        if (next is not null) next.Click -= NextClick;
        if (playPause is not null)
        {
            playPause.Click -= PlayPauseClick;
            playPause.LostFocus -= PlayPauseLostFocus;
        }
        if (thumbnailStrip is not null) thumbnailStrip.SizeChanged -= IndicatorStripSizeChanged;
        if (dotStrip is not null) dotStrip.SizeChanged -= IndicatorStripSizeChanged;
        base.OnApplyTemplate(e);
        viewport = e.NameScope.Find<Control>("PART_Viewport");
        stage = e.NameScope.Find<Grid>("PART_Stage");
        miniatureBar = e.NameScope.Find<Grid>("PART_MiniatureBar");
        previous = e.NameScope.Find<Button>("PART_Previous");
        next = e.NameScope.Find<Button>("PART_Next");
        playPause = e.NameScope.Find<Button>("PART_PlayPause");
        var dotsPart = e.NameScope.Find<Control>("PART_Dots");
        var thumbnailsPart = e.NameScope.Find<Control>("PART_Thumbnails");
        dots = dotsPart as StackPanel;
        thumbnails = thumbnailsPart as StackPanel;
        virtualDots = dotsPart as AlderFilmstrip;
        virtualThumbnails = thumbnailsPart as AlderFilmstrip;
        if (virtualDots is not null) virtualDots.CreateButton = index => Indicator(index, thumbnail: false);
        if (virtualThumbnails is not null) virtualThumbnails.CreateButton = index => Indicator(index, thumbnail: true);
        thumbnailStrip = e.NameScope.Find<ScrollViewer>("PART_ThumbnailStrip");
        dotStrip = e.NameScope.Find<ScrollViewer>("PART_DotStrip");
        if (previous is not null) previous.Click += PreviousClick;
        if (next is not null) next.Click += NextClick;
        if (playPause is not null)
        {
            playPause.Click += PlayPauseClick;
            playPause.LostFocus += PlayPauseLostFocus;
        }
        if (thumbnailStrip is not null) thumbnailStrip.SizeChanged += IndicatorStripSizeChanged;
        if (dotStrip is not null) dotStrip.SizeChanged += IndicatorStripSizeChanged;
        itemsPrepared = true;
        RefreshItems(preserveSelection: false);
        UpdateLayoutState();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        inViewport = true;
        RefreshAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        autoPlay.Stop();
        CancelDrag();
        animation = null;
        Paint(Math.Max(0, SelectedIndex));
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!itemsPrepared) return;
        if (change.Property == SelectedIndexProperty && !settingIndex)
            GoTo(SelectedIndex);
        else if (change.Property == SlidesPerViewProperty || change.Property == LoopProperty)
            RefreshItems(preserveSelection: false);
        else if (change.Property == SlideSpacingProperty)
            Track?.InvalidateMeasure();
        else if (change.Property == LayoutProperty)
            UpdateLayoutState();
        else if (change.Property == BoundsProperty)
            UpdateCompactState();
        else if (change.Property == ShowDotsProperty || change.Property == ShowThumbnailsProperty || change.Property == ThumbnailTemplateProperty ||
                 change.Property == CultureProperty)
            UpdateIndicators();
        else if (change.Property == ShowNavigationProperty || change.Property == ShowPlayPauseProperty ||
                 change.Property == IsAutoPlayEnabledProperty || change.Property == AutoPlayIntervalProperty ||
                 change.Property == PauseOnHoverProperty || change.Property == IsPointerOverProperty ||
                 change.Property == IsKeyboardFocusWithinProperty || change.Property == IsVisibleProperty ||
                 change.Property == AlderMotion.ReducedProperty || change.Property == IsEffectivelyEnabledProperty)
            UpdateNavigation();
        else if (change.Property == IsDragEnabledProperty && !IsDragEnabled && drag is not null)
            GoTo(SelectedIndex);
    }

    private void RefreshItems(bool preserveSelection, bool resetIndicators = true)
    {
        CancelDrag();
        animation = null;
        int index = preserveSelection && selectedItem is not null ? ItemsView.IndexOf(selectedItem) : -1;
        if (ItemCount != 0) WriteIndex(Math.Clamp(index >= 0 ? index : SelectedIndex, 0, SnapCount - 1));
        SetAndRaise(SelectedItemProperty, ref selectedItem, ItemCount == 0 ? null : ItemsView[SelectedIndex]);
        Paint(ItemCount == 0 ? 0 : SelectedIndex);
        Track?.InvalidateMeasure();
        UpdateIndicators(resetIndicators);
    }

    private void Select(int requested, int direction, bool animate, double? releaseVelocity = null)
    {
        if (SnapCount == 0) return;
        CancelDrag();
        int index = CanLoop ? Mod(requested, SnapCount) : Math.Clamp(requested, 0, SnapCount - 1);
        double target = index;
        if (CanLoop)
        {
            if (releaseVelocity.HasValue) target = requested;
            else
            {
                target += SnapCount * Math.Round((Position - target) / SnapCount);
                if (direction > 0 && target < Position - .001) target += SnapCount;
                if (direction < 0 && target > Position + .001) target -= SnapCount;
            }
        }

        WriteIndex(index);
        if (SelectedIndex != index || ItemCount == 0) return;
        SetAndRaise(SelectedItemProperty, ref selectedItem, ItemsView[index]);
        double distance = Math.Abs(target - Position);
        if (animate && distance is > .002 and <= 3 && !AlderMotion.GetReduced(this) && TopLevel.GetTopLevel(this) is not null)
        {
            double duration = releaseVelocity.HasValue
                ? Math.Min(.46, .29 + distance * .10)
                : Math.Min(.34, .19 + distance * .07);
            double slope = releaseVelocity is { } velocity ? Math.Clamp(velocity * duration / (target - Position), 0, 3) : 3;
            animation = new Animation(Position, target, Stopwatch.GetTimestamp(), duration, slope);
            RequestFrame();
        }
        else
        {
            animation = null;
            Paint(target);
        }
        UpdateNavigation();
    }

    private void WriteIndex(int index)
    {
        settingIndex = true;
        try { SetCurrentValue(SelectedIndexProperty, index); }
        finally { settingIndex = false; }
    }

    private void Paint(double value)
    {
        if (Position == value) return;
        Position = value;
        Track?.InvalidatePosition();
    }

    private void RequestFrame()
    {
        if (framePending || TopLevel.GetTopLevel(this) is not { } root) return;
        framePending = true;
        root.RequestAnimationFrame(onFrame ??= RenderFrame);
    }

    private void RenderFrame(TimeSpan _)
    {
        framePending = false;
        if (animation is not { } moving) return;
        double progress = Stopwatch.GetElapsedTime(moving.Started).TotalSeconds / moving.Duration;
        if (progress >= 1 || AlderMotion.GetReduced(this))
        {
            animation = null;
            Paint(moving.To);
            return;
        }
        double remaining = 1 - progress;
        double eased = progress * progress * (3 - 2 * progress) + moving.InitialSlope * progress * remaining * remaining;
        Paint(moving.From + (moving.To - moving.From) * eased);
        RequestFrame();
    }

    private void UpdateLayoutState()
    {
        PseudoClasses.Set(":modal", Layout == AlderCarouselLayout.Modal);
        PseudoClasses.Set(":miniatures", Layout == AlderCarouselLayout.Miniatures);
        UpdateCompactState();
        var arrowRow = Layout == AlderCarouselLayout.Miniatures ? miniatureBar : stage;
        PlaceArrow(previous, arrowRow, Layout == AlderCarouselLayout.InPlace ? 1 : 0);
        PlaceArrow(next, arrowRow, Layout == AlderCarouselLayout.InPlace ? 1 : 2);
    }

    private void UpdateCompactState() => PseudoClasses.Set(":compact", Bounds.Width is > 0 and < 340);

    private static void PlaceArrow(Button? button, Grid? row, int column)
    {
        if (button is null || row is null) return;
        if (button.GetVisualParent() is Grid parent && parent != row)
        {
            parent.Children.Remove(button);
            row.Children.Add(button);
        }
        Grid.SetColumn(button, column);
    }

    private void UpdateIndicators(bool reset = true)
    {
        dots?.Children.Clear();
        thumbnails?.Children.Clear();
        int count = SnapCount;
        virtualDots?.UpdateCount(ShowDots && count > 1 ? count : 0, reset);
        virtualThumbnails?.UpdateCount(ShowThumbnails && count > 1 ? count : 0, reset);
        if (dots is not null || thumbnails is not null)
        {
            for (int i = 0; count > 1 && i < count; i++)
            {
                if (ShowDots && dots is not null) dots.Children.Add(Indicator(i, thumbnail: false));
                if (ShowThumbnails && thumbnails is not null) thumbnails.Children.Add(Indicator(i, thumbnail: true));
            }
        }
        if (dotStrip is not null) dotStrip.IsVisible = ShowDots && count > 1;
        if (thumbnailStrip is not null) thumbnailStrip.IsVisible = ShowThumbnails && count > 1;
        UpdateNavigation();
        RequestIndicatorReveal();
    }

    private Button Indicator(int index, bool thumbnail)
    {
        var button = new AlderButton { Name = thumbnail ? "Thumbnail" : "Dot" };
        var theme = thumbnail ? "AlderCarouselThumbnailTheme" : "AlderCarouselDotTheme";
        if (this.TryFindResource(theme, ActualThemeVariant, out var value)) button.Theme = value as ControlTheme;
        var number = (index + 1).ToString(DisplayCulture);
        if (thumbnail)
        {
            if (ItemsView[index] is AlderCarouselItem item) button.Content = item.Thumbnail ?? number;
            else if (ThumbnailTemplate is not null)
            {
                button.ContentTemplate = ThumbnailTemplate;
                button.Content = ItemsView[index];
            }
            else button.Content = number;
        }
        AutomationProperties.SetName(button, string.Format(DisplayCulture, Caption("CarouselGoToSlide", "Go to slide {0}"), number));
        MarkButton(button, index == SelectedIndex);
        button.Click += (_, _) => GoTo(index);
        return button;
    }

    private void UpdateNavigation()
    {
        if (previous is not null)
        {
            previous.IsVisible = ShowNavigation && SnapCount > 1;
            previous.IsEnabled = CanMovePrevious;
        }
        if (next is not null)
        {
            next.IsVisible = ShowNavigation && SnapCount > 1;
            next.IsEnabled = CanMoveNext;
        }
        if (playPause is not null)
        {
            playPause.IsVisible = ShowPlayPause && SnapCount > 1;
            playPause.Content = IsAutoPlayEnabled ? Caption("CarouselPause", "Pause") : Caption("CarouselPlay", "Play");
        }
        if (markedIndex != SelectedIndex)
        {
            MarkSelection(dots, markedIndex, false);
            MarkSelection(thumbnails, markedIndex, false);
            MarkButton(virtualDots?.ContainerFromIndex(markedIndex) as Button, false);
            MarkButton(virtualThumbnails?.ContainerFromIndex(markedIndex) as Button, false);
            markedIndex = SelectedIndex;
            MarkSelection(dots, markedIndex, true);
            MarkSelection(thumbnails, markedIndex, true);
            MarkButton(virtualDots?.ContainerFromIndex(markedIndex) as Button, true);
            MarkButton(virtualThumbnails?.ContainerFromIndex(markedIndex) as Button, true);
            RequestIndicatorReveal();
        }
        RefreshAutoPlay();
    }

    private void MarkSelection(StackPanel? row, int index, bool selected)
    {
        if (row is null || (uint)index >= (uint)row.Children.Count) return;
        MarkButton(row.Children[index] as Button, selected);
    }

    private void MarkButton(Button? button, bool selected)
    {
        if (button is null) return;
        button.Classes.Set("selected", selected);
        AutomationProperties.SetItemStatus(button, selected ? Caption("Selected", "Selected") : null);
    }

    private void IndicatorStripSizeChanged(object? sender, SizeChangedEventArgs e) => RequestIndicatorReveal();

    private void RequestIndicatorReveal()
    {
        if (indicatorRevealQueued) return;
        indicatorRevealQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            indicatorRevealQueued = false;
            RevealIndicator(thumbnailStrip, thumbnails);
            RevealIndicator(dotStrip, dots);
            virtualThumbnails?.Reveal(thumbnailStrip, SelectedIndex, (thumbnailStrip as AlderScrollShadow)?.ShadowSize ?? 0);
            virtualDots?.Reveal(dotStrip, SelectedIndex);
        }, DispatcherPriority.Loaded);
    }

    private void RevealIndicator(ScrollViewer? scroller, StackPanel? row)
    {
        if (scroller is null || row is null || !scroller.IsEffectivelyVisible || (uint)SelectedIndex >= (uint)row.Children.Count) return;
        double offset = scroller.Offset.X;
        double width = scroller.Viewport.Width;
        if (width <= 0 || scroller.Extent.Width <= width) return;
        var bounds = row.Children[SelectedIndex].Bounds;
        double inset = scroller is AlderScrollShadow fade ? fade.ShadowSize : 0;
        if (bounds.Left - inset < offset) offset = bounds.Left - inset;
        else if (bounds.Right + inset > offset + width) offset = bounds.Right + inset - width;
        scroller.Offset = new Vector(Math.Clamp(offset, 0, scroller.Extent.Width - width), scroller.Offset.Y);
    }

    private void PreviousClick(object? sender, RoutedEventArgs e) => MovePrevious();
    private void NextClick(object? sender, RoutedEventArgs e) => MoveNext();
    private void PlayPauseClick(object? sender, RoutedEventArgs e)
    {
        bool play = !IsAutoPlayEnabled;
        allowPlayButtonFocus = play;
        if (play && !CanMoveNext) GoTo(0);
        SetCurrentValue(IsAutoPlayEnabledProperty, play);
    }

    private void PlayPauseLostFocus(object? sender, RoutedEventArgs e) => allowPlayButtonFocus = false;

    private bool CanAutoPlay() => IsAutoPlayEnabled && CanMoveNext && inViewport && drag is null &&
        this.IsAttachedToVisualTree() && IsEffectivelyVisible && IsEffectivelyEnabled &&
        (!PauseOnHover || !IsPointerOver) && (!IsKeyboardFocusWithin || allowPlayButtonFocus && playPause?.IsFocused == true) &&
        !AlderMotion.GetReduced(this);

    private void RefreshAutoPlay()
    {
        autoPlay.Stop();
        autoPlay.Interval = AutoPlayInterval;
        if (CanAutoPlay()) autoPlay.Start();
    }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || SnapCount <= 1 || e.Source is not Visual source ||
            source.FindAncestorOfType<AlderCarousel>(includeSelf: true) != this ||
            source.FindAncestorOfType<AlderCarouselItem>(includeSelf: true) is { } slide && this.IsVisualAncestorOf(slide)) return;
        switch (e.Key)
        {
            case Key.Left:
                if (FlowDirection == FlowDirection.RightToLeft) MoveNext(); else MovePrevious();
                break;
            case Key.Right:
                if (FlowDirection == FlowDirection.RightToLeft) MovePrevious(); else MoveNext();
                break;
            case Key.Home: GoTo(0); break;
            case Key.End: GoTo(SnapCount - 1); break;
            default: return;
        }
        e.Handled = true;
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDragEnabled || SnapCount <= 1 || drag is not null || viewport is null ||
            e.Source is not Visual source || source.FindAncestorOfType<AlderCarousel>(includeSelf: true) != this ||
            !(source == viewport || viewport.IsVisualAncestorOf(source)) ||
            source.FindAncestorOfType<TextBox>(includeSelf: true) is not null ||
            source.FindAncestorOfType<RangeBase>(includeSelf: true) is not null ||
            e.Pointer.Type != PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        animation = null;
        drag = new Drag(e.Pointer, PointerPosition(e), Position);
        RefreshAutoPlay();
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (drag is not { } contact || contact.Pointer != e.Pointer || Track is not { Step: > 0 } track) return;
        Vector movement = PointerPosition(e) - contact.Origin;
        if (!contact.Active)
        {
            if (Math.Abs(movement.Y) >= 7 && Math.Abs(movement.Y) >= Math.Abs(movement.X))
            {
                GoTo(SelectedIndex);
                return;
            }
            if (Math.Abs(movement.X) < 7) return;
            animation = null;
            contact.Active = true;
            e.Pointer.Capture(this);
            PseudoClasses.Set(":dragging", true);
        }

        Paint(DragPosition(contact, movement.X, track.Step));
        contact.Sample(Position);
        e.Handled = true;
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (drag is not { } contact || contact.Pointer != e.Pointer) return;
        if (contact.Active && Track is { Step: > 0 } track)
        {
            double position = DragPosition(contact, PointerPosition(e).X - contact.Origin.X, track.Step);
            if (Math.Abs(Position - position) > .001)
            {
                Paint(position);
                contact.Sample(position);
            }
        }
        CancelDrag();
        if (!contact.Active)
        {
            GoTo(SelectedIndex);
            return;
        }

        double velocity = contact.RecentVelocity;
        double projected = Position + Math.Clamp(velocity * .16, -1.5, 1.5);
        int index = (int)Math.Round(projected, MidpointRounding.AwayFromZero);
        Select(index, 0, animate: true, releaseVelocity: velocity);
        e.Handled = true;
    }

    private double DragPosition(Drag contact, double delta, double step)
    {
        double direction = FlowDirection == FlowDirection.RightToLeft ? 1 : -1;
        double requested = contact.Start + direction * delta / step;
        if (CanLoop) return requested;
        double edge = Math.Clamp(requested, 0, SnapCount - 1);
        return edge + Math.Clamp((requested - edge) * .24, -.35, .35);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (drag?.Pointer == e.Pointer && e.Pointer.Captured != this) GoTo(SelectedIndex);
    }

    private Point PointerPosition(PointerEventArgs e) => e.GetPosition(TopLevel.GetTopLevel(this));

    private void CancelDrag()
    {
        var pointer = drag?.Pointer;
        drag = null;
        PseudoClasses.Set(":dragging", false);
        if (pointer?.Captured == this) pointer.Capture(null);
    }

    private string Caption(string key, string fallback) =>
        this.TryFindResource("AlderText." + key, ActualThemeVariant, out var value) && value is string localized ? localized : fallback;

    internal static int Mod(int value, int modulo) => (value % modulo + modulo) % modulo;

    private sealed record Animation(double From, double To, long Started, double Duration, double InitialSlope);

    private sealed class Drag(IPointer pointer, Point origin, double start)
    {
        private long sampled = Stopwatch.GetTimestamp();
        private double last = start;
        private double velocity;

        public IPointer Pointer { get; } = pointer;
        public Point Origin { get; } = origin;
        public double Start { get; } = start;
        public bool Active { get; set; }
        public double RecentVelocity => Stopwatch.GetElapsedTime(sampled).TotalMilliseconds < 95 ? velocity : 0;

        public void Sample(double position)
        {
            long now = Stopwatch.GetTimestamp();
            double elapsed = Stopwatch.GetElapsedTime(sampled, now).TotalSeconds;
            if (elapsed < .012) return;
            velocity = (position - last) / elapsed;
            last = position;
            sampled = now;
        }
    }
}

/// <summary>Hosts a slide and its optional thumbnail.</summary>
public sealed class AlderCarouselItem : ContentControl
{
    /// <summary>Identifies the <see cref="Thumbnail"/> Avalonia property.</summary>
    public static readonly StyledProperty<object?> ThumbnailProperty =
        AvaloniaProperty.Register<AlderCarouselItem, object?>(nameof(Thumbnail));

    /// <summary>The content used to represent the slide in thumbnail navigation.</summary>
    public object? Thumbnail { get => GetValue(ThumbnailProperty); set => SetValue(ThumbnailProperty, value); }
}
